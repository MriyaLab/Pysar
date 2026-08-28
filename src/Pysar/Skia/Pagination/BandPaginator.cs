using Pysar.Core.Enums;
using Pysar.Elements;
using Pysar.Skia.Layout;

namespace Pysar.Skia.Pagination;

/// <summary>
///     Slices the flow ribbon into page windows. Cuts prefer natural boundaries (row cut hints,
///     band edges) over mid-row breaks, honor <see cref="Band.KeepTogether"/>, and always respect
///     forced <see cref="PageBreak"/> markers. A progress invariant guarantees every slice advances.
/// </summary>
public static class BandPaginator
{
    public static IReadOnlyList<PageSlice> Paginate(
        IReadOnlyList<LayoutNode> flow, float windowHeight, float repeatHeaderHeight = 0f)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowHeight);
        if (repeatHeaderHeight > 0f && repeatHeaderHeight >= windowHeight)
            throw new InvalidOperationException(
                $"Repeat detail header ({repeatHeaderHeight}pt) does not fit the content window ({windowHeight}pt).");

        var flowEnd = flow.Count > 0 ? flow.Max(n => n.Bounds.Bottom) : 0;
        // Template-only page (empty flow, page bands claim the whole zone): one empty flow slice.
        if (windowHeight == 0f)
        {
            if (flowEnd > 0f)
                throw new InvalidOperationException(
                    "Content window height is 0pt but the flow has content.");
            return [new PageSlice(0, 0)];
        }

        var slices = new List<PageSlice>();
        // Slice from the flow origin (0 = the top of the content window), not the first band's top edge.
        // Positive top margins remain empty space below the window top. Negative top margins are leading
        // overflow rendered into the preceding template region and must not consume pagination height.
        var start = 0f;

        while (start < flowEnd)
        {
            // Continuation pages reserve the repeated header at the top of the window.
            var effWindow = slices.Count == 0 ? windowHeight : windowHeight - repeatHeaderHeight;
            var windowEnd = start + effWindow;
            float cut = windowEnd;

            // Natural cut when content overflows the window.
            if (windowEnd < flowEnd)
            {
                var candidates = CollectCuts(flow, start, windowEnd).ToList();

                // KeepTogether: a band crossing the bottom edge but fitting a full window → cut before it.
                foreach (var band in flow.Where(n => n.Element is Band { KeepTogether: true }))
                    if (band.Bounds.Top > start && band.Bounds.Top < windowEnd &&
                        band.Bounds.Bottom > windowEnd && band.Bounds.Height <= effWindow)
                        candidates = candidates.Where(c => c <= band.Bounds.Top).Append(band.Bounds.Top).ToList();

                var best = candidates.Where(c => c > start && c <= windowEnd).DefaultIfEmpty(windowEnd).Max();
                cut = best > start ? best : windowEnd;             // progress invariant
            }

            // Forced page breaks always apply, even when the remaining content would otherwise fit.
            // The earliest forced break in (start, windowEnd] wins if it precedes the natural cut.
            var forced = EarliestForcedCut(flow, start, windowEnd);
            if (forced is float f && f > start && f < cut)
                cut = f;

            slices.Add(new PageSlice(start, Math.Min(cut, windowEnd)));
            start = slices[^1].End;
        }

        return slices.Count > 0 ? slices : [new PageSlice(0, windowHeight)];
    }

    /// <summary>
    ///     Earliest forced cut in (start, windowEnd]: a band's <see cref="PageBreakMode.Before"/> at its
    ///     top, <see cref="PageBreakMode.After"/> at its bottom, or a <see cref="PageBreak"/> flow marker
    ///     at its top edge (anywhere in the tree — e.g. inside a group footer).
    /// </summary>
    private static float? EarliestForcedCut(IReadOnlyList<LayoutNode> flow, float start, float windowEnd)
    {
        float? earliest = null;
        void Consider(float y)
        {
            if (y > start && y <= windowEnd)
                earliest = earliest is float e ? Math.Min(e, y) : y;
        }

        foreach (var node in flow)
        {
            if (node.Element is Band band)
            {
                if (band.PageBreak == PageBreakMode.Before) Consider(node.Bounds.Top);
                if (band.PageBreak == PageBreakMode.After) Consider(node.Bounds.Bottom);
            }
            CollectPageBreakMarkers(node, Consider);
        }
        return earliest;
    }

    /// <summary>Reports the top edge of every <see cref="PageBreak"/> marker in the node's subtree.</summary>
    private static void CollectPageBreakMarkers(LayoutNode node, Action<float> consider)
    {
        if (node.Element is PageBreak) consider(node.Bounds.Top);
        foreach (var child in node.Children)
            CollectPageBreakMarkers(child, consider);
    }

    private static IEnumerable<float> CollectCuts(IReadOnlyList<LayoutNode> flow, float start, float end)
    {
        foreach (var band in flow)
        {
            if (band.Bounds.Top > start && band.Bounds.Top <= end) yield return band.Bounds.Top;
            if (band.Bounds.Bottom > start && band.Bounds.Bottom <= end) yield return band.Bounds.Bottom;
            foreach (var c in CollectNode(band, start, end)) yield return c;
        }
    }

    private static IEnumerable<float> CollectNode(LayoutNode node, float start, float end)
    {
        foreach (var h in node.CutHints)
            if (h > start && h <= end) yield return h;
        foreach (var child in node.Children)
            if (child.Bounds.Top < end && child.Bounds.Bottom > start)
                foreach (var c in CollectNode(child, start, end)) yield return c;
    }
}
