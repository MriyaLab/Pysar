using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using SkiaSharp;

namespace Pysar.Skia.Rendering;

/// <summary>Built-in drawer for <see cref="Text"/> — draws lines within the node's content box.</summary>
internal sealed class TextDrawer : IElementDrawer
{
    public void Draw(LayoutNode node, RenderContext ctx)
    {
        var element = (Text)node.Element;
        var bounds = node.Bounds;
        // Bounds is already the border-box (margin applied at measure). Only padding insets content.
        var padding = element.Padding;

        var contentLeft = bounds.Left + padding.Left;
        var contentTop = bounds.Top + padding.Top;
        var contentRight = bounds.Right - padding.Right;
        var contentBottom = bounds.Bottom - padding.Bottom;

        var contentWidth = contentRight - contentLeft;
        var contentHeight = contentBottom - contentTop;

        var x = ctx.ToPixels(contentLeft);
        var y = ctx.ToPixels(contentTop);
        var maxWidth = ctx.ToPixels(contentWidth);
        var maxHeight = ctx.ToPixels(contentHeight);

        var fontSize = ctx.ToPixels(element.Font.Size);
        using var font = TextMeasurer.BuildFont(element, fontSize, FontService.GetTypeface(element.Font));

        // Auto dimensions size the text to its own content, whose extent round-trips through the
        // point↔pixel scale during measurement. A sub-pixel shrink at render would make WordWrap re-break
        // (and the height limit then truncate) the very line the text was sized for. Guard Auto dimensions
        // by one pixel — far below a pixel of real content — so the text keeps its measured line breaks.
        // Line breaking is decided in the space the layout was measured in, not the space it is being
        // drawn in. They are the same for a plain render; a viewer measures once and draws at the
        // zoom, and deciding here would let a line that fitted during measurement pick up an ellipsis
        // purely because the user zoomed in - the report's own text would change with the zoom.
        var measuredWidth = ctx.ToMeasuredPixels(contentWidth);
        var measuredHeight = ctx.ToMeasuredPixels(contentHeight);

        var wrapWidth = element.Size.Width.IsAuto ? measuredWidth + 1f : measuredWidth;
        var wrapHeight = element.Size.Height.IsAuto || element.AutoHeight ? measuredHeight + 1f : measuredHeight;

        var lines = TextMeasurer.GetLinesForRendering(element, wrapWidth, wrapHeight, ctx.MeasureScale);

        var lineHeight = ctx.ToPixels(element.Font.Size * element.LineHeight);

        var drawY = AlignVertical(font, lines.Count, lineHeight, y, maxHeight, element.VerticalTextAlignment);

        using var paint = new SKPaint
        {
            Color = element.Font.Color.ToSkiaColor(),
            IsAntialias = true
        };

        foreach (var line in lines)
        {
            var lineX = AlignHorizontal(line, font, x, maxWidth, element.HorizontalTextAlignment);
            ctx.Canvas.DrawText(line, lineX, drawY, SKTextAlign.Left, font, paint);
            drawY += lineHeight;
        }
    }

    private static float AlignHorizontal(string line, SKFont font, float x, float maxWidth, TextAlignment alignment)
    {
        var lineWidth = TextMeasurer.MeasureText(line, font);
        return alignment switch
        {
            TextAlignment.Center => x + (maxWidth - lineWidth) / 2f,
            TextAlignment.End => x + maxWidth - lineWidth,
            _ => x
        };
    }

    private static float AlignVertical(SKFont font, int lineCount, float lineHeight, float y, float boxHeight, TextAlignment alignment)
    {
        var totalHeight = lineCount * lineHeight;
        var offset = alignment switch
        {
            TextAlignment.Center => (boxHeight - totalHeight) / 2f,
            TextAlignment.End => boxHeight - totalHeight,
            _ => 0f
        };
        return y + offset - font.Metrics.Ascent;
    }
}
