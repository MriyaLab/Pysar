namespace Pysar.Viewer.Geometry;

/// <summary>The empty space kept around the pages, in the units the document scrolls in.</summary>
/// <remarks>
///     Deliberately not a UI thickness type: this assembly stays free of them, and the control maps
///     its own onto this one.
/// </remarks>
public readonly record struct PagePadding(double Left, double Top, double Right, double Bottom)
{
    public PagePadding(double uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    ///     The same padding on every side. Written as a conversion so a host whose markup has no type
    ///     converter of its own - Razor, which compiles an attribute of a non-string parameter as a C#
    ///     expression - can still be given a plain number, as the XAML hosts are.
    /// </summary>
    public static implicit operator PagePadding(double uniform) => new(uniform);

    /// <summary>The same padding at <paramref name="zoom"/>, since it belongs to the document.</summary>
    public PagePadding Scaled(double zoom)
        => new(Left * zoom, Top * zoom, Right * zoom, Bottom * zoom);
}

/// <summary>
///     Where the pages of a report sit in a scrolling document, and which part of which page a
///     window onto that document shows.
/// </summary>
/// <remarks>
///     Plain arithmetic over the drawn page size, deliberately free of any UI type: a viewer built
///     on it can be tested here, where the MAUI control that uses it cannot.
/// </remarks>
/// <param name="PageCount">How many pages the report has.</param>
/// <param name="PageWidth">The drawn width of a page, in the units the document scrolls in.</param>
/// <param name="PageHeight">The drawn height of a page, in the same units.</param>
/// <param name="PageSpacing">The gap between two pages.</param>
/// <param name="PagePointWidth">The page width in points, which region coordinates use.</param>
/// <param name="Padding">The space kept around the pages, outside the gaps between them.</param>
public readonly record struct PageViewport(
    int PageCount,
    double PageWidth,
    double PageHeight,
    double PageSpacing,
    double PagePointWidth,
    PagePadding Padding = default)
{
    /// <summary>Units per point at the current page size.</summary>
    public double UnitsPerPoint => PagePointWidth > 0 ? PageWidth / PagePointWidth : 1;

    /// <summary>
    ///     The scale a cell of a page is drawn at: one rendered pixel per device pixel, at whatever the
    ///     zoom asks for.
    /// </summary>
    /// <remarks>
    ///     One expression, in one place, because two of them decide whether a drawn cell belongs to the
    ///     zoom on screen - the planner names the scale in a cell's key, and the presenter looks cells up
    ///     by it. Worked out separately on each side, the two could round differently and never match, so
    ///     the comparison had to be loose enough to hide that - and loose enough hid a real difference in
    ///     zoom as well.
    /// </remarks>
    public float RenderScale(double density) => (float)(UnitsPerPoint * density);

    public double DocumentWidth => Padding.Left + PageWidth + Padding.Right;

    public double DocumentHeight => PageCount <= 0
        ? 0
        : Padding.Top + PageCount * PageHeight + (PageCount - 1) * PageSpacing + Padding.Bottom;

    /// <summary>The left edge of every page in document coordinates.</summary>
    public double PageLeft => Padding.Left;

    /// <summary>The top of a page in document coordinates.</summary>
    public double PageTop(int index) => Padding.Top + index * (PageHeight + PageSpacing);

    /// <summary>
    ///     The left edge of every page once a document narrower than the viewport has been pushed
    ///     right to sit in the middle of it, as every PDF viewer shows it.
    /// </summary>
    public double PageOffsetX(double viewportWidth)
        => PageLeft + Math.Max(0, (viewportWidth - DocumentWidth) / 2);

    /// <summary>The point of the report at a position of the document.</summary>
    public DocumentPoint PointAt(double documentX, double documentY, double viewportWidth)
    {
        var unitsPerPoint = UnitsPerPoint;
        var step = PageHeight + PageSpacing;

        // Clamped to a real page, so a position in the gap between two pages - or above the first, or
        // below the last - still names a point, one just outside the page nearest to it.
        var index = step > 0 ? (int)Math.Floor((documentY - Padding.Top) / step) : 0;
        index = Math.Clamp(index, 0, Math.Max(0, PageCount - 1));

        return new DocumentPoint(
            index,
            (documentX - PageOffsetX(viewportWidth)) / unitsPerPoint,
            (documentY - PageTop(index)) / unitsPerPoint);
    }

    /// <summary>Where a point of the report sits in document coordinates.</summary>
    public (double X, double Y) PositionOf(DocumentPoint point, double viewportWidth)
    {
        var unitsPerPoint = UnitsPerPoint;

        return (PageOffsetX(viewportWidth) + point.XPt * unitsPerPoint,
            PageTop(point.PageIndex) + point.YPt * unitsPerPoint);
    }

    /// <summary>The pages a vertical window overlaps, in order.</summary>
    public IReadOnlyList<int> VisiblePages(double windowTop, double windowBottom)
    {
        var pages = new List<int>();

        for (var index = 0; index < PageCount; index++)
        {
            var top = PageTop(index);
            var bottom = top + PageHeight;

            if (bottom > windowTop && top < windowBottom)
                pages.Add(index);
        }

        return pages;
    }

    /// <summary>
    ///     The part of a page a window shows, in page points, clipped to the page. The window is in
    ///     document coordinates.
    /// </summary>
    public (double Left, double Top, double Right, double Bottom) VisibleRegionPt(
        int index, double windowLeft, double windowTop, double windowRight, double windowBottom)
    {
        var pageTop = PageTop(index);

        var left = Math.Clamp(windowLeft, 0, PageWidth);
        var right = Math.Clamp(windowRight, 0, PageWidth);
        var top = Math.Clamp(windowTop - pageTop, 0, PageHeight);
        var bottom = Math.Clamp(windowBottom - pageTop, 0, PageHeight);

        var unitsPerPoint = UnitsPerPoint;

        return (left / unitsPerPoint, top / unitsPerPoint, right / unitsPerPoint, bottom / unitsPerPoint);
    }

    /// <summary>The zoom at which a page fills <paramref name="viewportWidth"/>.</summary>
    public static double FitWidthZoom(double viewportWidth, double pageWidthAt100)
        => pageWidthAt100 > 0 ? viewportWidth / pageWidthAt100 : 1;

    /// <summary>The zoom at which a whole page fits the viewport, both axes.</summary>
    public static double FitPageZoom(
        double viewportWidth, double viewportHeight, double pageWidthAt100, double pageHeightAt100)
    {
        if (pageWidthAt100 <= 0 || pageHeightAt100 <= 0)
            return 1;

        return Math.Min(viewportWidth / pageWidthAt100, viewportHeight / pageHeightAt100);
    }
}
