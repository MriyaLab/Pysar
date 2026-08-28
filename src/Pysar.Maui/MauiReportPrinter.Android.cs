using Android.Content;
using Android.OS;
using Android.Print;
using Java.IO;

namespace Pysar.Maui;

public sealed partial class MauiReportPrinter
{
    private partial Task PrintPdfAsync(byte[] pdfBytes, string jobName)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Android activity for printing.");

        var printManager = (PrintManager)activity.GetSystemService(Context.PrintService)!
            ?? throw new InvalidOperationException("Print service is unavailable.");

        var adapter = new PdfPrintDocumentAdapter(jobName, pdfBytes);
        printManager.Print(jobName, adapter, null);

        return Task.CompletedTask;
    }

    private sealed class PdfPrintDocumentAdapter : PrintDocumentAdapter
    {
        private readonly string _jobName;
        private readonly byte[] _pdfBytes;

        public PdfPrintDocumentAdapter(string jobName, byte[] pdfBytes)
        {
            _jobName = jobName;
            _pdfBytes = pdfBytes;
        }

        public override void OnLayout(
            PrintAttributes? oldAttributes,
            PrintAttributes? newAttributes,
            CancellationSignal? cancellationSignal,
            LayoutResultCallback? callback,
            Bundle? extras)
        {
            if (cancellationSignal?.IsCanceled == true)
            {
                callback?.OnLayoutCancelled();
                return;
            }

            var info = new PrintDocumentInfo.Builder(_jobName)
                .SetContentType(PrintContentType.Document)
                .SetPageCount(PrintDocumentInfo.PageCountUnknown)
                .Build();

            callback?.OnLayoutFinished(info, true);
        }

        public override void OnWrite(
            PageRange[]? pages,
            ParcelFileDescriptor? destination,
            CancellationSignal? cancellationSignal,
            WriteResultCallback? callback)
        {
            try
            {
                if (destination is null)
                {
                    callback?.OnWriteFailed("Print destination is unavailable.");
                    return;
                }

                using var output = new FileOutputStream(destination.FileDescriptor);
                output.Write(_pdfBytes);
                callback?.OnWriteFinished([PageRange.AllPages!]);
            }
            catch (System.Exception ex)
            {
                callback?.OnWriteFailed(ex.Message);
            }
        }
    }
}
