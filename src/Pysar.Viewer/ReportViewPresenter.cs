using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;

namespace Pysar.Viewer;

/// <summary>
///     The viewer's state, and the arithmetic over it. It asks the host where the view is, works out
///     what should be on screen, and tells the host to put it there.
/// </summary>
public sealed class ReportViewPresenter
{
    private readonly IReportViewHost _host;
    private readonly ZoomModel _zoom = new();
    private readonly TilePlanner _planner = new();

    private ReportViewTiles? _tiles;

    /// <summary>Set while the scroll position is being published, so it is not scrolled back to.</summary>
    private bool _reportingScrollPosition;

    /// <summary>
    ///     Set while <see cref="Update"/> is running, so a scroll the host raises from inside
    ///     <see cref="IReportViewHost.SetExtent"/> - clamping an offset that no longer fits a shorter
    ///     document - is not mistaken for a reader scroll and does not re-enter the update or clear the
    ///     zoom's held point.
    /// </summary>
    private bool _updating;

    /// <summary>
    ///     The zoom the pages were last placed at, which is the zoom the scroll position belongs to.
    ///     Read when a point has to be held through a change of zoom, since by then the model's own
    ///     zoom is already the new one while nothing has been laid out for it.
    /// </summary>
    private double _placedZoom;

    /// <summary>
    ///     Where the view has been told to scroll to and has not reported reaching yet, or <c>null</c>
    ///     when its own position is the current one.
    /// </summary>
    /// <remarks>
    ///     A host may still apply <see cref="IReportViewHost.ScrollTo"/> asynchronously - MAUI's
    ///     <c>ScrollToAsync</c> does - so its reported position can belong to the zoom before the change
    ///     for a frame or more. Naming a point of the document against that reported position while
    ///     <see cref="_placedZoom"/> already describes the new one puts the point out by the scroll
    ///     still owed, and a zoom begun in that window holds the wrong point.
    /// </remarks>
    private ViewPoint? _pendingScroll;

    public ReportViewPresenter(IReportViewHost host)
    {
        _host = host;
        Gestures = new GestureModel(_zoom);
    }

    public GestureModel Gestures { get; }

    /// <summary>
    ///     Layout units per point at a zoom of 1. A host whose platform silently rescales its whole
    ///     interface (Mac Catalyst on the iPad idiom) corrects for that here rather than losing the
    ///     factor - there is nowhere else in this type for a host to hand it in.
    /// </summary>
    public double UnitsPerPoint
    {
        get => _zoom.UnitsPerPoint;
        set => _zoom.UnitsPerPoint = value;
    }

    /// <summary>The zoom mode a gesture has set, for a host to mirror onto its own bindable state.</summary>
    public ReportZoomMode ZoomMode => _zoom.Mode;

    /// <summary>
    ///     The factor a gesture has set, used when <see cref="ZoomMode"/> is
    ///     <see cref="Viewer.Zoom.ReportZoomMode.Custom"/>. Exposed so a host can read back what
    ///     <see cref="Gestures"/> just did without repeating its arithmetic.
    /// </summary>
    public double Zoom => _zoom.Zoom;

    /// <summary>The space kept around the pages, at a zoom of 1.</summary>
    /// <remarks>
    ///     Part of the document, so it grows and shrinks with the zoom, which is what a PDF viewer
    ///     does - measured in Chrome's, the gap between two pages tracks the zoom exactly. Keeping it
    ///     fixed instead makes the document at one zoom no longer the document at another times a
    ///     factor, and then nothing can both scale the pages and hold a point of them still.
    /// </remarks>
    public PagePadding Padding { get; set; }

    /// <summary>The gap between two pages, at a zoom of 1. Scales with the zoom, like <see cref="Padding"/>.</summary>
    public double PageSpacing { get; set; } = ReportViewDefaults.PageSpacing;

    public double PageBorderThickness { get; set; } = 1;

    public double VerticalOverdraw
    {
        get => _planner.VerticalOverdraw;
        set => _planner.VerticalOverdraw = value;
    }

    public double RenderBudget
    {
        get => _planner.RenderBudget;
        set => _planner.RenderBudget = value;
    }

    public double CoverageFactor
    {
        get => _planner.CoverageFactor;
        set => _planner.CoverageFactor = value;
    }

