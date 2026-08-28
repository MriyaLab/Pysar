using System.Collections;
using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     A data-repetition primitive: its children form a row template expanded once per data item at
///     build time. Use <see cref="DataSource"/> for a root/standalone repeater, or
///     <see cref="DataSourcePath"/> to bind a nested repeater to a collection property of the enclosing
///     record. Optional <see cref="Header"/>/<see cref="Footer"/> print once around the rows and inherit
///     the group's data context. Expanded by RepeaterExpander before the binding pass.
/// </summary>
public sealed class Repeater : ReportContainer<Repeater>
{
    public static BindableProperty DataSourceProperty { get; } =
        BindableProperty.Create(nameof(DataSource), typeof(IEnumerable), typeof(Repeater), null);

    public static BindableProperty DataSourcePathProperty { get; } =
        BindableProperty.Create(nameof(DataSourcePath), typeof(string), typeof(Repeater), null);

    public static BindableProperty HeaderProperty { get; } =
        BindableProperty.Create(nameof(Header), typeof(IReportElement), typeof(Repeater), null);

    public static BindableProperty FooterProperty { get; } =
        BindableProperty.Create(nameof(Footer), typeof(IReportElement), typeof(Repeater), null);

    /// <summary>Direct data for a root/standalone repeater. Mutually exclusive with <see cref="DataSourcePath"/>.</summary>
    public IEnumerable? DataSource
    {
        get => (IEnumerable?)GetValue(DataSourceProperty);
        set => SetValue(DataSourceProperty, value);
    }

    /// <summary>Property path on the enclosing record yielding an <see cref="IEnumerable"/> — for a nested repeater.</summary>
    public string? DataSourcePath
    {
        get => (string?)GetValue(DataSourcePathProperty);
        set => SetValue(DataSourcePathProperty, value);
    }

    /// <summary>Optional header printed once before the rows. Any element (e.g. a <see cref="Frame"/> or a <see cref="Grid"/>).</summary>
    public IReportElement? Header
    {
        get => (IReportElement?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Optional footer printed once after the rows. Any element (e.g. a <see cref="Frame"/> or a <see cref="Grid"/>).</summary>
    public IReportElement? Footer
    {
        get => (IReportElement?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    ///     When set, each row is built imperatively per data item (typed builder mode) instead of by
    ///     cloning the child template. The item is boxed as <see cref="object"/>; callers wrap a typed
    ///     delegate. A code-only escape hatch (not authorable from XAML), so it stays non-bindable.
    /// </summary>
    internal Action<object, StackPanel>? RowBuilder { get; set; }

    public Repeater WithDataSource(IEnumerable dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        DataSource = dataSource;
        return this;
    }

    public Repeater WithDataSourcePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        DataSourcePath = path;
        return this;
    }

    public Repeater WithHeader(Action<Frame> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var header = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        configure(header);
        Header = header;
        return this;
    }

    public Repeater WithFooter(Action<Frame> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var footer = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        configure(footer);
        Footer = footer;
        return this;
    }

    // DataSource/DataSourcePath are bindable, so CopyStateTo carries them. Header/Footer are bindable too
    // but hold a mutable element subtree, so they must be deep-cloned (CopyStateTo copies the reference).
    // RowBuilder is non-bindable, so it is copied explicitly.
    public override IReportElement Clone()
    {
        var clone = (Repeater)base.Clone();
        clone.Header = Header?.Clone();
        clone.Footer = Footer?.Clone();
        clone.RowBuilder = RowBuilder;
        return clone;
    }
}
