using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Xunit;

namespace Pysar.Viewer.Tests;

public class TilePlannerTests
{
    // An A4 page at a zoom deep enough that a page is far more than the whole-page limit of cells.
    private static TilePlanner Planner() => new()
    {
        PagePointWidth = 595.5f,
        PagePointHeight = 842f,
        Density = 2,
        RenderBudget = 192,
        VerticalOverdraw = 0.25
    };

    private static PageViewport Viewport(double zoom) => new(
        PageCount: 3,
        PageWidth: 595.5 * (96d / 72d) * zoom,
        PageHeight: 842 * (96d / 72d) * zoom,
        PageSpacing: 24,
        PagePointWidth: 595.5);

    [Fact]
    public void AtALowZoom_WholePagesAreAskedFor()
    {
        var plan = Planner().Plan(Viewport(1), scrollX: 0, scrollY: 0, viewportWidth: 800, viewportHeight: 1000);

        Assert.NotNull(plan);
        // A whole page means cells all the way to its bottom edge, not only the ones on screen.
        Assert.Contains(plan!.Requests, request => request.RegionPt.Bottom >= 841.9f);
    }

    [Fact]
    public void AnUnchangedRequest_IsNotAskedForTwice()
    {
        var planner = Planner();

        Assert.NotNull(planner.Plan(Viewport(1), 0, 0, 800, 1000));
        Assert.Null(planner.Plan(Viewport(1), 0, 0, 800, 1000));
    }

    [Fact]
    public void AtADeepZoom_ScrollingSidewaysAsksAgain()
    {
        var planner = Planner();
        var viewport = Viewport(5);

        Assert.NotNull(planner.Plan(viewport, scrollX: 0, scrollY: 0, 800, 1000));

        // Half a cell across, which is as often as the answer can change.
        var step = TilePlanner.TileSidePx / 2 / 2;

        Assert.NotNull(planner.Plan(viewport, scrollX: step + 1, scrollY: 0, 800, 1000));
    }

    [Fact]
    public void TheBudget_KeepsTheCellsNearestTheMiddleOfTheViewport()
    {
        var planner = Planner();
        planner.RenderBudget = 8;

        var plan = planner.Plan(Viewport(5), scrollX: 0, scrollY: 0, 800, 1000);

        Assert.NotNull(plan);
        Assert.True(plan!.Requests.Count > 0);
        Assert.True(plan.BudgetTrimmed);
    }

    [Fact]
    public void Plan_OrdersCellsNearestCentreFirst()
    {
        var viewport = Viewport(1);
        const double scrollX = 0;
        const double scrollY = 0;
        const double viewportWidth = 800;
        const double viewportHeight = 1000;

        var plan = Planner().Plan(viewport, scrollX, scrollY, viewportWidth, viewportHeight);

        Assert.NotNull(plan);
        Assert.True(plan!.Requests.Count > 1);

        var unitsPerPoint = viewport.UnitsPerPoint;
        var offsetX = viewport.PageOffsetX(viewportWidth);
        var centreX = scrollX + viewportWidth / 2;
        var centreY = scrollY + viewportHeight / 2;

        double Distance(TileRequest request)
        {
            var cellX = offsetX + (request.RegionPt.Left + request.RegionPt.Right) / 2 * unitsPerPoint;
            var cellY = viewport.PageTop(request.Key.PageIndex)
                        + (request.RegionPt.Top + request.RegionPt.Bottom) / 2 * unitsPerPoint;

            return Math.Abs(cellX - centreX) + Math.Abs(cellY - centreY);
        }

        for (var i = 1; i < plan.Requests.Count; i++)
            Assert.True(
                Distance(plan.Requests[i - 1]) <= Distance(plan.Requests[i]),
                $"Request {i} is closer to centre than request {i - 1}");
    }

    [Fact]
    public void CoverageFactorZero_MatchesSingleScalePlan()
    {
        var with = Planner();
        with.CoverageFactor = 0;
        var a = with.Plan(Viewport(2), 0, 0, 800, 1000);

        var without = Planner();
        var b = without.Plan(Viewport(2), 0, 0, 800, 1000);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(b!.Requests.Select(r => r.Key), a!.Requests.Select(r => r.Key));
    }

