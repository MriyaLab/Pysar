using Pysar.Skia.Rendering;
using Pysar.Viewer.Geometry;
using SkiaSharp;

namespace Pysar.Viewer.Tiles;

/// <summary>
///     Keeps the page images a <see cref="ReportView"/> draws: a whole-page base layer per page,
///     rendered once, and a grid of sharp cells for what is on screen.
/// </summary>
/// <remarks>
///     Cutting a page into cells is what keeps the zoom honest. A single image of the visible area
///     eventually exceeds the largest texture a GPU accepts, and the only way to stay under that
///     ceiling with one image is to render below the display's resolution - which is blur by
///     construction, and grows with every zoom step. Cells of a fixed pixel size never reach the
///     ceiling, so each is drawn at exactly the resolution the screen shows.
/// </remarks>
public sealed class ReportViewTiles(
    ReportRenderSession session,
    IRenderScheduler scheduler,
    Func<SKBitmap, byte[]>? package = null,
    int maxDegreeOfParallelism = 0) : IDisposable
{
    /// <summary>
    ///     How a drawn bitmap becomes bytes. A delegate rather than an interface: there is one
    ///     method and no state. The default keeps every existing host on PNG, which is what an
    ///     image source on those platforms wants.
    /// </summary>
    private readonly Func<SKBitmap, byte[]> _package = package ?? EncodePng;

    private readonly int _maxDegree = maxDegreeOfParallelism > 0
        ? maxDegreeOfParallelism
        : Math.Clamp(Environment.ProcessorCount, 1, 4);

    /// <summary>
    ///     The scale the always-present base layer is drawn at. One device pixel per point keeps a
    ///     four-page A4 report around 32 MB while staying legible under any sharp cell.
    /// </summary>
    private const float BaseScale = 1f;

    private readonly Dictionary<int, byte[]> _baseLayers = [];
    private readonly Dictionary<TileKey, Tile> _tiles = [];
    private readonly HashSet<TileKey> _inFlightKeys = [];
    private readonly object _gate = new();

    private readonly CancellationTokenSource _lifetime = new();

    private IReadOnlyList<TileRequest> _wanted = [];
    private bool _pumping;

    public int PageCount => session.PageCount;

    public (float Width, float Height) PageSizePt => session.PageSizePt;

    /// <summary>Raised when new pixels have arrived and the view should redraw.</summary>
    public event Action? Invalidated;

    /// <summary>Raised when a render failed, once per failure.</summary>
    public event Action<Exception>? Failed;

    /// <summary>The whole-page image for a page, or <c>null</c> until it has been drawn.</summary>
    public byte[]? BaseLayer(int pageIndex)
        => _baseLayers.TryGetValue(pageIndex, out var encoded) ? encoded : null;

    /// <summary>
    ///     How far two scales may differ and still be the same scale: enough to absorb the rounding of
    ///     the one expression that produces them - see <see cref="PageViewport.RenderScale"/> - and
    ///     nothing more.
    /// </summary>
    /// <remarks>
    ///     Relative, not absolute. A hundredth of a unit was absolute, and at a density of 1 that is
    ///     more than a whole zoom step below 75% - so two zooms a step apart counted as one scale, the
    ///     page was drawn at both at once, and every cell of the zoom left behind stayed on screen and
    ///     in memory until the next request dropped it.
    /// </remarks>
    private const float ScaleEpsilon = 1e-4f;

    /// <summary>Every drawn cell of a page at <paramref name="scale"/>, in no particular order.</summary>
    public IEnumerable<Tile> TilesFor(int pageIndex, float scale)
    {
        var tolerance = Math.Abs(scale) * ScaleEpsilon;

        List<Tile> snapshot;
        lock (_gate)
        {
            snapshot = [];
            foreach (var tile in _tiles.Values)
                if (tile.Key.PageIndex == pageIndex && Math.Abs(tile.Key.Scale - scale) <= tolerance)
                    snapshot.Add(tile);
        }

        return snapshot;
    }

    /// <summary>
    ///     Drawn cells of a page at any scale other than <paramref name="scale"/> - kept after a zoom
    ///     so the host can stretch them under the new layout until the new scale arrives, instead of
    ///     falling back to the soft base layer (worse than the pinch preview).
    /// </summary>
    public IEnumerable<Tile> BridgeTilesFor(int pageIndex, float scale)
    {
        var tolerance = Math.Abs(scale) * ScaleEpsilon;

        List<Tile> snapshot;
        lock (_gate)
        {
            snapshot = [];
            foreach (var tile in _tiles.Values)
                if (tile.Key.PageIndex == pageIndex && Math.Abs(tile.Key.Scale - scale) > tolerance)
                    snapshot.Add(tile);
        }

        return snapshot;
    }

    /// <summary>Draws every page whole, at the base scale, in the background.</summary>
    public async Task LoadBaseLayersAsync(CancellationToken ct = default)
    {
        var (width, height) = session.PageSizePt;

        for (var index = 0; index < session.PageCount; index++)
        {
            if (ct.IsCancellationRequested)
                return;

            try
            {
                var pageIndex = index;

                // Base layers always PNG: page Image hosts decode PNG, not raw tile payloads.
                var encoded = await scheduler.RunAsync(
                    async token =>
                    {
                        using var bitmap = await session.RenderRegionAsync(
                            pageIndex, new SKRect(0, 0, width, height), BaseScale, token);
                        return EncodePng(bitmap);
                    },
                    ct);

                _baseLayers[index] = encoded;
                Invalidated?.Invoke();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Failed?.Invoke(exception);
                return;
            }
        }
    }

    /// <summary>
    ///     Sets the cells the view wants and makes sure something is drawing them.
    /// </summary>
    /// <remarks>
    ///     The wanted set is replaced, never cancelled mid-flight. An earlier design cancelled the
    ///     running render on every new request, which during a scroll meant a render rarely survived
    ///     to the end - and the cells it had not reached yet were never asked for again, so a page
    ///     kept a sharp band down one side and the blurred base layer everywhere else.
    /// </remarks>
    public void RequestTiles(IReadOnlyList<TileRequest> requests)
    {
        lock (_gate)
        {
            // Wanted first so DropBridgeIfCurrentComplete_NoLock sees the new set.
            _wanted = requests;
            DropUnwanted(requests);

            if (_pumping)
                return;

            _pumping = true;
        }

        _ = PumpAsync();
    }

    /// <summary>
    ///     Draws missing cells with a small worker pool, always taking the next one the view still
    ///     wants, so a change of mind mid-run keeps what is already drawn and skips the rest.
    /// </summary>
    private async Task PumpAsync()
    {
        var workers = new List<Task>(_maxDegree);
        var faulted = false;

        try
        {
            while (true)
            {
                while (workers.Count < _maxDegree && TryTakeNext(out var request))
                    workers.Add(DrawOneAsync(request));

                if (workers.Count > 0)
                {
                    var finished = await Task.WhenAny(workers);
                    workers.Remove(finished);
                    await finished;
                    continue;
                }

                // No workers: either done, or RequestTiles replaced the wanted set after
                // TryTakeNext saw nothing - re-check under the gate before leaving.
                lock (_gate)
                {
                    if (!HasMissingWork_NoLock())
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The session went away; nothing here outlives it.
        }
        catch (Exception exception)
        {
            faulted = true;
            Failed?.Invoke(exception);
        }
        finally
        {
            if (workers.Count > 0)
            {
                try
                {
                    await Task.WhenAll(workers);
                }
                catch
                {
                    // Already reported, or cancelled with the session.
                }
            }

            var restart = false;

            lock (_gate)
            {
                // Atomically drop the pumping flag or keep it and run again so a RequestTiles
                // that landed between the idle check and here is never stranded.
                if (!faulted && HasMissingWork_NoLock())
                    restart = true;
                else
                    _pumping = false;
            }

            if (restart)
                _ = PumpAsync();
        }
    }

    private async Task DrawOneAsync(TileRequest request)
    {
        try
        {
            var tile = await scheduler.RunAsync(
                token => DrawAsync(request, token), _lifetime.Token);

            var stored = false;

            lock (_gate)
            {
                _inFlightKeys.Remove(request.Key);

                foreach (var wanted in _wanted)
                {
                    if (wanted.Key != request.Key)
                        continue;

                    _tiles[request.Key] = tile;
                    stored = true;
                    break;
                }
            }

            if (stored)
            {
                lock (_gate)
                    DropBridgeIfCurrentComplete_NoLock();

                Invalidated?.Invoke();
            }
        }
        catch
        {
            lock (_gate)
                _inFlightKeys.Remove(request.Key);

            throw;
        }
    }

    /// <summary>Caller holds <see cref="_gate"/>.</summary>
    private bool HasMissingWork_NoLock()
    {
        foreach (var candidate in _wanted)
        {
            if (_tiles.ContainsKey(candidate.Key))
                continue;

            if (_inFlightKeys.Contains(candidate.Key))
                continue;

            if (candidate.RegionPt.Width <= 0 || candidate.RegionPt.Height <= 0)
                continue;

            return true;
        }

        return false;
    }

    private bool TryTakeNext(out TileRequest request)
    {
        lock (_gate)
        {
            foreach (var candidate in _wanted)
            {
                if (_tiles.ContainsKey(candidate.Key))
                    continue;

                if (_inFlightKeys.Contains(candidate.Key))
                    continue;

                if (candidate.RegionPt.Width <= 0 || candidate.RegionPt.Height <= 0)
                    continue;

                _inFlightKeys.Add(candidate.Key);
                request = candidate;
                return true;
            }

            request = default;
            return false;
        }
    }

    /// <summary>
    ///     Forgets cells of active scales the view no longer asks for. Cells of other scales stay as a
    ///     bridge until every wanted cell is present - dropping them on the first RequestTiles after a
    ///     zoom left only the base layer on screen, which is blurrier than the pinch preview that had
    ///     been stretching the previous sharp tiles. Active scales may be more than one (coverage +
    ///     full-resolution).
    ///     Caller holds <see cref="_gate"/>.
    /// </summary>
    private void DropUnwanted(IReadOnlyList<TileRequest> requests)
    {
        if (requests.Count == 0)
        {
            _tiles.Clear();
            return;
        }

        var wanted = requests.Select(request => request.Key).ToHashSet();
        var activeScales = requests.Select(request => request.Key.Scale).ToList();

        foreach (var key in _tiles.Keys.ToList())
        {
            if (!wanted.Contains(key) && IsActiveScale(key.Scale, activeScales))
                _tiles.Remove(key);
        }

        DropBridgeIfCurrentComplete_NoLock();
    }

    private static bool IsActiveScale(float scale, List<float> activeScales)
    {
        foreach (var active in activeScales)
        {
            var tolerance = Math.Abs(active) * ScaleEpsilon;
            if (Math.Abs(scale - active) <= tolerance)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Drops every non-active-scale cell once the full wanted set is in the cache. Caller holds
    ///     <see cref="_gate"/>.
    /// </summary>
    private void DropBridgeIfCurrentComplete_NoLock()
    {
        if (_wanted.Count == 0)
            return;

        foreach (var request in _wanted)
            if (!_tiles.ContainsKey(request.Key))
                return;

        var activeScales = _wanted.Select(request => request.Key.Scale).ToList();

        foreach (var key in _tiles.Keys.ToList())
            if (!IsActiveScale(key.Scale, activeScales))
                _tiles.Remove(key);
    }

    /// <summary>
    ///     Packages the pixels and lets the bitmap go. This runs on the thread that drew them, so
    ///     the cost never lands on the gesture.
    /// </summary>
    private byte[] Package(SKBitmap bitmap)
    {
        using (bitmap)
        {
            return _package(bitmap);
        }
    }

    /// <summary>Draws one cell and packages it, keeping the pixel size the renderer settled on.</summary>
    private async Task<Tile> DrawAsync(TileRequest request, CancellationToken ct)
    {
        var bitmap = await session.RenderRegionAsync(
            request.Key.PageIndex, ToSkia(request.RegionPt), request.Key.Scale, ct);

        // Read before packaging: Package disposes the bitmap.
        var (width, height) = (bitmap.Width, bitmap.Height);

        return new Tile(request.Key, request.RegionPt, Package(bitmap), width, height);
    }

    /// <summary>
    ///     The one place page-point geometry crosses into the rendering backend. Keeping the
    ///     conversion here is what lets <see cref="Tile"/> and <see cref="TileRequest"/> - and so
    ///     <see cref="IReportViewHost"/> - stay free of backend types.
    /// </summary>
    private static SKRect ToSkia(RectPt region) => new(
        (float)region.Left, (float)region.Top, (float)region.Right, (float)region.Bottom);

    /// <summary>The default packaging, and what every host had before the choice existed.</summary>
    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();

        lock (_gate)
        {
            _wanted = [];
            _inFlightKeys.Clear();
            _baseLayers.Clear();
            _tiles.Clear();
        }
    }
}
