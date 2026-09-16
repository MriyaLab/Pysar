using Microsoft.UI.Dispatching;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;
using Pysar.Viewer.Platform;

namespace Pysar.Uno;

/// <summary>
///     Renders a vector PDF and hands it to whatever the host can print with: the macOS system
///     Print panel (PDFKit), the shell print verb on Windows, and the default viewer on Linux so
///     the user can print from there.
/// </summary>
/// <remarks>
///     Android, iOS and WebAssembly are not covered. Their print paths are each an activity's, a
///     view controller's or the browser's, and none is reachable from a class library without the
///     platform's own target framework - which this package deliberately does not have. An
///     application on those hosts produces the bytes through <see cref="PysarUno.ExportService"/>
///     and shares or downloads them itself.
///
///     These are runtime checks, not compile-time ones: this is a single <c>net10.0</c> assembly
///     that every Uno head loads, so the host is only known once it runs.
/// </remarks>
public sealed class UnoReportPrinter : IReportPrinter
{
    private readonly SkiaReportRenderer _renderer;

    public UnoReportPrinter(SkiaReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public async Task PrintAsync(Report report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "Printing is not implemented for this Uno host. Render the report through "
                + "PysarUno.ExportService and share or download the PDF bytes from the application.");
        }

        // Prefer the queue registration captured on OnLaunched: PrintAsync is often awaited off the
        // UI thread after PDF rendering, and GetForCurrentThread() is then null. Fall back to the
        // caller's queue when registration ran without one (tests). Uno has no static UI dispatcher.
        var dispatcher = OperatingSystem.IsMacOS()
            ? UnoRegistration.UiDispatcher ?? UnoRegistration.TryGetCurrentDispatcher()
            : null;

        var pdfBytes = await _renderer.RenderToPdfBytesAsync(report, cancellationToken)
            .ConfigureAwait(false);

        // Between rendering and the panel is the last point a cancellation can still be honoured:
        // once AppKit owns the print panel it is the user's, not the caller's, to dismiss.
        cancellationToken.ThrowIfCancellationRequested();

        if (dispatcher is not null
            && await TryShowMacPrintPanelAsync(dispatcher, report, pdfBytes, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        DesktopPdfPrint.OpenInShell(pdfBytes);
    }

    /// <summary>
    ///     Shows the macOS Print panel, returning <c>false</c> when it could not be shown so the
    ///     caller falls back to opening the PDF in the default viewer.
    /// </summary>
    private static async Task<bool> TryShowMacPrintPanelAsync(
        DispatcherQueue dispatcher, Report report, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        var jobName = string.IsNullOrWhiteSpace(report.Metadata.Title) ? "Report" : report.Metadata.Title;
        var paper = PrintPaper.From(report.PageFormat);

        if (dispatcher.HasThreadAccess)
            return DesktopPdfPrint.TryShowMacPrintPanel(pdfBytes, jobName, paper);

        var shown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                // Checked again here rather than only before the enqueue: the panel is modal, so a
                // cancellation that arrives while this sits in the queue is the last chance to avoid
                // opening a dialog for an operation the caller has already abandoned.
                if (cancellationToken.IsCancellationRequested)
                {
                    shown.TrySetCanceled(cancellationToken);
                    return;
                }

                try
                {
                    shown.TrySetResult(DesktopPdfPrint.TryShowMacPrintPanel(pdfBytes, jobName, paper));
                }
                catch (Exception ex)
                {
                    shown.TrySetException(ex);
                }
            }))
        {
            return false;
        }

        return await shown.Task.ConfigureAwait(false);
    }
}
