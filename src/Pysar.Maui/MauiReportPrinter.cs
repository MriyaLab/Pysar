using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Maui;

/// <summary>Renders a built report to PDF and opens the platform print UI.</summary>
public sealed partial class MauiReportPrinter : IReportPrinter
{
    private readonly SkiaReportRenderer _renderer;

    public MauiReportPrinter(SkiaReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public async Task PrintAsync(Report report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var pdfBytes = await Task.Run(
            () => _renderer.RenderToPdfBytesAsync(report, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        await MainThread.InvokeOnMainThreadAsync(
                () => PrintPdfAsync(pdfBytes, GetJobName(report), PrintPaper.From(report.PageFormat)))
            .ConfigureAwait(false);
    }

    private static string GetJobName(Report report)
        => string.IsNullOrWhiteSpace(report.Metadata.Title)
            ? "Report"
            : report.Metadata.Title;

    private partial Task PrintPdfAsync(byte[] pdfBytes, string jobName, PrintPaper paper);
}
