using Pysar.Viewer.Geometry;
using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ZoomModelTests
{
    // An A4 page, 595.5 x 842 points, in a viewport 800 x 1000 units.
    private static ZoomModel Model() => new()
    {
        PagePointWidth = 595.5,
        PagePointHeight = 842,
        UnitsPerPoint = 96d / 72d,
        ViewportWidth = 800,
        ViewportHeight = 1000
    };

    /// <summary>
    ///     The space around the pages is part of the document and scales with the zoom, so a fit mode
    ///     has to fit the page and that space together rather than fit the page into what is left over
    ///     - which would leave the padding, once scaled, pushing the page out of the viewport.
    /// </summary>
    [Fact]
    public void FitWidth_FitsThePageAndTheSpaceAroundItTogether()
    {
        var model = Model();
        model.Mode = ReportZoomMode.FitWidth;
        model.Padding = new PagePadding(20, 0, 20, 0);

        var pageAt100 = 595.5 * (96d / 72d);
        var zoom = model.EffectiveZoom;

        Assert.Equal(800 / (pageAt100 + 40), zoom, 4);

        // What that means on screen: the page and both margins come to exactly the viewport's width.
        Assert.Equal(800, pageAt100 * zoom + 40 * zoom, 3);
    }

    [Fact]
    public void Custom_UsesTheFactorAsGiven()
    {
        var model = Model();
        model.Mode = ReportZoomMode.Custom;
        model.Zoom = 1.75;

        Assert.Equal(1.75, model.EffectiveZoom, 4);
    }

    [Fact]
    public void Custom_IsHeldWithinTheLimits()
    {
        var model = Model();
        model.Mode = ReportZoomMode.Custom;
        model.Zoom = 42;

        Assert.Equal(ZoomModel.MaximumZoom, model.EffectiveZoom, 4);
    }

    [Fact]
    public void FitPage_TakesWhicheverSideRunsOutFirst()
    {
        var model = Model();
        model.Mode = ReportZoomMode.FitPage;

        var pageAt100 = 595.5 * (96d / 72d);
        var pageHeightAt100 = 842 * (96d / 72d);

        // The page is taller than it is wide against this viewport, so the height decides.
        Assert.Equal(Math.Min(800 / pageAt100, 1000 / pageHeightAt100), model.EffectiveZoom, 4);
    }
}
