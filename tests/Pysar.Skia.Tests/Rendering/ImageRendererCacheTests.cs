using System.Diagnostics.CodeAnalysis;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class ImageRendererCacheTests
{
    [Fact]
    public async Task SecondDraw_OnSameCache_ReusesDecodedBitmap()
    {
        var pngBytes = CreateOnePixelPng(SKColors.Red);
        using var cache = new ImageRenderCache();
        var source = new CountingImageSource(pngBytes);
        var image = new Image { Source = source };

        await ImageRenderer.PrefetchAsync([source], cache, CancellationToken.None);

        Draw(image, cache);
        Assert.Equal(1, cache.DecodeCount);

        Draw(image, cache);
        Assert.Equal(1, cache.DecodeCount);
        Assert.Equal(1, source.LoadCount);
    }

    [Fact]
    public async Task Dispose_ForcesNextCacheToDecodeAgain()
    {
        var pngBytes = CreateOnePixelPng(SKColors.Blue);
        var source = new CountingImageSource(pngBytes);
        var image = new Image { Source = source };

        var first = new ImageRenderCache();
        await ImageRenderer.PrefetchAsync([source], first, CancellationToken.None);
        Draw(image, first);
        Assert.Equal(1, first.DecodeCount);
        first.Dispose();

        using var second = new ImageRenderCache();
        await ImageRenderer.PrefetchAsync([source], second, CancellationToken.None);
        Draw(image, second);

        Assert.Equal(1, second.DecodeCount);
        Assert.Equal(2, source.LoadCount);
    }

    [Fact]
    public async Task PrefetchAsync_ThrowingSource_RecordsFailureAndDoesNotAbortOthers()
    {
        var goodBytes = CreateOnePixelPng(SKColors.Green);
        var good = new CountingImageSource(goodBytes);
        var bad = new ThrowingImageSource();
        using var cache = new ImageRenderCache();

        await ImageRenderer.PrefetchAsync([bad, good], cache, CancellationToken.None);

        Assert.Equal(1, good.LoadCount);
        var failure = Assert.Single(cache.Failures);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Contains("simulated load failure", failure.Exception.Message, StringComparison.Ordinal);

        var image = new Image { Source = good };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f) { Images = cache });
        Assert.Equal(SKColors.Green, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public void TwoCaches_DoNotShareDecodedBitmaps()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pysar-img-{Guid.NewGuid():N}.png");
        try
        {
            using (var bmp = new SKBitmap(8, 8))
            using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(path, data.ToArray());

            ReportPlatformHandler.Create(new RealFilePlatformHandler());
            var image = new Image { Source = new FileImageSource(path) };
            var bounds = new Rect(0, 0, 50, 50);

            using var first = new ImageRenderCache();
            using (var surface = new SKBitmap(50, 50))
            using (var canvas = new SKCanvas(surface))
                ImageRenderer.Draw(image, bounds, new RenderContext(canvas, 1f) { Images = first });
            Assert.Equal(1, first.DecodeCount);

            using var second = new ImageRenderCache();
            using (var surface = new SKBitmap(50, 50))
            using (var canvas = new SKCanvas(surface))
                ImageRenderer.Draw(image, bounds, new RenderContext(canvas, 1f) { Images = second });
            Assert.Equal(1, second.DecodeCount);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Dispose_ThenGetOrSetBytes_ThrowsObjectDisposedException()
    {
        var cache = new ImageRenderCache();
        cache.SetBytes("k", [1, 2, 3]);
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetBytes("k"));
        Assert.Throws<ObjectDisposedException>(() => cache.SetBytes("k", [4]));
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var cache = new ImageRenderCache();
        cache.Dispose();
        cache.Dispose();
    }

    private static void Draw(Image image, ImageRenderCache cache)
    {
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f) { Images = cache });
    }

    private static byte[] CreateOnePixelPng(SKColor color)
    {
        using var image = new SKBitmap(1, 1);
        image.SetPixel(0, 0, color);
        using var skImage = SKImage.FromBitmap(image);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed class CountingImageSource(byte[]? bytes) : ImageSource
    {
        public int LoadCount { get; private set; }

        public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
        {
            LoadCount++;
            return Task.FromResult(bytes);
        }
    }

    private sealed class ThrowingImageSource : ImageSource
    {
        public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("simulated load failure");
    }

    private sealed class RealFilePlatformHandler : IReportPlatformHandler
    {
        public IFileSystem FileSystem { get; } = new RealSyncFileSystem();
        public IFontCollection FontCollection { get; } = new EmptyFontCollection();
    }

    private sealed class RealSyncFileSystem : IFileSystem, ISyncFileSystem
    {
        public Task<byte[]?> ReadFileAsync(string filePath)
            => Task.FromResult(ReadFile(filePath));

        public byte[]? ReadFile(string filePath)
            => File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && File.Exists(filePath);
    }

    private sealed class EmptyFontCollection : Dictionary<string, object>, IFontCollection
    {
        public IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal) => this;
    }
}
