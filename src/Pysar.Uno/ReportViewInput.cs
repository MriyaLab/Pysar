using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pysar.Core.Platform;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Zoom;
using Windows.Foundation;
using Windows.System;

namespace Pysar.Uno;

/// <summary>
///     The input a document viewer is expected to have: a modified wheel to zoom, a pinch to zoom,
///     and a double tap to magnify and to come back to where the reader was.
/// </summary>
/// <remarks>
///     The arithmetic - how a step or a scale turns into a zoom, and what a double tap toggles
///     between - lives in <see cref="GestureModel"/>, reached through the presenter's
///     <c>Gestures</c>; a wheel notch itself becomes a step through <see cref="WheelZoom"/>. What is
///     left here is recognising the input, and turning a pointer position into the viewport point
///     the presenter anchors the zoom around.
///
///     Every handler is attached to the canvas rather than to the scroll viewer. WinUI routed events
///     only bubble - there is no tunnel phase - and the canvas is a descendant of the scroll viewer,
///     so a handler here runs before the scroll viewer sees the event and marking it handled is what
///     keeps a zoom from also scrolling. The Avalonia host reaches the same point from the other
///     direction, by handling the event on the scroll viewer during the tunnel phase.
///
///     macOS needs <see cref="MacPinchMonitor"/>, exactly as the Avalonia host does. Uno does not
///     deliver a trackpad pinch there in any form: measured over one pinch, 498
///     <see cref="UIElement.PointerWheelChanged"/> events and zero manipulation events, every wheel
///     event carrying no key modifier and so indistinguishable from a two-finger scroll. Its macOS
///     Skia host contains no magnify handling at all. The manipulation handlers below are kept for
///     the touch hosts - Android and iOS - where a pinch is a real two-pointer gesture.
/// </remarks>
public partial class ReportView
{
    /// <summary>
    ///     Orders the two writes back to <see cref="Zoom"/> and <see cref="ZoomMode"/> after an input
    ///     handler has already told the presenter what it did and where to anchor it, and flags that
    ///     the property-changed callbacks are seeing this control's own write rather than a fresh
    ///     request - which is correct for a menu or a binding but not for a zoom under the pointer.
    /// </summary>
    private readonly ZoomPublisher _zoomPublisher;

    /// <summary>
    ///     The running pinch. Its frames are shown through the canvas's own transform, which is the
    ///     only time what is on screen deliberately disagrees with what the presenter has been told.
    /// </summary>
    private readonly PinchSession _pinch;

    /// <summary>
    ///     The AppKit event monitor that delivers a trackpad pinch on macOS, since nothing in Uno's
    ///     own input pipeline does. Installed while the control is loaded; <see langword="null"/> on
    ///     every other platform.
    /// </summary>
    private MacPinchMonitor? _macPinchMonitor;

    private void StartMacPinchMonitor()
    {
        if (!OperatingSystem.IsMacOS() || _macPinchMonitor is not null)
            return;

        _macPinchMonitor = new MacPinchMonitor();
        _macPinchMonitor.Magnify += OnMacMagnify;
        _macPinchMonitor.Start();
    }

    private void StopMacPinchMonitor()
    {
        if (_macPinchMonitor is not { } monitor)
            return;

        monitor.Magnify -= OnMacMagnify;
        monitor.Dispose();
        _macPinchMonitor = null;
    }

    /// <summary>
    ///     Turns a native AppKit magnify into a frame of a pinch: each event's magnification is a
    ///     step from the frame before it, not a scale against the gesture's start, so the steps are
    ///     accumulated by <see cref="PinchSession"/>. Frames are shown by scaling what is already
    ///     drawn, and only the end of the gesture reaches the zoom itself.
    /// </summary>
    private void OnMacMagnify(object? sender, MacMagnifyEventArgs e)
    {
        if (XamlRoot?.Content is not FrameworkElement root)
            return;

        // AppKit's locationInWindow is bottom-left-origin window coordinates; XAML's are
        // top-left-origin, so the flip needs the root element's own height. TransformToVisual then
        // carries the point into _scroll's space - the one every other handler anchors against -
        // without asking AppKit for an NSRect (see the remarks on MacPinchMonitor for why).
        var rootPoint = new Point(e.WindowX, root.ActualHeight - e.WindowY);
        var point = root.TransformToVisual(_scroll).TransformPoint(rootPoint);

        // A local monitor sees every magnify in the application, so several report views - or none
        // under the pointer - must not all zoom together.
        if (point.X < 0 || point.Y < 0 || point.X > _scroll.ActualWidth || point.Y > _scroll.ActualHeight)
            return;

        // Also when a gesture is somehow already under way: the anchor belongs to one gesture, and
        // carrying the previous one's in would zoom around a point nobody touched.
        if (e.Began || !_pinch.Running)
            _pinch.Begin(new ViewPoint(point.X, point.Y));

        if (e.Ended)
        {
            CommitMacPinch();
            return;
        }

        ShowPinch(1 + e.Magnification);
    }

    /// <summary>Shows one frame of the gesture through the canvas transform.</summary>
    private void ShowPinch(double step)
    {
        if (_pinch.MoveByStep(step) is not { } preview)
            return;

        _canvas.RenderTransformOrigin = new Point(0, 0);
        _canvas.RenderTransform = new MatrixTransform
        {
            Matrix = new Matrix(preview.Scale, 0, 0, preview.Scale, preview.OffsetX, preview.OffsetY)
        };
    }

