using Pysar.Viewer.Geometry;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer;
using Pysar.Viewer.Tiles;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ReportViewSessionTests
{
    /// <summary>
    ///     A single renderer per test: <see cref="SkiaReportRenderer"/> carries no per-report state, so
    ///     there is nothing shared between tests by reusing one, and it saves each test having to know
    ///     how the hosts wrap it (each keeps its own <c>Renderer</c>).
    /// </summary>
    private static readonly SkiaReportRenderer Renderer = new();

    private static (FakeHost Host, ReportViewPresenter Presenter, ReportViewSession Session) Subject()
    {
        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);
        var session = new ReportViewSession(presenter, host, new TaskRunScheduler());

        return (host, presenter, session);
    }

    /// <summary>A one-page report with something on it, cheap enough to measure in every test.</summary>
    private static Report AReport()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        return design;
    }

    /// <summary>
    ///     A report that fails to measure: a page header fixed taller than the page itself leaves no
    ///     room in the content zone, which <see cref="Pysar.Skia.Layout.ReportLayoutEngine"/>
    ///     rejects with <see cref="InvalidOperationException"/>. Used to drive a real failure through
    ///     <see cref="SkiaReportRenderer.CreateSessionAsync"/> rather than faking one.
    /// </summary>
    private static Report AReportThatFailsToMeasure()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 }
        };

        design.Bands.Add(new PageHeaderBand());
        // A4 portrait is 842pt tall; a fixed 2000pt header leaves the content window negative.
        design.PageHeader!.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2000)) });
        design.Build();

        return design;
    }

    [Fact]
    public async Task LoadingNull_ClearsAndStops()
    {
        var (host, presenter, session) = Subject();
        var cleared = 0;
        var loaded = 0;

        session.Cleared += () => cleared++;
        session.Loaded += () => loaded++;

        await session.LoadAsync(null, Renderer);

        Assert.Equal(1, cleared);
        Assert.Equal(0, loaded);
        Assert.Null(session.Tiles);
        Assert.Equal(0, presenter.PageCount);
    }

    [Fact]
    public async Task ASuccessfulLoad_RaisesClearedBeforeLoaded_AndPopulatesThePresenter()
    {
        var (host, presenter, session) = Subject();
        var order = new List<string>();

        session.Cleared += () => order.Add("Cleared");
        session.Loaded += () => order.Add("Loaded");

        await session.LoadAsync(AReport(), Renderer);
        host.RunPosted();

        Assert.Equal("Cleared", order[0]);
        Assert.Contains("Loaded", order);
        Assert.True(order.IndexOf("Cleared") < order.IndexOf("Loaded"));

        Assert.NotNull(session.Tiles);
        Assert.Equal(1, presenter.PageCount);
        Assert.Equal(session.Tiles!.PageCount, presenter.PageCount);
    }

    /// <summary>
    ///     Pins the contract every host relies on: the first layout can finish before the view has a
    ///     real viewport, so <c>Loaded</c> fires once inline and once again through a post, after this
    ///     turn of the host's own loop. Deferring posts proves the second raise really goes through
    ///     <see cref="IReportViewHost.Post"/> rather than the handler simply being invoked twice inline.
    /// </summary>
    [Fact]
    public async Task Loaded_IsRaisedOnceInlineAndOnceThroughPost()
    {
        var (host, _, session) = Subject();
        var loaded = 0;
        session.Loaded += () => loaded++;

        host.DeferPosts = true;
        await session.LoadAsync(AReport(), Renderer);

        Assert.Equal(1, loaded);

        host.RunPosted();

        Assert.Equal(2, loaded);
    }

    /// <summary>
    ///     A second load supersedes the first: the previous cache is disposed, and <c>Cleared</c> fires
    ///     again before anything belonging to the new report is built. Disposal is only observable
    ///     indirectly here - <see cref="ReportViewTiles.Dispose"/> clears its own tile set, so a cell
    ///     drawn before the second load is gone by the time the second <c>Cleared</c> fires.
    /// </summary>
    [Fact]
    public async Task ASecondLoad_DisposesTheFirstCache_BeforeBuildingTheNext()
    {
        var (host, _, session) = Subject();

        await session.LoadAsync(AReport(), Renderer);
        host.RunPosted();

        var firstTiles = session.Tiles;
        Assert.NotNull(firstTiles);

        var firstTile = await DrawOneAsync(firstTiles!);
        Assert.Single(firstTiles!.TilesFor(firstTile.Key.PageIndex, firstTile.Key.Scale));

        var clearedCount = 0;
        var disposedByClear = false;

        // Subscribed only now: the Cleared this counts is the one the second LoadAsync raises, before
        // it builds anything belonging to the new report.
        session.Cleared += () =>
        {
            clearedCount++;
            disposedByClear = !firstTiles.TilesFor(firstTile.Key.PageIndex, firstTile.Key.Scale).Any();
        };

        await session.LoadAsync(AReport(), Renderer);
        host.RunPosted();

        Assert.Equal(1, clearedCount);
        Assert.True(disposedByClear, "the first cache should already be disposed when the second Cleared fires");
        Assert.NotSame(firstTiles, session.Tiles);
    }

    /// <summary>
    ///     A report the renderer cannot measure must route to <c>Failed</c>, and <see cref="ReportViewSession.LoadAsync"/>
    ///     must complete rather than let the exception propagate out of the fire-and-forget call sites
    ///     every host uses (<c>_ = view.StartSessionAsync(...)</c>).
    /// </summary>
    [Fact]
    public async Task AFailureToMeasure_RoutesToFailed_AndDoesNotThrow()
    {
        var (host, _, session) = Subject();
        Exception? failure = null;
        session.Failed += exception => failure = exception;

        var exception = await Record.ExceptionAsync(
            () => session.LoadAsync(AReportThatFailsToMeasure(), Renderer));

        Assert.Null(exception);
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Null(session.Tiles);
    }

    /// <summary>Asks for one cell and waits for it, as <see cref="ReportViewTilesTests"/> does.</summary>
    private static async Task<Tile> DrawOneAsync(ReportViewTiles tiles)
    {
        var arrived = new TaskCompletionSource<Tile>();
        var request = new TileRequest(
            new TileKey(PageIndex: 0, Column: 0, Row: 0, Scale: 1f),
            new RectPt(0, 0, 100, 50));

        tiles.Invalidated += () =>
        {
            foreach (var tile in tiles.TilesFor(request.Key.PageIndex, request.Key.Scale))
                arrived.TrySetResult(tile);
        };

        tiles.RequestTiles([request]);

        return await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
