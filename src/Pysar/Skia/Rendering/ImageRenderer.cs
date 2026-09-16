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
///     cache and places the bitmap or SVG picture per <see cref="Aspect"/>.
/// </summary>
internal static class ImageRenderer
{
    /// <summary>Loads image bytes into the cache. Safe to call from async render entry points.</summary>
    public static async Task PrefetchAsync(
        IEnumerable<ImageSource> sources, ImageRenderCache cache, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(cache);
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            if (source is null) continue;
            if (source is FontImageSource) continue;
            var key = CreateCacheKey(source);
            if (cache.GetBytes(key) is not null) continue;

            try
            {
                var bytes = await LoadBytesAsync(source, ct).ConfigureAwait(false);
                if (bytes is not null)
                    cache.SetBytes(key, bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One bad image must not abort the whole render; the session records it.
                cache.RecordFailure(key, exception);
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

        if (IsSvg(image.Source))
        {
            DrawSvg(image, bounds, ctx);
            return;
        }

        var bitmap = LoadBitmap(image.Source, ctx.Images);
        if (bitmap is null) return;

        var ownsBitmap = ctx.Images is null;
        try
        {
            var innerRect = bounds.ToElementLayout(image.Padding).InnerRect;
            var destinationBounds = innerRect.ToSkiaRect(ctx.Scale);
            var (sourceRect, destinationRect) = CalculatePlacement(bitmap.Width, bitmap.Height, destinationBounds, image.Aspect);

            using var paint = new SKPaint { IsAntialias = true };
            ctx.Canvas.DrawBitmap(bitmap, sourceRect, destinationRect, SKSamplingOptions.Default, paint);
        }
        finally
        {
            if (ownsBitmap)
                bitmap.Dispose();
        }
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
        catch (Exception exception)
        {
            ctx.Images?.RecordFailure($"font:{source.FontFamily}:{source.Glyph}", exception);
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

    private static void DrawSvg(Image image, Rect bounds, RenderContext ctx)
    {
        var source = image.Source!;
        var cache = ctx.Images;
        var cacheKey = CreateCacheKey(source);
        var picture = cache?.GetPicture(cacheKey);
        SKSvg? owned = null;
        if (picture is null)
        {
            var bytes = cache?.GetBytes(cacheKey) ?? LoadBytes(source);
            if (bytes is null || bytes.Length == 0)
                return;

            cache?.SetBytes(cacheKey, bytes);
            owned = ParseSvg(bytes);
            picture = owned.Picture;
            if (picture is not null && cache is not null)
            {
                picture = cache.AddSvg(cacheKey, owned);
                owned = null;
            }
        }

        try
        {
            if (picture is null || picture.CullRect.IsEmpty)
                return;

            var innerRect = bounds.ToElementLayout(image.Padding).InnerRect;
            var destinationBounds = innerRect.ToSkiaRect(ctx.Scale);
            var cull = picture.CullRect;
            var (sourceRect, destinationRect) = CalculatePlacement(
                cull.Width, cull.Height, destinationBounds, image.Aspect);

            ctx.Canvas.Save();
            try
            {
                ctx.Canvas.ClipRect(destinationRect);
                ctx.Canvas.Translate(destinationRect.Left, destinationRect.Top);
                ctx.Canvas.Scale(
                    destinationRect.Width / sourceRect.Width,
                    destinationRect.Height / sourceRect.Height);
                ctx.Canvas.Translate(-sourceRect.Left, -sourceRect.Top);
                ctx.Canvas.DrawPicture(picture);
            }
            finally
            {
                ctx.Canvas.Restore();
            }
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static SKSvg ParseSvg(byte[] bytes)
    {
        var svg = new SKSvg();
        using var stream = new MemoryStream(bytes);
        svg.Load(stream);
        return svg;
    }

    private static (SKRect source, SKRect destination) CalculatePlacement(
        float sourceWidth, float sourceHeight, SKRect destination, Aspect aspect)
    {
        var fullSource = new SKRect(0, 0, sourceWidth, sourceHeight);
        if (aspect == Aspect.Fill)
            return (fullSource, destination);

        var sourceAspect = sourceWidth / sourceHeight;
        var destinationAspect = destination.Width / destination.Height;

        return aspect == Aspect.AspectFit
            ? (fullSource, AspectFit(destination, sourceAspect, destinationAspect))
            : (AspectFillCrop(sourceWidth, sourceHeight, sourceAspect, destinationAspect), destination);
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

    private static SKRect AspectFillCrop(
        float sourceWidth, float sourceHeight, float sourceAspect, float destinationAspect)
    {
        if (sourceAspect > destinationAspect)
        {
            var cropWidth = sourceHeight * destinationAspect;
            var offsetX = (sourceWidth - cropWidth) / 2f;
            return new SKRect(offsetX, 0, offsetX + cropWidth, sourceHeight);
        }

        var cropHeight = sourceWidth / destinationAspect;
        var offsetY = (sourceHeight - cropHeight) / 2f;
        return new SKRect(0, offsetY, sourceWidth, offsetY + cropHeight);
    }

    private static async Task<byte[]?> LoadBytesAsync(ImageSource source, CancellationToken ct)
    {
        var raw = await source.LoadAsync(ct).ConfigureAwait(false);
        return raw;
    }

    private static SKBitmap? LoadBitmap(ImageSource source, ImageRenderCache? cache)
    {
        var cacheKey = CreateCacheKey(source);
        if (cache?.GetBitmap(cacheKey) is { } existing)
            return existing;

        var bytes = cache?.GetBytes(cacheKey) ?? LoadBytes(source);
        if (bytes is null)
            return null;

        cache?.SetBytes(cacheKey, bytes);

        var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
            return null;

        return cache is null ? bitmap : cache.AddBitmap(cacheKey, bitmap);
    }

    private static byte[]? LoadBytes(ImageSource source)
    {
        if (source is not FileImageSource file
            || ReportPlatformHandler.FileSystem is not ISyncFileSystem sync)
            return null;

        var bytes = sync.ReadFile(file.FilePath);
        return bytes;
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
        ResourceImageSource resource => resource.ResourceName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
