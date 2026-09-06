#if __IOS__ || __MACCATALYST__
using CoreGraphics;
using Foundation;
using Pysar.Export;
using UIKit;

namespace Pysar.Maui;

public sealed partial class MauiReportPrinter
{
    private partial async Task PrintPdfAsync(byte[] pdfBytes, string jobName, PrintPaper paper)
    {
#if __MACCATALYST__
        if (!MacOsPdfPrint.TryShowPrintPanel(pdfBytes, jobName, paper))
            throw new InvalidOperationException("macOS print panel could not be shown for this PDF.");
        await Task.CompletedTask;
        return;
#else
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
        printInfo.Orientation = paper.IsLandscape
            ? UIPrintInfoOrientation.Landscape
            : UIPrintInfoOrientation.Portrait;

        var controller = UIPrintInteractionController.SharedPrintController
            ?? throw new InvalidOperationException("The system print controller is unavailable.");

        var paperDelegate = new PaperChoosingDelegate(new CGSize(paper.WidthPt, paper.HeightPt));
        controller.PrintInfo = printInfo;
        controller.PrintPageRenderer = new PdfPrintPageRenderer(pdf);
        controller.Delegate = paperDelegate;

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
        GC.KeepAlive(paperDelegate);
#endif
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name) ? "Report" : name;
    }

    /// <summary>Picks printer stock that matches the report page size.</summary>
    private sealed class PaperChoosingDelegate : UIPrintInteractionControllerDelegate
    {
        private readonly CGSize _pageSize;

        public PaperChoosingDelegate(CGSize pageSize)
        {
            _pageSize = pageSize;
        }

        public override UIPrintPaper ChoosePaper(
            UIPrintInteractionController printInteractionController,
            UIPrintPaper[] paperList)
        {
            UIPrintPaper? best = null;
            var bestDelta = double.MaxValue;
            foreach (var candidate in paperList)
            {
                var size = candidate.PaperSize;
                var aligned = Math.Abs(size.Width - _pageSize.Width) + Math.Abs(size.Height - _pageSize.Height);
                var rotated = Math.Abs(size.Width - _pageSize.Height) + Math.Abs(size.Height - _pageSize.Width);
                var delta = Math.Min(aligned, rotated);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = candidate;
                }
            }

            return best ?? UIPrintPaper.ForPageSize(_pageSize, paperList);
        }
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