    public int PageCount { get; private set; }

    public int CurrentPage { get; private set; } = 1;

    public double EffectiveZoom => _zoom.EffectiveZoom;

    /// <summary>Raised when the current page or the effective zoom has changed.</summary>
    public event Action? StateChanged;

    /// <summary>The document's shape, without the means to draw it. Used by tests.</summary>
    public void SetDocument(int pageCount, float pagePointWidth, float pagePointHeight)
    {
        PageCount = pageCount;

        _zoom.PagePointWidth = pagePointWidth;
        _zoom.PagePointHeight = pagePointHeight;

        _planner.PagePointWidth = pagePointWidth;
        _planner.PagePointHeight = pagePointHeight;
        _planner.Reset();
    }

    /// <summary>The cache that draws the pages, or <c>null</c> when there is no report.</summary>
    public void SetTiles(ReportViewTiles? tiles)
    {
        _tiles = tiles;

        if (tiles is null)
        {
            PageCount = 0;
            _planner.Reset();

            return;
        }

        var (width, height) = tiles.PageSizePt;

        SetDocument(tiles.PageCount, width, height);
    }

    /// <summary>The view has moved or changed size.</summary>
    /// <remarks>
    ///     Nothing is held through this one. A resize changes a fitted zoom as well, and holding a
    ///     point through that drags the document away from wherever it was: at startup the viewport is
    ///     measured several times before it settles, and each measurement would push the view further
    ///     down.
    /// </remarks>
    public void ViewportChanged() => Update();

    /// <summary>The view has been scrolled.</summary>
    public void Scrolled()
    {
        // A shrink of the extent clamps the offset and raises this from inside Update. That clamp is
        // not a reader scroll: swallowing it keeps the zoom's held point and avoids a nested update
        // that would place the pages twice and drop the scroll the outer call is about to issue.
        if (_updating)
            return;

        // Wherever the view is now is where it is: a scroll this presenter asked for has either landed
        // or been overtaken by the reader, and either way it is no longer what the position should be
        // measured against.
        _pendingScroll = null;

        Update();
    }

    /// <summary>
    ///     The point of the report under a point of the viewport, or <c>null</c> when there is no
    ///     document under it. A gesture resolves the point it holds once, when it begins, and hands it
    ///     back to <see cref="PreviewZoom"/> and <see cref="SetZoom"/> for as long as it runs.
    /// </summary>
    /// <remarks>
    ///     Resolving it per frame instead reads the scroll position afresh every time, and anything that
    ///     moves the view during the gesture - a pan alongside the pinch, the platform's own momentum -
    ///     then names a different point and the drawing drifts under the fingers.
    /// </remarks>
    public DocumentPoint? PointAt(ViewPoint viewportPoint)
    {
        if (PageCount == 0 || _placedZoom <= 0)
            return null;

        return Held(Viewport(_placedZoom), viewportPoint);
    }

    /// <summary>A zoom the reader asked for, holding <paramref name="anchor"/> still.</summary>
    /// <param name="held">
    ///     The point to bring back under <paramref name="anchor"/>, for a caller that resolved it
    ///     earlier - a gesture, which must hold the point it started on. Left out, the point under the
    ///     anchor now is the one held, which is what a wheel notch or a menu wants.
    /// </param>
    public void SetZoom(ReportZoomMode mode, double zoom, ViewPoint anchor, DocumentPoint? held = null)
    {
        // Resolved before the zoom changes, and against the zoom the scroll position belongs to rather
        // than the model's own - a gesture has already put its new factor there by the time it calls
        // this, while nothing has been laid out for it yet.
        var hold = held is { } point ? (point, anchor) : Hold(anchor);

        _zoom.Zoom = zoom;
        _zoom.Mode = mode;

        Update(hold);
    }

