using Pysar.Viewer.Geometry;
using Xunit;

namespace Pysar.Viewer.Tests;

public class PageViewportTests
{
    // Three A4 pages drawn 800 x 1131 units each, 24 units apart.
    private static PageViewport Viewport() => new(
        PageCount: 3, PageWidth: 800, PageHeight: 1131, PageSpacing: 24, PagePointWidth: 595.5);

    [Fact]
    public void DocumentHeight_CountsThePagesAndTheGapsBetweenThem()
    {
        Assert.Equal(3 * 1131 + 2 * 24, Viewport().DocumentHeight, 3);
    }

    [Fact]
    public void DocumentHeight_OfASinglePage_HasNoGap()
    {
        var single = Viewport() with { PageCount = 1 };

        Assert.Equal(1131, single.DocumentHeight, 3);
    }

    [Fact]
    public void PageTop_StacksPagesWithTheSpacing()
    {
        Assert.Equal(0, Viewport().PageTop(0), 3);
        Assert.Equal(1155, Viewport().PageTop(1), 3);
        Assert.Equal(2310, Viewport().PageTop(2), 3);
    }

    [Fact]
    public void Padding_AddsSpaceAroundTheDocumentButNotBetweenThePages()
    {
        var padded = Viewport() with { Padding = new PagePadding(10, 16, 10, 32) };

        Assert.Equal(3 * 1131 + 2 * 24 + 16 + 32, padded.DocumentHeight, 3);
        Assert.Equal(800 + 10 + 10, padded.DocumentWidth, 3);
    }

    [Fact]
    public void Padding_MovesEveryPageWithoutChangingTheGaps()
    {
        var padded = Viewport() with { Padding = new PagePadding(10, 16, 10, 32) };

        Assert.Equal(10, padded.PageLeft, 3);
        Assert.Equal(16, padded.PageTop(0), 3);
        Assert.Equal(1171, padded.PageTop(1), 3);
        Assert.Equal(padded.PageTop(2) - padded.PageTop(1), Viewport().PageTop(2) - Viewport().PageTop(1), 3);
    }

    [Fact]
    public void Padding_OfADocumentWithoutPages_LeavesItEmpty()
    {
        var empty = Viewport() with { PageCount = 0, Padding = new PagePadding(16) };

        Assert.Equal(0, empty.DocumentHeight, 3);
    }

    [Fact]
    public void VisiblePages_WithPadding_AccountsForTheSpaceAboveTheFirstPage()
    {
        var padded = Viewport() with { Padding = new PagePadding(0, 100, 0, 0) };

        Assert.Empty(padded.VisiblePages(0, 90));
        Assert.Equal([0], padded.VisiblePages(0, 200));
    }

    [Fact]
    public void VisibleRegionPt_WithPadding_MeasuresFromThePageNotTheDocument()
    {
        var padded = Viewport() with { Padding = new PagePadding(0, 100, 0, 0) };

        var (_, top, _, bottom) = padded.VisibleRegionPt(0, 0, 100, 800, 200);
        var (_, plainTop, _, plainBottom) = Viewport().VisibleRegionPt(0, 0, 0, 800, 100);

        Assert.Equal(plainTop, top, 3);
        Assert.Equal(plainBottom, bottom, 3);
    }

    [Fact]
    public void VisiblePages_ReturnsOnlyThePagesTheWindowTouches()
    {
        Assert.Equal([0], Viewport().VisiblePages(0, 500));
        Assert.Equal([0, 1], Viewport().VisiblePages(1000, 1400));
        Assert.Equal([2], Viewport().VisiblePages(2400, 3000));
    }

    [Fact]
    public void VisiblePages_OfAWindowInTheGap_ReturnsNeither()
    {
        Assert.Empty(Viewport().VisiblePages(1135, 1150));
    }

    [Fact]
    public void VisibleRegionPt_ConvertsTheWindowIntoPagePoints()
    {
        // Page 0 drawn 800 units wide is 595.5 points, so one point is 1.343 units. A window from
        // 400 units down the page covers from 400 / 1.343 = 297.75 points.
        var region = Viewport().VisibleRegionPt(0, 0, 400, 800, 800);

        Assert.Equal(0, region.Left, 2);
        Assert.Equal(297.75, region.Top, 2);
        Assert.Equal(595.5, region.Right, 2);
        Assert.Equal(595.5, region.Bottom, 2);
    }

    [Fact]
    public void VisibleRegionPt_ClipsToThePage()
    {
        // A window taller than the page must not ask for content past its bottom edge.
        var region = Viewport().VisibleRegionPt(0, -100, -100, 2000, 2000);

        // 1131 units tall at 800 units per 595.5 points is 1131 x 595.5 / 800 = 841.89 points.
        Assert.Equal(0, region.Left, 2);
        Assert.Equal(0, region.Top, 2);
        Assert.Equal(595.5, region.Right, 2);
        Assert.Equal(841.89, region.Bottom, 2);
    }

    [Fact]
    public void FitWidthZoom_FillsTheViewport()
    {
        // 794 units is an A4 page at 100%; a 1588 unit viewport therefore fits it at 200%.
        Assert.Equal(2, PageViewport.FitWidthZoom(1588, 794), 3);
    }

    [Fact]
    public void FitPageZoom_TakesWhicheverAxisRunsOutFirst()
    {
        // Wide but short viewport: the height decides.
        Assert.Equal(0.5, PageViewport.FitPageZoom(4000, 561.5, 794, 1123), 3);
    }
}
