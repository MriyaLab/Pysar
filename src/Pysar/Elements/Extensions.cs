using System.Collections;
using Pysar.Core.Enums;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public static class Extensions
{
    /// <summary>
    ///     Inserts a page break "here". A <see cref="Band"/> is an overlay frame, so a child marker would
    ///     sit at its top and split the band; on a band this instead sets a band-level break after the
    ///     band (<see cref="PageBreakMode.After"/>). In vertical flow content it adds an invisible
    ///     <see cref="PageBreak"/> marker so content after it starts on a new page. The runtime check
    ///     covers fluent chains that have degraded to the <see cref="Frame"/> base type.
    /// </summary>
    public static T AddPageBreak<T>(this ReportContainer<T> container)
        where T : ReportContainer<T>
    {
        if (container is Band band)
            band.PageBreak = PageBreakMode.After;
        else
            container.AddElement(new PageBreak());
        return (T)container;
    }

    /// <summary>
    ///     Adds a nested data group (master-detail): creates a <see cref="Repeater"/> whose data source is
    ///     <em>bound</em> to <paramref name="itemsPath"/> on the enclosing record — the MAUI
    ///     <c>CollectionView ItemsSource="{Binding …}"</c> equivalent, resolved per item at expansion.
    ///     <paramref name="configure"/> sets its header/footer and row template.
    /// </summary>
    public static T AddGroup<T>(this ReportContainer<T> container, string itemsPath, Action<Repeater> configure)
        where T : ReportContainer<T>
    {
        ArgumentException.ThrowIfNullOrEmpty(itemsPath);
        ArgumentNullException.ThrowIfNull(configure);
        var group = new Repeater();
        group.SetBinding(Repeater.DataSourceProperty, itemsPath);
        configure(group);
        return container.AddElement(group);
    }

    /// <summary>
    ///     Adds a nested data group over a direct collection (used from a typed builder where the actual
    ///     record — and thus its child collection — is in hand). Sugar over
    ///     <c>new Repeater().WithDataSource(items)</c>.
    /// </summary>
    public static T AddGroup<T>(this ReportContainer<T> container, IEnumerable items, Action<Repeater> configure)
        where T : ReportContainer<T>
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(configure);
        var group = new Repeater().WithDataSource(items);
        configure(group);
        return container.AddElement(group);
    }

    /// <summary>
    ///     Adds a nested data group with a typed per-item builder: <paramref name="build"/> is invoked per
    ///     item with the actual record and a fresh row container. Lets nesting stay fully typed to any
    ///     depth (each level's real record in hand — no binding paths).
    /// </summary>
    public static T AddGroup<T, TItem>(this ReportContainer<T> container, IEnumerable<TItem> items, Action<TItem, StackPanel> build)
        where T : ReportContainer<T>
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(build);
        var group = new Repeater { DataSource = items, RowBuilder = (item, row) => build((TItem)item, row) };
        return container.AddElement(group);
    }


    public static Frame AddFrame(this Frame frame, Action<Frame> func)
    {
        var childFrame = new Frame();
        func(childFrame);
        frame.AddElement(childFrame);
        return frame;
    }

    public static Frame AddImage(this Frame frame, ImageSource source, Action<Image> func)
    {
        var image = new Image()
        {
            Source = source ?? throw new ArgumentNullException(nameof(source))
        };
        func(image);
        frame.AddElement(image);
        return frame;
    }

    public static Frame AddText(this Frame frame, string content, Action<Text> func)
    {
        var text = new Text()
        {
            Content = content
        };
        func(text);
        frame.AddElement(text);
        return frame;
    }
}
