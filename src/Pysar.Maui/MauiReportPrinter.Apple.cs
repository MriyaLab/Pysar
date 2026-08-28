#if __IOS__ || __MACCATALYST__
using CoreGraphics;
using Foundation;
using UIKit;

namespace Pysar.Maui;

public sealed partial class MauiReportPrinter
{
    private partial async Task PrintPdfAsync(byte[] pdfBytes, string jobName)
    {
        // Mac Catalyst does not implement PDFKit's macOS-only
        // -[PDFDocument printOperationForPrintInfo:scalingMode:autoRotate:].
        // UIPrintInteractionController + a page renderer that draws CGPDF pages
        // shows the system Print panel on both iOS and Mac Catalyst.
        if (!UIPrintInteractionController.PrintingAvailable)
            throw new NotSupportedException("Printing is not available on this device.");

        var cachePath = Path.Combine(
            FileSystem.CacheDirectory,
            $"{SanitizeFileName(jobName)}-{Guid.NewGuid():N}.pdf");

        await File.WriteAllBytesAsync(cachePath, pdfBytes).ConfigureAwait(true);

        using var pdf = CGPDFDocument.FromFile(cachePath)
            ?? throw new InvalidOperationException("The rendered PDF could not be opened for printing.");

        if (pdf.Pages < 1)
            throw new InvalidOperationException("The rendered PDF has no pages to print.");

        var printInfo = UIPrintInfo.PrintInfo;
        printInfo.JobName = jobName;
        printInfo.OutputType = UIPrintInfoOutputType.General;
        printInfo.Orientation = UIPrintInfoOrientation.Portrait;

        var controller = UIPrintInteractionController.SharedPrintController
            ?? throw new InvalidOperationException("The system print controller is unavailable.");

        controller.PrintInfo = printInfo;
        controller.PrintPageRenderer = new PdfPrintPageRenderer(pdf);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // User cancel is success; only NSError fails the task.
        controller.Present(true, (_, _, error) =>
        {
            if (error is not null)
                tcs.TrySetException(new InvalidOperationException(error.LocalizedDescription));
            else
                tcs.TrySetResult();
        });

        await tcs.Task.ConfigureAwait(true);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name) ? "Report" : name;
    }

    /// <summary>Draws each page of a <see cref="CGPDFDocument"/> into the print context.</summary>
    private sealed class PdfPrintPageRenderer : UIPrintPageRenderer
    {
        private readonly CGPDFDocument _document;

        public PdfPrintPageRenderer(CGPDFDocument document)
        {
            _document = document;
        }

        public override nint NumberOfPages => (nint)_document.Pages;

        public override void DrawPage(nint index, CGRect printableRect)
        {
            // CGPDF pages are 1-based.
            var page = _document.GetPage((int)index + 1);
            if (page is null)
                return;

            var context = UIGraphics.GetCurrentContext();
            if (context is null)
                return;

            var mediaBox = page.GetBoxRect(CGPDFBox.Media);
            if (mediaBox.Width <= 0 || mediaBox.Height <= 0)
                return;

            context.SaveState();

            // UIKit print context is top-left; PDF is bottom-left.
            context.TranslateCTM(printableRect.X, printableRect.Y + printableRect.Height);
            context.ScaleCTM(1, -1);

            var scale = (double)Math.Min(
                printableRect.Width / mediaBox.Width,
                printableRect.Height / mediaBox.Height);

            var drawWidth = mediaBox.Width * scale;
            var drawHeight = mediaBox.Height * scale;
            var offsetX = (printableRect.Width - drawWidth) / 2.0;
            var offsetY = (printableRect.Height - drawHeight) / 2.0;

            context.TranslateCTM((nfloat)offsetX, (nfloat)offsetY);
            context.ScaleCTM((nfloat)scale, (nfloat)scale);
            context.TranslateCTM(-mediaBox.X, -mediaBox.Y);

            context.DrawPDFPage(page);
            context.RestoreState();
        }
    }
}
#endif