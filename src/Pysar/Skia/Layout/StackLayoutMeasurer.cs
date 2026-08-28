using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Skia.Layout;

/// <summary>
///     Stack measurement: children are laid out along the panel's <see cref="StackPanel.Orientation"/> —
///     vertically top-to-bottom or horizontally left-to-right. Cut hints mark the row boundaries for
///     pagination: every child's bottom edge in vertical mode, the single row bottom in horizontal mode.
/// </summary>
internal static class StackLayoutMeasurer
{
    public static Task<LayoutNode> MeasureAsync(StackPanel panel, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct) =>
        panel.Orientation switch
        {
            StackOrientation.Vertical => MeasureVerticalAsync(panel, constraint, ctx, ct),
            StackOrientation.Horizontal => MeasureHorizontalAsync(panel, constraint, ctx, ct),
            _ => throw new InvalidOperationException($"Unsupported {nameof(StackOrientation)}: {panel.Orientation}")
        };

    private static async Task<LayoutNode> MeasureVerticalAsync(StackPanel panel, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct)
    {
        // Panel margin sits outside the border-box (same model as MeasureContainer / Frame).
        var available = ApplyMargin(constraint.AvailableRect, panel.Margin);
        var effectiveWidth = LayoutEngine.EffectiveWidth(panel, constraint);
        var effectiveHeight = LayoutEngine.EffectiveHeight(panel, constraint);
        var isAutoWidth = !effectiveWidth.IsFixed && !effectiveWidth.IsFill;

        var insetLeft = panel.Padding.Left + panel.BorderThickness.Left;
        var insetTop = panel.Padding.Top + panel.BorderThickness.Top;
        var insetRight = panel.Padding.Right + panel.BorderThickness.Right;
        var insetBottom = panel.Padding.Bottom + panel.BorderThickness.Bottom;

        // Phase 1: measure children to determine content extents (use available rect for probing).
        var probeContentLeft = available.Left + insetLeft;
        var probeContentWidth = (isAutoWidth ? available.Width : effectiveWidth.IsFixed ? effectiveWidth.Value : available.Width) - insetLeft - insetRight;
        var probeContentTop = available.Top + insetTop;

        var spacing = panel.Spacing;
        var visible = panel.Children.Where(ch => ch.IsVisible).ToList();

        var children = new List<LayoutNode>();
        var y = probeContentTop;
        for (var i = 0; i < visible.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var child = visible[i];

            // Leave Fill as Fill so child margins expand/inset against the content width.
            var childRect = new Rect(probeContentLeft, y, probeContentLeft + probeContentWidth, available.Bottom);
            var node = await LayoutEngine.MeasureAsync(child, new MeasureConstraint(childRect), ctx, ct);
            children.Add(node);
            y = node.Bounds.Bottom + child.Margin.Bottom;
            if (i < visible.Count - 1)
                y += spacing;
        }

        // Phase 2: compute final boxWidth/boxHeight from children's extents.
        var contentBottom = children.Count > 0 ? y : probeContentTop;
        var boxWidth = effectiveWidth.IsFixed ? effectiveWidth.Value : effectiveWidth.IsFill ? available.Width : 0f;
        if (isAutoWidth)
        {
            var maxRight = children.Count > 0 ? children.Max(n => n.Bounds.Right) : probeContentLeft;
            boxWidth = (maxRight + insetRight) - available.Left;
        }

        float boxHeight;
        if (effectiveHeight.IsFixed) boxHeight = effectiveHeight.Value;
        else
        {
            var contentH = (contentBottom - probeContentTop) + insetTop + insetBottom;
            boxHeight = effectiveHeight.IsFill ? Math.Max(available.Height, contentH) : contentH;
        }

        (boxWidth, boxHeight) = SizeConstraints.Clamp(boxWidth, boxHeight, panel.MinSize, panel.MaxSize);

        // Phase 3: resolve position with correct dimensions.
        var (originLeft, originTop) = constraint.IgnorePosition
            ? (available.Left, available.Top)
            : PositionResolver.Resolve(panel, boxWidth, boxHeight, available);

        // Phase 4: reposition children relative to the resolved origin.
        var contentLeft = originLeft + insetLeft;
        var contentWidth = (isAutoWidth ? boxWidth : effectiveWidth.IsFixed ? effectiveWidth.Value : available.Width) - insetLeft - insetRight;
        var contentTop = originTop + insetTop;

        var repositionedChildren = new List<LayoutNode>();
        var y2 = contentTop;
        for (var i = 0; i < visible.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var child = visible[i];

            var childRect = new Rect(contentLeft, y2, contentLeft + contentWidth, available.Bottom);
            var node = await LayoutEngine.MeasureAsync(child, new MeasureConstraint(childRect), ctx, ct);
            repositionedChildren.Add(node);
            y2 = node.Bounds.Bottom + child.Margin.Bottom;
            if (i < visible.Count - 1)
                y2 += spacing;
        }

        var cutHints = repositionedChildren.Count > 0 ? repositionedChildren.Select(n => n.Bounds.Bottom).ToArray() : LayoutNode.NoCuts;

        return new LayoutNode(panel, new Rect(originLeft, originTop, originLeft + boxWidth, originTop + boxHeight), repositionedChildren, cutHints);
    }

