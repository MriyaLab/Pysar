using Pysar.Viewer.Geometry;

namespace Pysar.Viewer.Tiles;

/// <summary>The cells a viewer wants drawn, and whether the budget had to drop any.</summary>
public sealed record TilePlan(IReadOnlyList<TileRequest> Requests, bool BudgetTrimmed);

/// <summary>
///     Works out which cells of which pages are worth drawing for where the view is now.
/// </summary>
/// <remarks>
///     Cutting a page into cells of a fixed pixel size is what keeps the zoom honest: a single image
///     of the visible area eventually exceeds the largest texture a GPU accepts, and staying under
///     that ceiling with one image means rendering below the display's resolution, which is blur by
///     construction and grows with every zoom step.
/// </remarks>
public sealed class TilePlanner
{
    /// <summary>The side, in pixels, of one grid cell.</summary>
    public const float TileSidePx = 1024;

    /// <summary>
    ///     How many cells a page may cost before whole pages stop being drawn ahead. Two screens'
    ///     worth on a phone, and the point past which drawing a page in advance costs more time than
    ///     the blur it saves is worth.
    /// </summary>
    public const int WholePageCellLimit = 24;

    private (int First, int Last, float Scale, long Down, long Across, double CoverageFactor) _lastRequest;
    private bool _budgetTrimmed;

    public float PagePointWidth { get; set; } = 595.5f;

    public float PagePointHeight { get; set; } = 842f;

    /// <summary>Device pixels per layout unit, which cells are rendered against.</summary>
    public double Density { get; set; } = 1;

    /// <summary>How far past the viewport a cell is drawn, as a fraction of the viewport height.</summary>
    public double VerticalOverdraw { get; set; } = ReportViewDefaults.VerticalOverdraw;

    /// <summary>How much memory, in megabytes, the requested cells may occupy before the furthest are trimmed.</summary>
    public double RenderBudget { get; set; } = ReportViewDefaults.RenderBudget;

    /// <summary>
    /// When &gt; 0, also request a lower-scale coverage set (viewport only, no overdraw)
    /// before full-DPI cells. 0 disables (desktop default).
    /// </summary>
    public double CoverageFactor { get; set; }

    /// <summary>Forgets the last request, so the next one is worked out however similar it is.</summary>
    public void Reset() => _lastRequest = default;

    /// <summary>
    ///     The cells to ask for, or <c>null</c> when the answer cannot have changed since last time.
    /// </summary>
    public TilePlan? Plan(
        PageViewport viewport, double scrollX, double scrollY, double viewportWidth, double viewportHeight)
    {
        var margin = viewportHeight * VerticalOverdraw;
        var windowTop = scrollY - margin;
        var windowBottom = scrollY + viewportHeight + margin;

        // Nothing caps this: a cell is bounded in pixels, so the scale never has to be lowered to fit
        // a texture.
        var fullScale = viewport.RenderScale(Density);
        var pages = viewport.VisiblePages(windowTop, windowBottom);

        if (pages.Count == 0)
            return null;

        var first = Math.Max(0, pages[0] - 1);
        var last = Math.Min(viewport.PageCount - 1, pages[^1] + 1);

        // A whole page is asked for while a page is a handful of cells: then scrolling through it
        // never blurs, because every pixel of it already exists. Deep into a zoom a single page runs
        // to hundreds of cells, so past that point only what is on screen is worth drawing.
        var wholePages = CellsPerPage(fullScale) <= WholePageCellLimit;

        // Whole pages depend only on the pages in play and the scale. Anything narrower depends on
        // where the view sits, and so does a request the budget is trimming: for those the position
        // joins the signature, quantised to half a cell - as often as the answer can change. Both
        // axes, because a page wider than the screen is scrolled sideways too.
        var positionMatters = !wholePages || _budgetTrimmed || CoverageFactor > 0;
        var step = TileSidePx / Math.Max(0.001, Density) / 2;

        var down = positionMatters ? (long)(scrollY / step) : 0;
        var across = positionMatters ? (long)(scrollX / step) : 0;

        var request = (first, last, fullScale, down, across, CoverageFactor);

        if (request == _lastRequest)
            return null;

        _lastRequest = request;

        var fullRequests = BuildRequests(
            viewport, scrollX, viewportWidth,
            fullScale, wholePages, first, last, pages, windowTop, windowBottom);

        if (CoverageFactor <= 0)
        {
            var ordered = OrderByCentre(fullRequests, viewport, scrollX, scrollY, viewportWidth, viewportHeight);
            var trimmed = Trim(ordered);
            _budgetTrimmed = trimmed.Count < fullRequests.Count;

            return new TilePlan(trimmed, _budgetTrimmed);
        }

        var coverageScale = fullScale * (float)CoverageFactor;
        var coveragePages = viewport.VisiblePages(scrollY, scrollY + viewportHeight);
        var coverageRequests = BuildRequests(
            viewport, scrollX, viewportWidth,
            coverageScale, wholePages: false, first, last, coveragePages,
            windowTop: scrollY, windowBottom: scrollY + viewportHeight);

        var fullOrdered = OrderByCentre(fullRequests, viewport, scrollX, scrollY, viewportWidth, viewportHeight);
        var coverageOrdered = OrderByCentre(coverageRequests, viewport, scrollX, scrollY, viewportWidth, viewportHeight);

        var cellBytes = TileSidePx * TileSidePx * 4;
        var allowed = (int)Math.Max(1, RenderBudget * 1024 * 1024 / cellBytes);

        var keptFull = fullOrdered.Take(allowed).ToList();
        var remaining = allowed - keptFull.Count;
        var keptCoverage = coverageOrdered.Take(remaining).ToList();

        var merged = new List<TileRequest>(keptCoverage.Count + keptFull.Count);
        merged.AddRange(keptCoverage);
        merged.AddRange(keptFull);

        _budgetTrimmed = keptFull.Count < fullOrdered.Count || keptCoverage.Count < coverageOrdered.Count;

        return new TilePlan(merged, _budgetTrimmed);
    }