    /// <summary>
    ///     How to show a zoom the reader is still performing, by scaling what is already drawn instead
    ///     of laying anything out for it: the scale to draw at, the translation to draw it with, and the
    ///     zoom it amounts to. <c>null</c> when there is nothing drawn to scale.
    /// </summary>
    /// <param name="factor">How far the gesture has scaled, against the zoom it started at.</param>
    /// <param name="anchor">The point of the viewport the gesture holds still.</param>
    /// <remarks>
    ///     Worked out here rather than by the host so that what the gesture shows and what ends it
    ///     cannot disagree: both go through the same arithmetic, over the space around the pages, the
    ///     centring of a document narrower than the viewport, and the limits of the scroll. A host that
    ///     scaled about the reader's fingers by itself is right about the point it holds and wrong about
    ///     everything the relayout will do differently, and the release then jumps.
    ///     <para>
    ///     The translation is expressed for a transform applied at the drawing's own origin, with the
    ///     scroll position left where the gesture found it - the gesture must not move it, since the
    ///     platform would clamp it against an extent that still describes the zoom before the gesture.
    ///     </para>
    /// </remarks>
    public ZoomPreview? PreviewZoom(double factor, ViewPoint anchor, DocumentPoint? held = null)
    {
        if (PageCount == 0 || _placedZoom <= 0 || factor <= 0)
            return null;

        var shown = Viewport(_placedZoom);
        var point = held ?? Held(shown, anchor);

        var zoom = Math.Clamp(_placedZoom * factor, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom);
        var target = Viewport(zoom);

        var scale = zoom / _placedZoom;
        var scroll = ScrollFor(target, point, anchor);

        // Exact for every page, not only the one under the fingers: the space around the pages and the
        // gaps between them scale with the zoom too, so the document at the target zoom is the document
        // on screen times the scale. All that is left is where the two put the document's own left
        // edge - which differs when a document narrower than the viewport is centred - and the scroll.
        var shownX = shown.PageOffsetX(_host.ViewportWidth);
        var targetX = target.PageOffsetX(_host.ViewportWidth);

        return new ZoomPreview(
            zoom,
            scale,
            targetX - scale * shownX - scroll.X + _host.ScrollX,
            -scroll.Y + _host.ScrollY);
    }

    /// <summary>Scrolls so that <paramref name="page"/> is at the top, one-based.</summary>
    public void GoToPage(int page)
    {
        if (_reportingScrollPosition || PageCount == 0)
            return;

        var viewport = Viewport();

        // Less the padding, so the space kept above a page is shown above it rather than scrolled
        // past - which for the first page is the difference between starting at the top and starting
        // with its top edge already against the frame.
        var top = viewport.PageTop(Math.Clamp(page - 1, 0, viewport.PageCount - 1)) - viewport.Padding.Top;

        if (Math.Abs(_host.ScrollY - top) < 1)
            return;

        _host.ScrollTo(_host.ScrollX, top);
    }

    /// <summary>The cells worth drawing now, or <c>null</c> when the answer cannot have changed.</summary>
    public TilePlan? PlanTiles()
    {
        if (PageCount == 0)
            return null;

        _planner.Density = _host.Density;

        return _planner.Plan(
            Viewport(), _host.ScrollX, _host.ScrollY, _host.ViewportWidth, _host.ViewportHeight);
    }

    /// <summary>Places the cells the cache has drawn, and removes the ones no longer wanted.</summary>
    /// <remarks>
    ///     After a zoom, cells of the previous scale are placed first (stretched into the new page
    ///     geometry) and current-scale cells on top. That keeps the release from looking softer than
    ///     the pinch preview, which had been scaling those same sharp pixels.
    /// </remarks>
    public void PlaceTiles(IReadOnlyCollection<TileKey> placed)
    {
        if (_tiles is null)
            return;

        var viewport = Viewport();
        var offsetX = PageOffsetX(viewport);
        var unitsPerPoint = viewport.UnitsPerPoint;
        var scale = viewport.RenderScale(_host.Density);
        var tolerance = Math.Abs(scale) * 1e-4f;

        var wanted = new HashSet<TileKey>();

        for (var index = 0; index < viewport.PageCount; index++)
        {
            foreach (var tile in _tiles.BridgeTilesFor(index, scale))
            {
                wanted.Add(tile.Key);
                Place(tile);
            }

            foreach (var tile in _tiles.TilesFor(index, scale))
            {
                wanted.Add(tile.Key);
                Place(tile);
            }
        }

        foreach (var key in placed)
            if (!wanted.Contains(key))
                _host.RemoveTile(key);

        // Snapped to the device grid: a cell placed at a fraction of a device pixel is resampled by
        // the platform, so its text goes soft - and softens differently after every layout, because
        // the fraction moves with the page offset. Only the page offset and the page top are ever
        // fractional here; a cell's own edge is a whole number of pixels into the page by
        // construction (TilePlanner cuts on TileSidePx), so every cell of a page moves by the same
        // snap and no seam opens between neighbours.
        // Every cell is sized by the region it stands for, whatever scale it was drawn at, so all
        // the layers cover exactly the same ground and none of them resizes the text as it replaces
        // another. A whole cell needs no help from that: its region is TileSidePx across by
        // construction, so its size in device pixels is already its pixel count. Only a cell cut
        // short by the page edge is off, and by under a pixel, at the margin.
        void Place(Tile tile) => _host.PlaceTile(new ViewRect(
            Snap(offsetX + tile.RegionPt.Left * unitsPerPoint),
            Snap(viewport.PageTop(tile.Key.PageIndex) + tile.RegionPt.Top * unitsPerPoint),
            tile.RegionPt.Width * unitsPerPoint,
            tile.RegionPt.Height * unitsPerPoint), tile);
    }

