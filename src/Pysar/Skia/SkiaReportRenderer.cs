using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;

namespace Pysar.Skia;

/// <summary>
///     Renders a <see cref="Report"/> to page bitmaps using the two-phase band pipeline
///     (measure → paginate → draw). Custom element types are supported by registering an
///     <see cref="IElementDrawer"/> via <see cref="WithDrawer{T}"/>.
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
    public async Task RenderToPdfAsync(Report reportDesign, Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);
        ArgumentNullException.ThrowIfNull(stream);

        await PageRenderer.RenderToPdfAsync(
            reportDesign, stream, ct, _drawers, reportDesign.Metadata, _measurers);
    }

    /// <summary>Renders the report as a vector PDF to <paramref name="filePath"/>.</summary>
    public async Task SavePdfAsync(Report reportDesign, string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        await using var stream = File.Create(filePath);
        await RenderToPdfAsync(reportDesign, stream, ct);
    }

    /// <summary>Renders the report as a vector PDF and returns the bytes.</summary>
    public async Task<byte[]> RenderToPdfBytesAsync(Report reportDesign, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportDesign);

        using var stream = new MemoryStream();
        await RenderToPdfAsync(reportDesign, stream, ct);
        return stream.ToArray();
    }
}
