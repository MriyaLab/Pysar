using Pysar.Viewer.Geometry;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using Pysar.Viewer.Tiles;
using SkiaSharp;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ReportViewTilesTests
{
    /// <summary>
    ///     A cell belongs to the scale it was drawn at, and to no neighbouring one. The tolerance that
    ///     decides this is only there to absorb the rounding of one expression, so it has to be far
    ///     below the smallest step of zoom that reaches the cache. An absolute hundredth of a unit was
    ///     not: at a density of 1 it is wider than a whole zoom step anywhere below 75%, so two zooms a
    ///     step apart counted as one scale and the page was drawn at both at once.
    /// </summary>
    [Fact]
    public async Task ACellDrawnAtOneScale_IsNotOfferedForANeighbouringOne()
    {
        var session = await SessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler());

        const float scale = 0.8f;

        await DrawOneAsync(tiles, new TileRequest(
            new TileKey(PageIndex: 0, Column: 0, Row: 0, scale), new RectPt(0, 0, 200, 200)));

        Assert.Single(tiles.TilesFor(pageIndex: 0, scale));

        // Half of what the old tolerance allowed, and still a whole zoom step at this scale.
        Assert.Empty(tiles.TilesFor(pageIndex: 0, scale + 0.005f));
        Assert.Empty(tiles.TilesFor(pageIndex: 0, scale - 0.005f));
    }

    /// <summary>
    ///     A cell answers for the page it was drawn for and no other. The cache indexes cells by page
    ///     rather than walking them all, because the view asks per page on every pass, and an index is
    ///     a second place for a page to be recorded - so this is what stops the two drifting apart.
    /// </summary>
    [Fact]
    public async Task ACellDrawnForOnePage_IsNotOfferedForAnother()
    {
        var session = await TallSessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler());

        Assert.True(session.PageCount >= 2, $"the report should span several pages, not {session.PageCount}");

        const float scale = 1f;

        var first = new TileRequest(new TileKey(PageIndex: 0, Column: 0, Row: 0, scale), new RectPt(0, 0, 200, 200));
        var second = new TileRequest(new TileKey(PageIndex: 1, Column: 0, Row: 0, scale), new RectPt(0, 0, 200, 200));

        tiles.RequestTiles([first, second]);

        await WaitUntilAsync(tiles,
            () => tiles.TilesFor(0, scale).Any() && tiles.TilesFor(1, scale).Any());

        Assert.Equal(first.Key, tiles.TilesFor(0, scale).Single().Key);
        Assert.Equal(second.Key, tiles.TilesFor(1, scale).Single().Key);
        Assert.Empty(tiles.TilesFor(2, scale));

        // And the other scales of a page are its own too, not every page's.
        Assert.Empty(tiles.BridgeTilesFor(0, scale));
    }

    [Fact]
    public async Task TaskRunScheduler_RunsTheWorkOffTheCallingThread()
    {
        var scheduler = new TaskRunScheduler();
        var calling = Environment.CurrentManagedThreadId;

        var ran = await scheduler.RunAsync(
            _ => Task.FromResult(Environment.CurrentManagedThreadId), CancellationToken.None);

        Assert.NotEqual(calling, ran);
    }

    /// <summary>
    ///     A one-page report with something on it. Deliberately trivial: these tests are about how a
    ///     drawn cell is packaged, not about what it contains.
    /// </summary>
    private static async Task<ReportRenderSession> SessionAsync()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        return await ReportRenderSession.CreateAsync(design);
    }

    /// <summary>The same report, on a band tall enough that the renderer splits it over several pages.</summary>
    private static async Task<ReportRenderSession> TallSessionAsync()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(4000)) });
        design.Build();

        return await ReportRenderSession.CreateAsync(design);
    }

    /// <summary>
    ///     Asks for one cell and waits for it. The cache draws on a background thread and announces
    ///     arrivals through an event, so there is nothing to await directly.
    /// </summary>
    private static async Task<Tile> DrawOneAsync(ReportViewTiles tiles, TileRequest request)
    {
        var arrived = new TaskCompletionSource<Tile>();

        tiles.Invalidated += () =>
        {
            foreach (var tile in tiles.TilesFor(request.Key.PageIndex, request.Key.Scale))
                arrived.TrySetResult(tile);
        };

        tiles.RequestTiles([request]);

        // A cell that never arrives must fail the test rather than hang the run.
        return await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static TileRequest ARequest()
        => new(new TileKey(PageIndex: 0, Column: 0, Row: 0, Scale: 2f), new RectPt(0, 0, 100, 50));

    [Fact]
    public async Task ByDefault_ACellIsAPng()
    {
        using var tiles = new ReportViewTiles(await SessionAsync(), new TaskRunScheduler());

        var tile = await DrawOneAsync(tiles, ARequest());

        // The PNG signature. Asserting on the bytes rather than on the length, because a change of
        // format that still produced plausible bytes is exactly the regression worth catching.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, tile.Bytes[..4]);
    }

    [Fact]
    public async Task ASuppliedPackager_ReplacesTheDefault()
    {
        var sentinel = new byte[] { 1, 2, 3 };

        using var tiles = new ReportViewTiles(
            await SessionAsync(), new TaskRunScheduler(), _ => sentinel);

        var tile = await DrawOneAsync(tiles, ARequest());

        Assert.Equal(sentinel, tile.Bytes);
    }

    [Fact]
    public async Task APackager_SeesTheBitmapBeforeItIsDisposed()
    {
        var seen = (Width: 0, Height: 0);

        using var tiles = new ReportViewTiles(
            await SessionAsync(), new TaskRunScheduler(),
            bitmap =>
            {
                seen = (bitmap.Width, bitmap.Height);
                return [];
            });

        await DrawOneAsync(tiles, ARequest());

        // 100 x 50 points at scale 2.
        Assert.Equal((200, 100), seen);
    }

    [Fact]
    public async Task ACell_KnowsItsPixelSize()
    {
        using var tiles = new ReportViewTiles(await SessionAsync(), new TaskRunScheduler());

        var tile = await DrawOneAsync(tiles, ARequest());

        // The host needs these to hand the pixels to a canvas, and must not recompute them: the
        // rounding lives in RenderRegionAsync and two copies of it would drift.
        Assert.Equal(200, tile.PixelWidth);
        Assert.Equal(100, tile.PixelHeight);
    }

    [Fact]
    public async Task PackagingAsBgra_YieldsPixelBytesNotPngSignature()
    {
        var session = await SessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler(), TilePixels.Bgra);

        var tile = await DrawOneAsync(tiles, new TileRequest(
            new TileKey(0, 0, 0, 1f), new RectPt(0, 0, 50, 50)));

        Assert.True(tile.Bytes.Length >= tile.PixelWidth * tile.PixelHeight * 4);
        Assert.False(tile.Bytes is [0x89, 0x50, 0x4E, 0x47, ..]);
    }

    [Fact]
    public async Task Pump_RunsMultipleCellsConcurrently()
    {
        var scheduler = new CountingScheduler();
        using var tiles = new ReportViewTiles(
            await SessionAsync(), scheduler, maxDegreeOfParallelism: 4);

        var done = new TaskCompletionSource();
        var arrived = 0;

        tiles.Invalidated += () =>
        {
            if (Interlocked.Increment(ref arrived) >= 4)
                done.TrySetResult();
        };

        tiles.RequestTiles(
        [
            new(new TileKey(0, 0, 0, 1f), new RectPt(0, 0, 40, 40)),
            new(new TileKey(0, 1, 0, 1f), new RectPt(40, 0, 80, 40)),
            new(new TileKey(0, 0, 1, 1f), new RectPt(0, 40, 40, 80)),
            new(new TileKey(0, 1, 1, 1f), new RectPt(40, 40, 80, 80)),
        ]);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(scheduler.MaxInFlight >= 2, $"MaxInFlight was {scheduler.MaxInFlight}");
    }

    /// <summary>
    ///     After a zoom, cells of the previous scale stay until the new scale is fully present, so the
    ///     host can stretch them instead of falling back to the soft base layer.
    /// </summary>
    [Fact]
    public async Task AfterAZoom_PreviousScaleCellsRemainUntilTheNewScaleIsComplete()
    {
        using var tiles = new ReportViewTiles(await SessionAsync(), new TaskRunScheduler());

        const float oldScale = 0.8f;
        const float newScale = 1.6f;

        await DrawOneAsync(tiles, new TileRequest(
            new TileKey(0, 0, 0, oldScale), new RectPt(0, 0, 200, 200)));

        Assert.Single(tiles.TilesFor(0, oldScale));

        // New scale requested but not yet drawn — old cell must remain as a bridge.
        tiles.RequestTiles([
            new TileRequest(new TileKey(0, 0, 0, newScale), new RectPt(0, 0, 200, 200))
        ]);

        Assert.Single(tiles.BridgeTilesFor(0, newScale));
        Assert.Single(tiles.TilesFor(0, oldScale));

        var arrived = new TaskCompletionSource();
        tiles.Invalidated += () =>
        {
            if (tiles.TilesFor(0, newScale).Any())
                arrived.TrySetResult();
        };

        // Pump is already running from RequestTiles; wait for the new cell.
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Single(tiles.TilesFor(0, newScale));
        Assert.Empty(tiles.BridgeTilesFor(0, newScale));
        Assert.Empty(tiles.TilesFor(0, oldScale));
    }

    [Fact]
    public async Task WantedOrder_IsDrawOrder_FirstListedKeyCompletesFirst()
    {
        var session = await SessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler(), maxDegreeOfParallelism: 1);

        var coverage = new TileRequest(new TileKey(0, 0, 0, 1f), new RectPt(0, 0, 200, 200));
        var full = new TileRequest(new TileKey(0, 0, 0, 2f), new RectPt(0, 0, 100, 100));

        var order = new List<float>();
        var done = new TaskCompletionSource();
        tiles.Invalidated += () =>
        {
            foreach (var scale in new[] { 1f, 2f })
            foreach (var tile in tiles.TilesFor(0, scale))
                if (!order.Contains(tile.Key.Scale))
                    order.Add(tile.Key.Scale);

            if (order.Count >= 2)
                done.TrySetResult();
        };

        tiles.RequestTiles([coverage, full]);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1f, order[0]);
        Assert.Equal(2f, order[1]);
    }

    /// <summary>
    ///     Coverage + full-resolution can be wanted together. Stale cells of those scales drop;
    ///     only the wanted keys remain at each scale once the set is complete.
    /// </summary>
    [Fact]
    public async Task DualScaleWanted_KeepsBothScales_AndDropsOnlyUnwantedOfThoseScales()
    {
        var session = await SessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler());

        var full = new TileRequest(new TileKey(0, 0, 0, 2f), new RectPt(0, 0, 100, 100));
        var coverage = new TileRequest(new TileKey(0, 0, 0, 1f), new RectPt(0, 0, 200, 200));
        var staleFull = new TileRequest(new TileKey(0, 1, 0, 2f), new RectPt(100, 0, 200, 100));

        // Same-scale seed: multi-scale DrawMany cannot complete until DropBridge is multi-scale-aware.
        await DrawManyAsync(tiles, [full, staleFull]);

        tiles.RequestTiles([coverage, full]);

        Assert.Single(tiles.TilesFor(0, 2f));
        Assert.Equal(full.Key, tiles.TilesFor(0, 2f).Single().Key);

        await WaitUntilAsync(
            tiles,
            () => tiles.TilesFor(0, 1f).Any() && tiles.TilesFor(0, 2f).Any());

        Assert.Single(tiles.TilesFor(0, 2f));
        Assert.Single(tiles.TilesFor(0, 1f));
        Assert.Equal(full.Key, tiles.TilesFor(0, 2f).Single().Key);
    }

    /// <summary>
    ///     A bridge at a third scale stays while any wanted cell is missing, and drops only when
    ///     every wanted cell (possibly at more than one scale) is present.
    /// </summary>
    [Fact]
    public async Task BridgeOfThirdScale_StaysUntilAllWantedPresent_ThenDrops()
    {
        var session = await SessionAsync();
        using var tiles = new ReportViewTiles(session, new TaskRunScheduler());

        var bridge = new TileRequest(new TileKey(0, 0, 0, 3f), new RectPt(0, 0, 80, 80));
        var coverage = new TileRequest(new TileKey(0, 0, 0, 1f), new RectPt(0, 0, 200, 200));
        var full = new TileRequest(new TileKey(0, 0, 0, 2f), new RectPt(0, 0, 100, 100));

        await DrawOneAsync(tiles, bridge);

        tiles.RequestTiles([coverage]);
        Assert.Single(tiles.BridgeTilesFor(0, 1f));

        await WaitUntilAsync(tiles, () => tiles.TilesFor(0, 1f).Any());
        Assert.Empty(tiles.BridgeTilesFor(0, 1f));

        await DrawOneAsync(tiles, bridge);
        tiles.RequestTiles([coverage, full]);
        // Third-scale cell is a bridge until every wanted key (both scales) is present.
        Assert.Single(tiles.TilesFor(0, 3f));

        await WaitUntilAsync(tiles, () => !tiles.TilesFor(0, 3f).Any());

        Assert.Empty(tiles.TilesFor(0, 3f));
        Assert.True(tiles.TilesFor(0, 1f).Any());
        Assert.True(tiles.TilesFor(0, 2f).Any());
    }

    /// <summary>
    ///     Requests several cells in one shot and waits until every key is in the cache.
    ///     <see cref="DrawOneAsync"/> replaces the wanted set each time, so it cannot seed dual-scale
    ///     state on its own.
    /// </summary>
    private static async Task DrawManyAsync(
        ReportViewTiles tiles, IReadOnlyList<TileRequest> requests)
    {
        var gate = new object();
        var remaining = requests.Select(request => request.Key).ToHashSet();
        var done = new TaskCompletionSource();

        void OnInvalidated()
        {
            lock (gate)
            {
                foreach (var request in requests)
                {
                    if (tiles.TilesFor(request.Key.PageIndex, request.Key.Scale)
                        .Any(tile => tile.Key == request.Key))
                        remaining.Remove(request.Key);
                }

                if (remaining.Count == 0)
                    done.TrySetResult();
            }
        }

        tiles.Invalidated += OnInvalidated;
        try
        {
            tiles.RequestTiles(requests);
            OnInvalidated();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            tiles.Invalidated -= OnInvalidated;
        }
    }

    private static async Task WaitUntilAsync(ReportViewTiles tiles, Func<bool> predicate)
    {
        if (predicate())
            return;

        var done = new TaskCompletionSource();

        void OnInvalidated()
        {
            if (predicate())
                done.TrySetResult();
        }

        tiles.Invalidated += OnInvalidated;
        try
        {
            if (predicate())
                return;

            await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            tiles.Invalidated -= OnInvalidated;
        }
    }

    private sealed class CountingScheduler : IRenderScheduler
    {
        private int _inFlight;
        public int MaxInFlight;

        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref _inFlight);
            MaxInFlight = Math.Max(MaxInFlight, n);
            try
            {
                await Task.Delay(50, ct);
                return await work(ct);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }
}
