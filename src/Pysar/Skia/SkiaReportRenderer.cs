using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;

namespace Pysar.Skia;

/// <summary>
///     Renders a <see cref="Report"/> to page bitmaps using the two-phase band pipeline
///     (measure → paginate → draw). Custom element types are supported by registering an
///     <see cref="IElementDrawer"/> via <see cref="WithDrawer{T}"/>.
///     <para>
///     A built report may be passed to these methods more than once, sequentially. Concurrent renders
///     of the same instance are not supported: page bands alias live elements, and
///     <see cref="Report.PageNumber"/> / <see cref="Report.PageCount"/> are scratch for the pass.
///     </para>
/// </summary>
public sealed class SkiaReportRenderer
{
    private readonly DrawerRegistry _drawers = DrawerRegistry.CreateDefault();

    private readonly MeasurerRegistry _measurers = new();

    /// <summary>Registers a custom drawer for element type <typeparamref name="T"/>.</summary>
    public SkiaReportRenderer WithDrawer<T>(IElementDrawer drawer) where T : IReportElement
    {
        _drawers.Register<T>(drawer);
        return this;
    }

    /// <summary>
    ///     Registers how element type <typeparamref name="T"/> measures its own content, so an
    ///     <c>Auto</c> width or height on it resolves to that instead of to zero.
    /// </summary>
    public SkiaReportRenderer WithMeasurer<T>(IElementMeasurer measurer) where T : IReportElement
    {
        _measurers.Register<T>(measurer);
        return this;
    }

    /// <summary>
    ///     Measures <paramref name="reportDesign"/> once and returns a session that can draw any part
    ///     of any page at any scale - what an on-screen viewer needs to stay sharp while it zooms.
    ///     Custom drawers registered with <see cref="WithDrawer{T}"/> apply inside it.
    /// </summary>
    public Task<ReportRenderSession> CreateSessionAsync(Report reportDesign, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);

        return ReportRenderSession.CreateAsync(reportDesign, _drawers, ct, _measurers);
    }

    public async Task<IEnumerable<SKBitmap>> RenderPageAsync(
        Report reportDesign, float scale = 1f, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        return await PageRenderer.RenderAsync(reportDesign, scale, ct, _drawers, _measurers);
    }

    /// <summary>Renders the report as a vector PDF onto <paramref name="stream"/> (crisp at any zoom).</summary>
    public Task RenderToPdfAsync(Report reportDesign, Stream stream, CancellationToken ct = default)
        => RenderToPdfAsync(reportDesign, stream, new PdfExportOptions(), ct);

    /// <summary>
    ///     Renders the report as a vector PDF onto <paramref name="stream"/>.
    ///     <see cref="PdfExportOptions.PdfA"/> asks Skia for PDF/A-2b document setup, not a validation.
    /// </summary>
    public Task RenderToPdfAsync(
        Report reportDesign, Stream stream, PdfExportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return PageRenderer.RenderToPdfAsync(
            reportDesign, stream, ct, _drawers, reportDesign.Metadata, _measurers, options.PdfA);
    }

    /// <summary>Renders the report as a vector PDF to <paramref name="filePath"/>.</summary>
    public Task SavePdfAsync(Report reportDesign, string filePath, CancellationToken ct = default)
        => SavePdfAsync(reportDesign, filePath, new PdfExportOptions(), ct);

    /// <summary>
    ///     Renders the report as a vector PDF to <paramref name="filePath"/>.
    ///     <see cref="PdfExportOptions.PdfA"/> asks Skia for PDF/A-2b document setup, not a validation.
    /// </summary>
    public async Task SavePdfAsync(
        Report reportDesign, string filePath, PdfExportOptions options, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(options);

        await using var stream = File.Create(filePath);
        await RenderToPdfAsync(reportDesign, stream, options, ct);
    }

    /// <summary>Renders the report as a vector PDF and returns the bytes.</summary>
    public Task<byte[]> RenderToPdfBytesAsync(Report reportDesign, CancellationToken ct = default)
        => RenderToPdfBytesAsync(reportDesign, new PdfExportOptions(), ct);

    /// <summary>
    ///     Renders the report as a vector PDF and returns the bytes.
    ///     <see cref="PdfExportOptions.PdfA"/> asks Skia for PDF/A-2b document setup, not a validation.
    /// </summary>
    public async Task<byte[]> RenderToPdfBytesAsync(
        Report reportDesign, PdfExportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);
        ArgumentNullException.ThrowIfNull(options);

        using var stream = new MemoryStream();
        await RenderToPdfAsync(reportDesign, stream, options, ct);
        return stream.ToArray();
    }
}