    private static async Task<LayoutNode> MeasureHorizontalAsync(StackPanel panel, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct)
    {
        var available = ApplyMargin(constraint.AvailableRect, panel.Margin);
        var effectiveWidth = LayoutEngine.EffectiveWidth(panel, constraint);
        var effectiveHeight = LayoutEngine.EffectiveHeight(panel, constraint);
        var isAutoWidth = !effectiveWidth.IsFixed && !effectiveWidth.IsFill;
        var isAutoHeight = !effectiveHeight.IsFixed && !effectiveHeight.IsFill;

        var insetLeft = panel.Padding.Left + panel.BorderThickness.Left;
        var insetTop = panel.Padding.Top + panel.BorderThickness.Top;
        var insetRight = panel.Padding.Right + panel.BorderThickness.Right;
        var insetBottom = panel.Padding.Bottom + panel.BorderThickness.Bottom;

        var boxWidth = effectiveWidth.IsFixed ? effectiveWidth.Value : effectiveWidth.IsFill ? available.Width : 0f;
        var boxHeight = effectiveHeight.IsFixed ? effectiveHeight.Value : effectiveHeight.IsFill ? available.Height : 0f;

        // Phase 1: measure children to determine content extents (use available rect for probing).
        var probeContentLeft = available.Left + insetLeft;
        var probeContentTop = available.Top + insetTop;
        var probeContentHeight = Math.Max(0, (isAutoHeight ? available.Height : boxHeight) - insetTop - insetBottom);
        var probeContentWidth = Math.Max(0, (isAutoWidth ? available.Width : boxWidth) - insetLeft - insetRight);

        var visible = panel.Children.Where(ch => ch.IsVisible).ToList();

        // Pass 1: resolve the main-axis size of every child — Fixed directly, Auto by probing
        // (measurement is pure; the probed position is discarded), Fill deferred to pass 2.
        // Slot widths include horizontal margin so siblings do not overlap.
        var widths = new float[visible.Count];
        var fillIndexes = new List<int>();
        for (var index = 0; index < visible.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var child = visible[index];
            if (child.Size.Width.IsFixed)
            {
                widths[index] = child.Size.Width.Value + child.Margin.Left + child.Margin.Right;
                continue;
            }

            if (child.Size.Width.IsFill)
            {
                fillIndexes.Add(index);
                continue;
            }

            var probeRect = new Rect(probeContentLeft, probeContentTop, available.Right, probeContentTop + probeContentHeight);
            var probe = await LayoutEngine.MeasureAsync(child, new MeasureConstraint(probeRect), ctx, ct);
            widths[index] = probe.Bounds.Width + child.Margin.Left + child.Margin.Right;
        }

        // Pass 2: Fill children split the content width left by Fixed/Auto children and gaps equally.
        var spacing = panel.Spacing;
        var gapTotal = spacing * Math.Max(0, visible.Count - 1);
        var share = fillIndexes.Count > 0
            ? Math.Max(0, probeContentWidth - widths.Sum() - gapTotal) / fillIndexes.Count
            : 0f;
        foreach (var index in fillIndexes) widths[index] = share;

        // Phase 2: compute final boxWidth/boxHeight from children's extents.
        // Pass 3: final placement in document order, left to right (using probe origin).
        var probeChildren = new List<LayoutNode>();
        var x = probeContentLeft;
        for (var index = 0; index < visible.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var child = visible[index];
            var slot = new Rect(x, probeContentTop, x + widths[index], probeContentTop + probeContentHeight);
            // No Fixed pin: Fill resolves against ApplyMargin(slot) so child margins work.
            var node = await LayoutEngine.MeasureAsync(child, new MeasureConstraint(slot), ctx, ct);
            probeChildren.Add(node);
            x += widths[index];
            if (index < visible.Count - 1)
                x += spacing;
        }

        var contentRight = probeChildren.Count > 0 ? x : probeContentLeft;
        if (isAutoHeight)
        {
            var maxBottom = probeChildren.Count > 0 ? probeChildren.Max(n => n.Bounds.Bottom) : probeContentTop;
            // Include the bottom margin of the tallest child so Auto height clears it.
            var tallest = probeChildren.Count > 0
                ? probeChildren.MaxBy(n => n.Bounds.Bottom)!
                : null;
            var bottomMargin = tallest is null ? 0f : tallest.Element.Margin.Bottom;
            boxHeight = (maxBottom + bottomMargin + insetBottom) - available.Top;
        }

        float finalWidth;
        if (effectiveWidth.IsFixed) finalWidth = effectiveWidth.Value;
        else
        {
            var contentW = (contentRight - probeContentLeft) + insetLeft + insetRight;
            finalWidth = effectiveWidth.IsFill ? Math.Max(available.Width, contentW) : contentW;
        }

        (finalWidth, boxHeight) = SizeConstraints.Clamp(finalWidth, boxHeight, panel.MinSize, panel.MaxSize);

        // Phase 3: resolve position with correct dimensions.
        var (originLeft, originTop) = constraint.IgnorePosition
            ? (available.Left, available.Top)
            : PositionResolver.Resolve(panel, finalWidth, boxHeight, available);

        // Phase 4: reposition children relative to the resolved origin.
        var contentLeft = originLeft + insetLeft;
        var contentTop = originTop + insetTop;
        var contentHeight = (isAutoHeight ? boxHeight : effectiveHeight.IsFixed ? effectiveHeight.Value : available.Height) - insetTop - insetBottom;

        var children = new List<LayoutNode>();
        var x2 = contentLeft;
        for (var index = 0; index < visible.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var child = visible[index];
            var slot = new Rect(x2, contentTop, x2 + widths[index], contentTop + contentHeight);
            var node = await LayoutEngine.MeasureAsync(child, new MeasureConstraint(slot), ctx, ct);
            children.Add(node);
            x2 += widths[index];
            if (index < visible.Count - 1)
                x2 += spacing;
        }

        // A horizontal row is unbreakable: the only cut hint is the row's bottom edge.
        var cutHints = children.Count > 0
            ? new[] { children.Max(n => n.Bounds.Bottom) }
            : LayoutNode.NoCuts;

        return new LayoutNode(panel, new Rect(originLeft, originTop, originLeft + finalWidth, originTop + boxHeight), children, cutHints);
    }

    private static Rect ApplyMargin(Rect available, Thickness margin) =>
        new(available.Left + margin.Left, available.Top + margin.Top,
            available.Right - margin.Right, available.Bottom - margin.Bottom);
}
