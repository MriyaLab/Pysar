using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Helpers;

namespace Pysar.Skia.Layout;

/// <summary>
///     Pure measure phase: produces an immutable <see cref="LayoutNode"/> tree without
///     mutating any element. Coordinates are absolute on the ribbon the element sits on.
/// </summary>
public static class LayoutEngine
{
    public static async Task<LayoutNode> MeasureAsync(
        IReportElement element, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return element switch
        {
            Grid grid => await GridLayoutMeasurer.MeasureAsync(grid, constraint, ctx, ct),
            StackPanel panel => await StackLayoutMeasurer.MeasureAsync(panel, constraint, ctx, ct),
            Text text => MeasureText(text, constraint, ctx),
            IReportContainer container => await MeasureContainerAsync(container, constraint, ctx, ct),
            _ => MeasureBox(element, constraint, ctx)
        };
    }

    internal static SizeLength EffectiveWidth(IReportElement e, MeasureConstraint constraint) => constraint.WidthOverride ?? e.Size.Width;
    internal static SizeLength EffectiveHeight(IReportElement e, MeasureConstraint constraint) => constraint.HeightOverride ?? e.Size.Height;

    /// <summary>
    ///     Leaf box: Fixed → the value; Fill → available; Auto → the element's own content size when
    ///     it registered an <see cref="IElementMeasurer"/>, and 0 when it did not.
    /// </summary>
    private static LayoutNode MeasureBox(IReportElement element, MeasureConstraint constraint, MeasureContext ctx)
    {
        var avail = ApplyMargin(constraint.AvailableRect, element);

        var width = EffectiveWidth(element, constraint);
        var height = EffectiveHeight(element, constraint);

        var w = ResolveLength(width, avail.Width);
        var h = ResolveLength(height, avail.Height);

        if ((width.IsAuto || height.IsAuto) && ctx.Measurers.TryGet(element.GetType(), out var measurer))
        {
            // The already-resolved dimension is passed in, so an element that sizes itself against
            // its width - a square badge, a barcode of a fixed aspect - can read it.
            var content = measurer.Measure(element, (w, h), ctx);

            if (width.IsAuto) w = content.Width;
            if (height.IsAuto) h = content.Height;
        }

        (w, h) = SizeConstraints.Clamp(w, h, element.MinSize, element.MaxSize);

        var (left, top) = ResolvePosition(element, w, h, avail, constraint.IgnorePosition);

        return new LayoutNode(element, new Rect(left, top, left + w, top + h),
            LayoutNode.NoChildren, LayoutNode.NoCuts);
    }

    internal static float ResolveLength(SizeLength len, float available) =>
        len.IsFixed ? len.Value : len.IsFill ? available : 0f;

    /// <summary>
    ///     The element's box occupies its available rect deflated by its own Margin. A negative margin
    ///     inflates the rect, so the box extends outward (e.g. a full-bleed band beyond the content zone).
    /// </summary>
    private static Rect ApplyMargin(Rect available, IReportElement element)
    {
        var m = element.Margin;
        return new Rect(available.Left + m.Left, available.Top + m.Top,
                        available.Right - m.Right, available.Bottom - m.Bottom);
    }

    private static (float, float) ResolvePosition(IReportElement e, float w, float h, Rect availableRect, bool ignorePosition) =>
        ignorePosition
            ? (availableRect.Left, availableRect.Top)
            : PositionResolver.Resolve(e, w, h, availableRect);

    private static LayoutNode MeasureText(Text text, MeasureConstraint constraint, MeasureContext ctx)
    {
        // Same margin model as MeasureBox / MeasureContainer: margin sits outside the border-box.
        var avail = ApplyMargin(constraint.AvailableRect, text);
        var (contentW, contentH) = TextMeasurer.MeasureTextByTrimmingMode(text, avail, ctx.Scale);
        var effW = EffectiveWidth(text, constraint);
        var effH = EffectiveHeight(text, constraint);
        var w = effW.IsFixed ? effW.Value
              : effW.IsFill ? avail.Width
              : contentW + text.Padding.Left + text.Padding.Right;
        var h = effH.IsFixed ? effH.Value
              : effH.IsFill ? avail.Height
              : contentH + text.Padding.Top + text.Padding.Bottom;
        (w, h) = SizeConstraints.Clamp(w, h, text.MinSize, text.MaxSize);
        var (left, top) = ResolvePosition(text, w, h, avail, constraint.IgnorePosition);
        return new LayoutNode(text, new Rect(left, top, left + w, top + h),
            LayoutNode.NoChildren, LayoutNode.NoCuts);
    }

