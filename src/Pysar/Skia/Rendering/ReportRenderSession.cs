using System.Collections.Concurrent;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Pagination;
using SkiaSharp;

namespace Pysar.Skia.Rendering;

/// <summary>
///     A report measured once and ready to be drawn piecewise: any rectangle of any page, at any
///     scale. A viewer uses it to rasterise only what is on screen, so its cost follows the size of
///     the viewport rather than the zoom level.
/// </summary>
/// <remarks>
///     Measurement happens at scale 1, the same as the PDF path, so pagination does not shift as the
///     user zooms - text metrics round differently at different scales, which can move a line break
///     and with it a page break.
/// </remarks>
public sealed class ReportRenderSession
{
    private readonly ReportLayout _layout;
    private readonly IReadOnlyList<PageSlice> _slices;
    private readonly PageBandResolver _resolver;
    private readonly DrawerRegistry? _drawers;
    private readonly Color _pageBackgroundColor;
    private readonly Color _pageBorderColor;
    private readonly Thickness _pageBorderThickness;
    private readonly BorderLineStyle _pageBorderLineStyle;

    // Resolve mutates live design elements and returns nodes that alias them. Serialize Resolve and
    // freeze each page's bands before caching so DrawPage can run concurrently on stable snapshots.
    private readonly SemaphoreSlim _resolveGate = new(1, 1);
    private readonly ConcurrentDictionary<int, (LayoutNode? Header, LayoutNode? Footer)> _bands = new();

    /// <summary>The scale the layout was measured at, shared with the PDF path.</summary>
    private const float MeasureScale = 1f;

    private ReportRenderSession(
        ReportLayout layout,
        IReadOnlyList<PageSlice> slices,
        PageBandResolver resolver,
        DrawerRegistry? drawers,
        (float Width, float Height) pageSizePt,
        Color pageBackgroundColor,
        Color pageBorderColor,
        Thickness pageBorderThickness,
        BorderLineStyle pageBorderLineStyle)
    {
        _layout = layout;
        _slices = slices;
        _resolver = resolver;
        _drawers = drawers;
        PageSizePt = pageSizePt;
        _pageBackgroundColor = pageBackgroundColor;
        _pageBorderColor = pageBorderColor;
        _pageBorderThickness = pageBorderThickness;
        _pageBorderLineStyle = pageBorderLineStyle;
    }

    /// <summary>How many pages the report paginated into.</summary>
    public int PageCount => _slices.Count;

    /// <summary>The page size in points, which region coordinates are expressed in.</summary>
    public (float Width, float Height) PageSizePt { get; }

    /// <summary>Measures <paramref name="report"/> and prepares it for piecewise drawing.</summary>
    public static async Task<ReportRenderSession> CreateAsync(
        Report report, DrawerRegistry? drawers = null, CancellationToken ct = default,
        MeasurerRegistry? measurers = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var (layout, slices, resolver) = await PageRenderer.PrepareAsync(report, MeasureScale, ct, measurers);

        return new ReportRenderSession(
            layout,
            slices,
            resolver,
            drawers,
            report.PageFormat.GetPageSizePt(),
            report.BackgroundColor,
            report.BorderColor,
            report.BorderThickness,
            report.BorderLineStyle);
    }

    /// <summary>
    ///     Draws <paramref name="regionPt"/> of page <paramref name="pageIndex"/> - both in page
    ///     points, origin at the page's top left - into a bitmap of the region times
    ///     <paramref name="scale"/>.
    /// </summary>
    public async Task<SKBitmap> RenderRegionAsync(
        int pageIndex, SKRect regionPt, float scale, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        if (regionPt.Width <= 0 || regionPt.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(regionPt), regionPt, "The region is empty.");

        var (header, footer) = await GetBandsAsync(pageIndex, ct);

        var width = Math.Max(1, (int)MathF.Round(regionPt.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(regionPt.Height * scale));

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var chrome = new PageChrome(
            _pageBackgroundColor, _pageBorderColor, _pageBorderThickness, _pageBorderLineStyle);
        // Clear ignores the CTM, so fill the tile first; then translate so border/content use page space.
        PageRenderer.PaintPageSurface(canvas, chrome);
        canvas.Translate(-regionPt.Left * scale, -regionPt.Top * scale);

        // The layout was measured once, at scale 1; line breaking must follow that measurement
        // rather than the zoom, or the text itself would change as the user zooms.
        // visibleRegionPt lets DrawPage skip bands/elements outside this tile (cost ∝ cell, not page).
        PageRenderer.DrawPage(
            canvas, _layout, _slices[pageIndex], scale, _drawers, PageSizePt.Width, header, footer,
            measureScale: MeasureScale, visibleRegionPt: regionPt);

        PageRenderer.PaintPageBorder(canvas, chrome, PageSizePt.Width, PageSizePt.Height, scale);

        canvas.Flush();

        return bitmap;
    }

    /// <summary>Frozen page-level chrome captured at session create (report is not kept alive).</summary>
    private sealed class PageChrome(
        Color backgroundColor,
        Color borderColor,
        Thickness borderThickness,
        BorderLineStyle borderLineStyle) : Core.Abstractions.IReportObject
    {
        public object? DataContext { get; set; }
        public Color BackgroundColor { get; set; } = backgroundColor;
        public BorderLineStyle BorderLineStyle { get; set; } = borderLineStyle;
        public Thickness BorderThickness { get; set; } = borderThickness;
        public Color BorderColor { get; set; } = borderColor;
        public Thickness Padding { get; set; }
        public Thickness Margin { get; set; }
    }

    private async Task<(LayoutNode? Header, LayoutNode? Footer)> GetBandsAsync(
        int pageIndex, CancellationToken ct)
    {
        if (_bands.TryGetValue(pageIndex, out var hit))
            return hit;

        await _resolveGate.WaitAsync(ct);
        try
        {
            if (_bands.TryGetValue(pageIndex, out hit))
                return hit;

            var (header, footer) = await _resolver.ResolveAsync(pageIndex + 1, PageCount, ct);
            // Freeze so a later Resolve for another page cannot rewrite content on these nodes.
            var frozen = (Freeze(header), Freeze(footer));
            _bands[pageIndex] = frozen;
            return frozen;
        }
        finally
        {
            _resolveGate.Release();
        }
    }

    /// <summary>
    ///     Deep-copies a measured band tree so draw can keep reading resolved property values after
    ///     the live design is stamped for a different page.
    /// </summary>
    private static LayoutNode? Freeze(LayoutNode? node)
    {
        if (node is null)
            return null;

        var element = node.Element.Clone();
        var children = node.Children.Count == 0
            ? LayoutNode.NoChildren
            : node.Children.Select(static child => Freeze(child)!).ToArray();

        return new LayoutNode(element, node.Bounds, children, node.CutHints);
    }
}
