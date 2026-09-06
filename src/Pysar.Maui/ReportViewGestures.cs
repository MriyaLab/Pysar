using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Zoom;

namespace Pysar.Maui;

/// <summary>
///     The zoom gestures a document viewer is expected to have: pinch to zoom, and double tap to
///     magnify and to come back to where the reader was.
/// </summary>
/// <remarks>
///     The arithmetic - how a step or a scale turns into a zoom, what a double tap toggles between, and
///     what a pinch in progress should look like - lives in the viewer core, reached through
///     <see cref="PinchSession"/> and the presenter's <c>Gestures</c>. What is left here is recognising
///     the gestures themselves, turning a platform's point into the viewport point the zoom is
///     anchored around, and drawing what the session says a running gesture looks like.
///     <para>
///     A pinch relays nothing out while it runs: it scales what is already drawn, and one relayout at
///     the end pays for the zoom it reached. See <see cref="Show"/> for what that avoids.
///     </para>
/// </remarks>
public partial class ReportView
{
    /// <summary>The running pinch, shown by scaling the content rather than relaying it out.</summary>
    private readonly PinchSession _pinch;

    /// <summary>
    ///     Set while a platform gesture is driving the zoom, so the cross-platform recogniser stays
    ///     out of the way on a platform where both happen to fire. Only ever written by a platform
    ///     with a native recogniser of its own (Android, Catalyst) - on one without it stays false,
    ///     which is the correct answer there too, so leaving it unassigned is not a bug.
    /// </summary>
    private bool _platformPinchActive = false;

    private void AddGestures()
    {
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;

        // On the content rather than the scroll view: a scroll view hands its gestures to the
        // platform's own scrolling, and a recogniser attached there never sees the second tap.
        _content.GestureRecognizers.Add(pinch);
        _content.GestureRecognizers.Add(doubleTap);

        // A platform recogniser lives on the native scroll view, and Shell flyout navigation
        // disconnects the handler and builds a new native view when the page comes back - so the
        // recognisers have to move with it, or the reader returns to a report that no longer zooms.
        // Dropped from HandlerChanging, which is the last moment the old native view is still there
        // to be unhooked from; by HandlerChanged it is already gone.
        _scroll.HandlerChanging += (_, e) =>
        {
            if (e.OldHandler is not null)
                RemovePlatformGestures();
        };

        _scroll.HandlerChanged += (_, _) => AddPlatformGestures();
    }

    /// <summary>
    ///     Hook for gestures the cross-platform recognisers do not deliver on a given platform.
    /// </summary>
    partial void AddPlatformGestures();

    /// <summary>
    ///     Unhooks what <see cref="AddPlatformGestures"/> attached, so the next native view can be
    ///     wired in its turn. Called while the view it attached to is still alive.
    /// </summary>
    partial void RemovePlatformGestures();

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (_platformPinchActive)
            return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                // The gesture reports its origin against the content, which is the whole document;
                // what the anchor needs is where that lands in the viewport.
                BeginPinch(new Point(
                    e.ScaleOrigin.X * _content.Width - _scroll.ScrollX,
                    e.ScaleOrigin.Y * _content.Height - _scroll.ScrollY));
                break;