    /// <summary>
    ///     The point of the report under a point of the viewport, and where it should stay, or
    ///     <c>null</c> when there is nothing to hold.
    /// </summary>
    private (DocumentPoint Point, ViewPoint At)? Hold(ViewPoint anchor)
    {
        if (PageCount == 0 || _placedZoom <= 0)
            return null;

        return (Held(Viewport(_placedZoom), anchor), anchor);
    }

    /// <summary>The point of the report under a point of the viewport, as <paramref name="shown"/> has it.</summary>
    private DocumentPoint Held(PageViewport shown, ViewPoint anchor)
    {
        var scroll = _pendingScroll ?? new ViewPoint(_host.ScrollX, _host.ScrollY);

        return shown.PointAt(scroll.X + anchor.X, scroll.Y + anchor.Y, _host.ViewportWidth);
    }

    /// <summary>
    ///     Where the view has to be scrolled for <paramref name="held"/> to appear at
    ///     <paramref name="at"/>, as far as the document allows.
    /// </summary>
    private ViewPoint ScrollFor(PageViewport viewport, DocumentPoint held, ViewPoint at)
    {
        var (x, y) = viewport.PositionOf(held, _host.ViewportWidth);

        // Clamped here rather than left to the platform, so that a gesture being shown and the relayout
        // that ends it are held to the same limits and cannot disagree about where the document stops.
        return new ViewPoint(
            Clamp(x - at.X, Math.Max(viewport.DocumentWidth, _host.ViewportWidth) - _host.ViewportWidth),
            Clamp(y - at.Y, Math.Max(viewport.DocumentHeight, 1) - _host.ViewportHeight));

        static double Clamp(double value, double limit) => Math.Clamp(value, 0, Math.Max(0, limit));
    }

    private void Update((DocumentPoint Point, ViewPoint At)? held = null)
    {
        _updating = true;

        try
        {
            // The zoom model learns the viewport before anything is measured against it. Reading the
            // geometry first left the extent one update behind: the pages were laid out at the new zoom
            // while the scrollable area still described the old one, so at startup there was nothing to
            // scroll until some later change happened to bring the two together.
            _zoom.ViewportWidth = _host.ViewportWidth;
            _zoom.ViewportHeight = _host.ViewportHeight;
            _zoom.Padding = Padding;

            var viewport = Viewport();

            // The extent first: a scroll is clamped to whatever the document's size currently is. The
            // host must make that size effective for ScrollTo before returning - see IReportViewHost -
            // so the scroll is not clamped against the size before this update. Both axes are at least
            // the viewport: a short document would otherwise leave a canvas smaller than the view, and
            // hosts that centre undersized content (Avalonia's ScrollViewer) put the pages in the
            // middle while hosts that top-align them (MAUI) did not. Filling the viewport keeps pages
            // at the top on every platform, with empty space below - matching a PDF viewer's layout.
            _host.SetExtent(
                Math.Max(viewport.DocumentWidth, _host.ViewportWidth),
                Math.Max(viewport.DocumentHeight, _host.ViewportHeight));

            // Without a document there is nothing to hold or scroll to: a zoom set before a report is
            // loaded must not issue a scroll command against a document that does not exist yet.
            if (PageCount > 0 && held is { } hold)
            {
                var scroll = ScrollFor(viewport, hold.Point, hold.At);

                // Remembered until the view reports having moved: what a point of the document is measured
                // against is where the view is going, not where it still is. Issued here rather than
                // through Post so the pages placed below and the scroll that belongs to them share one
                // frame: a posted scroll left the host painting the new layout under the old offset for
                // a turn, which jumped the first and last pages after a pinch released.
                _pendingScroll = scroll;
                _host.ScrollTo(scroll.X, scroll.Y);
            }

            PlacePages();
            UpdateCurrentPage();

            _placedZoom = _zoom.EffectiveZoom;

            StateChanged?.Invoke();
        }
        finally
        {
            _updating = false;
        }
    }