    [Fact]
    public void CoverageFactorHalf_EmitsCoverageScalesBeforeFull_AndCoverageUsesLowerScale()
    {
        var planner = Planner();
        planner.CoverageFactor = 0.5;
        planner.Reset();

        var viewport = Viewport(2);
        var plan = planner.Plan(viewport, 0, 0, 800, 1000);
        Assert.NotNull(plan);

        var fullScale = viewport.RenderScale(planner.Density);
        var scales = plan!.Requests.Select(r => r.Key.Scale).Distinct().OrderBy(s => s).ToList();
        Assert.True(scales.Count >= 2);

        var firstFullIndex = plan.Requests.ToList().FindIndex(r => Math.Abs(r.Key.Scale - fullScale) <= Math.Abs(fullScale) * 1e-4f);
        var lastCoverageIndex = plan.Requests.ToList().FindLastIndex(r => r.Key.Scale < fullScale * 0.9f);
        Assert.True(firstFullIndex >= 0);
        Assert.True(lastCoverageIndex >= 0);
        Assert.True(lastCoverageIndex < firstFullIndex);
        Assert.True(plan.Requests[0].Key.Scale < fullScale * 0.9f);
    }

    /// <summary>
    ///     Coverage and full-DPI have to reach exactly as far across the page as each other. A cell
    ///     rounded to whole pixels of its own scale rounds by twice as much at half the scale, and
    ///     the two layers then cover slightly different ground - which the reader sees as the text
    ///     resizing the moment the sharp layer replaces the soft one.
    /// </summary>
    [Fact]
    public void CoverageAndFullDpi_ReachTheSameEdgesOfThePage()
    {
        var planner = Planner();
        planner.CoverageFactor = 0.5;

        var plan = planner.Plan(Viewport(1.37), scrollX: 0, scrollY: 0, viewportWidth: 801, viewportHeight: 997);

        Assert.NotNull(plan);
        Assert.NotEmpty(plan!.Requests);

        var scales = plan.Requests.Select(request => request.Key.Scale).Distinct().ToList();

        Assert.True(scales.Count >= 2, "the plan should have a coverage pass and a full-DPI one");

        // Every cell stops at the page, whatever scale it is for. A cell rounded out to its own
        // whole pixels would stop a different distance past the edge at each scale, and the layers
        // would no longer describe the same page.
        foreach (var request in plan.Requests)
        {
            Assert.True(
                request.RegionPt.Right <= 595.5f + 1e-3f,
                $"a cell for scale {request.Key.Scale} reaches {request.RegionPt.Right} past the page");

            Assert.True(
                request.RegionPt.Bottom <= 842f + 1e-3f,
                $"a cell for scale {request.Key.Scale} reaches {request.RegionPt.Bottom} below the page");
        }

        // And a cell ends either on its own grid or at the page edge - never a rounded distance
        // short of one, which is what differed between the scales.
        foreach (var request in plan.Requests)
        {
            var cell = TilePlanner.TileSidePx / request.Key.Scale;
            var onGrid = Math.Abs(request.RegionPt.Right - (request.Key.Column + 1) * cell) < 1e-2f;
            var atEdge = Math.Abs(request.RegionPt.Right - 595.5f) < 1e-2f;

            Assert.True(
                onGrid || atEdge,
                $"a cell for scale {request.Key.Scale} ends at {request.RegionPt.Right}, "
                + "neither on its grid nor at the page edge");
        }
    }

    [Fact]
    public void WhenBudgetTight_FullDpiPreferredOverCoverage()
    {
        var planner = Planner();
        planner.CoverageFactor = 0.5;
        planner.RenderBudget = 4;
        planner.Reset();

        var viewport = Viewport(5);
        var fullScale = viewport.RenderScale(planner.Density);
        var plan = planner.Plan(viewport, 0, 0, 800, 1000);
        Assert.NotNull(plan);
        Assert.Contains(plan!.Requests, r => Math.Abs(r.Key.Scale - fullScale) <= Math.Abs(fullScale) * 1e-4f);
    }
}
