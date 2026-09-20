using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Platform;
using Pysar.Viewer.Zoom;

namespace Pysar.Avalonia;

/// <summary>
///     The desktop input a document viewer is expected to have: a modified wheel to zoom, and a
///     double click to magnify and to come back to where the reader was.
/// </summary>
/// <remarks>
///     The arithmetic - how a step or a scale turns into a zoom, and what a double tap toggles
///     between - lives in <see cref="Pysar.Viewer.Zoom.GestureModel"/>, reached through the
///     presenter's <c>Gestures</c>; a wheel notch itself becomes a step through
///     <see cref="Pysar.Viewer.Zoom.WheelZoom"/>. What is left here is recognising the input
///     itself, and turning a pointer position into the viewport point the presenter anchors the zoom
///     around.
///
///     A throwaway probe of Avalonia 11.3.12 on macOS, with a pinch recogniser attached to a
    ///     hit-testable surface, found that a real trackpad pinch arrives as 1034
    ///     <see cref="InputElement.PointerWheelChangedEvent"/> events and zero pinch events - the
    ///     platform never delivers a pinch gesture for a trackpad. Trackpad pinch therefore goes
    ///     through <see cref="MacPinchMonitor"/> and <see cref="PinchSession"/>, not a second
    ///     recogniser that would relayout every frame.
/// </remarks>
public partial class ReportView
{
    /// <summary>
    ///     How far a swipe has to travel before it is called straight or diagonal. Small enough that
    ///     the freedom below it is invisible, large enough that the first jittery samples of a touch
    ///     do not decide the axis.
    /// </summary>
    private const double AxisLockTravel = 8;

    /// <summary>How far one axis must lead the other before the swipe is locked onto it.</summary>
    private const double AxisLockRatio = 2;

    /// <summary>The axis a touch scroll has been locked onto, if any.</summary>
    private enum ScrollAxis
    {
        None,
        Horizontal,
        Vertical
    }

    /// <summary>The gesture the axis lock belongs to; a new id starts the decision over.</summary>
    private int _scrollGestureId;

    /// <summary>Whether <see cref="_scrollLock"/> has been settled for the running gesture.</summary>
    private bool _scrollAxisDecided;

    /// <summary>How far the running gesture has travelled on each axis, unsigned.</summary>
    private Vector _scrollTravel;

    private ScrollAxis _scrollLock;

    /// <summary>The offset the locked-out axis is held at for the rest of the gesture.</summary>
    private Vector _scrollLockOffset;

    /// <summary>
    ///     Orders the two writes back to <see cref="ReportView.Zoom"/> and
    ///     <see cref="ReportView.ZoomMode"/> after an input handler has already told the presenter
    ///     what it did and where to anchor it, and flags that the property-changed handler in
    ///     <c>ReportView.cs</c> is seeing its own write rather than a fresh request - which is correct
    ///     for a menu or a binding but not for a zoom under the reader's pointer.
    /// </summary>
    private readonly ZoomPublisher _zoomPublisher;

    /// <summary>
    ///     The running pinch. Its frames are shown through the canvas's own transform, which is the
    ///     only time what is on screen deliberately disagrees with what the presenter has been told.
    /// </summary>
    private readonly PinchSession _pinch;

    /// <summary>
    ///     How long a gap between two modified wheel events ends one zoom gesture and starts the
    ///     next.
    /// </summary>
    /// <remarks>
    ///     A trackpad pinch reaches a browser head as a stream of wheel events a frame or so apart -
    ///     there is no platform pinch to read here, as the remarks on this type record - so anything
    ///     comfortably longer than a frame separates two gestures without ever cutting one in half. A
    ///     mouse wheel notch stands alone by this measure, which is what keeps it behaving as a notch
    ///     rather than as a gesture.
    /// </remarks>
    private static readonly TimeSpan WheelGestureGap = TimeSpan.FromMilliseconds(150);

    /// <summary>
    ///     When the last modified wheel event arrived, or 0 before the first one. Timestamps rather
    ///     than the event's own: a synthesised wheel event may carry no time at all, and a gesture
    ///     that begins again on every event is exactly the fault this is here to avoid.
    /// </summary>
    private long _lastWheelTimestamp;

