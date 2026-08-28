using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using SkiaSharp;

namespace Pysar.Skia.Helpers;

internal static class RenderHelper
{
    internal static void DrawBackground(SKCanvas canvas, SKColor color, SKRect rect)
    {
        if (color == SKColors.Transparent) 
            return;
        
        using var paint = new SKPaint();
        paint.Color = color;
        canvas.DrawRect(rect, paint);
    }

    internal static void DrawBorder(
        SKCanvas canvas, 
        SKColor color, 
        Thickness borderThickness, 
        BorderLineStyle borderLineStyle,  
        SKRect rect, float scale)
    {
        var border = borderThickness;
        if (border == Thickness.Zero) 
            return;

        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsStroke = true;
        paint.IsAntialias = false;
        ApplyLineStyle(paint, borderLineStyle);

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
