using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Pagination;
using Xunit;

namespace Pysar.Skia.Tests.Pagination;

public class BandPaginatorTests
{
    private static LayoutNode Node(IReportElement el, float top, float bottom,
        IReadOnlyList<LayoutNode>? children = null, IReadOnlyList<float>? cuts = null) =>
        new(el, new Rect(0, top, 100, bottom), children ?? LayoutNode.NoChildren, cuts ?? LayoutNode.NoCuts);

    [Fact]
    public void Paginate_FitsInOneWindow_SinglePage()
    {
        var flow = new[] { Node(new DetailBand(), 0, 300) };
        var slices = BandPaginator.Paginate(flow, windowHeight: 500);
        Assert.Equal([new PageSlice(0, 500)], slices);
    }

    [Fact]
    public void Paginate_ZeroWindow_EmptyFlow_SingleEmptyPage()
    {
        var slices = BandPaginator.Paginate([], windowHeight: 0);
        Assert.Equal([new PageSlice(0, 0)], slices);
    }

    [Fact]
    public void Paginate_ZeroWindow_WithFlowContent_Throws()
    {
        var flow = new[] { Node(new DetailBand(), 0, 100) };
        Assert.ThrowsAny<Exception>(() => BandPaginator.Paginate(flow, windowHeight: 0));
    }

    [Fact]
    public void Paginate_CutsAtHint_NotMidRow()
    {
        var grid = Node(new Grid(), 0, 600, cuts: [200f, 400f, 600f]);
        var flow = new[] { Node(new DetailBand(), 0, 600, [grid], [600f]) };
        var slices = BandPaginator.Paginate(flow, 500);
        Assert.Equal(400, slices[0].End);
        Assert.Equal(400, slices[1].Start);
    }

    [Fact]
    public void Paginate_NoHintsInWindow_HardCutAdvances()
    {
        var flow = new[] { Node(new DetailBand(), 0, 1200) };   // atomic, no hints
        var slices = BandPaginator.Paginate(flow, 500);
        Assert.Equal(3, slices.Count);                           // 0-500, 500-1000, 1000-1200 (window up to 1500)
        Assert.Equal(500, slices[0].End);
    }

    [Fact]
    public void Paginate_KeepTogether_MovesBandWhole()
    {
        var b1 = Node(new ReportHeaderBand(), 0, 400);
        var band2El = new DetailBand { KeepTogether = true };
        var b2 = Node(band2El, 400, 800);                        // doesn't fit the remaining 100, fits in 500
        var slices = BandPaginator.Paginate([b1, b2], 500);
        Assert.Equal(400, slices[0].End);                        // cut BEFORE the band
    }

    [Fact]
    public void Paginate_PageBreakElement_ForcesCutAtItsTop()
    {
        // A PageBreak marker element at y=300 inside the band forces a cut there, even though the
        // content (0..400) would otherwise fit a single 500pt window.
        var marker = Node(new PageBreak(), 300, 300);
        var flow = new[] { Node(new DetailBand(), 0, 400, [marker]) };
        var slices = BandPaginator.Paginate(flow, 500);
        Assert.Equal(300, slices[0].End);
        Assert.Equal(300, slices[1].Start);
    }

    [Fact]
    public void Paginate_PageBreakBefore_ForcesCut()
    {
        var b1 = Node(new ReportHeaderBand(), 0, 100);
        var el = new DetailBand { PageBreak = PageBreakMode.Before };
        var b2 = Node(el, 100, 200);
        var slices = BandPaginator.Paginate([b1, b2], 500);
        Assert.Equal(100, slices[0].End);
    }

    [Fact]
    public void Paginate_FirstBandOffsetByTopMargin_SliceStartsAtFlowOrigin()
    {
        // The first band starts at y=10 (its top margin). The slice must start at the flow origin 0,
        // not at 10 — otherwise the translate cancels the top margin and pulls the flow up onto the header.
        var flow = new[] { Node(new ReportHeaderBand(), 10, 60), Node(new DetailBand(), 60, 300) };
        var slices = BandPaginator.Paginate(flow, 500);
        Assert.Equal(0, slices[0].Start);
    }

    [Fact]
    public void Paginate_FirstBandWithNegativeTopMargin_SliceStartsAtFlowOrigin()
    {
        // Leading negative margin is overflow into the preceding template region. It must not shift
        // the slice start or consume part of the first page's flow window.
        var flow = new[] { Node(new ReportHeaderBand(), -20, 80), Node(new DetailBand(), 80, 300) };
        var slices = BandPaginator.Paginate(flow, 500);
        Assert.Equal(0, slices[0].Start);
    }

    [Fact]
    public void Paginate_ProgressInvariant_NeverLoops()
    {
        var flow = new[] { Node(new DetailBand(), 0, 10_000, cuts: [0f]) };  // degenerate hint at the start
        var slices = BandPaginator.Paginate(flow, 100);
        Assert.All(slices, s => Assert.True(s.End > s.Start));
        Assert.Equal(10_000 / 100, slices.Count);
    }

    [Fact]
    public void Paginate_RepeatHeaderHeight_ReservesOnContinuationWindows()
    {
        // An atomic 2000pt band, window 500, repeat header 100: the first window is full (500), every
        // later window reserves the header height (400).
        var flow = new[] { Node(new DetailBand(), 0, 2000) };
        var slices = BandPaginator.Paginate(flow, windowHeight: 500, repeatHeaderHeight: 100);

        Assert.Equal(500, slices[0].End - slices[0].Start);
        Assert.Equal(400, slices[1].End - slices[1].Start);
        Assert.Equal(400, slices[2].End - slices[2].Start);
    }
}
