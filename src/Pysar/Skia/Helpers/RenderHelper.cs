using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using SkiaSharp;

namespace Pysar.Skia.Helpers;

internal static class RenderHelper
{
    /// <summary>
    ///     Builds the rounded box for <paramref name="rect"/>, which is already in pixels — only the
    ///     radii are scaled here. Every radius is clamped to half the shorter side, so an oversized
    ///     value degrades to a capsule instead of a malformed path.
    /// </summary>
    internal static SKRoundRect ToRoundRect(SKRect rect, CornerRadius radius, float scale)
    {
        var max = Math.Min(rect.Width, rect.Height) / 2f;
        float Corner(float value) => Math.Clamp(value.ToPixels(scale), 0f, max);

        var rrect = new SKRoundRect();
        // Skia takes the corners clockwise from the upper-left; CornerRadius is in the MAUI order
        // (top-left, top-right, bottom-left, bottom-right), so the last two are swapped here.
        rrect.SetRectRadii(rect,
        [
            new SKPoint(Corner(radius.TopLeft), Corner(radius.TopLeft)),
            new SKPoint(Corner(radius.TopRight), Corner(radius.TopRight)),
            new SKPoint(Corner(radius.BottomRight), Corner(radius.BottomRight)),
            new SKPoint(Corner(radius.BottomLeft), Corner(radius.BottomLeft))
        ]);
        return rrect;
    }

    /// <summary>
    ///     The box a rounded container clips its children to, and the border's inner edge: the
    ///     padding-box — <paramref name="rect"/> deflated by the border — carrying the authored
    ///     <paramref name="radius"/> unchanged. <c>CornerRadius</c> is the content corner, so a
    ///     thicker border grows the box outward instead of eating the rounding away.
    /// </summary>
    internal static SKRoundRect ToInnerRoundRect(SKRect rect, CornerRadius radius, Thickness border, float scale)
    {
        var inset = CollapsedBorder(border);
        if (inset <= 0)
            return ToRoundRect(rect, radius, scale);

        var inner = rect;
        inner.Inflate(-inset.ToPixels(scale), -inset.ToPixels(scale));
        if (inner.Width <= 0 || inner.Height <= 0)
            return new SKRoundRect(SKRect.Create(rect.MidX, rect.MidY, 0, 0));

        return ToRoundRect(inner, radius, scale);
    }

    /// <summary>
    ///     The box's own corner: the authored radius plus the border, since <c>CornerRadius</c> names
    ///     the inner (content) corner and the border wraps around it. Background and border's outer
    ///     edge both use this, so they stay on the same arc.
    /// </summary>
    private static CornerRadius OuterRadius(CornerRadius radius, Thickness border) =>
        Inflate(radius, CollapsedBorder(border));

    /// <summary>Once the box is rounded there is a single ring, so per-side thicknesses collapse to their maximum.</summary>
    private static float CollapsedBorder(Thickness border) =>
        Math.Max(Math.Max(border.Left, border.Right), Math.Max(border.Top, border.Bottom));

    /// <summary>Grows every corner by <paramref name="by"/>.</summary>
    private static CornerRadius Inflate(CornerRadius radius, float by) => new(
        radius.TopLeft + by,
        radius.TopRight + by,
        radius.BottomLeft + by,
        radius.BottomRight + by);

    internal static void DrawBackground(
        SKCanvas canvas, SKColor color, SKRect rect, CornerRadius radius, Thickness border, float scale)
    {
        if (color == SKColors.Transparent)
            return;

        using var paint = new SKPaint();
        paint.Color = color;

        if (radius.IsZero)
        {
            canvas.DrawRect(rect, paint);
            return;
        }

        paint.IsAntialias = true;
        canvas.DrawRoundRect(ToRoundRect(rect, OuterRadius(radius, border), scale), paint);
    }

