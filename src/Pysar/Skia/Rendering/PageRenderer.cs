using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using Pysar.Skia.Pagination;
using SkiaSharp;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Measures the report ribbons, slices the flow into page windows, then paints each page:
///     the repeated template regions (PageHeader/PageFooter) and the window-clipped flow. Bands
///     intersecting a window are drawn in full and trimmed by the clip, so an ancestor's background
///     appears on every page it spans.
///     <para>
///     <see cref="RenderToPdfAsync"/> draws directly onto the PDF page canvas, so text and shapes are
///     stored as vector content (crisp at any zoom). <see cref="RenderAsync"/> rasterizes to bitmaps.
///     </para>
/// </summary>
public static class PageRenderer
{
    public static async Task<IReadOnlyList<SKBitmap>> RenderAsync(
        Report design, float scale, CancellationToken ct, DrawerRegistry? drawers = null,
        MeasurerRegistry? measurers = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        var (layout, slices, resolver) = await PrepareAsync(design, scale, ct, measurers);

        var page = design.PageFormat.GetPageSizePt();
        var pageW = (int)(page.Width * scale);
        var pageH = (int)(page.Height * scale);
        var pages = new List<SKBitmap>(slices.Count);

        // Strictly resolve → draw → next: the returned nodes alias live design elements (see PageBandResolver).
        for (var i = 0; i < slices.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (header, footer) = await resolver.ResolveAsync(i + 1, slices.Count, ct);

            var bitmap = new SKBitmap(pageW, pageH);
            using var canvas = new SKCanvas(bitmap);
            PaintPageSurface(canvas, design);
            DrawPage(canvas, layout, slices[i], scale, drawers, page.Width, header, footer);
            PaintPageBorder(canvas, design, page.Width, page.Height, scale);
            canvas.Flush();
            pages.Add(bitmap);
        }

        return pages;
    }

    /// <summary>
    ///     Renders the report as a vector PDF onto <paramref name="stream"/>. Text and shapes are
    ///     emitted as PDF vector content (not a rasterized page image), so output stays crisp at any
    ///     zoom and files are smaller.
    /// </summary>
    public static async Task RenderToPdfAsync(
        Report design, Stream stream, CancellationToken ct,
        DrawerRegistry? drawers = null, Metadata? metadata = null, MeasurerRegistry? measurers = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(stream);

        // PDF coordinates are in points, so measure and draw at scale 1 (no supersampling needed).
        var (layout, slices, resolver) = await PrepareAsync(design, scale: 1f, ct, measurers);

        var page = design.PageFormat.GetPageSizePt();
        using var document = CreatePdf(stream, metadata);

        // Strictly resolve → draw → next: the returned nodes alias live design elements (see PageBandResolver).
        for (var i = 0; i < slices.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (header, footer) = await resolver.ResolveAsync(i + 1, slices.Count, ct);

            var canvas = document.BeginPage(page.Width, page.Height);
            PaintPageSurface(canvas, design);
            DrawPage(canvas, layout, slices[i], scale: 1f, drawers, page.Width, header, footer);
            PaintPageBorder(canvas, design, page.Width, page.Height, scale: 1f);
            document.EndPage();
        }

        document.Close();
    }

    /// <summary>
    ///     Measures the report, slices it into pages and builds the resolver for the page bands — the
    ///     setup both render paths share.
    ///     <para>
    ///     It exists to keep an invisible coupling in one place: the <i>same</i> <see cref="MeasureContext"/>
    ///     must reach both the measurement and the <see cref="PageBandResolver"/>, so the per-page nodes
    ///     stay geometrically comparable to the reserved ones.
    ///     </para>
    ///     <para>
    ///     The report is seeded as page 1 of 1 because the real page count is only known after pagination
    ///     — which is exactly why the header/footer heights that measurement reserves are never recomputed.
    ///     </para>
    /// </summary>
    internal static async Task<(ReportLayout Layout, IReadOnlyList<PageSlice> Slices, PageBandResolver Resolver)>
        PrepareAsync(Report design, float scale, CancellationToken ct, MeasurerRegistry? measurers = null)
    {
        var measure = new MeasureContext(scale) { Measurers = measurers ?? new MeasurerRegistry() };
        PageBandResolver.Stamp(design, pageNumber: 1, pageCount: 1);
        var layout = await ReportLayoutEngine.MeasureAsync(design, measure, ct);
        var slices = BandPaginator.Paginate(layout.Flow, layout.ContentWindowHeight, layout.RepeatDetailHeaderHeight);

        var imageSources = CollectImageSources(design, layout);
        await ImageRenderer.PrefetchAsync(imageSources, ct).ConfigureAwait(false);

        return (layout, slices, new PageBandResolver(design, layout, measure));
    }

