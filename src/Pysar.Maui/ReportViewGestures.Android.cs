using Android.Views;
using Android.Widget;
using AView = Android.Views.View;

namespace Pysar.Maui;

/// <summary>
///     Pinch zoom on Android.
/// </summary>
/// <remarks>
///     A scroll view claims the touch stream for its own scrolling, so a second finger never reaches
///     the cross-platform recogniser attached to the content and the report did not zoom at all.
///     Feeding the events to a <see cref="ScaleGestureDetector"/> from a touch listener on the scroll
///     view itself is what makes the gesture reach the control: a listener runs before the view
///     handles the event, so the detector sees everything without taking scrolling away.
///     <para>
///     Bidirectional MAUI scroll is a NestedScrollView wrapping a HorizontalScrollView. Horizontal
///     moves are intercepted by that child, which then owns the stream - a listener on the parent
///     alone never sees them, so a horizontal pinch did nothing while a vertical one worked. Both
///     views are wired, and two-finger touches are marked handled so neither scroller pans under the
///     pinch.
///     </para>
/// </remarks>
public partial class ReportView
{
    private ScaleGestureDetector? _scaleDetector;

    /// <summary>
    ///     The scroll view the touch listener was attached to, so it can be taken off that same view
    ///     rather than off whichever one the handler holds by then.
    /// </summary>
    private AView? _scaleTouchView;

    /// <summary>The horizontal scroller under the NestedScrollView, once found and wired.</summary>
    private HorizontalScrollView? _horizontalScrollTouch;

    /// <summary>Dedupes the same <see cref="MotionEvent"/> when both scroll views receive it.</summary>
    private long _lastScaleEventTime = long.MinValue;

    private int _lastScaleAction = int.MinValue;

    partial void AddPlatformGestures()
    {
        if (_scroll.Handler?.PlatformView is not AView view || view.Context is null)
            return;

        // Against the view rather than against the detector: after Shell flyout navigation the
        // handler comes back with a new NestedScrollView, and a listener still held from the previous
        // one is attached to a view nobody's fingers can reach - which is what left a report reopened
        // from the flyout no longer zooming.
        if (ReferenceEquals(_scaleTouchView, view))
            return;

        RemovePlatformGestures();

        _scaleDetector = new ScaleGestureDetector(view.Context, new PinchListener(this));
        _scaleTouchView = view;

        view.Touch += OnPlatformTouch;
        TryWireHorizontalScrollTouch(view);
    }

    partial void RemovePlatformGestures()
    {
        // Handle checked rather than trusted: this runs from HandlerChanging, where the views are
        // still alive, but a disposed peer arriving by any other route must not throw out of teardown.
        if (_scaleTouchView is { } view && view.Handle != IntPtr.Zero)
            view.Touch -= OnPlatformTouch;

        if (_horizontalScrollTouch is { } horizontal && horizontal.Handle != IntPtr.Zero)
            horizontal.Touch -= OnPlatformTouch;

        _scaleTouchView = null;
        _horizontalScrollTouch = null;
        _scaleDetector = null;

        // A half-finished gesture must not describe the next scroll view: the dedupe would drop its
        // first event if it happened to carry the same time and action, and a pinch left marked as
        // active would keep the cross-platform recogniser out of the way for good.
        _lastScaleEventTime = long.MinValue;
        _lastScaleAction = int.MinValue;
        _platformPinchActive = false;
    }

    private void OnPlatformTouch(object? sender, AView.TouchEventArgs e)
    {
        if (_scaleDetector is null || e.Event is null)
            return;

        if (sender is AView origin)
            TryWireHorizontalScrollTouch(origin);

        var ev = e.Event;
        var time = ev.EventTime;
        var action = (int)ev.ActionMasked;

        // Parent and child can both surface the same event; feed the detector once.
        var duplicate = time == _lastScaleEventTime && action == _lastScaleAction;

        if (!duplicate)
        {
            _lastScaleEventTime = time;
            _lastScaleAction = action;
            _scaleDetector.OnTouchEvent(ev);
        }

        // Claim the stream from the first second finger, not only after IsInProgress: the horizontal
        // scroller otherwise intercepts horizontal moves before the detector decides a scale began.
        var multiTouch = ev.PointerCount >= 2
            || _scaleDetector.IsInProgress
            || _platformPinchActive;

        if (multiTouch && sender is AView view)
            view.Parent?.RequestDisallowInterceptTouchEvent(true);

        e.Handled = multiTouch;
    }

    private void TryWireHorizontalScrollTouch(AView origin)
    {
        if (_horizontalScrollTouch is not null)
            return;

        var root = _scroll.Handler?.PlatformView as AView ?? origin;

        if (FindHorizontalScrollTouchTarget(root) is not { } horizontal)
            return;

        _horizontalScrollTouch = horizontal;
        horizontal.Touch += OnPlatformTouch;
    }

    private static HorizontalScrollView? FindHorizontalScrollTouchTarget(AView parent)
    {
        if (parent is HorizontalScrollView found)
            return found;

        if (parent is not ViewGroup group)
            return null;

        for (var i = 0; i < group.ChildCount; i++)
        {
            if (group.GetChildAt(i) is { } child && FindHorizontalScrollTouchTarget(child) is { } match)
                return match;
        }

        return null;
    }

    private sealed class PinchListener(ReportView view) : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        public override bool OnScaleBegin(ScaleGestureDetector detector)
        {
            view._platformPinchActive = true;

            // The focus is in device pixels, while the viewport is measured in device independent units.
            var density = view._density;

            view.BeginPinch(new Point(detector.FocusX / density, detector.FocusY / density));

            return true;
        }

        public override bool OnScale(ScaleGestureDetector detector)
        {
            // The detector reports each step against the previous one.
            view.ShowPinchStep(detector.ScaleFactor);

            return true;
        }

        public override void OnScaleEnd(ScaleGestureDetector detector)
        {
            view._platformPinchActive = false;
            view.CommitPinch();
        }
    }
}
