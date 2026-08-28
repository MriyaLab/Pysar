using Pysar.Viewer.Tiles;
using SkiaSharp;
using Xunit;

namespace Pysar.Blazor.Tests;

public class TilePixelsTests
{
    private static SKBitmap OnePixel(SKColor colour)
    {
        var bitmap = new SKBitmap(1, 1);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(colour);
            canvas.Flush();
        }

        return bitmap;
    }

    [Fact]
    public void Pixels_ComeOutInTheOrderACanvasExpects()
    {
        using var bitmap = OnePixel(new SKColor(red: 10, green: 20, blue: 30, alpha: 255));

        var pixels = TilePixels.Rgba(bitmap);

        // putImageData reads red, green, blue, alpha in that order. Skia's own bitmaps are BGRA on
        // several platforms, so a straight copy of its buffer would swap red and blue - which shows
        // up as a page that renders perfectly but in the wrong colours.
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, pixels);
    }

    [Fact]
    public void TheBufferIs_FourBytesPerPixel()
    {
        using var bitmap = new SKBitmap(7, 3);

        Assert.Equal(7 * 3 * 4, TilePixels.Rgba(bitmap).Length);
    }

    [Fact]
    public void AnOpaquePixel_StaysFullyOpaque()
    {
        using var bitmap = OnePixel(SKColors.White);

        var pixels = TilePixels.Rgba(bitmap);

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels);
    }

    [Fact]
    public void ATranslucentPixel_ComesBackWithItsColourUndimmed()
    {
        // The test that can tell the two implementations apart. Skia stores colours multiplied by
        // their alpha, so a half-transparent white sits in the buffer as roughly 128, while a canvas
        // expects the colour at full strength with the alpha alongside it. Every other test here
        // passes just as happily against a straight copy of Skia's own buffer, because this
        // machine's bitmaps already happen to be RGBA and a report's cells are opaque - so without
        // this one, nothing would fail if the conversion were dropped.
        using var bitmap = OnePixel(new SKColor(255, 255, 255, 128));

        var pixels = TilePixels.Rgba(bitmap);

        Assert.Equal(128, pixels[3]);
        Assert.InRange(pixels[0], 254, 255);
    }
}