    /// <summary>
    ///     Fills the page canvas from <see cref="Report.BackgroundColor"/> (defaults to white paper).
    ///     Must run before content so bands paint on top of the page surface. Ignores the canvas
    ///     transform — <see cref="SKCanvas.Clear"/> always covers the whole device surface.
    /// </summary>
    internal static void PaintPageSurface(SKCanvas canvas, IReportObject pageChrome)
    {
        var color = pageChrome.BackgroundColor.ToSkiaColor();
        // Transparent still yields white paper — PDF/bitmaps need an opaque base.
        canvas.Clear(color.Alpha == 0 ? SKColors.White : color);
    }

    /// <summary>
    ///     Draws <see cref="Report"/> border chrome after content so the frame stays visible at the
    ///     page edges even when bands bleed full-width.
    /// </summary>
    internal static void PaintPageBorder(
        SKCanvas canvas, IReportObject pageChrome, float pageWidthPt, float pageHeightPt, float scale)
    {
        var rect = new SKRect(0, 0, pageWidthPt * scale, pageHeightPt * scale);
        RenderHelper.DrawBorder(
            canvas,
            pageChrome.BorderColor.ToSkiaColor(),
            pageChrome.BorderThickness,
            pageChrome.BorderLineStyle,
            rect,
            scale);
    }

    /// <summary>
    ///     A band that reaches a physical page edge is extended past it by this much so its fill fully
    ///     covers the last (fractional) edge pixel — the page canvas crops the over-shoot crisply. One
    ///     point exceeds the sub-pixel remainder at any DPI.
    /// </summary>
    private const float EdgeBleed = 1f;

    /// <summary>
    ///     Extra points around a visible region so strokes and AA at the tile edge still paint.
    ///     Shadows that extend farther can still clip; inflate further if that becomes visible.
    /// </summary>
    private const float RegionCullPadPt = 2f;

