using System.Collections;
using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     Build-time transformation that expands data repetition before the binding pass. The detail band's
///     root <see cref="Repeater"/> and any nested <see cref="Repeater"/> share the same recursive core: a
///     row template is deep-cloned once per data item into a <see cref="StackPanel"/>, each row carrying
///     the item as <see cref="IReportObject.DataContext"/>, and nested repeaters are expanded against that
///     item. Header/footer print once around the rows. Re-running the transform is stable (idempotent):
///     once a repeater is replaced by its expanded stack there is nothing left to expand.
/// </summary>
internal static class RepeaterExpander
{
    public static void Expand(Report design)
    {
        var detail = design.Detail;
        var expanded = new List<IReportElement>();
        foreach (var child in detail.Children.ToList())
            expanded.AddRange(ExpandTopLevel(child));
        ((IContainerMutator)detail).ReplaceChildren(expanded);
    }

    /// <summary>
    ///     Expands a top-level child of the detail band. A data-less root repeater (no source and no path)
    ///     is unwrapped to its template so a static detail renders its content as-is; otherwise the child
    ///     is expanded normally (a data-bound repeater becomes its rows stack).
    /// </summary>
    private static IEnumerable<IReportElement> ExpandTopLevel(IReportElement node)
    {
        if (node is Repeater { DataSource: null, DataSourcePath: null } root)
            return root.Children.Select(c => ExpandInTree(c, null));
        return [ExpandInTree(node, null)];
    }

    /// <summary>
    ///     Expands <paramref name="items"/> into a vertical <c>[header?, rows, footer?]</c> stack: one row
    ///     per item (carrying the item as its data context). A row is either cloned from the repeater's
    ///     child template or, in builder mode, built imperatively by <see cref="Repeater.RowBuilder"/>.
    ///     Nested repeaters are expanded against the item.
    /// </summary>
    private static StackPanel BuildRows(IEnumerable items, Repeater repeater)
    {
        var rows = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        foreach (var item in items)
        {
            if (item is string) continue; // guard: a string is IEnumerable<char>, not a row record
            rows.AddElement(repeater.RowBuilder is { } build
                ? BuildRowWithBuilder(item, build)
                : BuildRowFromTemplate(item, repeater.Children));
        }

        // Header/footer are added as-is (not per-row cloned) so they print once; they inherit the
        // enclosing group's DataContext through binding propagation.
        var outer = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        if (repeater.Header is not null) outer.AddElement(repeater.Header);
        outer.AddElement(rows);
        if (repeater.Footer is not null) outer.AddElement(repeater.Footer);
        return outer;
    }

    /// <summary>Builder mode: a fresh row stack populated by the typed builder, then nested groups expanded.</summary>
    private static StackPanel BuildRowWithBuilder(object item, Action<object, StackPanel> build)
    {
        var row = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        row.DataContext = item;
        build(item, row);
        ExpandInTree(row, item); // expand any AddGroup the builder added
        return row;
    }

    /// <summary>Template mode: a fresh row frame holding a clone of each template child, nested groups expanded.</summary>
    private static Frame BuildRowFromTemplate(object item, IReadOnlyList<IReportElement> template)
    {
        var row = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        row.DataContext = item;
        foreach (var templateChild in template)
        {
            var clone = templateChild.Clone();
            row.AddElement(ExpandInTree(clone, item));
        }
        return row;
    }

    /// <summary>
    ///     Recursively expands nested <see cref="Repeater"/>s in <paramref name="node"/>'s subtree
    ///     against <paramref name="context"/>. A <see cref="Repeater"/> is replaced by its expanded
    ///     <c>[header?, rows, footer?]</c> stack (its own rows produced against the collection the
    ///     repeater resolves from the context). Other containers have their children expanded in place
    ///     via <see cref="IContainerMutator"/>. Grid is skipped — nested repeaters inside grid cells are
    ///     out of scope for v1.
    /// </summary>
    private static IReportElement ExpandInTree(IReportElement node, object? context)
    {
        if (node is Repeater repeater)
        {
            return BuildRows(ResolveItems(repeater, context), repeater);
        }

        if (node is IReportContainer container && node is not Grid && container.Children.Count > 0)
        {
            var expanded = container.Children.Select(c => ExpandInTree(c, context)).ToList();
            ((IContainerMutator)container).ReplaceChildren(expanded);
        }
        return node;
    }

    /// <summary>
    ///     Resolves a repeater's data at expansion time against <paramref name="context"/> (the enclosing
    ///     item). A DataSource <em>binding</em> (MAUI ItemsSource style, e.g. from <c>AddGroup(path)</c> or
    ///     XAML <c>DataSource="{Binding …}"</c>) wins and is resolved fresh per item; otherwise a literal
    ///     DataSource is used, or the legacy DataSourcePath.
    /// </summary>
    private static IEnumerable ResolveItems(Repeater repeater, object? context)
    {
        if (repeater.TryGetBinding(Repeater.DataSourceProperty, out var binding) && binding is not null)
            return AsEnumerable(PropertyPathResolver.Resolve(context, binding.Path));
        if (repeater.DataSource is { } literal)
            return AsEnumerable(literal);
        return AsEnumerable(PropertyPathResolver.Resolve(context, repeater.DataSourcePath));
    }

    private static IEnumerable AsEnumerable(object? value)
        => value is IEnumerable e and not string ? e : Array.Empty<object>();
}