            case GestureStatus.Running:
                ShowPinchStep(e.Scale);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                CommitPinch();
                break;
        }
    }

    /// <summary>Starts a gesture anchored at <paramref name="anchor"/>, a point of the viewport.</summary>
    private void BeginPinch(Point anchor) => _pinch.Begin(new ViewPoint(anchor.X, anchor.Y));

    /// <summary>Shows a frame reported as a step against the previous frame of the same gesture.</summary>
    private void ShowPinchStep(double step) => Show(_pinch.MoveByStep(step));

    /// <summary>Shows a frame reported as a scale against the zoom the gesture began at.</summary>
    private void ShowPinchScale(double scale) => Show(_pinch.MoveByScale(scale));

    /// <summary>
    ///     Draws what <see cref="PinchSession"/> says the frame looks like, by scaling the content.
    /// </summary>
    /// <remarks>
    ///     Relaying pages out per frame instead left the view showing pixels of the previous zoom
    ///     wherever the geometry had moved away from - blank bands at the top of a page, a page header
    ///     ghosted into the gap below it - and they survived until a scroll disturbed them. What to
    ///     draw is <see cref="PinchSession"/>'s arithmetic, not this file's: the release has to land
    ///     exactly where the gesture was showing, and only the presenter behind it knows the space
    ///     around the pages, the centring of a document narrower than the viewport, and how far the
    ///     document scrolls. The scroll position is left where the gesture found it, since the platform
    ///     would clamp it against an extent that still describes the zoom the gesture started at.
    /// </remarks>
    private void Show(ZoomPreview? frame)
    {
        // No frame is the ordinary case, not an error: the session reports nothing whenever the
        // gesture has already reached the zoom limits, whenever a step arrives after the gesture
        // ended - which Catalyst does, with two recognisers over the same fingers - and whenever
        // there is no document under the anchor. Most calls during a pinch held at the limit end
        // here.
        if (frame is not { } preview)
            return;

        // MAUI scales a view about its own anchor, so the translation the preview asks for is
        // expressed as that anchor: scaling by s about f maps p to s*p + (1 - s)*f, which is the
        // preview's offset when f is offset / (1 - s). At a scale of one there is nothing to show
        // and no such point.
        var rest = 1 - preview.Scale;

        if (Math.Abs(rest) < 1e-6)
            return;

        _content.AnchorX = preview.OffsetX / rest / Math.Max(1, _content.Width);
        _content.AnchorY = preview.OffsetY / rest / Math.Max(1, _content.Height);
        _content.Scale = preview.Scale;
    }

    /// <summary>
    ///     Ends the gesture: puts the zoom it reached through the same path a double tap takes, so
    ///     the relayout and the sharp cells are paid for once.
    /// </summary>
    private void CommitPinch()
    {
        if (_pinch.End() is not { } commit)
        {
            // Suppressed across the clear for the same reason as the commit path below: dropping
            // the scale is itself something the platform can report as a scroll.
            _suppressScrollReaction++;
            try
            {
                ClearPinchPreview();
            }
            finally
            {
                _suppressScrollReaction--;
            }

            return;
        }

        var before = _presenter.EffectiveZoom;

        // BeginPinch first: the commit's factor is measured against the zoom the gesture started
        // at, and PinchByScale is the entry point that reads it that way.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByScale(commit.Factor);

        // Suppression opens BEFORE the clear, not after. Dropping _content.Scale from 2 back to 1
        // is a change the platform can report as a scroll, and until this task that write was
        // guarded only by _pinchShowing, which was still true because MAUI cleared it last of all.
        // PinchSession.Running goes false inside End(), so the flag that used to cover this window
        // is gone and the suppression counter has to cover it instead.
        _suppressScrollReaction++;
        try
        {
            // Drop Scale/Anchor before SetExtent. MAUI's anchor is a fraction of the view size;
            // growing the content while a non-1 Scale is still applied moves the anchor in pixels
            // and the release jumps. Avalonia and Blazor keep a layout-independent transform over
            // the relayout; here the native extent/scroll path makes the real geometry effective in
            // this same turn, so the covering scale is not needed and would only fight the size
            // change.
            ClearPinchPreview();

            ApplyGestureZoom(before, new Point(commit.Anchor.X, commit.Anchor.Y), commit.Held);

            // SetLayoutBounds only stores rects; VisualElement.Arrange does not walk children. Push
            // the new frames to the platform before the next paint.
            var width = Math.Max(1, _content.WidthRequest);
            var height = Math.Max(1, _content.HeightRequest);
            _content.CrossPlatformArrange(new Rect(0, 0, width, height));
        }
        finally
        {
            _suppressScrollReaction--;
        }
    }

    /// <summary>
    ///     Clears the pinch Scale/Anchor. The caller is responsible for suppressing the scroll
    ///     reaction around this: dropping the scale back to 1 is itself a change the platform can
    ///     report as a scroll.
    /// </summary>
    private void ClearPinchPreview()
    {
        _content.Scale = 1;
        _content.AnchorX = 0.5;
        _content.AnchorY = 0.5;
    }

    /// <summary>
    ///     Publishes the zoom a gesture just applied, if it moved far enough from the one before it,
    ///     and refreshes anchored at the gesture's point so what is under the fingers stays there.
    /// </summary>
    /// <remarks>
    ///     The gesture model has already applied the new zoom to the presenter's own state; reading
    ///     it back through <see cref="ReportViewPresenter.Zoom"/> and
    ///     <see cref="ReportViewPresenter.ZoomMode"/> is what lets the bindable properties mirror it
    ///     without this file repeating the arithmetic that produced it. A pinch always lands on a
    ///     custom factor, so the mode read back here is always <see cref="ReportZoomMode.Custom"/>.
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

    /// <summary>
    ///     Magnifies around the tapped point, and puts the reader back where they were when tapped
    ///     again - so 100% goes to 200% and back to 100%, and a fit mode returns to that fit mode.
    /// </summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        var point = e.GetPosition(_scroll) ?? new Point(ViewportWidth / 2, ViewportHeight / 2);

        _presenter.Gestures.DoubleTap();

        var mode = _presenter.ZoomMode;
        var zoom = _presenter.Zoom;

        _zoomPublisher.Publish(mode, zoom);

        _presenter.SetZoom(ZoomMode, Zoom, new ViewPoint(point.X, point.Y));
        AfterPresenterUpdate(immediate: false);
    }
}
