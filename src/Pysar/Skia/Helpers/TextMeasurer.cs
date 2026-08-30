using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using SkiaSharp;

namespace Pysar.Skia.Helpers;

/// <summary>
/// Caches font objects for performance.
/// </summary>
internal sealed class FontCache
{
    private readonly IFontCollection? _fonts;
    private readonly Dictionary<string, SKTypeface> _resolved = [];

    /// <summary>Keys answered from the system rather than from <see cref="_fonts"/>.</summary>
    private readonly HashSet<string> _fromSystem = [];

    private readonly object _gate = new();
    private int _knownFontCount;

    public FontCache() { }

    /// <param name="fontCollection">
    ///     Read on every miss rather than copied here: a host is free to register a font after the
    ///     first glyph has been measured, and copying would latch whatever was registered first.
    /// </param>
    public FontCache(IFontCollection? fontCollection)
    {
        _fonts = fontCollection;
        _knownFontCount = fontCollection?.Count ?? 0;
    }

    public SKTypeface GetTypeface(Font font)
    {
        var key = SkiaFontCollection.GetCacheKey(font.Family, font.Style);

        lock (_gate)
        {
            DropSystemFallbacksIfFontsWereAdded();

            if (_resolved.TryGetValue(key, out var cached))
                return cached;

            if (_fonts is not null
                && _fonts.TryGetValue(key, out var registered)
                && registered is SKTypeface typeface)
            {
                _resolved[key] = typeface;
                return typeface;
            }

            var fallback = SKTypeface.FromFamilyName(font.Family, TextMeasurer.ConvertFontStyle(font.Style))
                           ?? SKTypeface.Default;

            _resolved[key] = fallback;
            _fromSystem.Add(key);

            return fallback;
        }
    }

    /// <summary>
    ///     A font registered after we answered its family from the system supersedes that answer, so
    ///     those entries are dropped once the collection grows. Entries that came from the collection
    ///     itself are kept - the collection never replaces an existing key.
    /// </summary>
    private void DropSystemFallbacksIfFontsWereAdded()
    {
        var count = _fonts?.Count ?? 0;
        if (count == _knownFontCount)
            return;

        foreach (var key in _fromSystem)
            _resolved.Remove(key);

        _fromSystem.Clear();
        _knownFontCount = count;
    }
}

/// <summary>
/// Provides font services using the font cache.
/// </summary>
internal static class FontService
{
    private static readonly object Gate = new();
    private static IFontCollection? _source;
    private static FontCache? _cache;

    public static SKTypeface GetTypeface(Font font) => CacheFor(Core.ReportPlatformHandler.FontCollection)
        .GetTypeface(font);

    /// <summary>
    ///     Rebuilds when the ambient collection is replaced - installing a second platform handler has
    ///     to take effect, and holding the first one's cache forever is what made that silently fail.
    /// </summary>
    private static FontCache CacheFor(IFontCollection fonts)
    {
        lock (Gate)
        {
            if (_cache is null || !ReferenceEquals(_source, fonts))
            {
                _source = fonts;
                _cache = new FontCache(fonts);
            }

            return _cache;
        }
    }
}

/// <summary>
/// Measures text and provides text layout utilities.
/// </summary>
internal static class TextMeasurer
{
    /// <summary>
    /// Converts font style to SkiaSharp font style.
    /// </summary>
    internal static SKFontStyle ConvertFontStyle(Core.Enums.FontStyle style) => style switch
    {
        Core.Enums.FontStyle.Bold => SKFontStyle.Bold,
        Core.Enums.FontStyle.Italic => SKFontStyle.Italic,
        Core.Enums.FontStyle.BoldItalic => SKFontStyle.BoldItalic,
        _ => SKFontStyle.Normal
    };

    /// <summary>
    /// Builds an SKFont from the specified text element, font size, and typeface.
    /// </summary>
    /// <remarks>
    ///     Hinting off and subpixel positioning on, which together make a glyph's width a plain
    ///     multiple of the scale it is drawn at. Skia's defaults fit each glyph to the pixel grid,
    ///     and fit it differently at every scale: a line then measures a fraction wider at one zoom
    ///     than another. The layout is measured once, at <c>MeasureScale</c>, so that fraction is
    ///     the drawn text disagreeing with the layout it was laid out into - and in a viewer that
    ///     draws the same page at several scales at once, it is the text appearing to resize as a
    ///     sharper layer replaces a softer one.
    /// </remarks>
    internal static SKFont BuildFont(Pysar.Elements.Text text, float fontSizePx, SKTypeface typeface)
        => new(typeface, fontSizePx)
        {
            Hinting = SKFontHinting.None,
            Subpixel = true
        };

