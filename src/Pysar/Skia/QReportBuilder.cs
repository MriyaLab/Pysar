using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Skia.Rendering;

namespace Pysar.Skia;

/// <summary>
///     Configures QReport during a host's own <c>UseQReport</c> extension: the fonts reports refer to
///     by family name, and the drawers for any custom element types.
/// </summary>
public sealed class QReportBuilder
{
    private readonly SkiaReportRenderer _renderer;

    /// <summary>
    ///     Constructed by a host package's own <c>UseQReport</c> extension (MAUI, Avalonia, ...),
    ///     which already holds the renderer and the platform handler's font collection.
    /// </summary>
    public QReportBuilder(SkiaReportRenderer renderer, IFontCollection fonts)
    {
        _renderer = renderer;
        Fonts = fonts;
    }

    /// <summary>The collection reports resolve font families from.</summary>
    public IFontCollection Fonts { get; }

    /// <summary>
    ///     Registers a font packaged as an application asset. <paramref name="filename"/> is its
    ///     logical name in the package - the same relative path the asset was declared under.
    /// </summary>
    /// <exception cref="FileNotFoundException">The font is not in the application package.</exception>
    public QReportBuilder AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal)
    {
        Fonts.AddFont(filename, alias, fontStyle);

        return this;
    }

    /// <summary>Registers several fonts at once, for hosts that keep their font list elsewhere.</summary>
    public QReportBuilder RegisterFonts(Action<IFontCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);

        register(Fonts);

        return this;
    }

    /// <summary>Registers the drawer for a custom element type.</summary>
    public QReportBuilder AddDrawer<TElement>(IElementDrawer drawer) where TElement : IReportElement
    {
        _renderer.WithDrawer<TElement>(drawer);

        return this;
    }
}
