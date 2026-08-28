using Pysar.Core.Structs;
using SkiaSharp;

namespace Pysar.Skia.Helpers;

public static class Extensions
{
    /// <summary>
    /// Converts report Rect to SKRect.
    /// </summary>
    public static SKRect ToSkiaRect(this Rect rect, float scale)
    {
        return new SKRect(rect.Left.ToPixels(scale), rect.Top.ToPixels(scale), rect.Right.ToPixels(scale), rect.Bottom.ToPixels(scale));
    }
    
    /// <summary>
    /// Converts report Color to SKColor.
    /// </summary>
    public static SKColor ToSkiaColor(this Color color) => new(color.R, color.G, color.B, color.A);
    
    /// <summary>
    /// Converts report points to pixels at the current scale.
    /// </summary>
    public static float ToPixels(this float pt, float scale) => pt * scale;
}