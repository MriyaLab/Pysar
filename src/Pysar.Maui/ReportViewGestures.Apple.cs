#if MACCATALYST
using ObjCRuntime;
using UIKit;

namespace Pysar.Maui;

/// <summary>
///     Trackpad zoom on Mac Catalyst.
/// </summary>
/// <remarks>
///     A trackpad pinch arrives as a native gesture on the scroll view, which the cross-platform
///     recogniser attached to the content never sees - so on this platform the report simply did not
///     zoom. Attaching <see cref="UIPinchGestureRecognizer"/> to the scroll view itself is what makes
///     the gesture reach the control; on iOS the cross-platform recogniser already works, so this is
///     compiled for Catalyst alone and never doubles up with it.
/// </remarks>
public partial class ReportView
{
    private UIPinchGestureRecognizer? _platformPinch;

    /// <summary>
    ///     The view <see cref="_platformPinch"/> was added to, so it can be taken off that same view
    ///     rather than off whichever one the handler holds by then.
    /// </summary>
    private UIView? _platformPinchView;

    partial void AddPlatformGestures()
    {
        if (_scroll.Handler?.PlatformView is not UIView view)
            return;

        // Against the view rather than against the recogniser: after Shell flyout navigation the
        // handler comes back with a new UIScrollView, and a recogniser still held from the previous
        // one is attached to a view nobody's fingers can reach - which is what left a report reopened
        // from the flyout no longer zooming.
        if (ReferenceEquals(_platformPinchView, view))
            return;

        RemovePlatformGestures();

        _platformPinch = new UIPinchGestureRecognizer(OnPlatformPinch)
        {
            // The scroll view keeps its own gestures: zooming must not cost the reader panning.
            ShouldRecognizeSimultaneously = (_, _) => true
        };

        _platformPinchView = view;
        view.AddGestureRecognizer(_platformPinch);
    }

    partial void RemovePlatformGestures()
    {
        // Handle checked rather than trusted: this runs from HandlerChanging, where the view is still
        // alive, but a disposed peer arriving by any other route must not throw out of teardown.
        if (_platformPinchView is { } view && view.Handle != NativeHandle.Zero && _platformPinch is { } recognizer)
            view.RemoveGestureRecognizer(recognizer);

        _platformPinch = null;
        _platformPinchView = null;
        _platformPinchActive = false;
    }

    private void OnPlatformPinch(UIPinchGestureRecognizer recognizer)
    {
        switch (recognizer.State)
        {
            case UIGestureRecognizerState.Began:
                _platformPinchActive = true;

                // Less the scroll position, which locationInView: has already added: this recogniser is
                // attached to the scroll view, and a UIScrollView's own coordinate system starts at its
                // content offset - so the point it reports is a point of the document, while the anchor
                // has to be a point of the viewport. Left as it came, the zoom held a point as far below
                // the fingers as the view was scrolled, and the document crept upwards through the
                // gesture.
                var start = recognizer.LocationInView(recognizer.View);

                BeginPinch(new Point(start.X - _scroll.ScrollX, start.Y - _scroll.ScrollY));
                break;

            case UIGestureRecognizerState.Changed:
                // Unlike the cross-platform recogniser, this one reports the scale against the start
                // of the gesture rather than against the previous frame.
                ShowPinchScale(recognizer.Scale);
                break;

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                _platformPinchActive = false;
                CommitPinch();
                break;
        }
    }
}
#endif
