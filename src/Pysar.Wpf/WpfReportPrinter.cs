using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Wpf;

public sealed class WpfReportPrinter : IReportPrinter
{
    private readonly SkiaReportRenderer _renderer;

    public WpfReportPrinter(SkiaReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public async Task PrintAsync(Report report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var pdfBytes = await _renderer.RenderToPdfBytesAsync(report, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        DesktopPdfPrint.OpenInShell(pdfBytes);
    }
}
