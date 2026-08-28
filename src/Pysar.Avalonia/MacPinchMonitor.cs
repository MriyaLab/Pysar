using System;
using System.Runtime.InteropServices;

namespace Pysar.Avalonia;

/// <summary>
///     A trackpad magnify event, reported directly by AppKit rather than through Avalonia's own
///     input pipeline - see the remarks on <see cref="MacPinchMonitor"/> for why that pipeline
///     never delivers one on macOS. <see cref="WindowX"/>/<see cref="WindowY"/> are
///     <c>-[NSEvent locationInWindow]</c> untouched: AppKit's own bottom-left-origin window
///     coordinates, still needing the flip into Avalonia's top-left space, which is left to the
///     caller because it needs the control's own <see cref="Avalonia.Controls.TopLevel"/> to do it.
/// </summary>
internal readonly record struct MacMagnifyEventArgs(double Magnification, double WindowX, double WindowY, bool Began, bool Ended);

/// <summary>
///     Installs an AppKit local event monitor for <c>NSEventTypeMagnify</c> (trackpad pinch) and
///     raises <see cref="Magnify"/> for each one. <see cref="Start"/>/<see cref="Stop"/> are no-ops
///     on anything but macOS, since this assembly is shared with Windows and Linux applications.
/// </summary>
/// <remarks>
///     Avalonia never surfaces a trackpad pinch as a gesture on macOS - a real pinch arrives as
///     plain wheel deltas indistinguishable from a two-finger scroll, per the measurement recorded
///     on <c>ReportViewInput</c> (1034 wheel events and zero pinch events on 11.3.12; 55 and zero on
///     12.1.1) - so this goes straight to
///     <c>+[NSEvent addLocalMonitorForEventsMatchingMask:handler:]</c>, the only place the platform
///     actually reports a magnify.
///
///     The handler is an Objective-C block built by hand, since there is no marshalled-delegate path
///     from a block to .NET. It is a <see cref="BlockLiteral"/> allocated with
///     <see cref="Marshal.AllocHGlobal"/> - unmanaged heap memory the GC does not know about and so
///     cannot move or collect, unlike a struct on the managed heap - whose <c>invoke</c> field is a
///     raw function pointer to a static <see cref="UnmanagedCallersOnlyAttribute"/> method rather
///     than a delegate, so there is no managed thunk that could go stale under a GC. The block, its
///     descriptor, and the <see cref="GCHandle"/> that lets the static callback find its owning
///     instance are all freed only by <see cref="Stop"/>; nothing frees or collects them while the
///     monitor is installed.
///
///     The one Objective-C call this class deliberately avoids is any whose return type is a struct
///     bigger than two doubles (an <c>NSRect</c>, for instance): x86_64 and arm64 Macs disagree on
///     whether such a return goes through <c>objc_msgSend</c> or <c>objc_msgSend_stret</c>, and
///     getting that wrong does not throw - it silently reads the wrong registers. This class only
///     ever reads <c>-[NSEvent magnification]</c> (a <c>CGFloat</c>), <c>-[NSEvent phase]</c> (an
///     <c>NSUInteger</c>) and <c>-[NSEvent locationInWindow]</c> (an <c>NSPoint</c>, two doubles, 16
///     bytes) - all small enough to return in registers on both architectures, so plain
///     <c>objc_msgSend</c> is correct either way. Turning that point into the control's own
///     coordinate space, including the y-flip, is left to the caller, which has the control's
///     <c>TopLevel</c> and can do it in managed code instead of asking AppKit for a window frame.
/// </remarks>
internal sealed class MacPinchMonitor : IDisposable
{
    private const string ObjCLib = "/usr/lib/libobjc.dylib";
    private const string SystemLib = "/usr/lib/libSystem.B.dylib";

    /// <summary>1UL &lt;&lt; NSEventTypeMagnify (30).</summary>
    private const ulong NSEventMaskMagnify = 1UL << 30;

    private const ulong NSEventPhaseBegan = 1;
    private const ulong NSEventPhaseEnded = 8;

