#if IOS || MACCATALYST
using CoreGraphics;
using UIKit;

namespace Pysar.Maui;

/// <summary>
///     Makes extent and scroll take effect on the UIScrollView in the same turn the presenter asks
///     for them. MAUI's WidthRequest / ScrollToAsync only land after a later layout pass, which left
///     one frame of pages at the new zoom under the old offset after a pinch released - the jump on
///     the first and last pages.
/// </summary>
public partial class ReportView
{
    /// <summary>
    ///     How far a size may be out before it is written again: under half a unit is under a device
    ///     pixel on every display this runs on, and so nothing the reader could see.
    /// </summary>
    private const double SizeTolerance = 0.5;

    partial void SetExtentNative(double width, double height, ref bool handled)
    {
        if (_scroll.Handler?.PlatformView is not UIScrollView scrollView)
            return;

        // Only when it has actually changed. UIKit re-clamps the content offset and relays the scroll
        // view out on every write of the content size, whether or not the value differs - and this is
        // reached from a scroll as well as from a zoom, so an unconditional write landed on every
        // frame of one and was felt as the view snagging under a fast scroll.
        var size = scrollView.ContentSize;

        if (Differs(size.Width, width) || Differs(size.Height, height))
            scrollView.ContentSize = new CGSize(width, height);

        if (_content.Handler?.PlatformView is UIView contentView)
        {
            var frame = contentView.Frame;

            if (Differs(frame.Width, width) || Differs(frame.Height, height))
                contentView.Frame = new CGRect(frame.X, frame.Y, width, height);
        }

        handled = true;

        static bool Differs(double current, double wanted) => Math.Abs(current - wanted) >= SizeTolerance;
    }

    partial void ScrollToNative(double x, double y, ref bool handled)
    {
        if (_scroll.Handler?.PlatformView is not UIScrollView scrollView)
            return;

        scrollView.SetContentOffset(new CGPoint(x, y), animated: false);
        handled = true;
    }
}
#endif
