using System.Runtime.InteropServices;
using System.Text;

namespace Pysar.Export;

/// <summary>
///     Shows the macOS / Mac Catalyst system print panel for a PDF via PDFKit
///     (<c>-[PDFDocument printOperationForPrintInfo:scalingMode:autoRotate:]</c> +
///     <c>-[NSPrintOperation runOperation]</c>). No managed AppKit package required.
/// </summary>
public static class MacOsPdfPrint
{
    private const string ObjCLib = "/usr/lib/libobjc.dylib";
    private const string SystemLib = "/usr/lib/libSystem.B.dylib";
    private const string PdfKitPath = "/System/Library/Frameworks/PDFKit.framework/PDFKit";
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string PrintCorePath =
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/PrintCore.framework/PrintCore";

    /// <summary>PDFPrintScalingMode / kPDFPrintPageScaleDownToFit.</summary>
    private const nint PdfPrintScaleDownToFit = 1;

    private const int RtldLazy = 1;

    [DllImport(SystemLib)]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(SystemLib)]
    private static extern IntPtr dlsym(IntPtr handle, string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr sel);

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

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_id(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nint(IntPtr receiver, IntPtr selector, nint value);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_double(IntPtr receiver, IntPtr selector, double value);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_id_nint(IntPtr receiver, IntPtr selector, nint value);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_id_id_id(
        IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nint_id(
        IntPtr receiver, IntPtr selector, nint arg1, IntPtr arg2);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_size(IntPtr receiver, IntPtr selector, NsSize size);

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, nint extraBytes);

    [DllImport(ObjCLib)]
    private static extern byte class_addMethod(IntPtr cls, IntPtr name, IntPtr imp, string types);

    [DllImport(ObjCLib)]
    private static extern void objc_registerClassPair(IntPtr cls);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PmSetOrientation(IntPtr pageFormat, ushort orientation, byte lockFlag);

    private const nint NSPaperOrientationPortrait = 0;
    private const nint NSPaperOrientationLandscape = 1;
    private const ushort PmPortrait = 1;
    private const ushort PmLandscape = 2;

    /// <summary>
    ///     Runs the system print panel for <paramref name="pdfBytes"/> on the calling thread
    ///     (must be the UI thread). Returns <c>false</c> when the platform cannot print this way.
    /// </summary>
    public static bool TryShowPrintPanel(byte[] pdfBytes, string? jobName, PrintPaper paper)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (!IsAppleDesktop() || pdfBytes.Length == 0)
            return false;

        if (!TryLoadAppKit())
            return false;

        _ = dlopen(PdfKitPath, RtldLazy);

        var nsDataClass = objc_getClass("NSData");
        var pdfDocumentClass = objc_getClass("PDFDocument");
        var nsPrintInfoClass = objc_getClass("NSPrintInfo");
        if (nsDataClass == IntPtr.Zero || pdfDocumentClass == IntPtr.Zero || nsPrintInfoClass == IntPtr.Zero)
            return false;

        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr document = IntPtr.Zero;
        IntPtr printInfo = IntPtr.Zero;

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

            var shared = objc_msgSend_id(nsPrintInfoClass, sel_registerName("sharedPrintInfo"));
            if (shared == IntPtr.Zero)
                return false;

            printInfo = objc_msgSend_id(shared, sel_registerName("copy"));
            if (printInfo == IntPtr.Zero)
                return false;

            if (!string.IsNullOrWhiteSpace(jobName))
                SetPrintInfoJobName(printInfo, jobName);

            ApplyPrintPaper(printInfo, paper);
            ApplyPrintPaper(shared, paper);
            objc_msgSend_void_nint(printInfo, sel_registerName("setHorizontalPagination:"), 1);
            objc_msgSend_void_nint(printInfo, sel_registerName("setVerticalPagination:"), 1);

            var printOpSelector = sel_registerName("printOperationForPrintInfo:scalingMode:autoRotate:");
            IntPtr printOperation;
            if (class_getInstanceMethod(pdfDocumentClass, printOpSelector) != IntPtr.Zero)
            {
                printOperation = objc_msgSend_printOp(
                    document,
                    printOpSelector,
                    printInfo,
                    PdfPrintScaleDownToFit,
                    autoRotate: 1);
            }
            else
            {
                printOperation = CreateViewPrintOperation(document, printInfo, paper);
            }

            if (printOperation == IntPtr.Zero)
                return false;

            var operationPrintInfo = objc_msgSend_id(printOperation, sel_registerName("printInfo"));
            if (operationPrintInfo != IntPtr.Zero)
                ApplyPrintPaper(operationPrintInfo, paper);

            objc_msgSend_void_bool(printOperation, sel_registerName("setShowsPrintPanel:"), 1);
            objc_msgSend_void_bool(printOperation, sel_registerName("setShowsProgressPanel:"), 1);

            // Presents the system Print panel and blocks until the user dismisses it.
            _ = objc_msgSend_bool(printOperation, sel_registerName("runOperation"));
            return true;
        }
        finally
        {
            t_printView = null;
            if (printInfo != IntPtr.Zero)
                objc_msgSend_id(printInfo, sel_registerName("release"));
            if (document != IntPtr.Zero)
                objc_msgSend_id(document, sel_registerName("release"));
            handle.Free();
        }
    }

    /// <summary>
    ///     Writes paper size and orientation onto AppKit's shared <c>NSPrintInfo</c>, which is what
    ///     the Mac Catalyst <c>UIPrintInteractionController</c> panel reads as Default Settings.
    /// </summary>
    public static bool ApplySharedPrintInfo(PrintPaper paper)
    {
        if (!IsAppleDesktop())
            return false;

        if (dlopen(AppKitPath, RtldLazy) == IntPtr.Zero)
            return false;

        var nsPrintInfoClass = objc_getClass("NSPrintInfo");
        if (nsPrintInfoClass == IntPtr.Zero)
            return false;

        var shared = objc_msgSend_id(nsPrintInfoClass, sel_registerName("sharedPrintInfo"));
        if (shared == IntPtr.Zero)
            return false;

        ApplyPrintPaper(shared, paper);
        return true;
    }

    private static bool IsAppleDesktop()
        => OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst();

    private static readonly string[] AppKitPaths =
    [
        "/System/Library/Frameworks/AppKit.framework/AppKit",
        "/System/Library/Frameworks/AppKit.framework/Versions/C/AppKit",
        "/System/iOSSupport/System/Library/Frameworks/AppKit.framework/AppKit"
    ];

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte NsApplicationLoad();

    private static bool TryLoadAppKit()
    {
        if (objc_getClass("NSView") != IntPtr.Zero && objc_getClass("NSPrintInfo") != IntPtr.Zero)
            return true;

        foreach (var path in AppKitPaths)
        {
            var handle = dlopen(path, RtldLazy);
            if (handle == IntPtr.Zero)
                continue;

            var load = dlsym(handle, "NSApplicationLoad");
            if (load != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<NsApplicationLoad>(load)();

            if (objc_getClass("NSView") != IntPtr.Zero && objc_getClass("NSPrintInfo") != IntPtr.Zero)
                return true;
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NsSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NsRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte KnowsPageRangeDelegate(IntPtr self, IntPtr cmd, IntPtr rangePtr);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NsRect RectForPageDelegate(IntPtr self, IntPtr cmd, nint page);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DrawRectDelegate(IntPtr self, IntPtr cmd, NsRect dirty);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte IsFlippedDelegate(IntPtr self, IntPtr cmd);

    private static readonly KnowsPageRangeDelegate KnowsPageRangeFn = KnowsPageRange;
    private static readonly RectForPageDelegate RectForPageFn = RectForPage;
    private static readonly DrawRectDelegate DrawRectFn = DrawRect;
    private static readonly IsFlippedDelegate IsFlippedFn = IsFlipped;

    [ThreadStatic]
    private static PrintViewState? t_printView;

    private static IntPtr s_printViewClass;

    private sealed class PrintViewState
    {
        public IntPtr Document;
        public nint PageCount;
        public double Width;
        public double Height;
    }

    private static IntPtr CreateViewPrintOperation(IntPtr document, IntPtr printInfo, PrintPaper paper)
    {
        var nsPrintOperationClass = objc_getClass("NSPrintOperation");
        var viewClass = EnsurePrintViewClass();
        if (nsPrintOperationClass == IntPtr.Zero || viewClass == IntPtr.Zero)
            return IntPtr.Zero;

        var pageCount = objc_msgSend_nint(document, sel_registerName("pageCount"));
        if (pageCount < 1)
            return IntPtr.Zero;

        t_printView = new PrintViewState
        {
            Document = document,
            PageCount = pageCount,
            Width = paper.WidthPt,
            Height = paper.HeightPt
        };

        var view = objc_msgSend_id(objc_msgSend_id(viewClass, sel_registerName("alloc")), sel_registerName("init"));
        if (view == IntPtr.Zero)
            return IntPtr.Zero;

        objc_msgSend_void_size(
            view,
            sel_registerName("setFrameSize:"),
            new NsSize { Width = paper.WidthPt, Height = paper.HeightPt });

        var printOperation = objc_msgSend_id_id_id(
            nsPrintOperationClass,
            sel_registerName("printOperationWithView:printInfo:"),
            view,
            printInfo);

        objc_msgSend_id(view, sel_registerName("release"));
        return printOperation;
    }

    private static IntPtr EnsurePrintViewClass()
    {
        if (s_printViewClass != IntPtr.Zero)
            return s_printViewClass;

        var existing = objc_getClass("PysarPdfPrintView");
        if (existing != IntPtr.Zero)
        {
            s_printViewClass = existing;
            return s_printViewClass;
        }

        var nsViewClass = objc_getClass("NSView");
        if (nsViewClass == IntPtr.Zero)
            return IntPtr.Zero;

        var cls = objc_allocateClassPair(nsViewClass, "PysarPdfPrintView", 0);
        if (cls == IntPtr.Zero)
            return IntPtr.Zero;

        class_addMethod(
            cls,
            sel_registerName("knowsPageRange:"),
            Marshal.GetFunctionPointerForDelegate(KnowsPageRangeFn),
            "B@:^{_NSRange=QQ}");
        class_addMethod(
            cls,
            sel_registerName("rectForPage:"),
            Marshal.GetFunctionPointerForDelegate(RectForPageFn),
            "{CGRect={CGPoint=dd}{CGSize=dd}}@:@q");
        class_addMethod(
            cls,
            sel_registerName("drawRect:"),
            Marshal.GetFunctionPointerForDelegate(DrawRectFn),
            "v@:{CGRect={CGPoint=dd}{CGSize=dd}}");
        class_addMethod(
            cls,
            sel_registerName("isFlipped"),
            Marshal.GetFunctionPointerForDelegate(IsFlippedFn),
            "B@:");

        objc_registerClassPair(cls);
        s_printViewClass = cls;
        return s_printViewClass;
    }

    private static byte KnowsPageRange(IntPtr self, IntPtr cmd, IntPtr rangePtr)
    {
        if (t_printView is null || rangePtr == IntPtr.Zero)
            return 0;

        Marshal.WriteIntPtr(rangePtr, 0, 1);
        Marshal.WriteIntPtr(rangePtr, IntPtr.Size, (IntPtr)t_printView.PageCount);
        return 1;
    }

    private static NsRect RectForPage(IntPtr self, IntPtr cmd, nint page)
        => t_printView is null
            ? default
            : new NsRect { Width = t_printView.Width, Height = t_printView.Height };

    private static void DrawRect(IntPtr self, IntPtr cmd, NsRect dirty)
    {
        if (t_printView is null)
            return;

        var op = objc_msgSend_id(objc_getClass("NSPrintOperation"), sel_registerName("currentOperation"));
        nint pageNumber = 1;
        if (op != IntPtr.Zero)
            pageNumber = objc_msgSend_nint(op, sel_registerName("currentPage"));

        var pageIndex = pageNumber - 1;
        if (pageIndex < 0 || pageIndex >= t_printView.PageCount)
            return;

        var page = objc_msgSend_id_nint(t_printView.Document, sel_registerName("pageAtIndex:"), pageIndex);
        if (page == IntPtr.Zero)
            return;

        var gc = objc_msgSend_id(objc_getClass("NSGraphicsContext"), sel_registerName("currentContext"));
        if (gc == IntPtr.Zero)
            return;

        var ctx = objc_msgSend_id(gc, sel_registerName("CGContext"));
        if (ctx == IntPtr.Zero)
            return;

        objc_msgSend_void_nint_id(page, sel_registerName("drawWithBox:toContext:"), 0, ctx);
    }

    private static byte IsFlipped(IntPtr self, IntPtr cmd) => 0;

    private static void ApplyPrintPaper(IntPtr printInfo, PrintPaper paper)
    {
        if (!string.IsNullOrWhiteSpace(paper.PaperName))
            SetPrintInfoPaperName(printInfo, paper.PaperName);

        var orientation = paper.IsLandscape ? NSPaperOrientationLandscape : NSPaperOrientationPortrait;
        objc_msgSend_void_nint(printInfo, sel_registerName("setOrientation:"), orientation);
        SetPrintInfoOrientation(printInfo, orientation);
        ApplyPmOrientation(printInfo, paper.IsLandscape);

        objc_msgSend_void_double(printInfo, sel_registerName("setLeftMargin:"), 0);
        objc_msgSend_void_double(printInfo, sel_registerName("setRightMargin:"), 0);
        objc_msgSend_void_double(printInfo, sel_registerName("setTopMargin:"), 0);
        objc_msgSend_void_double(printInfo, sel_registerName("setBottomMargin:"), 0);
    }

    private static void ApplyPmOrientation(IntPtr printInfo, bool landscape)
    {
        var pageFormat = objc_msgSend_id(printInfo, sel_registerName("PMPageFormat"));
        if (pageFormat == IntPtr.Zero)
            return;

        var printCore = dlopen(PrintCorePath, RtldLazy);
        if (printCore == IntPtr.Zero)
            return;

        var symbol = dlsym(printCore, "PMSetOrientation");
        if (symbol == IntPtr.Zero)
            return;

        var setOrientation = Marshal.GetDelegateForFunctionPointer<PmSetOrientation>(symbol);
        setOrientation(pageFormat, landscape ? PmLandscape : PmPortrait, 0);
        objc_msgSend_void(printInfo, sel_registerName("updateFromPMPageFormat"));
    }

    private static void SetPrintInfoOrientation(IntPtr printInfo, nint orientation)
    {
        var nsNumberClass = objc_getClass("NSNumber");
        var nsStringClass = objc_getClass("NSString");
        if (nsNumberClass == IntPtr.Zero || nsStringClass == IntPtr.Zero)
            return;

        var number = objc_msgSend_id_nint(
            nsNumberClass, sel_registerName("numberWithInteger:"), orientation);
        if (number == IntPtr.Zero)
            return;

        using var keyUtf8 = new Utf8String("NSOrientation");
        var key = objc_msgSend_id_id(
            objc_msgSend_id(nsStringClass, sel_registerName("alloc")),
            sel_registerName("initWithUTF8String:"),
            keyUtf8.Pointer);

        try
        {
            var dictionary = objc_msgSend_id(printInfo, sel_registerName("dictionary"));
            if (dictionary != IntPtr.Zero && key != IntPtr.Zero)
                objc_msgSend_void_id_id(dictionary, sel_registerName("setObject:forKey:"), number, key);
        }
        finally
        {
            if (key != IntPtr.Zero)
                objc_msgSend_id(key, sel_registerName("release"));
        }
    }

    private static void SetPrintInfoPaperName(IntPtr printInfo, string paperName)
    {
        var nsStringClass = objc_getClass("NSString");
        if (nsStringClass == IntPtr.Zero)
            return;

        using var utf8 = new Utf8String(paperName);
        var name = objc_msgSend_id_id(
            objc_msgSend_id(nsStringClass, sel_registerName("alloc")),
            sel_registerName("initWithUTF8String:"),
            utf8.Pointer);

        if (name == IntPtr.Zero)
            return;

        try
        {
            objc_msgSend_void_id(printInfo, sel_registerName("setPaperName:"), name);
        }
        finally
        {
            objc_msgSend_id(name, sel_registerName("release"));
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
