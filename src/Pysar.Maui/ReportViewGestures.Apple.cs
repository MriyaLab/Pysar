#if MACCATALYST
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

    partial void AddPlatformGestures()
    {
        if (_platformPinch is not null || _scroll.Handler?.PlatformView is not UIView view)
            return;

        _platformPinch = new UIPinchGestureRecognizer(OnPlatformPinch)
        {
            // The scroll view keeps its own gestures: zooming must not cost the reader panning.
            ShouldRecognizeSimultaneously = (_, _) => true
        };

        view.AddGestureRecognizer(_platformPinch);
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
