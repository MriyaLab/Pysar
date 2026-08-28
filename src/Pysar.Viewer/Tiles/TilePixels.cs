using System.Runtime.InteropServices;
using SkiaSharp;

namespace Pysar.Viewer.Tiles;

/// <summary>Turns a drawn cell into raw pixel bytes a host can upload without PNG encode/decode.</summary>
public static class TilePixels
{
    /// <summary>
    ///     The cell's pixels as straight, non-premultiplied RGBA - the layout of a canvas
    ///     <c>ImageData</c>.
    /// </summary>
    /// <remarks>
    ///     Read through <see cref="SKImage.ReadPixels(SKImageInfo, nint, int, int, int)"/> rather
    ///     than handed straight out of the bitmap's own buffer. Skia's bitmaps are premultiplied and
    ///     their channel order follows the platform, which is BGRA on several of them; a canvas
    ///     wants neither. Naming the destination format makes Skia do both conversions, and makes
    ///     this correct on a platform whose defaults change rather than on the one it was written
    ///     on.
    /// </remarks>
    public static byte[] Rgba(SKBitmap bitmap)
        => Read(bitmap, SKColorType.Rgba8888, "RGBA");

    /// <summary>
    ///     The cell's pixels as straight, non-premultiplied BGRA - Avalonia
    ///     <c>WriteableBitmap</c> with <c>Bgra8888</c> / <c>Unpremul</c>.
    /// </summary>
    public static byte[] Bgra(SKBitmap bitmap)
        => Read(bitmap, SKColorType.Bgra8888, "BGRA");

    private static byte[] Read(SKBitmap bitmap, SKColorType colorType, string label)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var info = new SKImageInfo(bitmap.Width, bitmap.Height, colorType, SKAlphaType.Unpremul);
        var buffer = new byte[info.BytesSize];

        using var image = SKImage.FromBitmap(bitmap);

        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, pinned.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                throw new InvalidOperationException($"The cell's pixels could not be read as {label}.");
        }
        finally
        {
            pinned.Free();
        }

        return buffer;
    }
}
