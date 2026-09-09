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

    internal static void DrawBackground(SKCanvas canvas, SKColor color, SKRect rect, CornerRadius radius, float scale)
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
        canvas.DrawRoundRect(ToRoundRect(rect, radius, scale), paint);
    }

    /// <summary>
    ///     Draws the border. Square boxes get one line per side, so each side keeps its own thickness.
    ///     Once <paramref name="radius"/> is set the border becomes a single rounded stroke and
    ///     per-side thicknesses collapse to the largest of them — a corner arc has no point at which
    ///     to switch thickness.
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
        var strokeWidth = Math.Max(Math.Max(border.Left, border.Right), Math.Max(border.Top, border.Bottom))
            .ToPixels(scale);
        if (strokeWidth <= 0)
            return;

        paint.StrokeWidth = strokeWidth;
        paint.IsAntialias = true;

        // Inset by half the stroke so the border sits inside the box, as the straight-line branch does.
        var inset = rect;
        inset.Inflate(-strokeWidth / 2f, -strokeWidth / 2f);
        canvas.DrawRoundRect(ToRoundRect(inset, radius, scale), paint);
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
