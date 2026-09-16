using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pysar.Export;

/// <summary>
///     Hands a rendered PDF to the desktop print UI: PDFKit on macOS, the shell print verb on
///     Windows, and the default viewer on Linux.
/// </summary>
public static class DesktopPdfPrint
{
    /// <summary>
    ///     Shows the macOS print panel on the calling thread (must be the UI thread). Returns
    ///     <see langword="false"/> when this is not an Apple desktop or the panel could not open.
    /// </summary>
    public static bool TryShowMacPrintPanel(byte[] pdfBytes, string? jobName, PrintPaper paper)
        => MacOsPdfPrint.TryShowPrintPanel(pdfBytes, jobName, paper);

    /// <summary>
    ///     Writes <paramref name="pdfBytes"/> to a temp file and opens the OS print UI or viewer.
    /// </summary>
    public static void OpenInShell(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        OpenPath(WriteTemporaryPdf(pdfBytes));
    }

    public static string WriteTemporaryPdf(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        var path = Path.Combine(Path.GetTempPath(), $"pysar-print-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    private static void OpenPath(string pdfPath)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true, Verb = "print" });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo("open") { ArgumentList = { pdfPath }, UseShellExecute = false });
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { pdfPath }, UseShellExecute = false });
            return;
        }

        throw new PlatformNotSupportedException(
            $"Printing is not supported on {RuntimeInformation.OSDescription}.");
    }
}
