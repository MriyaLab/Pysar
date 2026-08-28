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
    public void SecondDraw_ReusesDecodedBitmap()
    {
        ImageRenderer.ResetDecodeCountForTests();

        var path = Path.Combine(Path.GetTempPath(), $"qreport-img-{Guid.NewGuid():N}.png");
        try
        {
            using (var bmp = new SKBitmap(8, 8))
            using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(path, data.ToArray());

            ReportPlatformHandler.Create(new RealFilePlatformHandler());

            var image = new Image { Source = new FileImageSource(path) };
            var bounds = new Rect(0, 0, 50, 50);

            using (var surface = new SKBitmap(50, 50))
            using (var canvas = new SKCanvas(surface))
                ImageRenderer.Draw(image, bounds, new RenderContext(canvas, 1f));

            Assert.Equal(1, ImageRenderer.DecodeCount);

            using (var surface = new SKBitmap(50, 50))
            using (var canvas = new SKCanvas(surface))
                ImageRenderer.Draw(image, bounds, new RenderContext(canvas, 1f));

            Assert.Equal(1, ImageRenderer.DecodeCount);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
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
