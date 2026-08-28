using System.Runtime.InteropServices;
using System.Text;

namespace Pysar.Avalonia;

/// <summary>
///     Shows the macOS system print panel for a PDF via PDFKit
///     (<c>-[PDFDocument printOperationForPrintInfo:scalingMode:autoRotate:]</c> +
///     <c>-[NSPrintOperation runOperation]</c>). Same objc_msgSend style as
///     <see cref="MacPinchMonitor"/> — no managed AppKit package required.
/// </summary>
internal static class MacOsPdfPrint
{
    private const string ObjCLib = "/usr/lib/libobjc.dylib";
    private const string SystemLib = "/usr/lib/libSystem.B.dylib";
    private const string PdfKitPath = "/System/Library/Frameworks/PDFKit.framework/PDFKit";
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary>PDFPrintScalingMode / kPDFPrintPageScaleDownToFit.</summary>
    private const nint PdfPrintScaleDownToFit = 1;

    private const int RtldLazy = 1;

    [DllImport(SystemLib)]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_id(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_id_id(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_data(
        IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_printOp(
        IntPtr receiver, IntPtr selector, IntPtr printInfo, nint scalingMode, byte autoRotate);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(IntPtr receiver, IntPtr selector, byte value);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern byte objc_msgSend_bool(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_id_id(
        IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    /// <summary>
    ///     Runs the system print panel for <paramref name="pdfBytes"/> on the calling thread
    ///     (must be the UI thread). Returns <c>false</c> when the platform cannot print this way.
    /// </summary>
    public static bool TryShowPrintPanel(byte[] pdfBytes, string? jobName = null)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (!OperatingSystem.IsMacOS() || pdfBytes.Length == 0)
            return false;

        if (dlopen(AppKitPath, RtldLazy) == IntPtr.Zero || dlopen(PdfKitPath, RtldLazy) == IntPtr.Zero)
            return false;

        var nsDataClass = objc_getClass("NSData");
        var pdfDocumentClass = objc_getClass("PDFDocument");
        var nsPrintInfoClass = objc_getClass("NSPrintInfo");
        if (nsDataClass == IntPtr.Zero || pdfDocumentClass == IntPtr.Zero || nsPrintInfoClass == IntPtr.Zero)
            return false;

        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr document = IntPtr.Zero;

        try
        {
            var data = objc_msgSend_data(
                nsDataClass,
                sel_registerName("dataWithBytes:length:"),
                handle.AddrOfPinnedObject(),
                (nuint)pdfBytes.Length);

            if (data == IntPtr.Zero)
                return false;

            var allocated = objc_msgSend_id(pdfDocumentClass, sel_registerName("alloc"));
            document = objc_msgSend_id_id(allocated, sel_registerName("initWithData:"), data);
            if (document == IntPtr.Zero)
            {
                objc_msgSend_id(allocated, sel_registerName("release"));
                return false;
            }

            var printInfo = objc_msgSend_id(nsPrintInfoClass, sel_registerName("sharedPrintInfo"));
            if (printInfo == IntPtr.Zero)
                return false;

            if (!string.IsNullOrWhiteSpace(jobName))
                SetPrintInfoJobName(printInfo, jobName);

            // Autoreleased NSPrintOperation — do not release.
            var printOperation = objc_msgSend_printOp(
                document,
                sel_registerName("printOperationForPrintInfo:scalingMode:autoRotate:"),
                printInfo,
                PdfPrintScaleDownToFit,
                autoRotate: 1);

            if (printOperation == IntPtr.Zero)
                return false;

            objc_msgSend_void_bool(printOperation, sel_registerName("setShowsPrintPanel:"), 1);
            objc_msgSend_void_bool(printOperation, sel_registerName("setShowsProgressPanel:"), 1);

            // Presents the system Print panel and blocks until the user dismisses it.
            _ = objc_msgSend_bool(printOperation, sel_registerName("runOperation"));
            return true;
        }
        finally
        {
            if (document != IntPtr.Zero)
                objc_msgSend_id(document, sel_registerName("release"));
            handle.Free();
        }
    }

    private static void SetPrintInfoJobName(IntPtr printInfo, string jobName)
    {
        var nsStringClass = objc_getClass("NSString");
        if (nsStringClass == IntPtr.Zero)
            return;

        using var jobUtf8 = new Utf8String(jobName);
        using var keyUtf8 = new Utf8String("NSJobName");

        var job = objc_msgSend_id_id(
            objc_msgSend_id(nsStringClass, sel_registerName("alloc")),
            sel_registerName("initWithUTF8String:"),
            jobUtf8.Pointer);

        var key = objc_msgSend_id_id(
            objc_msgSend_id(nsStringClass, sel_registerName("alloc")),
            sel_registerName("initWithUTF8String:"),
            keyUtf8.Pointer);

        try
        {
            var dictionary = objc_msgSend_id(printInfo, sel_registerName("dictionary"));
            if (dictionary != IntPtr.Zero && job != IntPtr.Zero && key != IntPtr.Zero)
                objc_msgSend_void_id_id(dictionary, sel_registerName("setObject:forKey:"), job, key);
        }
        finally
        {
            if (job != IntPtr.Zero)
                objc_msgSend_id(job, sel_registerName("release"));
            if (key != IntPtr.Zero)
                objc_msgSend_id(key, sel_registerName("release"));
        }
    }

    private sealed class Utf8String : IDisposable
    {
        private GCHandle _handle;

        public Utf8String(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        }

        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