    /// <summary>Paints one page window onto <paramref name="canvas"/> at the given scale.</summary>
    /// <param name="measureScale">
    ///     The scale <paramref name="layout"/> was measured at, when it is not <paramref name="scale"/>.
    ///     Only a viewer needs this: it measures once and draws the same layout at every zoom.
    /// </param>
    /// <param name="visibleRegionPt">
    ///     Optional page-space rectangle (origin at the page top-left) that will actually be shown.
    ///     When set, bands and elements outside it (plus <see cref="RegionCullPadPt"/>) are skipped.
    ///     Full-page paths (PDF, base layer) leave this null so everything still draws.
    /// </param>
    internal static void DrawPage(
        SKCanvas canvas, ReportLayout layout, PageSlice slice, float scale, DrawerRegistry? drawers,
        float pageWidth, LayoutNode? pageHeader, LayoutNode? pageFooter, float? measureScale = null,
        SKRect? visibleRegionPt = null)
    {
        var ctx = new RenderContext(canvas, scale, measureScale);
        var contentLeft = layout.ContentZone.Left;
        var paddedVisible = InflateRegion(visibleRegionPt, RegionCullPadPt);

        // PageHeader: measured at (0,0) → translate to the top of the content zone.
        if (pageHeader is not null)
        {
            var headerOx = contentLeft;
            var headerOy = layout.ContentZone.Top;
            if (IntersectsVisible(pageHeader.Bounds, headerOx, headerOy, paddedVisible))
            {
                ctx.CullBoundsPt = ToLocalCull(paddedVisible, headerOx, headerOy);
                DrawTranslated(ApplyEdgeBleed(pageHeader, contentLeft, pageWidth),
                    ctx, headerOx, headerOy, scale, drawers);
            }
        }

        // Flow: clip vertically to the sliced height (so a cut that snapped back to a row boundary leaves
        // blank space below instead of bleeding the next row). Horizontally the clip is left unbounded so
        // a full-bleed band with a negative side margin can over-shoot the page edge; the page canvas
        // (MediaBox / bitmap) is the real horizontal crop, and a clean over-shoot covers the last edge
        // pixel fully instead of the anti-aliased hairline a clip at the fractional page width produced.
        var flowTop = layout.ContentZone.Top + layout.PageHeaderHeight;

        // Repeating detail header: on a continuation page (the header is not in this slice) redraw it at
        // the top of the flow window and push the rows down by its height.
        var repeatHeader = layout.RepeatDetailHeader;
        var rowsTop = flowTop;
        if (repeatHeader is not null && slice.Start > repeatHeader.Bounds.Top)
        {
            var repeatOx = contentLeft;
            var repeatOy = flowTop - repeatHeader.Bounds.Top;
            if (IntersectsVisible(repeatHeader.Bounds, repeatOx, repeatOy, paddedVisible))
            {
                ctx.CullBoundsPt = ToLocalCull(paddedVisible, repeatOx, repeatOy);
                DrawTranslated(ApplyEdgeBleed(repeatHeader, contentLeft, pageWidth),
                    ctx, repeatOx, repeatOy, scale, drawers);
            }
            rowsTop = flowTop + layout.RepeatDetailHeaderHeight;
        }

        var sliceHeight = slice.End - slice.Start;
        // A negative top margin on the first flow band is intentional leading overflow (for example,
        // cancelling PageHeader.Margin.Bottom). Allow only that first-page portion above flowTop;
        // continuation pages remain clipped to their own flow window.
        var leadingOverflow = slice.Start == 0f && layout.Flow.Count > 0
            ? Math.Min(0f, layout.Flow[0].Bounds.Top)
            : 0f;
        var clipTop = rowsTop + leadingOverflow;
        var flowOx = contentLeft;
        var flowOy = rowsTop - slice.Start;
        ctx.CullBoundsPt = ToLocalCull(paddedVisible, flowOx, flowOy);
        canvas.Save();
        canvas.ClipRect(new SKRect(
            -pageWidth * scale, clipTop * scale,
            pageWidth * 2 * scale, (rowsTop + sliceHeight) * scale));
        canvas.Translate(flowOx * scale, flowOy * scale);
        foreach (var band in layout.Flow)
        {
            if (band.Bounds.Top >= slice.End || band.Bounds.Bottom <= slice.Start)
                continue;
            // Band-level cull before ApplyEdgeBleed; ElementDrawer also culls children inside the band.
            if (ctx.CullBoundsPt is { } cull
                && (band.Bounds.Right <= cull.Left || band.Bounds.Left >= cull.Right
                    || band.Bounds.Bottom <= cull.Top || band.Bounds.Top >= cull.Bottom))
                continue;
            ElementDrawer.Draw(ApplyEdgeBleed(band, contentLeft, pageWidth), ctx, drawers);
        }
        canvas.Restore();

        // PageFooter: into the bottom region of the content zone.
        // The offset comes from the RESERVED height (layout.PageFooterHeight), not from the freshly
        // measured node — a band whose content grew must not push the flow around.
        if (pageFooter is not null)
        {
            var footerOx = contentLeft;
            var footerOy = layout.ContentZone.Bottom - layout.PageFooterHeight;
            if (IntersectsVisible(pageFooter.Bounds, footerOx, footerOy, paddedVisible))
            {
                ctx.CullBoundsPt = ToLocalCull(paddedVisible, footerOx, footerOy);
                DrawTranslated(ApplyEdgeBleed(pageFooter, contentLeft, pageWidth),
                    ctx, footerOx, footerOy, scale, drawers);
            }
        }
    }

    private static SKRect? InflateRegion(SKRect? region, float pad)
    {
        if (region is not { } r)
            return null;
        return new SKRect(r.Left - pad, r.Top - pad, r.Right + pad, r.Bottom + pad);
    }

