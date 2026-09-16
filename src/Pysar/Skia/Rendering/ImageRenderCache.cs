using SkiaSharp;
using Svg.Skia;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Bytes and decoded bitmaps for one render or viewer session. Dispose with the session so
///     images do not live until process exit.
/// </summary>
internal sealed class ImageRenderCache : IDisposable
{
    private readonly Dictionary<string, byte[]> _bytes = new();
    private readonly Dictionary<string, SKBitmap> _bitmaps = new();
    private readonly Dictionary<string, SKSvg> _svgs = new();
    private readonly object _gate = new();
    private readonly List<ImageLoadFailure> _failures = [];
    private bool _disposed;

    internal int DecodeCount { get; private set; }

    internal IReadOnlyList<ImageLoadFailure> Failures
    {
        get
        {
            lock (_gate)
                return [.. _failures];
        }
    }

    internal byte[]? GetBytes(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
            return _bytes.TryGetValue(key, out var data) ? data : null;
    }

    internal void SetBytes(string key, byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
            _bytes[key] = data;
    }

    internal SKBitmap? GetBitmap(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
            return _bitmaps.TryGetValue(key, out var bitmap) ? bitmap : null;
    }

    internal SKPicture? GetPicture(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
            return _svgs.TryGetValue(key, out var svg) ? svg.Picture : null;
    }

    internal SKPicture? AddSvg(string key, SKSvg svg)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_svgs.TryGetValue(key, out var existing))
            {
                svg.Dispose();
                return existing.Picture;
            }

            _svgs[key] = svg;
            return svg.Picture;
        }
    }

    internal SKBitmap AddBitmap(string key, SKBitmap bitmap)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_bitmaps.TryGetValue(key, out var existing))
            {
                bitmap.Dispose();
                return existing;
            }

            DecodeCount++;
            _bitmaps[key] = bitmap;
            return bitmap;
        }
    }

    internal void RecordFailure(string cacheKey, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (_gate)
            _failures.Add(new ImageLoadFailure(cacheKey, exception));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var bitmap in _bitmaps.Values)
                bitmap.Dispose();
            _bitmaps.Clear();
            foreach (var svg in _svgs.Values)
                svg.Dispose();
            _svgs.Clear();
            _bytes.Clear();
            DecodeCount = 0;
        }
    }
}

internal readonly record struct ImageLoadFailure(string CacheKey, Exception Exception);
