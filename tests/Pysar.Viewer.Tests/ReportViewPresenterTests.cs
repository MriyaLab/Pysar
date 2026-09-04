using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ReportViewPresenterTests
{
    private static (FakeHost Host, ReportViewPresenter Presenter) Subject()
    {
        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);

        presenter.SetDocument(pageCount: 3, pagePointWidth: 595.5f, pagePointHeight: 842f);

        return (host, presenter);
    }

    [Fact]
    public void AResizeDuringStartup_DoesNotMoveTheScrollPosition()
    {
        var (host, presenter) = Subject();

        // The viewport is measured several times before it settles, and each measurement changes the
        // fitted zoom. None of them is a zoom the reader asked for.
        foreach (var height in new double[] { 1, 1234, 1635, 1203 })
        {
            host.ViewportHeight = height;
            presenter.ViewportChanged();
        }

        Assert.Empty(host.Scrolls);
        Assert.Equal(0, host.ScrollY, 3);
    }

    [Fact]
    public void AResize_SetsTheExtentForTheNewViewportInTheSameUpdate()
    {
        var (host, presenter) = Subject();
        presenter.ViewportChanged();

        var before = host.Extent.Y;

        host.ViewportWidth = 1200;
        presenter.ViewportChanged();

        // Fitting a viewport half again as wide makes the whole document half again as tall - the
        // gaps between the pages and the space around them included, since those belong to the
        // document. An extent that still described the old zoom would leave nothing to scroll.
        Assert.Equal(before * 1.5, host.Extent.Y, 0);
    }

    /// <summary>
    ///     A document shorter than the viewport still fills it: pages stay at the top (plus padding),
    ///     and empty space sits below - the same on every host. An extent only as tall as the pages
    ///     lets a scroll viewer centre the canvas, which is what put Avalonia's pages in the middle
    ///     while MAUI kept them at the top.
    /// </summary>
    [Fact]
    public void AShortDocument_FillsTheViewportHeightSoPagesStayAtTheTop()
    {
        var host = new FakeHost { ViewportWidth = 800, ViewportHeight = 1000 };
        var presenter = new ReportViewPresenter(host)
        {
            PageBorderThickness = 0,
            Padding = new PagePadding(16)
        };

        presenter.SetDocument(pageCount: 1, pagePointWidth: 595.5f, pagePointHeight: 842f);
        presenter.SetZoom(ReportZoomMode.Custom, 0.25, new ViewPoint(400, 500));

        Assert.True(host.Extent.Y >= host.ViewportHeight - 0.5,
            $"extent {host.Extent.Y} should fill viewport {host.ViewportHeight}");
        Assert.Equal(16 * presenter.EffectiveZoom, host.Pages[0].Y, 3);
        Assert.Equal(0, host.ScrollY, 3);
    }

    [Fact]
    public void ADeliberateZoom_MovesTheScrollToKeepItsAnchor()
    {
        var (host, presenter) = Subject();
        presenter.ViewportChanged();

        host.ScrollY = 1000;
        presenter.SetZoom(ReportZoomMode.Custom, 2, new ViewPoint(400, 500));

        Assert.NotEmpty(host.Scrolls);
    }

    /// <summary>
    ///     The point the reader is holding has to come out from under a zoom exactly where it went in,
    ///     which is what naming that point rather than scaling the scroll position by the change in zoom
    ///     buys. It holds for a point anywhere in the document, however much unscaled space - a page
    ///     border, a viewport that has to centre a narrow document - stands between it and the top.
    /// </summary>
    [Fact]
    public void AZoom_LeavesWhatWasUnderTheAnchorUnderIt()
    {
        var (host, presenter) = Subject();

        // No border, so the recorded page frames are the pages themselves.
        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 1000;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 500);

        // What is under the anchor now, as a point of a page rather than of the document: the page it
        // is on, and how far into that page it is.
        const int page = 1;
        var before = host.Pages[page];

        var intoPageX = host.ScrollX + anchor.X - before.X;
        var intoPageY = host.ScrollY + anchor.Y - before.Y;

        Assert.InRange(intoPageY, 0, before.Height);

        presenter.SetZoom(ReportZoomMode.Custom, 2, anchor);

        var after = host.Pages[page];

        Assert.Equal(anchor.X, after.X + intoPageX * 2 - host.ScrollX, 3);
        Assert.Equal(anchor.Y, after.Y + intoPageY * 2 - host.ScrollY, 3);
    }

    /// <summary>
    ///     A gesture is shown by scaling what is already drawn, and then paid for with one relayout
    ///     when it ends. Those two have to agree, or the release moves the document under the reader's
    ///     fingers - which no amount of care in the gesture itself can prevent, since what the relayout
    ///     does depends on the space around the pages, on the centring of a document narrower than the
    ///     viewport, and on how far the document can be scrolled at all.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(0.5)]
    public void APreviewOfAZoom_ShowsWhereApplyingItWouldPutThePages(double factor)
    {
        var (host, presenter) = Subject();

        // No border, so the recorded page frames are the pages themselves.
        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 1000;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 500);
        var scrolledX = host.ScrollX;
        var scrolledY = host.ScrollY;

        var preview = presenter.PreviewZoom(factor, anchor);
        Assert.NotNull(preview);

        // Where the preview draws each page: the canvas is scaled and translated whole, and the scroll
        // position stays where the gesture found it.
        var drawn = host.Pages.ToDictionary(
            page => page.Key,
            page => (X: preview!.Value.Scale * page.Value.X + preview.Value.OffsetX - scrolledX,
                Y: preview.Value.Scale * page.Value.Y + preview.Value.OffsetY - scrolledY));

        presenter.SetZoom(ReportZoomMode.Custom, preview!.Value.Zoom, anchor);

        // Where applying the same zoom actually puts them. Every page, not only the one the gesture
        // was anchored on: the space around the pages scales with the zoom, so one scale places them all.
        Assert.Equal(3, drawn.Count);

        foreach (var (index, expected) in drawn)
        {
            Assert.Equal(expected.X, host.Pages[index].X - host.ScrollX, 3);
            Assert.Equal(expected.Y, host.Pages[index].Y - host.ScrollY, 3);
        }
    }

    /// <summary>
    ///     A gesture names the point it holds when it begins and hands that point back for as long as it
    ///     runs, so that a scroll while it runs - a pan alongside the pinch, a platform's own momentum -
    ///     cannot change what the zoom is anchored to. Resolving the point afresh on each frame instead
    ///     lets the drawing drift under the fingers.
    /// </summary>
    [Fact]
    public void AZoomHoldingAPointGivenToIt_IgnoresWhereTheViewHasScrolledSince()
    {
        var (host, presenter) = Subject();

        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 1000;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 500);
        var held = presenter.PointAt(anchor);

        Assert.NotNull(held);

        // The view moves while the gesture is still running.
        host.ScrollY = 1400;
        presenter.Scrolled();

        presenter.SetZoom(ReportZoomMode.Custom, 2, anchor, held);

        Assert.Equal(anchor.Y, ScreenY(host, held!.Value), 3);
    }

    /// <summary>
    ///     MAUI applies <c>ScrollToAsync</c> on a later turn, so for a frame or more it reports the
    ///     position it had before the zoom. A point named in that window has to be measured against
    ///     where the view is going, not where it still is.
    /// </summary>
    [Fact]
    public void APointNamedBeforeADeferredScrollHasLanded_IsTheOneUnderTheAnchor()
    {
        var (host, presenter) = Subject();

        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 1000;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 500);

        host.DeferScroll = true;
        presenter.SetZoom(ReportZoomMode.Custom, 2, anchor);

        // Named while the host still reports the position it had at the zoom before.
        var held = presenter.PointAt(anchor);

        Assert.NotNull(held);

        host.RunDeferredScroll();

        Assert.Equal(anchor.Y, ScreenY(host, held!.Value), 3);
    }

    /// <summary>Where a point of the report appears in the viewport, for a page drawn without a border.</summary>
    private static double ScreenY(FakeHost host, DocumentPoint point)
    {
        var page = host.Pages[point.PageIndex];

        return page.Y + point.YPt * (page.Width / 595.5) - host.ScrollY;
    }

    [Fact]
    public void ThePadding_IsSpaceAboveTheFirstPageAndNotPartOfIt()
    {
        var (host, presenter) = Subject();

        // No border, so this test is about the padding alone: the page's frame is grown by the
        // border's width, which the test below covers on its own.
        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.ViewportChanged();

        // At the zoom the fit resolved to, since the padding is part of the document and scales with it.
        Assert.Equal(16 * presenter.EffectiveZoom, host.Pages[0].Y, 3);
        Assert.Equal(0, host.ScrollY, 3);
    }

    [Fact]
    public void ThePageFrame_IsGrownByTheBorderSoTheCellsDoNotCoverIt()
    {
        var (host, presenter) = Subject();

        presenter.PageBorderThickness = 2;
        presenter.Padding = new PagePadding(16);
        presenter.ViewportChanged();

        // The cells are laid over the page exactly, so the line has to sit outside them. The line is
        // drawn at its own width whatever the zoom, unlike the padding above it.
        Assert.Equal(16 * presenter.EffectiveZoom - 2, host.Pages[0].Y, 3);
    }

    [Fact]
    public void ScrollingSideways_AsksForTheCellsThatCameIntoView()
    {
        var (host, presenter) = Subject();

        presenter.SetZoom(ReportZoomMode.Custom, 5, new ViewPoint(400, 500));
        presenter.ViewportChanged();

        var first = presenter.PlanTiles();
        Assert.NotNull(first);

        host.ScrollX += 600;

        Assert.NotNull(presenter.PlanTiles());
    }

    [Fact]
    public void TheCurrentPage_IsTheOneAtTheTopOfTheViewport()
    {
        var (host, presenter) = Subject();

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));
        presenter.ViewportChanged();

        var pageHeight = host.Pages[0].Height;

        host.ScrollY = pageHeight + 24 + 10;
        presenter.Scrolled();

        Assert.Equal(2, presenter.CurrentPage);
    }

    /// <summary>
    ///     A scroll view has already moved every page and cell by the time this is heard about, and a
    ///     scroll changes neither the size of the document nor where a page sits in it. Laying any of
    ///     it out again costs the length of the report on every frame of a scroll - and on UIKit the
    ///     extent write alone re-clamps the offset, which is felt as the view snagging.
    /// </summary>
    [Fact]
    public void AScroll_LeavesTheExtentAndThePagePositionsAlone()
    {
        var (host, presenter) = Subject();

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        var extent = host.Extent;
        var pages = host.Pages.ToDictionary(page => page.Key, page => page.Value);

        var extentWrites = host.ExtentWrites;
        var placements = host.PagePlacements;

        host.ScrollY = host.Pages[0].Height / 3;
        presenter.Scrolled();

        Assert.Equal(extentWrites, host.ExtentWrites);
        Assert.Equal(placements, host.PagePlacements);

        // And what was laid out before is still where it was, since nothing about it depends on the
        // scroll: a page's position is a position in the document, not on the screen.
        Assert.Equal(extent, host.Extent);
        Assert.Equal(pages, host.Pages);
    }

    /// <summary>
    ///     What a scroll does still have to do: say which page is at the top, and say so only when it
    ///     has changed - a host writes that into a bindable property, and this runs per frame.
    /// </summary>
    [Fact]
    public void AScroll_ReportsTheCurrentPageOnlyWhenItMovedToAnother()
    {
        var (host, presenter) = Subject();

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        var reports = 0;
        presenter.StateChanged += () => reports++;

        var step = host.Pages[0].Height;

        // Within the first page: nothing to say.
        host.ScrollY = step / 4;
        presenter.Scrolled();

        Assert.Equal(0, reports);
        Assert.Equal(1, presenter.CurrentPage);

        // Onto the second: said once.
        host.ScrollY = step + 24 + 10;
        presenter.Scrolled();

        Assert.Equal(1, reports);
        Assert.Equal(2, presenter.CurrentPage);

        host.ScrollY += 5;
        presenter.Scrolled();

        Assert.Equal(1, reports);
    }

    [Fact]
    public void AZoomBeforeAReportIsLoaded_DoesNotScroll()
    {
        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);

        presenter.SetZoom(ReportZoomMode.Custom, 2, new ViewPoint(400, 500));

        Assert.Empty(host.Scrolls);
    }

    /// <summary>
    ///     A zoom's scroll has to land before <see cref="ReportViewPresenter.SetZoom"/> returns: the
    ///     host drops any pinch transform on the same call, and a posted scroll would leave one frame
    ///     of pages at the new zoom under the old offset - felt as the page jumping after the zoom,
    ///     especially on the first and last pages where that delta is largest.
    /// </summary>
    [Fact]
    public void AZoom_AppliesItsScrollBeforeReturningEvenWhenPostsAreDeferred()
    {
        var (host, presenter) = Subject();

        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 0;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 100);
        var held = presenter.PointAt(anchor);
        Assert.NotNull(held);

        host.DeferPosts = true;
        presenter.SetZoom(ReportZoomMode.Custom, 2, anchor, held);

        Assert.Equal(anchor.Y, ScreenY(host, held!.Value), 3);
        Assert.Empty(host.Posted);
    }

    /// <summary>
    ///     Zooming in on the first page while already at the top must keep the held point under the
    ///     anchor once the scroll is clamped to zero - the case where the preview transform had been
    ///     doing the work the scroll cannot.
    /// </summary>
    [Fact]
    public void ZoomIn_FirstPageAtTop_KeepsTheHeldPointUnderTheAnchor()
    {
        var (host, presenter) = Subject();

        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;

        presenter.SetZoom(ReportZoomMode.Custom, 1, new ViewPoint(400, 500));

        host.ScrollY = 0;
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 80);
        var held = presenter.PointAt(anchor);
        Assert.NotNull(held);

        presenter.SetZoom(ReportZoomMode.Custom, 2, anchor, held);

        Assert.Equal(anchor.Y, ScreenY(host, held!.Value), 3);
    }

    /// <summary>
    ///     Zooming out on the last page shortens the document under the reader. A real scroll viewer
    ///     clamps the offset as the extent shrinks and reports that as a scroll - which must not clear
    ///     the zoom's pending scroll or re-enter the update, or the release lands somewhere other than
    ///     the preview was showing.
    /// </summary>
    [Fact]
    public void ZoomOut_LastPage_SurvivesThePlatformClampingTheExtent()
    {
        var host = new ClampingFakeHost();
        var presenter = new ReportViewPresenter(host);

        presenter.PageBorderThickness = 0;
        presenter.Padding = new PagePadding(16);
        presenter.PageSpacing = 24;
        presenter.SetDocument(pageCount: 3, pagePointWidth: 595.5f, pagePointHeight: 842f);

        host.ScrollClamped = () => presenter.Scrolled();

        presenter.SetZoom(ReportZoomMode.Custom, 2, new ViewPoint(400, 500));

        var maxScroll = Math.Max(0, host.Extent.Y - host.ViewportHeight);
        host.ReportScroll(0, maxScroll);
        presenter.Scrolled();

        var anchor = new ViewPoint(400, 900);
        var held = presenter.PointAt(anchor);
        Assert.NotNull(held);

        var scrolledY = host.ScrollY;
        var preview = presenter.PreviewZoom(0.5, anchor, held);
        Assert.NotNull(preview);

        // Where the preview draws the held point, with the scroll left where the gesture found it.
        var page = host.Pages[held!.Value.PageIndex];
        var heldDocumentY = page.Y + held.Value.YPt * (page.Width / 595.5);
        var underPreview = preview!.Value.Scale * heldDocumentY + preview.Value.OffsetY - scrolledY;

        presenter.SetZoom(ReportZoomMode.Custom, preview.Value.Zoom, anchor, held);

        Assert.Equal(underPreview, ScreenY(host, held.Value), 3);
    }

    /// <summary>Where a point of the report appears in the viewport, for a page drawn without a border.</summary>
    private static double ScreenY(ClampingFakeHost host, DocumentPoint point)
    {
        var page = host.Pages[point.PageIndex];

        return page.Y + point.YPt * (page.Width / 595.5) - host.ScrollY;
    }

    [Fact]
    public async Task APlacedCell_ArrivesWithItsPixelsAndItsPosition()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);

        using var tiles = new ReportViewTiles(
            await ReportRenderSession.CreateAsync(design), new TaskRunScheduler());

        presenter.SetTiles(tiles);

        // Through the real path rather than a hand-made key: the scale the presenter will later look
        // cells up by is derived from the zoom and the density, and a guess at it here would make
        // the test pass or fail for reasons that have nothing to do with placement.
        presenter.ViewportChanged();

        var plan = presenter.PlanTiles();

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Requests);

        var arrived = new TaskCompletionSource();

        tiles.Invalidated += () => arrived.TrySetResult();
        tiles.RequestTiles(plan.Requests);

        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        presenter.PlaceTiles([]);

        // At least one, not exactly one: the planner decides how many cells an A4 page needs at this
        // zoom and density, and the event fires as soon as the first of them lands.
        Assert.NotEmpty(host.Tiles);

        var placed = host.Tiles.Values.First();

        Assert.NotEmpty(placed.Tile.Bytes);
        Assert.True(placed.Tile.PixelWidth > 0, "the tile should know its pixel width");
        Assert.True(placed.Tile.PixelHeight > 0, "the tile should know its pixel height");
        Assert.True(placed.Bounds.Width > 0, "the tile should be placed somewhere with a size");
    }

    /// <summary>
    ///     A cell drawn for the device grid has to land on it. Placed at a fraction of a device pixel,
    ///     or given a size its pixels do not divide into, every glyph in it is resampled: text turns
    ///     soft, and the softness changes with each layout because the fraction does - which is what
    ///     "the text shimmers" is.
    /// </summary>
    [Fact]
    public async Task PlacedCells_LandOnWholeDevicePixelsAndKeepTheirPixelSize()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        // Padding and a viewport that do not divide evenly: the page offset, and with it every cell
        // on the page, then falls between device pixels unless placement snaps it.
        var host = new FakeHost { Density = 2, ViewportWidth = 801, ViewportHeight = 997 };
        var presenter = new ReportViewPresenter(host) { Padding = new PagePadding(16.3) };

        using var tiles = new ReportViewTiles(
            await ReportRenderSession.CreateAsync(design), new TaskRunScheduler());

        presenter.SetTiles(tiles);
        presenter.ViewportChanged();

        // Deep enough that the page is several cells across, so there are cells the page edge does
        // not cut - those are the ones that have to be pixel-exact.
        presenter.SetZoom(ReportZoomMode.Custom, 3, new ViewPoint(400, 500));

        var plan = presenter.PlanTiles();

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Requests);

        var arrived = new TaskCompletionSource();

        tiles.Invalidated += () => arrived.TrySetResult();
        tiles.RequestTiles(plan.Requests);

        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        presenter.PlaceTiles([]);

        Assert.NotEmpty(host.Tiles);

        var whole = 0;

        foreach (var (bounds, tile) in host.Tiles.Values)
        {
            AssertWholeDevicePixels(bounds.X * host.Density, "left");
            AssertWholeDevicePixels(bounds.Y * host.Density, "top");

            // A cell the page edge did not cut short covers TileSidePx of it, so its size in device
            // pixels is its pixel count and nothing is resampled. The cut ones are off by under a
            // pixel; sizing those by their pixels instead would move them off the region they stand
            // for, which is what makes layers of different scales disagree.
            if (Math.Abs(tile.PixelWidth - TilePlanner.TileSidePx) > 0.5)
                continue;

            whole++;

            Assert.Equal(tile.PixelWidth, bounds.Width * host.Density, 6);
            Assert.Equal(tile.PixelHeight, bounds.Height * host.Density, 6);
        }

        Assert.True(whole > 0, "the page should be more than one cell across at this zoom");

        static void AssertWholeDevicePixels(double value, string edge)
            => Assert.True(
                Math.Abs(value - Math.Round(value)) < 1e-6,
                $"the {edge} edge sits at {value} device pixels, which resamples the cell");
    }

    /// <summary>
    ///     A cell drawn for a zoom the view has left still covers the page region it was drawn for,
    ///     stretched into the new geometry. Sized by its own pixels instead, it would draw the
    ///     previous zoom's page at the previous zoom's size on top of the current one.
    /// </summary>
    [Fact]
    public async Task ACellFromThePreviousZoom_IsStretchedIntoTheNewGeometry()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        var host = new FakeHost { Density = 2 };
        var presenter = new ReportViewPresenter(host);

        using var tiles = new ReportViewTiles(
            await ReportRenderSession.CreateAsync(design), new TaskRunScheduler());

        presenter.SetTiles(tiles);
        presenter.ViewportChanged();

        var plan = presenter.PlanTiles();

        Assert.NotNull(plan);

        var arrived = new TaskCompletionSource();

        tiles.Invalidated += () => arrived.TrySetResult();
        tiles.RequestTiles(plan.Requests);

        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        presenter.PlaceTiles([]);

        var drawnAt = presenter.EffectiveZoom;
        var before = host.Tiles.Values.First();

        // Zoomed without drawing anything for the new scale: what is on screen is now every one of
        // them a bridge cell.
        presenter.SetZoom(ReportZoomMode.Custom, drawnAt * 2, new ViewPoint(400, 500));
        presenter.PlaceTiles(host.Tiles.Keys.ToList());

        var after = host.Tiles[before.Tile.Key];

        Assert.Equal(before.Bounds.Width * 2, after.Bounds.Width, 3);
        Assert.Equal(before.Bounds.Height * 2, after.Bounds.Height, 3);
    }

    /// <summary>
    ///     Placement walks the pages the planner draws for, not the whole report: a pass otherwise
    ///     costs the length of the report however little of it is on screen, and this runs whenever a
    ///     cell arrives. A page left far behind gives its views up, which is also what the next plan
    ///     does to its cells.
    /// </summary>
    [Fact]
    public async Task PlacingCells_LeavesBehindThePagesTheViewHasScrolledAwayFrom()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        // One band taller than several pages, so there is a page far enough away to be left behind.
        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(4000)) });
        design.Build();

        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);

        using var tiles = new ReportViewTiles(
            await ReportRenderSession.CreateAsync(design), new TaskRunScheduler());

        presenter.SetTiles(tiles);
        presenter.ViewportChanged();

        Assert.True(presenter.PageCount >= 4, $"the report should span several pages, not {presenter.PageCount}");

        var plan = presenter.PlanTiles();
        Assert.NotNull(plan);

        var arrived = new TaskCompletionSource();

        tiles.Invalidated += () => arrived.TrySetResult();
        tiles.RequestTiles(plan.Requests);

        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        presenter.PlaceTiles([]);

        Assert.Contains(host.Tiles.Keys, key => key.PageIndex == 0);

        // To the last page, with the first one now several viewports behind.
        host.ScrollY = Math.Max(0, host.Extent.Y - host.ViewportHeight);
        presenter.Scrolled();

        presenter.PlaceTiles(host.Tiles.Keys.ToList());

        Assert.DoesNotContain(host.Tiles.Keys, key => key.PageIndex == 0);
    }
}
