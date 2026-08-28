using System.Collections;
using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     The flow band that repeats a row template over a data source. Its repetition is a root
///     <see cref="Repeater"/> the band owns and delegates to (data source, header, footer, row template);
///     the band itself only adds band-level concerns (flow placement, page breaks, and repeating the
///     detail header on every page). Composing the repeater avoids duplicating the repetition surface.
/// </summary>
public sealed class DetailBand : Band, IContainerMutator
{
    private readonly Repeater _root = new();

    public DetailBand() => base.AddElement(_root);

    // A real dependency property so the data source can be data-bound (SetBinding / XAML). Changes are
    // mirrored onto the root repeater, which the expander reads. A bound value is resolved before
    // expansion by the Resolve→Expand→Resolve order in ReportBuilder.Build.
    public static BindableProperty DataSourceProperty { get; } =
        BindableProperty.Create(nameof(DataSource), typeof(IEnumerable), typeof(DetailBand), null,
            (bindable, _, newValue) => ((DetailBand)bindable)._root.DataSource = (IEnumerable?)newValue);

    /// <summary>The data the row template repeats over.</summary>
    public IEnumerable? DataSource
    {
        get => (IEnumerable?)GetValue(DataSourceProperty);
        set => SetValue(DataSourceProperty, value);
    }

    /// <summary>Optional header rendered once before the repeated detail rows. Any element (e.g. a <see cref="Frame"/> or a <see cref="Grid"/>).</summary>
    public IReportElement? DetailHeader
    {
        get => _root.Header;
        set => _root.Header = value;
    }

    /// <summary>Optional footer rendered once after the repeated detail rows. Any element (e.g. a <see cref="Frame"/> or a <see cref="Grid"/>).</summary>
    public IReportElement? DetailFooter
    {
        get => _root.Footer;
        set => _root.Footer = value;
    }

    public static BindableProperty RepeatDetailHeaderOnEveryPageProperty { get; } =
        BindableProperty.Create(nameof(RepeatDetailHeaderOnEveryPage), typeof(bool), typeof(DetailBand), false);

    /// <summary>When true, the detail header is repeated at the top of every page.</summary>
    public bool RepeatDetailHeaderOnEveryPage
    {
        get => (bool)GetValue(RepeatDetailHeaderOnEveryPageProperty)!;
        set => SetValue(RepeatDetailHeaderOnEveryPageProperty, value);
    }

    public DetailBand WithDataSource(IEnumerable dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        DataSource = dataSource;
        return this;
    }

    /// <summary>
    ///     Binds the root data source to a property path on the report-level data context (resolved
    ///     before expansion). The fluent equivalent of <c>SetBinding(DataSourceProperty, path)</c>.
    /// </summary>
    public DetailBand WithDataSourceBinding(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        SetBinding(DataSourceProperty, path);
        return this;
    }

    /// <summary>
    ///     Typed builder mode: sets the data source and a per-record builder that receives the actual
    ///     record and a fresh row container to populate (real typed access — no binding paths). Use for
    ///     record-dependent content, e.g. a nested <c>AddGroup(record.Children, ...)</c>. Mutually
    ///     exclusive with the <c>WithDataSource</c> + row-template approach.
    /// </summary>
    public DetailBand WithData<T>(IEnumerable<T> source, Action<T, StackPanel> build)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(build);
        DataSource = source;
        _root.RowBuilder = (item, row) => build((T)item, row);
        return this;
    }

    public DetailBand WithDetailHeader(Action<Frame> configure)
    {
        _root.WithHeader(configure);
        return this;
    }

    public DetailBand WithDetailFooter(Action<Frame> configure)
    {
        _root.WithFooter(configure);
        return this;
    }

    public DetailBand WithRepeatDetailHeader(bool repeat = true)
    {
        RepeatDetailHeaderOnEveryPage = repeat;
        return this;
    }

    /// <summary>Adds a row-template element to the root repeater (public fluent API, not the band frame).</summary>
    public new DetailBand AddElement(IReportElement element)
    {
        _root.AddElement(element);
        return this;
    }

    // The XAML loader (and any generic tree code) adds template children through IContainerMutator.AddChild;
    // route those to the root repeater, exactly like the fluent AddElement. ReplaceChildren, however, is
    // used by the expander to set the band's OWN rendered children (the expanded [header?, rows, footer?]
    // stack), so it must target the band frame, not the repeater.
    void IContainerMutator.AddChild(IReportElement element) => _root.AddElement(element);

    void IContainerMutator.ReplaceChildren(IEnumerable<IReportElement> children)
    {
        ClearElements();
        foreach (var child in children)
            base.AddElement(child);
    }
}
