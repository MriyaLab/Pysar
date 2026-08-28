using Pysar.Elements;
using Pysar.Export;

namespace Pysar.Skia;

internal sealed class PdfReportExporter : IReportExporter
{
    private readonly SkiaReportRenderer _renderer;

    public PdfReportExporter(SkiaReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public Task ExportAsync(Report report, Stream destination, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        return _renderer.RenderToPdfAsync(report, destination, ct);
    }
}