    /// <summary>
    ///     Maps a page-space cull rect into the local point space used after
    ///     <c>Translate(originX, originY)</c>, matching <see cref="LayoutNode.Bounds"/>.
    /// </summary>
    private static SKRect? ToLocalCull(SKRect? paddedPageRegion, float originX, float originY)
    {
        if (paddedPageRegion is not { } r)
            return null;
        return new SKRect(r.Left - originX, r.Top - originY, r.Right - originX, r.Bottom - originY);
    }

    private static bool IntersectsVisible(Rect bounds, float originX, float originY, SKRect? paddedPageRegion)
    {
        if (paddedPageRegion is not { } r)
            return true;
        var left = originX + bounds.Left;
        var top = originY + bounds.Top;
        var right = originX + bounds.Right;
        var bottom = originY + bounds.Bottom;
        return left < r.Right && right > r.Left && top < r.Bottom && bottom > r.Top;
    }

    /// <summary>
    ///     Extends any node whose box reaches a physical page edge (left ≤ 0 or right ≥ page width, in
    ///     page coordinates) by <paramref name="bleed"/>, so its fill covers the last edge pixel fully
    ///     instead of leaving an anti-aliased hairline. Applied recursively: a nested full-bleed child
    ///     (negative side margin inside a band) must grow even when the band itself stays in the content
    ///     zone. Cut hints are untouched. Content that stays inside the content zone is returned unchanged.
    /// </summary>
    internal static LayoutNode ApplyEdgeBleed(LayoutNode node, float contentLeft, float pageWidth, float bleed = EdgeBleed)
    {
        var b = node.Bounds;
        var newLeft = contentLeft + b.Left <= 0f ? b.Left - bleed : b.Left;
        var newRight = contentLeft + b.Right >= pageWidth ? b.Right + bleed : b.Right;

        IReadOnlyList<LayoutNode> children = node.Children;
        if (children.Count > 0)
        {
            LayoutNode[]? bledChildren = null;
            for (var i = 0; i < children.Count; i++)
            {
                var bledChild = ApplyEdgeBleed(children[i], contentLeft, pageWidth, bleed);
                if (!ReferenceEquals(bledChild, children[i]))
                {
                    bledChildren ??= children.ToArray();
                    bledChildren[i] = bledChild;
                }
            }

            if (bledChildren is not null)
                children = bledChildren;
        }

        if (newLeft == b.Left && newRight == b.Right && ReferenceEquals(children, node.Children))
            return node;

        return node with { Bounds = new Rect(newLeft, b.Top, newRight, b.Bottom), Children = children };
    }

    private static SKDocument CreatePdf(Stream stream, Metadata? metadata)
    {
        if (metadata is null)
            return SKDocument.CreatePdf(stream);

        return SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            Title = metadata.Title,
            Author = metadata.Author,
            Creation = metadata.CreatedAt
        });
    }

    private static void DrawTranslated(LayoutNode node, RenderContext ctx, float dx, float dy, float scale, DrawerRegistry? drawers)
    {
        ctx.Canvas.Save();
        ctx.Canvas.Translate(dx * scale, dy * scale);
        ElementDrawer.Draw(node, ctx, drawers);
        ctx.Canvas.Restore();
    }

    private static IEnumerable<ImageSource> CollectImageSources(Report design, ReportLayout layout)
    {
        var list = new List<ImageSource>();
        foreach (var band in design.Bands)
            CollectImagesFromElement(band, list);

        if (layout.PageHeader is not null)
            CollectImagesFromNode(layout.PageHeader, list);
        if (layout.PageFooter is not null)
            CollectImagesFromNode(layout.PageFooter, list);
        if (layout.RepeatDetailHeader is not null)
            CollectImagesFromNode(layout.RepeatDetailHeader, list);
        foreach (var node in layout.Flow)
            CollectImagesFromNode(node, list);

        return list;
    }

    private static void CollectImagesFromElement(IReportElement element, List<ImageSource> list)
    {
        if (element is Image { Source: not null } img)
            list.Add(img.Source);

        if (element is IReportContainer { Children.Count: > 0 } container)
        {
            foreach (var child in container.Children)
                CollectImagesFromElement(child, list);
        }
    }

    private static void CollectImagesFromNode(LayoutNode node, List<ImageSource> list)
    {
        if (node.Element is Image { Source: not null } img)
            list.Add(img.Source);

        foreach (var child in node.Children)
            CollectImagesFromNode(child, list);
    }
}