    /// <summary>
    ///     1UL &lt;&lt; 4. A cancelled pinch is reported with this phase and never with
    ///     <see cref="NSEventPhaseEnded"/>, so a caller that watches only for the latter would keep
    ///     waiting for an end that never comes. Not 1UL &lt;&lt; 2, which is the phase every ordinary
    ///     frame of a gesture carries: reading it as a cancellation ends the gesture on its first
    ///     frame, and the pinch then does nothing at all.
    /// </summary>
    private const ulong NSEventPhaseCancelled = 16;

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr sel_registerName(string name);

    // objc_msgSend must be declared once per distinct signature actually used - the native symbol
    // is untyped, so the .NET P/Invoke declaration is what tells the runtime how to marshal
    // arguments and the return value for that particular call site.
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_addMonitor(IntPtr receiver, IntPtr selector, ulong mask, IntPtr handlerBlock);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_id(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_removeMonitor(IntPtr receiver, IntPtr selector, IntPtr monitor);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern double objc_msgSend_double(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern ulong objc_msgSend_ulong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern NSPoint objc_msgSend_point(IntPtr receiver, IntPtr selector);

    [StructLayout(LayoutKind.Sequential)]
    private struct NSPoint
    {
        public double X;
        public double Y;
    }

    /// <summary>
    ///     Mirrors clang's <c>Block_literal_1</c> layout exactly through <see cref="Descriptor"/>.
    ///     <see cref="Context"/> is this class's own addition, appended after the fields AppKit reads
    ///     - the block captures no Objective-C variables, so this is the only place to carry the
    ///     <see cref="GCHandle"/> the static invoke needs to find its owning
    ///     <see cref="MacPinchMonitor"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public IntPtr Descriptor;
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public IntPtr Reserved;
        public IntPtr Size;
    }

    /// <summary>Cached address of the <c>_NSConcreteGlobalBlock</c> symbol, resolved once.</summary>
    private static IntPtr s_globalBlockIsa;

    public event EventHandler<MacMagnifyEventArgs>? Magnify;

    private IntPtr _blockPtr;
    private IntPtr _descriptorPtr;
    private IntPtr _monitor;
    private GCHandle _selfHandle;

    /// <summary>
    ///     Installs the monitor. A no-op off macOS, and a no-op if already installed - the caller is
    ///     expected to pair this one-for-one with <see cref="Stop"/> around attach/detach, but a
    ///     stray double-attach must not leak a second block.
    /// </summary>
    public void Start()
    {
        if (!OperatingSystem.IsMacOS() || _monitor != IntPtr.Zero)
            return;

        var nsEventClass = objc_getClass("NSEvent");
        var addMonitorSelector = sel_registerName("addLocalMonitorForEventsMatchingMask:handler:");

        _descriptorPtr = Marshal.AllocHGlobal(Marshal.SizeOf<BlockDescriptor>());
        Marshal.StructureToPtr(
            new BlockDescriptor { Reserved = IntPtr.Zero, Size = (IntPtr)Marshal.SizeOf<BlockLiteral>() },
            _descriptorPtr,
            fDeleteOld: false);

        _selfHandle = GCHandle.Alloc(this);

        _blockPtr = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
        unsafe
        {
            var literal = new BlockLiteral
            {
                Isa = GetGlobalBlockIsa(),
                Flags = 0,
                Reserved = 0,
                Invoke = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&InvokeBlock,
                Descriptor = _descriptorPtr,
                Context = GCHandle.ToIntPtr(_selfHandle)
            };
            Marshal.StructureToPtr(literal, _blockPtr, fDeleteOld: false);
        }

        _monitor = objc_msgSend_addMonitor(nsEventClass, addMonitorSelector, NSEventMaskMagnify, _blockPtr);

        if (_monitor != IntPtr.Zero)
        {
            // The method returns the monitor autoreleased, per ordinary Cocoa convention (its name
            // does not start with alloc/new/copy/mutableCopy); without an explicit retain here it
            // can be deallocated - which stops the monitor firing - as soon as the current
            // autorelease pool drains, which on the main run loop is soon.
            objc_msgSend_id(_monitor, sel_registerName("retain"));
        }
        else
        {
            // Registration failed; nothing is installed, so free what was allocated for it rather
            // than leaking it until Stop() is eventually called on a monitor that never started.
            FreeNativeState();
        }
    }

    /// <summary>Removes the monitor. A no-op if it was never installed.</summary>
    public void Stop()
    {
        if (_monitor != IntPtr.Zero)
        {
            var nsEventClass = objc_getClass("NSEvent");
            objc_msgSend_removeMonitor(nsEventClass, sel_registerName("removeMonitor:"), _monitor);
            objc_msgSend_id(_monitor, sel_registerName("release"));
            _monitor = IntPtr.Zero;
        }

        FreeNativeState();
    }

    /// <summary>
    ///     Releases what is safe to release, which is nothing AppKit can still reach.
    /// </summary>
    /// <remarks>
    ///     The block, its descriptor and the handle the callback reads are deliberately never freed.
    ///     <c>removeMonitor:</c> stops the events, but the observer object AppKit created is
    ///     autoreleased, and the pool it sits in is not drained until <c>-[NSApplication run]</c>
    ///     returns at shutdown. Its dealloc then releases the block and walks the descriptor - so
    ///     freeing either one here left AppKit reading memory that had gone back to the allocator,
    ///     and the application segfaulted on exit every time. A global block is static storage by
    ///     definition; treating it as ours to free was the mistake. What leaks is one block, one
    ///     descriptor and one handle per view that ever installed a monitor, tens of bytes, for the
    ///     lifetime of the process.
    /// </remarks>
    private void FreeNativeState()
    {
        _blockPtr = IntPtr.Zero;
        _descriptorPtr = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private static IntPtr GetGlobalBlockIsa()
    {
        if (s_globalBlockIsa != IntPtr.Zero)
            return s_globalBlockIsa;

        var handle = NativeLibrary.Load(SystemLib);
        s_globalBlockIsa = NativeLibrary.GetExport(handle, "_NSConcreteGlobalBlock");
        return s_globalBlockIsa;
    }

    /// <summary>
    ///     The block's own invoke function, called by AppKit as <c>invoke(block, event)</c> on the
    ///     main thread for every matching event. Must never throw - an exception crossing this
    ///     boundary from an <see cref="UnmanagedCallersOnlyAttribute"/> method aborts the process
    ///     rather than unwinding into managed code, so the body is a single defensive try/catch that
    ///     swallows everything rather than risk that.
    /// </summary>
    [UnmanagedCallersOnly]
    private static IntPtr InvokeBlock(IntPtr blockPtr, IntPtr eventPtr)
    {
        try
        {
            var contextOffset = Marshal.OffsetOf<BlockLiteral>(nameof(BlockLiteral.Context));
            var contextPtr = Marshal.ReadIntPtr(blockPtr, (int)contextOffset);
            if (contextPtr != IntPtr.Zero && GCHandle.FromIntPtr(contextPtr).Target is MacPinchMonitor monitor)
                monitor.HandleEvent(eventPtr);
        }
        catch
        {
            // See the remarks above: nothing may escape this callback.
        }

        // Returning the event unchanged is what lets it continue through AppKit's normal dispatch.
        return eventPtr;
    }

    private void HandleEvent(IntPtr eventPtr)
    {
        var magnification = objc_msgSend_double(eventPtr, sel_registerName("magnification"));
        var phase = objc_msgSend_ulong(eventPtr, sel_registerName("phase"));
        var location = objc_msgSend_point(eventPtr, sel_registerName("locationInWindow"));

        Magnify?.Invoke(
            this,
            new MacMagnifyEventArgs(
                magnification,
                location.X,
                location.Y,
                Began: phase == NSEventPhaseBegan,
                Ended: phase is NSEventPhaseEnded or NSEventPhaseCancelled));
    }
}