    private void PlacePages()
    {
        var viewport = Viewport();
        var offsetX = PageOffsetX(viewport);

        // Grown by the border's width: the cells are laid over the page exactly, so a line drawn on
        // the page's own edge would be painted over by the first sharp cell to arrive.
        var border = PageBorderThickness;

        // Deliberately not snapped, where the cells are: a page frame is white fill and a border, and
        // rounding it would make the drawn page a fraction wider or shorter than the zoom says it is
        // - which is the scale everything anchored to a page is measured against.
        for (var index = 0; index < viewport.PageCount; index++)
            _host.PlacePage(index, new ViewRect(
                offsetX - border,
                viewport.PageTop(index) - border,
                viewport.PageWidth + 2 * border,
                viewport.PageHeight + 2 * border));
    }

    private void UpdateCurrentPage()
    {
        var viewport = Viewport();
        var step = viewport.PageHeight + viewport.PageSpacing;

        if (viewport.PageCount == 0 || step <= 0)
            return;

        // Measured from the first page rather than from the document, so the padding above it does
        // not count as part of the first page's height.
        var page = (int)Math.Floor((_host.ScrollY - viewport.PageTop(0) + step / 2) / step) + 1;
        page = Math.Clamp(page, 1, viewport.PageCount);

        if (page == CurrentPage)
            return;

        // Reporting where the scroll landed must not be mistaken for a request to go there: that
        // would scroll to the top of that page mid-gesture, which is felt as the view tugging itself
        // into place.
        _reportingScrollPosition = true;
        try
        {
            CurrentPage = page;
        }
        finally
        {
            _reportingScrollPosition = false;
        }
    }

    private double PageOffsetX(PageViewport viewport) => viewport.PageOffsetX(_host.ViewportWidth);

    /// <summary>Device pixels per layout unit, never zero, so a snap is always defined.</summary>
    private double DeviceDensity => Math.Max(0.001, _host.Density);

    /// <summary>
    ///     <paramref name="value"/> moved to the nearest whole device pixel. Only what is drawn is
    ///     snapped: the zoom and the anchor arithmetic stay exact, so a gesture still holds the point
    ///     it was given rather than the pixel that point rounded to.
    /// </summary>
    private double Snap(double value)
    {
        var density = DeviceDensity;

        return Math.Round(value * density, MidpointRounding.AwayFromZero) / density;
    }

    public float ViewportRenderScaleForPerf() => (float)Viewport().RenderScale(_host.Density);

    private PageViewport Viewport() => Viewport(_zoom.EffectiveZoom);

    /// <summary>
    ///     Where the pages sit at <paramref name="zoom"/>. The space around them and the gaps between
    ///     them are scaled with it, so the document at one zoom is the document at any other times a
    ///     factor - which is what lets a gesture be shown by scaling the drawing whole, and a point of
    ///     a page be held still through a change of zoom.
    /// </summary>
    private PageViewport Viewport(double zoom)
    {
        return new PageViewport(
            PageCount: PageCount,
            PageWidth: _zoom.PagePointWidth * _zoom.UnitsPerPoint * zoom,
            PageHeight: _zoom.PagePointHeight * _zoom.UnitsPerPoint * zoom,
            PageSpacing: PageSpacing * zoom,
            PagePointWidth: _zoom.PagePointWidth,
            Padding: Padding.Scaled(zoom));
    }
}
