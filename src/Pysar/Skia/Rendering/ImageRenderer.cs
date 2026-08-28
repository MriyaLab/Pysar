using System.Runtime.CompilerServices;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Helpers;
using SkiaSharp;
using Svg.Skia;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Draws an <see cref="Image"/> into its measured bounds. Bytes are loaded via
///     <see cref="PrefetchAsync"/> (or a sync file fallback); <see cref="Draw"/> only decodes the
///     cache and places the bitmap per <see cref="Aspect"/>.
/// </summary>
internal static class ImageRenderer
{
    private static readonly IImageCache Cache = new LocalImageCache();
    private static readonly Dictionary<string, SKBitmap> Bitmaps = new();
    private static readonly object BitmapGate = new();

    internal static int DecodeCount { get; private set; }

    internal static void ResetDecodeCountForTests()
    {
        lock (BitmapGate)
        {
            DecodeCount = 0;
        }
    }

    /// <summary>Loads image bytes into the cache. Safe to call from async render entry points.</summary>
    public static async Task PrefetchAsync(IEnumerable<ImageSource> sources, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            if (source is null) continue;
            if (source is FontImageSource) continue;
            var key = CreateCacheKey(source);
            if (Cache.Get(key) is not null) continue;

            try
            {
                var bytes = await LoadBytesAsync(source, ct).ConfigureAwait(false);
                if (bytes is not null)
                    Cache.Set(key, bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Same as the old Draw path: one bad image must not abort the whole render.
            }
        }
    }

    public static void Draw(Image image, Rect bounds, RenderContext ctx)
    {
        if (image.Source is null || bounds.IsEmpty) return;

        if (image.Source is FontImageSource font)
        {
            DrawGlyph(font, image, bounds, ctx);
            return;
        }

        var bitmap = LoadBitmap(image.Source);
        if (bitmap is null) return;

        var innerRect = bounds.ToElementLayout(image.Padding).InnerRect;
        var destinationBounds = innerRect.ToSkiaRect(ctx.Scale);
        var (sourceRect, destinationRect) = CalculatePlacement(bitmap, destinationBounds, image.Aspect);

        using var paint = new SKPaint { IsAntialias = true };
        ctx.Canvas.DrawBitmap(bitmap, sourceRect, destinationRect, SKSamplingOptions.Default, paint);
    }

    private static void DrawGlyph(FontImageSource source, Image image, Rect bounds, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(source.Glyph)) return;