    /// <summary>
    /// Measures the width of the specified text using the specified font.
    /// </summary>
    internal static float MeasureText(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        var glyphs = new ushort[text.Length];
        font.GetGlyphs(text, glyphs);
        return font.MeasureText(glyphs);
    }

    /// <summary>
    ///     The vertical extent the drawn lines actually occupy, in whatever units <paramref name="font"/>
    ///     and <paramref name="lineHeight"/> are expressed in. Lines advance by <paramref name="lineHeight"/>,
    ///     but <see cref="Rendering.TextDrawer"/> puts the first baseline an ascent below the box top, so the
    ///     last line's descenders reach ascent+descent below it. A font whose own extent exceeds its line
    ///     height - Kanit and most faces carrying Thai or Devanagari, or any explicitly small LineHeight -
    ///     would otherwise be clipped at the bottom by the enclosing container.
    /// </summary>
    internal static float MeasureLinesHeight(SKFont font, int lineCount, float lineHeight)
    {
        if (lineCount <= 0) return 0f;
        var fontExtent = font.Metrics.Descent - font.Metrics.Ascent;
        return Math.Max(lineCount * lineHeight, fontExtent + (lineCount - 1) * lineHeight);
    }

    /// <summary>
    /// Calculates the maximum number of lines that can fit in the given height.
    /// </summary>
    internal static int CalculateMaxLines(float maxHeightPx, float lineHeight)
    {
        if (maxHeightPx <= 0 || lineHeight <= 0) return int.MaxValue;
        return Math.Max(1, (int)Math.Floor(maxHeightPx / lineHeight));
    }

    #region Truncation Methods