    /// <summary>Ends a magnify gesture through the same path a wheel notch takes.</summary>
    private void CommitMacPinch()
    {
        if (_pinch.End() is not { } commit)
        {
            _canvas.RenderTransform = null;
            return;
        }

        var before = _presenter.EffectiveZoom;

        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByScale(commit.Factor);

        ApplyGestureZoom(before, new Point(commit.Anchor.X, commit.Anchor.Y), commit.Held);

        _canvas.RenderTransform = null;
    }

    private void AddInputHandlers()
    {
        Loaded += (_, _) => StartMacPinchMonitor();
        Unloaded += (_, _) => StopMacPinchMonitor();

        _canvas.PointerWheelChanged += OnPointerWheelChanged;
        _canvas.DoubleTapped += OnDoubleTapped;

        // Scale only: panning stays the ScrollViewer's, which is what moves the page and cell views
        // without the application repainting them.
        _canvas.ManipulationMode = ManipulationModes.Scale;
        _canvas.ManipulationStarted += OnManipulationStarted;
        _canvas.ManipulationDelta += OnManipulationDelta;
        _canvas.ManipulationCompleted += OnManipulationCompleted;
    }

    /// <summary>
    ///     A wheel notch under Ctrl or the platform's command key zooms around the pointer instead of
    ///     scrolling; a plain wheel notch is left for the <c>ScrollViewer</c> to handle as it always has.
    /// </summary>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if ((e.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) == 0)
            return;

        var point = e.GetCurrentPoint(_scroll);
        var before = _presenter.EffectiveZoom;

        // A wheel notch is a step from wherever the zoom already is, not from where a gesture
        // started, so beginning and stepping together on every notch is the correct call rather than
        // a shortcut - there is no multi-event gesture here for a start to belong to.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByStep(WheelZoom.StepForWindowsDelta(point.Properties.MouseWheelDelta));

        ApplyGestureZoom(before, point.Position);

        // Marks the event handled on the way up, before the ScrollViewer ever sees it - see the
        // routing note on this file.
        e.Handled = true;
    }

    /// <summary>
    ///     Magnifies around the tapped point, and puts the reader back where they were when tapped
    ///     again - so 100% goes to 200% and back to 100%, and a fit mode returns to that fit mode.
    /// </summary>
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var point = e.GetPosition(_scroll);

        _presenter.Gestures.DoubleTap();

        _zoomPublisher.Publish(_presenter.ZoomMode, _presenter.Zoom);

        _presenter.SetZoom(ZoomMode, Zoom, new ViewPoint(point.X, point.Y));
        AfterPresenterUpdate(immediate: false);

        e.Handled = true;
    }

    private void OnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        // The anchor belongs to one gesture, so it is taken once here: carrying a previous
        // gesture's anchor into this one would zoom around a point nobody touched. The position
        // arrives in the canvas's space - the whole document - and what the anchor needs is where
        // that lands in the viewport.
        var origin = _canvas.TransformToVisual(_scroll).TransformPoint(e.Position);

        _pinch.Begin(new ViewPoint(origin.X, origin.Y));

        e.Handled = true;
    }

    /// <summary>
    ///     Shows a frame of the gesture by scaling what is already drawn, instead of relaying out
    ///     every page and cell for it. The arithmetic is <see cref="PinchSession"/>'s; what is left
    ///     here is putting the matrix on the canvas.
    /// </summary>
    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        e.Handled = true;

        // Delta.Scale is the increment for this event, not a cumulative scale against the gesture's
        // start - the same shape WheelZoom.StepFor gives a wheel delta.
        if (_pinch.MoveByStep(e.Delta.Scale) is not { } preview)
            return;

        _canvas.RenderTransformOrigin = new Point(0, 0);
        _canvas.RenderTransform = new MatrixTransform
        {
            Matrix = new Matrix(preview.Scale, 0, 0, preview.Scale, preview.OffsetX, preview.OffsetY)
        };
    }

    /// <summary>
    ///     Ends the gesture: puts the zoom it reached through the same path a wheel notch takes, so
    ///     the relayout and the sharp cells are paid for once, and only then drops the transform.
    /// </summary>
    private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        e.Handled = true;

        if (_pinch.End() is not { } commit)
        {
            _canvas.RenderTransform = null;
            return;
        }

        var before = _presenter.EffectiveZoom;

        // BeginPinch first: the commit's factor is measured against the zoom the gesture started at,
        // and PinchByScale is the entry point that reads it that way.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByScale(commit.Factor);

        // Relayout and scroll first, while the preview still covers the canvas: dropping the
        // transform before the scroll has landed paints one frame of pages at the new zoom under
        // the old offset.
        ApplyGestureZoom(before, new Point(commit.Anchor.X, commit.Anchor.Y), commit.Held);

        _canvas.RenderTransform = null;
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
    ///     without this file repeating the arithmetic that produced it.
    /// </remarks>
    private void ApplyGestureZoom(double before, Point viewportPoint, DocumentPoint? held = null)
    {
        var zoom = _presenter.EffectiveZoom;

        if (Math.Abs(zoom - before) < before * ReportViewDefaults.ZoomStepThreshold)
            return;

        _zoomPublisher.Publish(_presenter.ZoomMode, _presenter.Zoom);

        _presenter.SetZoom(ZoomMode, Zoom, new ViewPoint(viewportPoint.X, viewportPoint.Y), held);
        AfterPresenterUpdate(immediate: true);
    }
}
