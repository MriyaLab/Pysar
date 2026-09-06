using Pysar.Export;
using Windows.Storage;
using Windows.System;

namespace Pysar.Maui;

public sealed partial class MauiReportPrinter
{
    private partial async Task PrintPdfAsync(byte[] pdfBytes, string jobName, PrintPaper paper)
    {
        _ = paper;

        var fileName = $"{Sanitize(jobName)}.pdf";
        var folder = ApplicationData.Current.TemporaryFolder;
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteBytesAsync(file, pdfBytes);

        // Opens the file with the default PDF handler; user prints from there when a direct
        // WinRT print path is not wired. Prefer a real PrintManager flow if already used in-repo.
        var success = await Launcher.LaunchFileAsync(file);
        if (!success)
            throw new InvalidOperationException("Could not open the PDF for printing on Windows.");
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Report" : name;
    }
}
