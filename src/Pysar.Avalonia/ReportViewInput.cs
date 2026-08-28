using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
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
///     A throwaway probe of Avalonia 11.3.12 on macOS, with a <see cref="PinchGestureRecognizer"/>
///     attached to a hit-testable surface, found that a real trackpad pinch arrives as 1034
///     <see cref="InputElement.PointerWheelChangedEvent"/> events and zero
///     <see cref="InputElement.PinchEvent"/> ones - the platform never delivers a pinch gesture for a
///     trackpad. The modifiers on those wheel events were reliable (Control 309, Meta 176, None
///     549), which is what lets a modifier decide between scrolling and zooming below. The pinch
///     recogniser is still wired up, for the touch platforms a later plan covers, but no desktop
///     behaviour may depend on it - and nothing here claims it has been exercised on desktop, since
///     there is no sample application yet to run it through.
/// </remarks>
public partial class ReportView
{
    /// <summary>
    ///     Orders the two writes back to <see cref="ReportView.Zoom"/> and
    ///     <see cref="ReportView.ZoomMode"/> after an input handler has already told the presenter
    ///     what it did and where to anchor it, and flags that the property-changed handler in
    ///     <c>ReportView.cs</c> is seeing its own write rather than a fresh request - which is correct
    ///     for a menu or a binding but not for a zoom under the reader's pointer.
    /// </summary>
    private readonly ZoomPublisher _zoomPublisher;

    /// <summary>Set between a pinch's first frame and its last, so the first frame can begin it.</summary>
    private bool _pinchActive;

    /// <summary>
    ///     The running pinch. Its frames are shown through the canvas's own transform, which is the
    ///     only time what is on screen deliberately disagrees with what the presenter has been told.
    /// </summary>
    private readonly PinchSession _pinch;

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

        // DEBUG: t_commit -> t_first_centre_tile -> t_viewport_full via SamplePinchCommitPerf.
        BeginPinchCommitPerf();

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

        _scroll.PointerPressed += OnPointerPressed;

        var pinch = new PinchGestureRecognizer();
        _canvas.GestureRecognizers.Add(pinch);
        _canvas.AddHandler(InputElement.PinchEvent, OnPinch);
        _canvas.AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
    }

    /// <summary>
    ///     A wheel notch under Ctrl or Meta zooms around the pointer instead of scrolling; a plain
    ///     wheel notch is left for the <see cref="ScrollViewer"/> to handle as it always has.
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
            return;

        var before = _presenter.EffectiveZoom;

        // A wheel notch is a step from wherever the zoom already is, not from where a gesture
        // started, so beginning and stepping together on every notch is the correct call rather than
        // a shortcut - there is no multi-event gesture here for a start to belong to.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByStep(WheelZoom.StepFor(e.Delta.Y));

        ApplyGestureZoom(before, e.GetPosition(_scroll));

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
    ///     Routes a pinch frame to <see cref="GestureModel"/>. Wired up for the touch platforms a
    ///     later plan covers; on desktop a trackpad pinch never reaches here - see the remarks on
    ///     this file for the measurement that found that out.
    /// </summary>
    /// <remarks>
    ///     A second, unrelated pinch state machine: Avalonia's own <see cref="PinchGestureRecognizer"/>,
    ///     which relays every frame out rather than scaling what is already drawn the way
    ///     <see cref="_pinch"/> does. It is kept beside the thing it was extracted to replace only
    ///     because it is the one path left for the touch platforms; do not reach for
    ///     <see cref="_pinchActive"/> or this handler on desktop.
    /// </remarks>
    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var before = _presenter.EffectiveZoom;

        if (!_pinchActive)
        {
            _presenter.Gestures.BeginPinch();
            _pinchActive = true;
        }

        // The recogniser reports the origin against the canvas, which is the whole document; what
        // the anchor needs is where that lands in the viewport.
        var viewportPoint = new Point(
            e.ScaleOrigin.X - _scroll.Offset.X,
            e.ScaleOrigin.Y - _scroll.Offset.Y);

        _presenter.Gestures.PinchByScale(e.Scale);

        ApplyGestureZoom(before, viewportPoint);

        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _pinchActive = false;

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