    /// <summary>
    ///     Draws the border. Square boxes get one line per side, so each side keeps its own thickness.
    ///     Once <paramref name="radius"/> is set the border becomes a single rounded ring, running from
    ///     the content corner (<paramref name="radius"/>) out to the box corner (radius + thickness),
    ///     and per-side thicknesses collapse to the largest of them — a corner arc has no point at
    ///     which to switch thickness.
    /// </summary>
    internal static void DrawBorder(
        SKCanvas canvas,
        SKColor color,
        Thickness borderThickness,
        BorderLineStyle borderLineStyle,
        SKRect rect, float scale,
        CornerRadius radius)
    {
        var border = borderThickness;
        if (border == Thickness.Zero)
            return;

        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsStroke = true;
        paint.IsAntialias = false;
        ApplyLineStyle(paint, borderLineStyle);

        if (!radius.IsZero)
        {
            DrawRoundedBorder(canvas, paint, border, rect, scale, radius);
            return;
        }

        if (border.Top > 0)
        {
            float sw = border.Top.ToPixels(scale);
            paint.StrokeWidth = sw;
            canvas.DrawLine(rect.Left, rect.Top + sw / 2, rect.Right, rect.Top + sw / 2, paint);
        }

        if (border.Bottom > 0)
        {
            float sw = border.Bottom.ToPixels(scale);
            paint.StrokeWidth = sw;
            canvas.DrawLine(rect.Left, rect.Bottom - sw / 2, rect.Right, rect.Bottom - sw / 2, paint);
        }

        if (border.Left > 0)
        {
            float sw = border.Left.ToPixels(scale);
            paint.StrokeWidth = sw;
            canvas.DrawLine(rect.Left + sw / 2, rect.Top, rect.Left + sw / 2, rect.Bottom, paint);
        }

        if (border.Right > 0)
        {
            float sw = border.Right.ToPixels(scale);
            paint.StrokeWidth = sw;
            canvas.DrawLine(rect.Right - sw / 2, rect.Top, rect.Right - sw / 2, rect.Bottom, paint);
        }
    }

    private static void DrawRoundedBorder(
        SKCanvas canvas, SKPaint paint, Thickness border, SKRect rect, float scale, CornerRadius radius)
    {
        var widthPt = CollapsedBorder(border);
        if (widthPt <= 0)
            return;

        paint.IsAntialias = true;

        // A dashed border is a path effect, which only has meaning on a stroke: keep the centred
        // stroke there, on a path midway between the two edges — the authored radius plus half the
        // width — so the dashes sit where the solid ring would.
        if (paint.PathEffect is not null)
        {
            paint.StrokeWidth = widthPt.ToPixels(scale);
            var stroked = rect;
            stroked.Inflate(-paint.StrokeWidth / 2f, -paint.StrokeWidth / 2f);
            canvas.DrawRoundRect(ToRoundRect(stroked, Inflate(radius, widthPt / 2f), scale), paint);
            return;
        }

        // A solid border is the ring between the box and the content: fill it directly instead of
        // stroking a middle path. A centred stroke can only ever place one edge exactly; the ring
        // pins both — the outer on r + w, the inner on r, which is the very box ToInnerRoundRect
        // clips the content to.
        paint.IsStroke = false;
        using var ring = new SKPath { FillType = SKPathFillType.EvenOdd };
        ring.AddRoundRect(ToRoundRect(rect, OuterRadius(radius, border), scale));
        ring.AddRoundRect(ToInnerRoundRect(rect, radius, border, scale));
        canvas.DrawPath(ring, paint);
    }

    private static void ApplyLineStyle(SKPaint paint, BorderLineStyle style)
    {
        paint.PathEffect = style switch
        {
            BorderLineStyle.Dot => SKPathEffect.CreateDash([1f, 4f], 0f),
            BorderLineStyle.Dash => SKPathEffect.CreateDash([8f, 4f], 0f),
            BorderLineStyle.DashDot => SKPathEffect.CreateDash([8f, 4f, 1f, 4f], 0f),
            _ => null
        };
    }
}
