using System.Windows;
using System.Windows.Input;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Zoom;

namespace Pysar.Wpf;

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
///     Touch pinch and trackpad magnify are not wired for v1; only Ctrl+wheel and double-click.
/// </remarks>
public partial class ReportView
{
    partial void AddInputHandlers()
    {
        // Preview, not the bubble MouseWheel: ScrollViewer handles the wheel on the bubble path, so a
        // bubble handler here would only see the event after scrolling had already run. Preview runs
        // first, so marking it handled for a zoom keeps the scroll from also happening.
        _scroll.PreviewMouseWheel += OnPreviewMouseWheel;
        _scroll.MouseDoubleClick += OnMouseDoubleClick;
    }

    /// <summary>
    ///     A wheel notch under Ctrl zooms around the pointer instead of scrolling; a plain wheel
    ///     notch is left for the <see cref="System.Windows.Controls.ScrollViewer"/> to handle as it
    ///     always has.
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        var before = _presenter.EffectiveZoom;

        // A wheel notch is a step from wherever the zoom already is, not from where a gesture
        // started, so beginning and stepping together on every notch is the correct call rather than
        // a shortcut - there is no multi-event gesture here for a start to belong to.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByStep(WheelZoom.StepForWindowsDelta(e.Delta));

        ApplyGestureZoom(before, e.GetPosition(_scroll));

        e.Handled = true;
    }

    /// <summary>
    ///     Magnifies around the clicked point, and puts the reader back where they were when clicked
    ///     again - so 100% goes to 200% and back to 100%, and a fit mode returns to that fit mode.
    /// </summary>
    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(_scroll);

        _presenter.Gestures.DoubleTap();

        var mode = _presenter.ZoomMode;
        var zoom = _presenter.Zoom;

        _zoomPublisher.Publish(mode, zoom);

        _presenter.SetZoom(ZoomMode, Zoom, new ViewPoint(point.X, point.Y));
        AfterPresenterUpdate(immediate: false);
    }

    private static ViewPoint ToViewPoint(Point point) => new(point.X, point.Y);

    /// <summary>
    ///     Publishes the zoom an input handler just applied, if it moved far enough from the one
    ///     before it, and refreshes anchored at that handler's point so what is under the pointer
    ///     stays there.
    /// </summary>
    /// <remarks>
    ///     The gesture model has already applied the new zoom to the presenter's own state; reading
    ///     it back through <see cref="ReportViewPresenter.Zoom"/> and
    ///     <see cref="ReportViewPresenter.ZoomMode"/> is what lets the bindable properties mirror it
    ///     without this file repeating the arithmetic that produced it. A wheel always lands on a
    ///     custom factor, so the mode read back here is always <see cref="ReportZoomMode.Custom"/>.
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
