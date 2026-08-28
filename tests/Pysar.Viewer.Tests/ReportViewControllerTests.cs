using Pysar.Viewer.Zoom;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer.Tiles;
using Xunit;

namespace Pysar.Viewer.Tests;

/// <summary>
///     The orchestration each host used to write out for itself. None of it was reachable by a test
///     while it lived in four <c>ReportView</c> classes.
/// </summary>
public class ReportViewControllerTests
{
    private static readonly SkiaReportRenderer Renderer = new();

    private sealed class FakeSurface : IReportViewSurface
    {
        public int Refreshes { get; private set; }
        public int Clears { get; private set; }
        public int Invalidations { get; private set; }
        public bool Suppress { get; set; }
        public (double VerticalOverdraw, double RenderBudget) TilePolicy { get; set; } = (1, 200);
        public List<(int Page, double Zoom)> States { get; } = [];

        public void RefreshVisuals() => Refreshes++;
        public void ClearVisuals() => Clears++;
        public void InvalidateSurface() => Invalidations++;
        public bool SuppressesViewportReaction => Suppress;
        public void ReportState(int currentPage, double effectiveZoom) => States.Add((currentPage, effectiveZoom));
    }

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

    private static (FakeHost Host, FakeSurface Surface, ReportViewPresenter Presenter, ReportViewController Controller)
        Subject()
    {
        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);
        var session = new ReportViewSession(presenter, host, new TaskRunScheduler());
        var chrome = new FakeSurface();

        return (host, chrome, presenter, new ReportViewController(presenter, session, chrome));
    }

    [Fact]
    public void AfterPresenterUpdate_Immediate_RefreshesAndAsksForTilesWithoutWaiting()
    {
        var (_, chrome, _, controller) = Subject();

        controller.AfterPresenterUpdate(immediate: true);

        Assert.Equal(1, chrome.Refreshes);
        // The surface is not invalidated on the immediate path: the tiles themselves are what
        // repaints it, and invalidating as well would draw the old pixels once more first.
        Assert.Equal(0, chrome.Invalidations);
    }

    [Fact]
    public void AfterPresenterUpdate_Deferred_RepaintsWhatIsDrawnRatherThanPlanningNow()
    {
        var (_, chrome, _, controller) = Subject();

        controller.AfterPresenterUpdate(immediate: false);

        Assert.Equal(1, chrome.Refreshes);
        Assert.Equal(1, chrome.Invalidations);
    }

    [Fact]
    public void Scrolled_WhileAGestureOwnsTheViewport_LeavesThePresenterAlone()
    {
        var (_, chrome, _, controller) = Subject();
        chrome.Suppress = true;

        controller.Scrolled();

        Assert.Equal(0, chrome.Refreshes);
    }

    [Fact]
    public void Scrolled_WhenNothingIsDrivingTheViewport_UpdatesImmediately()
    {
        var (_, chrome, _, controller) = Subject();

        controller.Scrolled();

        Assert.Equal(1, chrome.Refreshes);
        Assert.Equal(0, chrome.Invalidations);
    }

    [Fact]
    public void ViewportChanged_DefersBecauseAResizeIsUsuallyStillMoving()
    {
        var (_, chrome, _, controller) = Subject();

        controller.ViewportChanged();

        Assert.Equal(1, chrome.Refreshes);
        Assert.Equal(1, chrome.Invalidations);
    }

    [Fact]
    public void RequestTiles_WithNoReportLoaded_DoesNothing()
    {
        var (_, chrome, _, controller) = Subject();
        var planned = 0;
        controller.TilesRequested += _ => planned++;

        controller.RequestTiles();

        Assert.Equal(0, planned);
    }

    [Fact]
    public async Task RequestTiles_AppliesTheHostsTilePolicyBeforePlanning()
    {
        var (host, chrome, presenter, controller) = Subject();
        chrome.TilePolicy = (VerticalOverdraw: 3, RenderBudget: 42);

        await controller.Session.LoadAsync(AReport(), Renderer);
        host.RunPosted();

        TilePlan? plan = null;
        controller.TilesRequested += p => plan = p;

        controller.RequestTiles();

        Assert.NotNull(plan);
        // Read from the chrome on every pass, not captured once: a host exposes these as bindable
        // properties the application can change at any time.
        Assert.Equal(3, presenter.VerticalOverdraw);
        Assert.Equal(42, presenter.RenderBudget);
    }

    [Fact]
    public async Task PresenterStateChanges_AreReportedToTheHost()
    {
        var (host, chrome, presenter, controller) = Subject();

        await controller.Session.LoadAsync(AReport(), Renderer);
        host.RunPosted();
        chrome.States.Clear();

        presenter.SetZoom(ReportZoomMode.Custom, 2, new Geometry.ViewPoint(0, 0));

        Assert.Contains(chrome.States, state => Math.Abs(state.Zoom - 2) < 0.001);
    }
}
