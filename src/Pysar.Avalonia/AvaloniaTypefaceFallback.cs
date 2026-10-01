using Avalonia.Media;
using Pysar.Core.Enums;
using SkiaSharp;
using AvaloniaFontStyle = Avalonia.Media.FontStyle;
using FontStyle = Pysar.Core.Enums.FontStyle;

namespace Pysar.Avalonia;

/// <summary>
///     Turns a family <see cref="FontManager"/> already knows into an <see cref="SKTypeface"/>.
///     A miss returns null so <c>FontCache</c> can keep its Skia fallback. Does not throw for a
///     missing face.
/// </summary>
internal static class AvaloniaTypefaceFallback
{
    internal static SKTypeface? TryResolve(string family, FontStyle style)
    {
        if (string.IsNullOrWhiteSpace(family))
            return null;

        FontManager manager;
        try
        {
            manager = FontManager.Current;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var (avaloniaStyle, weight) = Map(style);
        // Typeface parses a '#' key (fonts:Ubuntu#Ubuntu). A bare name has a null key, so
        // TryGetGlyphTypeface searches system fonts and substitutes the default face.
        if (!manager.TryGetGlyphTypeface(
                new Typeface(family, avaloniaStyle, weight, FontStretch.Normal),
                out var glyph)
            || !string.Equals(glyph.FamilyName, PrimaryName(family), StringComparison.OrdinalIgnoreCase)
            || glyph.PlatformTypeface is null
            || !glyph.PlatformTypeface.TryGetStream(out var stream)
            || stream is null)
            return null;

        // FromStream owns this copy. Avalonia's stream stays with the glyph typeface.
        if (stream.CanSeek)
            stream.Position = 0;

        var copy = new MemoryStream();
        stream.CopyTo(copy);
        if (copy.Length == 0)
        {
            copy.Dispose();
            return null;
        }

        copy.Position = 0;
        return SKTypeface.FromStream(copy);
    }

    private static string PrimaryName(string family)
    {
        var separator = family.LastIndexOf('#');
        return separator < 0 ? family : family[(separator + 1)..].Trim();
    }

    private static (AvaloniaFontStyle Style, FontWeight Weight) Map(FontStyle style) => style switch
    {
        FontStyle.Bold => (AvaloniaFontStyle.Normal, FontWeight.Bold),
        FontStyle.Italic => (AvaloniaFontStyle.Italic, FontWeight.Normal),
        FontStyle.BoldItalic => (AvaloniaFontStyle.Italic, FontWeight.Bold),
        _ => (AvaloniaFontStyle.Normal, FontWeight.Normal)
    };
}
