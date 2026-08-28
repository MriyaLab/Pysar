using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Skia.Rendering;

namespace Pysar.Skia;

/// <summary>
///     Configures Pysar during a host's own <c>UsePysar</c> extension: the fonts reports refer to
///     by family name, and the drawers for any custom element types.
/// </summary>
public sealed class PysarBuilder
{
    private readonly SkiaReportRenderer _renderer;

    /// <summary>
    ///     Constructed by a host package's own <c>UsePysar</c> extension (MAUI, Avalonia, ...),
    ///     which already holds the renderer and the platform handler's font collection.
    /// </summary>
    public PysarBuilder(SkiaReportRenderer renderer, IFontCollection fonts)
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
    public PysarBuilder AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal)
    {
        Fonts.AddFont(filename, alias, fontStyle);

        return this;
    }

    /// <summary>Registers several fonts at once, for hosts that keep their font list elsewhere.</summary>
    public PysarBuilder RegisterFonts(Action<IFontCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);

        register(Fonts);

        return this;
    }

    /// <summary>Registers the drawer for a custom element type.</summary>
    public PysarBuilder AddDrawer<TElement>(IElementDrawer drawer) where TElement : IReportElement
    {
        _renderer.WithDrawer<TElement>(drawer);

        return this;
    }
}
