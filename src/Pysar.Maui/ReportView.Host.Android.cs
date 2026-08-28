#if ANDROID
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Pysar.Maui;

/// <summary>
///     Makes extent and scroll take effect on the Android NestedScrollView in the same turn the
///     presenter asks for them. MAUI's WidthRequest / ScrollToAsync only land after a later layout
///     pass, which left one frame of pages at the new zoom under the old offset after a pinch
///     released - the same flash Mac Catalyst had before its native path.
/// </summary>
/// <remarks>
///     Android bidirectional scrolling is a <see cref="MauiScrollView"/> (vertical) wrapping a
///     <see cref="HorizontalScrollView"/>. The horizontal scroller must stay viewport-wide with a
///     document-wide child - sizing the scroller itself to the document removes the horizontal range
///     and clamps X to zero on every commit.
/// </remarks>
public partial class ReportView
{
    partial void SetExtentNative(double width, double height, ref bool handled)
    {
        if (_scroll.Handler?.PlatformView is not MauiScrollView scrollView)
            return;

        if (_content.Handler?.PlatformView is not AView contentView)
            return;

        var context = scrollView.Context;

        if (context is null)
            return;

        // Before the first layout the viewport is unknown; let MAUI measure normally.
        if (scrollView.Width <= 0 || scrollView.Height <= 0)
            return;

        var widthPx = Math.Max(1, (int)context.ToPixels(width));
        var heightPx = Math.Max(1, (int)context.ToPixels(height));
        var viewportWidthPx = scrollView.Width;
        var viewportHeightPx = scrollView.Height;

        var contentWidthSpec = AView.MeasureSpec.MakeMeasureSpec(widthPx, MeasureSpecMode.Exactly);
        var contentHeightSpec = AView.MeasureSpec.MakeMeasureSpec(heightPx, MeasureSpecMode.Exactly);

        // Resizing can clamp offsets and raise Scrolled; swallow that until ScrollToNative runs.
        _suppressScrollReaction++;
        try
        {
            // Document-sized content, and the inset panel MAUI inserts around it.
            contentView.Measure(contentWidthSpec, contentHeightSpec);
            contentView.Layout(0, 0, widthPx, heightPx);

            if (contentView.Parent is not AView inset || ReferenceEquals(inset, scrollView))
            {
                handled = true;
                return;
            }

            inset.Measure(contentWidthSpec, contentHeightSpec);
            inset.Layout(0, 0, widthPx, heightPx);

            // Horizontal scroller: viewport width × tall enough for the vertical parent to scroll.
            // Matches MauiScrollView.OnLayout for ScrollOrientation.Both.
            if (inset.Parent is HorizontalScrollView horizontal)
            {
                var horizontalHeightPx = Math.Max(viewportHeightPx, heightPx);
                var horizontalWidthSpec = AView.MeasureSpec.MakeMeasureSpec(
                    viewportWidthPx, MeasureSpecMode.Exactly);
                var horizontalHeightSpec = AView.MeasureSpec.MakeMeasureSpec(
                    horizontalHeightPx, MeasureSpecMode.Exactly);

                horizontal.Measure(horizontalWidthSpec, horizontalHeightSpec);
                horizontal.Layout(0, 0, viewportWidthPx, horizontalHeightPx);
            }
        }
        finally
        {
            _suppressScrollReaction--;
        }

        handled = true;
    }

    partial void ScrollToNative(double x, double y, ref bool handled)
    {
        if (_scroll.Handler?.PlatformView is not MauiScrollView scrollView)
            return;

        var context = scrollView.Context;

        if (context is null)
            return;

        var xPx = (int)context.ToPixels(x);
        var yPx = (int)context.ToPixels(y);

        // Scroll each axis on the view that owns it. MauiScrollView.ScrollTo(x,y) (JumpTo) does both
        // but NestedScrollView's ScrollChange then writes HorizontalOffset from its own ScrollX (0),
        // wiping the X the horizontal child just applied - so the reported offsets are corrected below.
        _suppressScrollReaction++;
        try
        {
            if (FindHorizontalScrollView(scrollView) is { } horizontal)
                horizontal.ScrollTo(xPx, 0);

            scrollView.ScrollTo(0, yPx);
            _scroll.SetScrolledPosition(x, y);
        }
        finally
        {
            _suppressScrollReaction--;
        }

        handled = true;
    }

    private static HorizontalScrollView? FindHorizontalScrollView(AView parent)
    {
        if (parent is HorizontalScrollView found)
            return found;

        if (parent is not ViewGroup group)
            return null;

        for (var i = 0; i < group.ChildCount; i++)
        {
            if (group.GetChildAt(i) is { } child && FindHorizontalScrollView(child) is { } match)
                return match;
        }

        return null;
    }
}
#endif