    /// <summary>
    ///     The point the running wheel gesture is anchored at, fixed for its whole stream for the
    ///     reason <see cref="PinchSession.Anchor"/> gives: taking it per frame adds a little
    ///     translation every time, and they accumulate into a visible drift.
    /// </summary>
    private Point _wheelAnchor;

    /// <summary>
    ///     The AppKit event monitor that delivers a trackpad pinch on macOS, since the remark above
    ///     found nothing else does. Installed on <see cref="OnAttachedToVisualTree"/> and removed on
    ///     <see cref="OnDetachedFromVisualTree"/>; <see langword="null"/> on every other platform.
    /// </summary>
    private MacPinchMonitor? _macPinchMonitor;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!OperatingSystem.IsMacOS())
            return;

        _macPinchMonitor = new MacPinchMonitor();
        _macPinchMonitor.Magnify += OnMacMagnify;
        _macPinchMonitor.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_macPinchMonitor is { } monitor)
        {
            monitor.Magnify -= OnMacMagnify;
            monitor.Dispose();
            _macPinchMonitor = null;
        }

        // Detaching is also what reparenting looks like, so the session decides on the next turn of
        // the loop, by which point a reparented control is attached again.
        _reportSession.DisposeWhenStillDetached(() => TopLevel.GetTopLevel(this) is not null);

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    ///     Turns a native AppKit magnify into a frame of a pinch: each event's magnification is a step
    ///     from the frame before it, not a scale against the gesture's start, so the steps are
    ///     accumulated here. The frames are shown by scaling what is already drawn - see
    ///     <see cref="ShowPinch"/> - and only the end of the gesture reaches the zoom itself, through
    ///     the same publish path <see cref="OnPointerWheelChanged"/> uses for a wheel notch.
    /// </summary>
    private void OnMacMagnify(object? sender, MacMagnifyEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        // AppKit's locationInWindow is bottom-left-origin window-client coordinates; Avalonia's are
        // top-left-origin, so the y-flip needs the window's own client height. TranslatePoint then
        // carries the point the rest of the way into _scroll's own space - the same space
        // OnPointerWheelChanged and ApplyGestureZoom already anchor against - without asking AppKit
        // for an NSRect (see the remarks on MacPinchMonitor for why that call is avoided).
        var windowPoint = new Point(e.WindowX, topLevel.ClientSize.Height - e.WindowY);

        if (topLevel.TranslatePoint(windowPoint, _scroll) is not { } point ||
            !new Rect(_scroll.Bounds.Size).Contains(point))
        {
            // Several report views, or none focused, must not all zoom together - only the view the
            // pointer sits over reacts to a pinch anywhere in the app.
            return;
        }

        // Also when a gesture is somehow already under way: the anchor belongs to one gesture, and
        // carrying the previous one's into this gesture would zoom around a point nobody touched.
        if (e.Began || !_pinch.Running)
            _pinch.Begin(ToViewPoint(point));

        if (e.Ended)
        {
            CommitPinch();
            return;
        }

        // magnification is the increment for this event, not a cumulative scale, so 1 + it is the
        // step - the same reasoning WheelZoom.StepFor applies to a wheel delta.
        ShowPinch(1 + e.Magnification);
    }

    /// <summary>
    ///     Shows a frame of the gesture by scaling what is already drawn, instead of relaying out
    ///     every page and cell for it. The arithmetic is <see cref="PinchSession"/>'s; what is left
    ///     here is putting the matrix on the canvas.
    /// </summary>
    private void ShowPinch(double step)
    {
        if (_pinch.MoveByStep(step) is not { } preview)
            return;

        _canvas.RenderTransformOrigin = RelativePoint.TopLeft;
        _canvas.RenderTransform = new MatrixTransform(new Matrix(
            preview.Scale, 0, 0, preview.Scale, preview.OffsetX, preview.OffsetY));
    }

    /// <summary>
    ///     Ends the gesture: puts the zoom it reached through the same path a wheel notch takes, so
    ///     the relayout and the sharp cells are paid for once, and only then drops the transform.
    /// </summary>
    private void CommitPinch()
    {
        if (_pinch.End() is not { } commit)
        {
            _canvas.RenderTransform = null;
            return;
        }

        var before = _presenter.EffectiveZoom;

        // BeginPinch first: the commit's factor is measured against the zoom the gesture started
        // at, and PinchByScale is the entry point that reads it that way.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByScale(commit.Factor);

        // Relayout and scroll first while the preview still covers the canvas: dropping the
        // transform before ScrollTo landed painted one frame of pages at the new zoom under the old
        // offset.
        ApplyGestureZoom(before, new Point(commit.Anchor.X, commit.Anchor.Y), commit.Held);
        _canvas.RenderTransform = null;
    }

    private static ViewPoint ToViewPoint(Point point) => new(point.X, point.Y);

    private void AddInputHandlers()
    {
        // Tunnel, not the default bubble: the ScrollViewer's own wheel handling lives on a
        // descendant (ScrollContentPresenter) and runs during the bubble phase, so a bubble handler
        // here would only ever see the event after that descendant had already scrolled with it and
        // marked it handled. Tunnelling runs first, so marking the event handled for a zoom actually
        // keeps the scroll from also happening.
        _scroll.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

        // Touch only: a wheel or a trackpad arrives as PointerWheelChanged, so locking the axis here
        // leaves every desktop input alone. Bubbling, not tunnelling: ScrollGestureEventArgs.Delta is
        // read only outside Avalonia, so the off-axis movement is undone after the
        // ScrollContentPresenter has applied it rather than stopped before.
        // handledEventsToo: the ScrollContentPresenter marks the gesture handled once it has scrolled
        // with it, and that is exactly the event this has to see.
        _scroll.AddHandler(
            InputElement.ScrollGestureEvent, OnScrollGesture, RoutingStrategies.Bubble, handledEventsToo: true);
        _scroll.AddHandler(
            InputElement.ScrollGestureEndedEvent, OnScrollGestureEnded, RoutingStrategies.Bubble,
            handledEventsToo: true);

        _scroll.PointerPressed += OnPointerPressed;
    }

    /// <summary>
    ///     Keeps a swipe on the axis it started on, the way a platform scroll view does. Avalonia's
    ///     scroll gesture reports both axes at once, so a finger that is a few degrees off vertical
    ///     slides the page sideways as it goes down - which reads as dragging the sheet around rather
    ///     than scrolling it, the one thing that gave the mobile viewer away.
    /// </summary>
    /// <remarks>
    ///     The inertia that follows the lift reports through this same event, so the lock has to
    ///     outlast the finger: it is released on <see cref="Gestures.ScrollGestureEndedEvent"/>, not
    ///     when the touch ends, or the fling would drift off-axis after a straight swipe.
    ///
    ///     A gesture that is genuinely diagonal is left alone. Zoomed past the frame the reader is
    ///     panning a sheet rather than scrolling a list, and forcing that onto one axis would take
    ///     away the diagonal pan every other document viewer has.
    /// </remarks>
    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (e.Id != _scrollGestureId)
        {
            _scrollGestureId = e.Id;
            _scrollAxisDecided = false;
            _scrollTravel = default;
        }

        if (!_scrollAxisDecided)
        {
            _scrollTravel += new Vector(Math.Abs(e.Delta.X), Math.Abs(e.Delta.Y));

            // Too early to tell a straight swipe from a diagonal one; a few pixels of freedom here
            // is what the platforms allow too, and it is below what a reader can see.
            if (_scrollTravel.X + _scrollTravel.Y < AxisLockTravel)
                return;

            _scrollAxisDecided = true;
            _scrollLock = Dominant(_scrollTravel);

            // Where the off axis is pinned for the rest of the gesture, read before this event's
            // own drift is undone below.
            _scrollLockOffset = _scroll.Offset;
        }

        if (_scrollLock == ScrollAxis.None)
            return;

        _scroll.Offset = _scrollLock == ScrollAxis.Vertical
            ? new Vector(_scrollLockOffset.X, _scroll.Offset.Y)
            : new Vector(_scroll.Offset.X, _scrollLockOffset.Y);
    }

    private void OnScrollGestureEnded(object? sender, ScrollGestureEndedEventArgs e)
    {
        _scrollGestureId = 0;
        _scrollAxisDecided = false;
        _scrollTravel = default;
        _scrollLock = ScrollAxis.None;
    }

    /// <summary>
    ///     The axis a swipe belongs to, or <see cref="ScrollAxis.None"/> when neither leads the other
    ///     clearly enough to call it anything but diagonal.
    /// </summary>
    private static ScrollAxis Dominant(Vector travel)
    {
        if (travel.Y >= travel.X * AxisLockRatio)
            return ScrollAxis.Vertical;

        return travel.X >= travel.Y * AxisLockRatio ? ScrollAxis.Horizontal : ScrollAxis.None;
    }

    /// <summary>
    ///     A wheel notch under Ctrl or Meta zooms around the pointer instead of scrolling; a plain
    ///     wheel notch is left for the <see cref="ScrollViewer"/> to handle as it always has.
    /// </summary>
    /// <remarks>
    ///     Consecutive events are one gesture, not a notch each. A mouse wheel is unaffected - its
    ///     notches are far enough apart to each begin their own gesture, and one notch is a 50% step
    ///     either way - but a trackpad pinch, which is what a browser head delivers here and nothing
    ///     else, arrives as hundreds of events whose individual steps measure about 1%. Beginning a
    ///     gesture on every one of those threw the accumulated total away each time, so a slow pinch
    ///     never reached <see cref="ReportViewDefaults.ZoomStepThreshold"/> and never zoomed at all.
    /// </remarks>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
            return;

        var before = _presenter.EffectiveZoom;

        var now = Stopwatch.GetTimestamp();

        if (_lastWheelTimestamp == 0 || Stopwatch.GetElapsedTime(_lastWheelTimestamp, now) > WheelGestureGap)
        {
            _wheelAnchor = e.GetPosition(_scroll);
            _presenter.Gestures.BeginPinch();
        }

        _lastWheelTimestamp = now;

        // Held back by the gesture model until the stream has moved far enough to be worth a
        // relayout, so what it writes and what ApplyGestureZoom lays out are never two different
        // zooms - see the remarks on GestureModel.
        _presenter.Gestures.PinchByStep(WheelZoom.StepFor(e.Delta.Y));

        ApplyGestureZoom(before, _wheelAnchor);

        // Marks the event handled during the tunnel phase, before the ScrollViewer's own bubble
        // handling ever sees it - see the routing note on AddInputHandlers.
        e.Handled = true;
    }

    /// <summary>
    ///     Magnifies around the clicked point, and puts the reader back where they were when clicked
    ///     again - so 100% goes to 200% and back to 100%, and a fit mode returns to that fit mode.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2)
            return;

        var point = e.GetPosition(_scroll);

        _presenter.Gestures.DoubleTap();

        var mode = _presenter.ZoomMode;
        var zoom = _presenter.Zoom;

        _zoomPublisher.Publish(mode, zoom);

        _presenter.SetZoom(ZoomMode, Zoom, new ViewPoint(point.X, point.Y));
        AfterPresenterUpdate(immediate: false);
    }

    /// <summary>
    ///     Publishes the zoom an input handler just applied, if it moved far enough from the one
    ///     before it, and refreshes anchored at that handler's point so what is under the pointer or
    ///     the fingers stays there.
    /// </summary>
    /// <remarks>
    ///     The gesture model has already applied the new zoom to the presenter's own state; reading
    ///     it back through <see cref="ReportViewPresenter.Zoom"/> and
    ///     <see cref="ReportViewPresenter.ZoomMode"/> is what lets the bindable properties mirror it
    ///     without this file repeating the arithmetic that produced it. A wheel or a pinch always
    ///     lands on a custom factor, so the mode read back here is always
    ///     <see cref="ReportZoomMode.Custom"/>.
    /// </remarks>
    private void ApplyGestureZoom(double before, Point viewportPoint, DocumentPoint? held = null)
    {
        var zoom = _presenter.EffectiveZoom;

        if (Math.Abs(zoom - before) < before * ReportViewDefaults.ZoomStepThreshold)
            return;

        _zoomPublisher.Publish(_presenter.ZoomMode, _presenter.Zoom);

        _presenter.SetZoom(ZoomMode, Zoom, ToViewPoint(viewportPoint), held);
        AfterPresenterUpdate(immediate: true);
    }
}
