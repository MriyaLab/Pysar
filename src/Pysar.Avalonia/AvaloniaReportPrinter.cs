using System.Diagnostics;
using System.Runtime.InteropServices;
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
            {
                if (!MacOsPdfPrint.TryShowPrintPanel(pdfBytes, jobName, paper))
                    throw new InvalidOperationException("macOS print panel could not be shown for this PDF.");
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!MacOsPdfPrint.TryShowPrintPanel(pdfBytes, jobName, paper))
                        throw new InvalidOperationException("macOS print panel could not be shown for this PDF.");
                });
            }

            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"pysar-print-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, pdfBytes, cancellationToken).ConfigureAwait(false);
        OpenPrintUi(path);
    }

    private static void OpenPrintUi(string pdfPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(pdfPath)
            {
                UseShellExecute = true,
                Verb = "print"
            });
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start(new ProcessStartInfo("xdg-open")
            {
                ArgumentList = { pdfPath },
                UseShellExecute = false
            });
            return;
        }

        throw new NotSupportedException($"Printing is not supported on {RuntimeInformation.OSDescription}.");
    }
}