    private static async Task<LayoutNode> MeasureContainerAsync(
        IReportContainer element, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct)
    {
        var effW = EffectiveWidth(element, constraint);
        var effH = EffectiveHeight(element, constraint);
        var isAutoW = !effW.IsFixed && !effW.IsFill;
        var isAutoH = !effH.IsFixed && !effH.IsFill;

        // The box occupies the available rect deflated by the element's own Margin (negative → outward).
        var avail = ApplyMargin(constraint.AvailableRect, element);

        // Outer box from the known dimensions (Auto is refined after the children)
        var boxW = ResolveLength(effW, avail.Width);
        var boxH = ResolveLength(effH, avail.Height);
        var (left, top) = ResolvePosition(element, boxW, boxH, avail, constraint.IgnorePosition);

        // Content zone for the children (for Auto — the whole available rect, as in MeasurerHelper)
        var iL = element.Padding.Left + element.BorderThickness.Left;
        var iT = element.Padding.Top + element.BorderThickness.Top;
        var iR = element.Padding.Right + element.BorderThickness.Right;
        var iB = element.Padding.Bottom + element.BorderThickness.Bottom;
        var contentRect = new Rect(
            (isAutoW ? avail.Left : left) + iL,
            (isAutoH ? avail.Top : top) + iT,
            (isAutoW ? avail.Right : left + boxW) - iR,
            (isAutoH ? avail.Bottom : top + boxH) - iB);

        var children = new List<LayoutNode>();
        foreach (var child in element.Children)
        {
            ct.ThrowIfCancellationRequested();
            if (!child.IsVisible) continue;
            // Auto parents have no leftover space to distribute: a Fill child would otherwise stretch
            // to the full available window and inflate the Auto parent to match (Auto ≈ Fill). Treat
            // Fill as Auto here — same idea as star tracks collapsing to content in an Auto grid.
            SizeLength? widthOverride = isAutoW && child.Size.Width.IsFill ? SizeLength.Auto : null;
            SizeLength? heightOverride = isAutoH && child.Size.Height.IsFill ? SizeLength.Auto : null;
            children.Add(await MeasureAsync(child,
                new MeasureConstraint(contentRect, widthOverride, heightOverride), ctx, ct));
        }

        // Auto sizes from the children's extents; Fill height grows to max(available, content)
        if (children.Count > 0)
        {
            var minLeft = children.Min(n => n.Bounds.Left) - iL;
            var minTop = children.Min(n => n.Bounds.Top) - iT;
            var maxRight = children.Max(n => n.Bounds.Right) + iR;
            var maxBottom = children.Max(n => n.Bounds.Bottom) + iB;
            if (isAutoW) { left = element.Position.IsEmpty && !constraint.IgnorePosition ? minLeft : left; boxW = maxRight - minLeft; }
            if (isAutoH) { top = element.Position.IsEmpty && !constraint.IgnorePosition ? minTop : top; boxH = maxBottom - minTop; }
            if (effH.IsFill) boxH = Math.Max(boxH, maxBottom - top);       // max(window, content) rule
        }

        (boxW, boxH) = SizeConstraints.Clamp(boxW, boxH, element.MinSize, element.MaxSize);
        if (!constraint.IgnorePosition)
            (left, top) = PositionResolver.Resolve(element, boxW, boxH, avail);

        var cutHints = children.Count > 0
            ? children.Select(n => n.Bounds.Bottom).Where(y => y > top && y < top + boxH).Distinct().OrderBy(y => y).ToArray()
            : LayoutNode.NoCuts;

        return new LayoutNode(element, new Rect(left, top, left + boxW, top + boxH), children, cutHints);
    }
}
