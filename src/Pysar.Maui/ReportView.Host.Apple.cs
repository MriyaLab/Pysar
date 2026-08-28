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
    partial void SetExtentNative(double width, double height, ref bool handled)
    {
        if (_scroll.Handler?.PlatformView is not UIScrollView scrollView)
            return;

        scrollView.ContentSize = new CGSize(width, height);

        if (_content.Handler?.PlatformView is UIView contentView)
        {
            var frame = contentView.Frame;
            contentView.Frame = new CGRect(frame.X, frame.Y, width, height);
        }

        handled = true;
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
