using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class FontImageSourceRendererTests
{
    [Fact]
    public async Task PrefetchAsync_DoesNotCallLoadAsyncOnFontImageSource()
    {
        var source = new CountingFontImageSource { Glyph = "A" };

        await ImageRenderer.PrefetchAsync([source], CancellationToken.None);

        Assert.Equal(0, source.LoadCount);
    }

    [Fact]
    public void Draw_EmptyGlyph_LeavesCanvasUntouched()
    {
        var image = new Image { Source = new FontImageSource { Glyph = "" } };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var untouched = bitmap.GetPixel(25, 25);

        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f));

        Assert.Equal(untouched, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public void Draw_NonEmptyGlyph_PaintsInk()
    {
        var image = new Image
        {
            Source = new FontImageSource
            {
                Glyph = "A",
                FontFamily = "Arial",
                Size = 24,
                Color = Colors.Black
            }
        };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var exception = Record.Exception(() =>
            ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f)));

        Assert.Null(exception);
        Assert.True(HasOpaquePixel(bitmap));
    }

    private static bool HasOpaquePixel(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                    return true;
            }
        }

        return false;
    }

    private sealed class CountingFontImageSource : FontImageSource
    {
        public int LoadCount { get; private set; }

        public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
        {
            LoadCount++;
            return base.LoadAsync(ct);
        }
    }
}
