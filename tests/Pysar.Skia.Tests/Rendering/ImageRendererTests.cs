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

public class ImageRendererTests
{
    [Fact]
    public async Task PrefetchAsync_ThenDraw_RendersCachedFileImageWithoutBlockingAsync()
    {
        var pngBytes = CreateOnePixelPng(SKColors.Red);
        ReportPlatformHandler.Create(new FakePlatformHandler(("test.png", pngBytes)));
        var image = new Image { Source = new FileImageSource("test.png") };
        var sources = new ImageSource[] { image.Source };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var ctx = new RenderContext(canvas, 1f);

        await ImageRenderer.PrefetchAsync(sources, CancellationToken.None);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), ctx);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), ctx);
        canvas.Flush();

        Assert.Equal(SKColors.Red, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public async Task PrefetchAsync_LoadsSourceIntoCache_DrawDoesNotCallLoadAgain()
    {
        var pngBytes = CreateOnePixelPng(SKColors.Blue);
        var source = new CountingImageSource(pngBytes);
        var sources = new ImageSource[] { source };

        await ImageRenderer.PrefetchAsync(sources, CancellationToken.None);

        Assert.Equal(1, source.LoadCount);

        var image = new Image { Source = source };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var ctx = new RenderContext(canvas, 1f);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), ctx);

        Assert.Equal(1, source.LoadCount);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public async Task Draw_WithoutPrefetch_AndAsyncOnlySource_DoesNotCallGetResult_SkipsOrNoThrow()
    {
        var hungTcs = new TaskCompletionSource<byte[]?>();
        var source = new NeverCompletingImageSource(hungTcs);
        var image = new Image { Source = source };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        var ctx = new RenderContext(canvas, 1f);

        var drawTask = Task.Run(() => ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), ctx));
        var completed = await Task.WhenAny(drawTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(drawTask, completed);
        Assert.False(hungTcs.Task.IsCompleted);
    }

    [Fact]
    public async Task PrefetchAsync_ThrowingSource_DoesNotAbortOtherSources()
    {
        var goodBytes = CreateOnePixelPng(SKColors.Green);
        var good = new CountingImageSource(goodBytes);
        var bad = new ThrowingImageSource();

        await ImageRenderer.PrefetchAsync([bad, good], CancellationToken.None);

        Assert.Equal(1, good.LoadCount);

        var image = new Image { Source = good };
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f));

        Assert.Equal(SKColors.Green, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public async Task PrefetchAsync_ThenDraw_RendersStreamImageSourceFromCache()
    {
        var pngBytes = CreateOnePixelPng(SKColors.Yellow);
        var source = new StreamImageSource(() => new MemoryStream(pngBytes));
        var image = new Image { Source = source };

        await ImageRenderer.PrefetchAsync([source], CancellationToken.None);
        using var bitmap = new SKBitmap(50, 50);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        ImageRenderer.Draw(image, new Rect(0, 0, 50, 50), new RenderContext(canvas, 1f));

        Assert.Equal(SKColors.Yellow, bitmap.GetPixel(25, 25));
    }

    [Fact]
    public async Task PrefetchAsync_NullSources_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ImageRenderer.PrefetchAsync(null!, CancellationToken.None));
    }

    private static byte[] CreateOnePixelPng(SKColor color)
    {
        using var image = new SKBitmap(1, 1);
        image.SetPixel(0, 0, color);
        using var skImage = SKImage.FromBitmap(image);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed class FakePlatformHandler : IReportPlatformHandler
    {
        public FakePlatformHandler(params (string Path, byte[] Content)[] files)
            => FileSystem = new FakeSyncFileSystem(files);

        public IFileSystem FileSystem { get; }

        public IFontCollection FontCollection { get; } = new FakeFontCollection();
    }

    private sealed class FakeSyncFileSystem : IFileSystem, ISyncFileSystem
    {
        private readonly Dictionary<string, byte[]> _files;

        public FakeSyncFileSystem(params (string Path, byte[] Content)[] files)
            => _files = files.ToDictionary(file => file.Path, file => file.Content);

        public Task<byte[]?> ReadFileAsync(string filePath)
            => Task.FromResult(_files.GetValueOrDefault(filePath));

        public byte[]? ReadFile(string filePath)
            => _files.GetValueOrDefault(filePath);

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && _files.ContainsKey(filePath);
    }

    private sealed class FakeFontCollection : Dictionary<string, object>, IFontCollection
    {
        public IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal) => this;
    }

    private sealed class CountingImageSource(byte[]? bytes = null) : ImageSource
    {
        public int LoadCount { get; private set; }

        public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
        {
            LoadCount++;
            return Task.FromResult(bytes);
        }
    }

    private sealed class NeverCompletingImageSource(TaskCompletionSource<byte[]?> tcs) : ImageSource
    {
        public override Task<byte[]?> LoadAsync(CancellationToken ct = default) => tcs.Task;
    }

    private sealed class ThrowingImageSource : ImageSource
    {
        public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("simulated load failure");
    }
}
