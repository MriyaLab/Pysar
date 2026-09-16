using Avalonia.Threading;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Avalonia;

/// <summary>
///     Desktop printer: renders a vector PDF and opens the OS print UI. On macOS this is the
///     system Print panel (PDFKit). On Windows the shell print verb is used. On Linux the PDF is
///     opened in the default viewer so the user can print from there.
/// </summary>
public sealed class AvaloniaReportPrinter : IReportPrinter
{
    private readonly SkiaReportRenderer _renderer;

    public AvaloniaReportPrinter(SkiaReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public async Task PrintAsync(Report report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var pdfBytes = await _renderer.RenderToPdfBytesAsync(report, cancellationToken)
            .ConfigureAwait(false);

        var jobName = string.IsNullOrWhiteSpace(report.Metadata.Title)
            ? "Report"
            : report.Metadata.Title;
        var paper = PrintPaper.From(report.PageFormat);

        if (OperatingSystem.IsMacOS())
        {
            // PDFKit runOperation must run on the AppKit UI thread.
            if (Dispatcher.UIThread.CheckAccess())
                ShowMacOrThrow(pdfBytes, jobName, paper);
            else
                await Dispatcher.UIThread.InvokeAsync(() => ShowMacOrThrow(pdfBytes, jobName, paper));

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DesktopPdfPrint.OpenInShell(pdfBytes);
    }

    private static void ShowMacOrThrow(byte[] pdfBytes, string jobName, PrintPaper paper)
    {
        if (!DesktopPdfPrint.TryShowMacPrintPanel(pdfBytes, jobName, paper))
            throw new InvalidOperationException("macOS print panel could not be shown for this PDF.");
    }
}