    private List<TileRequest> BuildRequests(
        PageViewport viewport,
        double scrollX,
        double viewportWidth,
        float scale,
        bool wholePages,
        int first,
        int last,
        IReadOnlyList<int> pages,
        double windowTop,
        double windowBottom)
    {
        var requests = new List<TileRequest>();

        if (wholePages)
        {
            var wholePage = new RectPt(0, 0, PagePointWidth, PagePointHeight);

            for (var index = first; index <= last; index++)
                AddCells(requests, index, wholePage, scale);
        }
        else
        {
            var pageLeft = viewport.PageOffsetX(viewportWidth);
            var windowLeft = scrollX - pageLeft;
            var windowRight = windowLeft + viewportWidth;

            foreach (var index in pages)
            {
                var (left, top, right, bottom) =
                    viewport.VisibleRegionPt(index, windowLeft, windowTop, windowRight, windowBottom);

                AddCells(
                    requests, index,
                    new RectPt(left, top, right, bottom), scale);
            }
        }

        return requests;
    }

    /// <summary>How many cells a whole page is cut into at <paramref name="scale"/>.</summary>
    private int CellsPerPage(float scale)
    {
        var cell = TileSidePx / scale;

        return (int)Math.Ceiling(PagePointWidth / cell) * (int)Math.Ceiling(PagePointHeight / cell);
    }

    /// <summary>
    ///     Adds the grid cells that <paramref name="regionPt"/> touches. A cell is a fixed slice of
    ///     the page, so the same cells come back for the same zoom however the view is scrolled, and
    ///     the ones already drawn are reused rather than drawn again.
    /// </summary>
    private void AddCells(List<TileRequest> requests, int pageIndex, RectPt regionPt, float scale)
    {
        if (regionPt.Width <= 0 || regionPt.Height <= 0)
            return;

        var cell = TileSidePx / scale;

        var firstColumn = (int)Math.Floor(regionPt.Left / cell);
        var lastColumn = (int)Math.Floor((regionPt.Right - 0.001f) / cell);
        var firstRow = (int)Math.Floor(regionPt.Top / cell);
        var lastRow = (int)Math.Floor((regionPt.Bottom - 0.001f) / cell);

        for (var column = Math.Max(0, firstColumn); column <= lastColumn; column++)
        for (var row = Math.Max(0, firstRow); row <= lastRow; row++)
        {
            var left = column * cell;
            var top = row * cell;

            // Cut at the page edge, not at the nearest whole pixel of this scale. Rounding here
            // rounds by a different amount at every scale - a coverage pixel is twice a full-DPI
            // one - and the layers then cover slightly different ground: the text visibly resizes
            // as one replaces the other. The page edge is the same for all of them.
            var right = Math.Min(PagePointWidth, left + cell);
            var bottom = Math.Min(PagePointHeight, top + cell);

            if (right <= left || bottom <= top)
                continue;

            requests.Add(new TileRequest(
                new TileKey(pageIndex, column, row, scale),
                new RectPt(left, top, right, bottom)));
        }
    }

    /// <summary>
    ///     Nearest the middle of the viewport first (Manhattan distance in document space).
    /// </summary>
    private static List<TileRequest> OrderByCentre(
        IReadOnlyList<TileRequest> requests, PageViewport viewport,
        double scrollX, double scrollY, double viewportWidth, double viewportHeight)
    {
        var unitsPerPoint = viewport.UnitsPerPoint;
        var offsetX = viewport.PageOffsetX(viewportWidth);

        var centreX = scrollX + viewportWidth / 2;
        var centreY = scrollY + viewportHeight / 2;

        // Distance in both directions: at a zoom deep enough to scroll sideways, ranking by height
        // alone would keep cells far off to one side and drop the ones actually on screen.
        return requests
            .OrderBy(request =>
            {
                var cellX = offsetX + (request.RegionPt.Left + request.RegionPt.Right) / 2 * unitsPerPoint;
                var cellY = viewport.PageTop(request.Key.PageIndex)
                            + (request.RegionPt.Top + request.RegionPt.Bottom) / 2 * unitsPerPoint;

                return Math.Abs(cellX - centreX) + Math.Abs(cellY - centreY);
            })
            .ToList();
    }

    /// <summary>
    ///     Drops the cells furthest from the middle of the viewport once the budget is spent.
    ///     Expects <paramref name="requests"/> already nearest-centre-first.
    /// </summary>
    private List<TileRequest> Trim(IReadOnlyList<TileRequest> requests)
    {
        var cellBytes = TileSidePx * TileSidePx * 4;
        var allowed = (int)Math.Max(1, RenderBudget * 1024 * 1024 / cellBytes);

        if (requests.Count <= allowed)
            return requests is List<TileRequest> list ? list : requests.ToList();

        return requests.Take(allowed).ToList();
    }
}
