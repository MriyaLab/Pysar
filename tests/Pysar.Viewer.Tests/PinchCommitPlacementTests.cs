using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

/// <summary>
///     Reproduction for the report being drawn wrong after a pinch commit on the browser hosts
///     (Blazor, Uno-wasm, Avalonia-browser), where it only comes right on the next zoom and not on a
///     scroll. The three hosts share the presenter/tile pipeline exercised here; a scroll does not
///     re-run <see cref="ReportViewPresenter.PlaceTiles"/> when the plan is unchanged, so whatever the
///     commit leaves placed survives every scroll until the next zoom re-plans.
///     <para>
///     These drive the same headless pipeline the hosts use: the cache draws the cells, an
///     invalidation asks the surface to place them, and <c>PlaceTiles</c> is what maps the drawn cells
///     onto the host's views. The assertion is that reaching a zoom by a pinch commit leaves exactly
///     the views that reaching the same zoom directly does - no stale cells of another scale left over.
///     </para>
/// </summary>
public class PinchCommitPlacementTests
{
    private static readonly SkiaReportRenderer Renderer = new();

    private static readonly ViewPoint Centre = new(400, 500);

    /// <summary>
    ///     A report of several pages, so a zoom brings different cells into play and there is a page
    ///     boundary for a stale cell to be stranded across.
    /// </summary>
    private static Report ATallReport()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(4000)) });
        design.Build();

        return design;
    }

    /// <summary>
    ///     The report drawn wrong after a pinch must hold the same views as the report drawn right by a
    ///     direct zoom to the same factor - which is what the reader gets when the "next zoom" fixes it.
    /// </summary>
    [Fact]
    public async Task AfterAPinchCommit_ThePlacedCellsMatchADirectZoomToTheSameFactor()
    {
        var byPinch = await ReachZoomByPinchAsync(startZoom: 1.0, factor: 1.6);
        var byDirect = await ReachZoomByDirectAsync(startZoom: 1.0, targetZoom: byPinch.EffectiveZoom);

        // Same zoom reached both ways.
        Assert.Equal(byDirect.EffectiveZoom, byPinch.EffectiveZoom, 4);

        // No stale cells of another scale left behind by the commit.
        Assert.All(
            byPinch.PlacedKeys,
            key => Assert.Equal(byPinch.RenderScale, key.Scale, 3));

        // The very set of cells is the one a clean zoom to this factor produces.
        Assert.Equal(byDirect.PlacedKeys, byPinch.PlacedKeys);

        // And they sit where that clean zoom put them.
        Assert.Equal(byDirect.ScrollY, byPinch.ScrollY, 1);
    }

    /// <summary>
    ///     Reaches <paramref name="factor"/> the way a touch pinch does: a preview while the fingers
    ///     move, then the commit sequence the hosts run - <c>BeginPinch</c>/<c>PinchByScale</c> to set
    ///     the zoom, <c>SetZoom</c> holding the point the gesture began on, then an immediate refresh.
    /// </summary>
    private static async Task<Outcome> ReachZoomByPinchAsync(double startZoom, double factor)
    {
        var harness = await StartedHarnessAsync(startZoom);

        var pinch = new PinchSession(harness.Presenter);

        pinch.Begin(Centre);
        pinch.MoveByScale(factor);

        var commit = pinch.End();
        Assert.NotNull(commit);

        // The commit sequence, as CommitPinch/ApplyGestureZoom run it.
        harness.Presenter.Gestures.BeginPinch();
        harness.Presenter.Gestures.PinchByScale(commit!.Value.Factor);

        harness.Presenter.SetZoom(
            harness.Presenter.ZoomMode, harness.Presenter.Zoom, commit.Value.Anchor, commit.Value.Held);

        await harness.SettleAsync(immediate: true);

        return harness.Snapshot();
    }

    /// <summary>Reaches <paramref name="targetZoom"/> the way the toolbar or the "next zoom" does.</summary>
    private static async Task<Outcome> ReachZoomByDirectAsync(double startZoom, double targetZoom)
    {
        var harness = await StartedHarnessAsync(startZoom);

        harness.Presenter.SetZoom(ReportZoomMode.Custom, targetZoom, Centre);

        await harness.SettleAsync(immediate: true);

        return harness.Snapshot();
    }

    /// <summary>A harness with a report loaded and its first zoom placed.</summary>
    private static async Task<Harness> StartedHarnessAsync(double startZoom)
    {
        var harness = new Harness();

        await harness.Session.LoadAsync(ATallReport(), Renderer);
        harness.Host.RunPosted();

        harness.Presenter.ViewportChanged();

        // A concrete starting zoom, so the factor is measured against a known point rather than
        // whatever a fit mode resolves to for this viewport.
        harness.Presenter.SetZoom(ReportZoomMode.Custom, startZoom, Centre);

        await harness.SettleAsync(immediate: true);

        return harness;
    }

    private readonly record struct Outcome(
        double EffectiveZoom, float RenderScale, double ScrollY, IReadOnlySet<TileKey> PlacedKeys);

    /// <summary>
    ///     The headless equivalent of one host: a <see cref="FakeHost"/> for the viewport, a surface
    ///     that places cells the way the hosts' <c>RefreshVisuals</c> does, and the controller that
    ///     sequences them.
    /// </summary>
    private sealed class Harness : IReportViewSurface
    {
        public FakeHost Host { get; } = new() { ViewportWidth = 800, ViewportHeight = 1000, Density = 2 };

        public ReportViewPresenter Presenter { get; }

        public ReportViewSession Session { get; }

        public ReportViewController Controller { get; }

        public Harness()
        {
            Presenter = new ReportViewPresenter(Host);
            Session = new ReportViewSession(Presenter, Host, new TaskRunScheduler());
            Controller = new ReportViewController(Presenter, Session, this);
        }

        /// <summary>
        ///     Asks for the cells this viewport needs, waits until the cache has drawn every one of
        ///     them, then places them - which is what a host does across a burst of invalidations.
        /// </summary>
        public async Task SettleAsync(bool immediate)
        {
            TilePlan? plan = null;
            void Capture(TilePlan p) => plan = p;

            Controller.TilesRequested += Capture;
            try
            {
                Controller.AfterPresenterUpdate(immediate);
            }
            finally
            {
                Controller.TilesRequested -= Capture;
            }

            if (Session.Tiles is { } tiles && plan is { } wanted)
                await WaitUntilDrawnAsync(tiles, wanted);

            // Place once the cache is complete: the steady state a host reaches after the last cell
            // of the pass has arrived and been drawn.
            RefreshVisuals();
        }

        private static async Task WaitUntilDrawnAsync(ReportViewTiles tiles, TilePlan plan)
        {
            bool Drawn()
            {
                foreach (var request in plan.Requests)
                {
                    if (request.RegionPt.Width <= 0 || request.RegionPt.Height <= 0)
                        continue;

                    var present = tiles
                        .TilesFor(request.Key.PageIndex, request.Key.Scale)
                        .Any(tile => tile.Key == request.Key);

                    if (!present)
                        return false;
                }

                return true;
            }

            if (Drawn())
                return;

            var done = new TaskCompletionSource();
            void OnInvalidated()
            {
                if (Drawn())
                    done.TrySetResult();
            }

            tiles.Invalidated += OnInvalidated;
            try
            {
                if (Drawn())
                    return;

                await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                tiles.Invalidated -= OnInvalidated;
            }
        }

        public Outcome Snapshot() => new(
            Presenter.EffectiveZoom,
            Presenter.ViewportRenderScale(),
            Host.ScrollY,
            Host.Tiles.Keys.ToHashSet());

        void IReportViewSurface.RefreshVisuals() => RefreshVisuals();

        private void RefreshVisuals()
        {
            if (Session.Tiles is not { PageCount: > 0 })
                return;

            Presenter.PlaceTiles(Host.Tiles.Keys.ToList());
        }

        void IReportViewSurface.ClearVisuals() { }

        void IReportViewSurface.InvalidateSurface() { }

        public bool SuppressesViewportReaction => false;

        public (double VerticalOverdraw, double RenderBudget) TilePolicy => (1, 200);

        public void ReportState(int currentPage, double effectiveZoom) { }
    }
}