    /// <summary>
    /// Applies tail truncation (ellipsis at end) to the text.
    /// </summary>
    internal static List<string> ApplyTailTruncation(string text, SKFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return [""];
        if (MeasureText(text, font) <= maxWidth) return [text];

        const string ellipsis = "...";
        var ellipsisWidth = MeasureText(ellipsis, font);
        
        if (ellipsisWidth > maxWidth) return [ellipsis];

        int left = 0;
        int right = text.Length;
        int bestLength = 0;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            var candidate = text[..mid] + ellipsis;
            var width = MeasureText(candidate, font);

            if (width <= maxWidth)
            {
                bestLength = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return [text[..bestLength] + ellipsis];
    }

    /// <summary>
    /// Applies head truncation (ellipsis at start) to the text.
    /// </summary>
    internal static List<string> ApplyHeadTruncation(string text, SKFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return [""];
        if (MeasureText(text, font) <= maxWidth) return [text];

        const string ellipsis = "...";
        var ellipsisWidth = MeasureText(ellipsis, font);
        
        if (ellipsisWidth > maxWidth) return [ellipsis];

        int left = 0;
        int right = text.Length;
        int bestStart = text.Length;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            var startIndex = Math.Max(0, text.Length - mid);
            var candidate = ellipsis + text[startIndex..];
            var width = MeasureText(candidate, font);

            if (width <= maxWidth)
            {
                bestStart = startIndex;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return [ellipsis + text[bestStart..]];
    }

    /// <summary>
    /// Applies middle truncation (ellipsis in middle) to the text.
    /// </summary>
    internal static List<string> ApplyMiddleTruncation(string text, SKFont font, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return [""];
        if (MeasureText(text, font) <= maxWidth) return [text];

        const string ellipsis = "...";
        var ellipsisWidth = MeasureText(ellipsis, font);
        
        if (ellipsisWidth > maxWidth) return [ellipsis];

        int left = 0;
        int right = text.Length;
        int bestLeft = 0;
        int bestRight = text.Length;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            int takeFromStart = mid / 2;
            int takeFromEnd = mid - takeFromStart;

            if (takeFromStart + takeFromEnd > text.Length)
            {
                right = mid - 1;
                continue;
            }

            var start = text[..takeFromStart];
            var end = text.Length > takeFromEnd ? text[^takeFromEnd..] : "";
            var candidate = start + ellipsis + end;

            if (MeasureText(candidate, font) <= maxWidth)
            {
                bestLeft = takeFromStart;
                bestRight = takeFromEnd;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        var result = text[..bestLeft] + ellipsis;
        if (bestRight > 0 && bestLeft + bestRight < text.Length)
        {
            result += text[^bestRight..];
        }

        return [result];
    }

    #endregion

    /// <summary>
    /// Performs word wrapping at word boundaries.
    /// </summary>
    internal static List<string> WordWrap(string text, SKFont font, float maxWidth, TextTrimming trimming)
    {
        if (string.IsNullOrEmpty(text)) return [""];

        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (MeasureText(candidate, font) <= maxWidth)
                currentLine = candidate;
            else
            {
                if (currentLine.Length > 0) lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (currentLine.Length > 0) lines.Add(currentLine);
        return lines.Count > 0 ? lines : [text];
    }

    #region Text Measurement Methods

    /// <summary>
    /// Measures text content based on the trimming mode.
    /// </summary>
    public static (float width, float height) MeasureTextByTrimmingMode(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        return element.TextTrimming switch
        {
            TextTrimming.None => MeasureTextNone(element, availableRect, scale),
            TextTrimming.Clip => MeasureTextClip(element, availableRect, scale),
            TextTrimming.WordWrap or TextTrimming.CharacterWrap => MeasureTextWordWrap(element, availableRect, scale),
            TextTrimming.TailTruncation => MeasureTextTailTruncation(element, availableRect, scale),
            TextTrimming.HeadTruncation => MeasureTextHeadTruncation(element, availableRect, scale),
            TextTrimming.MiddleTruncation => MeasureTextMiddleTruncation(element, availableRect, scale),
            _ => MeasureTextWordWrap(element, availableRect, scale)
        };
    }

    /// <summary>
    /// Gets the content width based on element size and available rect.
    /// </summary>
    public static float GetContentWidth(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        // availableRect / Fixed size are already the border-box (margin applied by LayoutEngine).
        if (element.Size.Width.IsFixed)
            return element.Size.Width.Value - element.Padding.Left - element.Padding.Right;

        return availableRect.Width - element.Padding.Left - element.Padding.Right;
    }

    /// <summary>
    /// Gets the content height based on element size and available rect.
    /// Returns 0 when height is unrestricted (Auto / <see cref="Text.AutoHeight"/>) so WordWrap
    /// grows to every line instead of height-clipping with an ellipsis.
    /// When <see cref="ReportElement.MaxSize"/> height is fixed, that cap (minus padding) limits wrap.
    /// </summary>
    public static float GetContentHeight(Pysar.Elements.Text element, Rect availableRect)
    {
        if (element.MaxSize.Height.IsFixed)
        {
            var cap = element.MaxSize.Height.Value - element.Padding.Top - element.Padding.Bottom;
            if (element.Size.Height.IsFixed)
                cap = Math.Min(cap, element.Size.Height.Value - element.Padding.Top - element.Padding.Bottom);
            return Math.Max(0f, cap);
        }

        if (element.AutoHeight || element.Size.Height.IsAuto)
            return 0f; // unlimited

        if (element.Size.Height.IsFixed)
            return element.Size.Height.Value - element.Padding.Top - element.Padding.Bottom;

        return availableRect.Height - element.Padding.Top - element.Padding.Bottom;
    }

    /// <summary>
    /// Measures text with no trimming - single line, may overflow.
    /// </summary>
    public static (float width, float height) MeasureTextNone(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var textWidth = MeasureText(element.Content, font) / scale;
        var textHeight = MeasureLinesHeight(font, 1, element.Font.Size * element.LineHeight * scale) / scale;
        
        return (textWidth, textHeight);
    }

    /// <summary>
    /// Measures text with clip - single line, clipped to bounds.
    /// </summary>
    public static (float width, float height) MeasureTextClip(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var maxWidth = GetContentWidth(element, availableRect, scale);
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var actualWidth = Math.Min(MeasureText(element.Content, font) / scale, maxWidth);
        var textHeight = MeasureLinesHeight(font, 1, element.Font.Size * element.LineHeight * scale) / scale;
        
        return (actualWidth, textHeight);
    }

    /// <summary>
    /// Measures text with word wrap - multiple lines, wraps at word boundaries.
    /// </summary>
    public static (float width, float height) MeasureTextWordWrap(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var maxWidth = GetContentWidth(element, availableRect, scale);
        var maxHeight = GetContentHeight(element, availableRect);
        
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var lines = WordWrap(element.Content, font, maxWidth * scale, TextTrimming.WordWrap);
        var lineHeight = element.Font.Size * element.LineHeight * scale;
        var textHeightPx = MeasureLinesHeight(font, lines.Count, lineHeight);
        
        var maxHeightPx = maxHeight * scale;
        if (textHeightPx > maxHeightPx && maxHeightPx > 0)
        {
            var maxLines = CalculateMaxLines(maxHeightPx, lineHeight);
            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
                var lastLine = lines[^1];
                var ellipsis = "...";
                
                for (int i = lastLine.Length; i >= 0; i--)
                {
                    var truncated = lastLine[..i] + ellipsis;
                    if (MeasureText(truncated, font) <= maxWidth * scale)
                    {
                        lines[^1] = truncated;
                        break;
                    }
                }
                
                textHeightPx = MeasureLinesHeight(font, lines.Count, lineHeight);
            }
        }
        
        var textWidth = lines.Count > 0 ? lines.Max(line => MeasureText(line, font)) / scale : 0;
        
        return (textWidth, textHeightPx / scale);
    }

    /// <summary>
    /// Measures text with tail truncation - single line with ellipsis at end.
    /// </summary>
    public static (float width, float height) MeasureTextTailTruncation(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var maxWidth = GetContentWidth(element, availableRect, scale);
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var lines = ApplyTailTruncation(element.Content, font, maxWidth * scale);
        var textWidth = lines.Count > 0 ? MeasureText(lines[0], font) / scale : 0;
        var textHeight = MeasureLinesHeight(font, 1, element.Font.Size * element.LineHeight * scale) / scale;
        
        return (textWidth, textHeight);
    }

    /// <summary>
    /// Measures text with head truncation - single line with ellipsis at start.
    /// </summary>
    public static (float width, float height) MeasureTextHeadTruncation(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var maxWidth = GetContentWidth(element, availableRect, scale);
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var lines = ApplyHeadTruncation(element.Content, font, maxWidth * scale);
        var textWidth = lines.Count > 0 ? MeasureText(lines[0], font) / scale : 0;
        var textHeight = MeasureLinesHeight(font, 1, element.Font.Size * element.LineHeight * scale) / scale;
        
        return (textWidth, textHeight);
    }

    /// <summary>
    /// Measures text with middle truncation - single line with ellipsis in middle.
    /// </summary>
    public static (float width, float height) MeasureTextMiddleTruncation(Pysar.Elements.Text element, Rect availableRect, float scale)
    {
        var maxWidth = GetContentWidth(element, availableRect, scale);
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));
        
        var lines = ApplyMiddleTruncation(element.Content, font, maxWidth * scale);
        var textWidth = lines.Count > 0 ? MeasureText(lines[0], font) / scale : 0;
        var textHeight = MeasureLinesHeight(font, 1, element.Font.Size * element.LineHeight * scale) / scale;
        
        return (textWidth, textHeight);
    }

    /// <summary>
    /// Gets lines for rendering based on the trimming mode.
    /// </summary>
    public static List<string> GetLinesForRendering(Pysar.Elements.Text element, float maxWidth, float maxHeight, float scale)
    {
        var fontSize = element.Font.Size * scale;
        using var font = BuildFont(element, fontSize, FontService.GetTypeface(element.Font));

        return element.TextTrimming switch
        {
            TextTrimming.None => [element.Content],
            TextTrimming.Clip => [element.Content],
            TextTrimming.WordWrap or TextTrimming.CharacterWrap => GetLinesWordWrap(element, font, maxWidth, maxHeight, scale),
            TextTrimming.TailTruncation => ApplyTailTruncation(element.Content, font, maxWidth),
            TextTrimming.HeadTruncation => ApplyHeadTruncation(element.Content, font, maxWidth),
            TextTrimming.MiddleTruncation => ApplyMiddleTruncation(element.Content, font, maxWidth),
            _ => GetLinesWordWrap(element, font, maxWidth, maxHeight, scale)
        };
    }

    /// <summary>
    /// Gets lines for word wrap mode with height constraint.
    /// </summary>
    private static List<string> GetLinesWordWrap(Pysar.Elements.Text element, SKFont font, float maxWidth, float maxHeight, float scale)
    {
        var lines = WordWrap(element.Content, font, maxWidth, TextTrimming.WordWrap);
        var lineHeight = element.Font.Size * element.LineHeight * scale;
        var textHeight = lines.Count * lineHeight;

        if (textHeight > maxHeight && maxHeight > 0)
        {
            var maxLines = CalculateMaxLines(maxHeight, lineHeight);
            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
                var lastLine = lines[^1];
                var ellipsis = "...";

                for (int i = lastLine.Length; i >= 0; i--)
                {
                    var truncated = lastLine[..i] + ellipsis;
                    if (MeasureText(truncated, font) <= maxWidth)
                    {
                        lines[^1] = truncated;
                        break;
                    }
                }
            }
        }

        return lines;
    }

    #endregion
}