        try
        {
            var fontSpec = new Font(source.FontFamily, source.Size, source.Color);
            var typeface = FontService.GetTypeface(fontSpec);
            var fontSizePx = ctx.ToPixels(source.Size);
            using var font = new SKFont(typeface, fontSizePx)
            {
                Hinting = SKFontHinting.None,
                Subpixel = true
            };

            var naturalWidth = TextMeasurer.MeasureText(source.Glyph, font);
            var metrics = font.Metrics;
            var naturalHeight = metrics.Descent - metrics.Ascent;
            if (naturalWidth <= 0 || naturalHeight <= 0) return;

            var innerRect = bounds.ToElementLayout(image.Padding).InnerRect;
            var destinationBounds = innerRect.ToSkiaRect(ctx.Scale);
            var dest = PlaceNatural(destinationBounds, naturalWidth, naturalHeight, image.Aspect);

            using var paint = new SKPaint
            {
                Color = source.Color.ToSkiaColor(),
                IsAntialias = true
            };

            var scaleX = dest.Width / naturalWidth;
            var scaleY = dest.Height / naturalHeight;
            ctx.Canvas.Save();
            try
            {
                ctx.Canvas.ClipRect(destinationBounds);
                ctx.Canvas.Translate(dest.Left, dest.Top - metrics.Ascent * scaleY);
                ctx.Canvas.Scale(scaleX, scaleY);
                ctx.Canvas.DrawText(source.Glyph, 0, 0, SKTextAlign.Left, font, paint);
            }
            finally
            {
                ctx.Canvas.Restore();
            }
        }
        catch (Exception)
        {
            // Same as PrefetchAsync: one bad glyph must not abort the page.
        }
    }

    private static SKRect PlaceNatural(SKRect destination, float sourceWidth, float sourceHeight, Aspect aspect)
    {
        if (aspect == Aspect.Fill || sourceWidth <= 0 || sourceHeight <= 0)
            return destination;

        if (aspect == Aspect.AspectFit)
        {
            if (sourceWidth <= destination.Width && sourceHeight <= destination.Height)
            {
                var left = destination.Left + (destination.Width - sourceWidth) / 2f;
                var top = destination.Top + (destination.Height - sourceHeight) / 2f;
                return new SKRect(left, top, left + sourceWidth, top + sourceHeight);
            }

            var sourceAspect = sourceWidth / sourceHeight;
            var destinationAspect = destination.Width / destination.Height;
            return AspectFit(destination, sourceAspect, destinationAspect);
        }

        return destination;
    }

    private static (SKRect source, SKRect destination) CalculatePlacement(SKBitmap bitmap, SKRect destination, Aspect aspect)
    {
        var fullSource = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        if (aspect == Aspect.Fill)
            return (fullSource, destination);

        var sourceAspect = (float)bitmap.Width / bitmap.Height;
        var destinationAspect = destination.Width / destination.Height;

        return aspect == Aspect.AspectFit
            ? (fullSource, AspectFit(destination, sourceAspect, destinationAspect))
            : (AspectFillCrop(bitmap, sourceAspect, destinationAspect), destination);
    }

    private static SKRect AspectFit(SKRect destination, float sourceAspect, float destinationAspect)
    {
        if (sourceAspect > destinationAspect)
        {
            var fittedHeight = destination.Width / sourceAspect;
            var offsetY = (destination.Height - fittedHeight) / 2f;
            return new SKRect(destination.Left, destination.Top + offsetY, destination.Right, destination.Top + offsetY + fittedHeight);
        }

        var fittedWidth = destination.Height * sourceAspect;
        var offsetX = (destination.Width - fittedWidth) / 2f;
        return new SKRect(destination.Left + offsetX, destination.Top, destination.Left + offsetX + fittedWidth, destination.Bottom);
    }

    private static SKRect AspectFillCrop(SKBitmap bitmap, float sourceAspect, float destinationAspect)
    {
        if (sourceAspect > destinationAspect)
        {
            var cropWidth = bitmap.Height * destinationAspect;
            var offsetX = (bitmap.Width - cropWidth) / 2f;
            return new SKRect(offsetX, 0, offsetX + cropWidth, bitmap.Height);
        }

        var cropHeight = bitmap.Width / destinationAspect;
        var offsetY = (bitmap.Height - cropHeight) / 2f;
        return new SKRect(0, offsetY, bitmap.Width, offsetY + cropHeight);
    }

    private static async Task<byte[]?> LoadBytesAsync(ImageSource source, CancellationToken ct)
    {
        var raw = await source.LoadAsync(ct).ConfigureAwait(false);
        if (raw is null) return null;
        return IsSvg(source) ? RasterizeSvgToPng(raw) : raw;
    }

    private static SKBitmap? LoadBitmap(ImageSource source)
    {
        var cacheKey = CreateCacheKey(source);

        lock (BitmapGate)
        {
            if (Bitmaps.TryGetValue(cacheKey, out var existing))
                return existing;
        }

        var cached = Cache.Get(cacheKey);
        byte[]? bytes = cached;
        if (bytes is null)
        {
            bytes = LoadBytes(source);
            if (bytes is null)
                return null;
            Cache.Set(cacheKey, bytes);
        }

        var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
            return null;

        lock (BitmapGate)
        {
            if (Bitmaps.TryGetValue(cacheKey, out var raced))
            {
                bitmap.Dispose();
                return raced;
            }

            DecodeCount++;
            Bitmaps[cacheKey] = bitmap;
            return bitmap;
        }
    }

    private static byte[]? LoadBytes(ImageSource source)
    {
        if (source is not FileImageSource file
            || ReportPlatformHandler.FileSystem is not ISyncFileSystem sync)
            return null;

        var bytes = sync.ReadFile(file.FilePath);
        if (bytes is null)
            return null;

        if (IsSvg(source))
            return RasterizeSvgToPng(bytes);

        return bytes;
    }

    private static byte[]? RasterizeSvgToPng(byte[] svgBytes)
    {
        if (svgBytes is null || svgBytes.Length == 0) return null;

        using var stream = new MemoryStream(svgBytes);
        var svg = new SKSvg();
        svg.Load(stream);
        if (svg.Picture is null) return null;

        var bounds = svg.Picture.CullRect;
        var info = new SKImageInfo((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(svg.Picture);
            canvas.Flush();
        }

        using var skImage = SKImage.FromBitmap(bitmap);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static string CreateCacheKey(ImageSource source) => source switch
    {
        FileImageSource file => $"file:{file.FilePath}",
        UriImageSource uri => $"uri:{uri.Uri}",
        ResourceImageSource res => $"resource:{res.ResourceName}",
        StreamImageSource => $"stream:{RuntimeHelpers.GetHashCode(source)}",
        _ => $"other:{source.GetType().Name}:{source.GetHashCode()}"
    };

    private static bool IsSvg(ImageSource source) => source switch
    {
        UriImageSource uri => uri.Uri?.OriginalString.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ?? false,
        FileImageSource file => file.FilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
