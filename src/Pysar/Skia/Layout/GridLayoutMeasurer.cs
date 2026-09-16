using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Skia.Layout;

/// <summary>
///     Pure grid measurement. Produces a <see cref="LayoutNode"/> with absolute cell
///     positions and per-row cut hints, without mutating the grid or its children.
///     Track math is ported from the legacy GridMeasurer; child probes go through
///     <see cref="LayoutEngine"/> with <see cref="MeasureConstraint"/> overrides instead
///     of temporary Size mutations.
/// </summary>
internal static class GridLayoutMeasurer
{
    public static LayoutNode Measure(Grid grid, MeasureConstraint constraint, MeasureContext ctx, CancellationToken ct)
    {
        var availableRect = constraint.AvailableRect;
        var effW = LayoutEngine.EffectiveWidth(grid, constraint);
        var effH = LayoutEngine.EffectiveHeight(grid, constraint);

        var marginHorizontal = grid.Margin.Left + grid.Margin.Right;
        var marginVertical = grid.Margin.Top + grid.Margin.Bottom;
        var borderHorizontal = grid.BorderThickness.Left + grid.BorderThickness.Right;
        var borderVertical = grid.BorderThickness.Top + grid.BorderThickness.Bottom;
        var paddingHorizontal = grid.Padding.Left + grid.Padding.Right;
        var paddingVertical = grid.Padding.Top + grid.Padding.Bottom;

        var isAutoWidth = !effW.IsFixed && !effW.IsFill;
        var isAutoHeight = !effH.IsFixed && !effH.IsFill;

        var contentWidth = effW.IsFixed ? effW.Value - marginHorizontal - borderHorizontal - paddingHorizontal :
            effW.IsFill ? availableRect.Width - marginHorizontal - borderHorizontal - paddingHorizontal : 0;
        var contentHeight = effH.IsFixed ? effH.Value - marginVertical - borderVertical - paddingVertical :
            effH.IsFill ? availableRect.Height - marginVertical - borderVertical - paddingVertical : 0;

        // Size tracks against a bounded rect (Auto uses a large sizing box so content sizes freely).
        var sizingRect = new Rect(0, 0,
            isAutoWidth ? 10000 : contentWidth,
            isAutoHeight ? 10000 : contentHeight);
        var columnWidths = CalculateColumnWidths(grid, sizingRect, ctx, ct, isAutoWidth);
        var rowHeights = CalculateRowHeights(grid, sizingRect, ctx, ct, isAutoHeight, columnWidths);

        var totalWidth = columnWidths.Sum() + grid.ColumnSpacing * Math.Max(0, columnWidths.Length - 1);
        var totalHeight = rowHeights.Sum() + grid.RowSpacing * Math.Max(0, rowHeights.Length - 1);

        if (isAutoWidth) contentWidth = totalWidth;
        if (isAutoHeight) contentHeight = totalHeight;

        var gridWidth = contentWidth + marginHorizontal + borderHorizontal + paddingHorizontal;
        var gridHeight = contentHeight + marginVertical + borderVertical + paddingVertical;
        (gridWidth, gridHeight) = SizeConstraints.Clamp(gridWidth, gridHeight, grid.MinSize, grid.MaxSize);

        // Min/Max clamp the border-box only; push the delta into tracks so cells grow/shrink and
        // child alignment (Center/End) still has free space inside the clamped grid.
        var clampedContentWidth = Math.Max(0f, gridWidth - marginHorizontal - borderHorizontal - paddingHorizontal);
        var clampedContentHeight = Math.Max(0f, gridHeight - marginVertical - borderVertical - paddingVertical);
        RedistributeTracks(columnWidths, grid.ColumnSpacing, contentWidth, clampedContentWidth);
        RedistributeTracks(rowHeights, grid.RowSpacing, contentHeight, clampedContentHeight);
        contentWidth = clampedContentWidth;
        contentHeight = clampedContentHeight;

        var (originLeft, originTop) = constraint.IgnorePosition
            ? (availableRect.Left, availableRect.Top)
            : PositionResolver.Resolve(grid, gridWidth, gridHeight, availableRect);

        var contentLeft = originLeft + grid.Margin.Left + grid.BorderThickness.Left + grid.Padding.Left;
        var contentTop = originTop + grid.Margin.Top + grid.BorderThickness.Top + grid.Padding.Top;

        var children = new List<LayoutNode>();
        foreach (var child in grid.Children)
        {
            ct.ThrowIfCancellationRequested();
            if (!child.IsVisible) continue;

            var row = GridAttached.GetRow(child);
            var column = GridAttached.GetColumn(child);
            var rowSpan = GridAttached.GetRowSpan(child);
            var columnSpan = GridAttached.GetColumnSpan(child);

            var cellBounds = GetCellBounds(column, row, columnSpan, rowSpan, columnWidths, rowHeights,
                grid.ColumnSpacing, grid.RowSpacing, contentLeft, contentTop);

            // Available rect is the cell. Leave Fill as Fill so LayoutEngine.ApplyMargin can
            // expand (negative margin) or inset (positive) the box; pinning Fixed(cellSize)
            // kept the pre-margin width and only shifted the origin.
            // At/alignment still resolve against the margin-adjusted cell rect.
            children.Add(LayoutEngine.Measure(child, new MeasureConstraint(cellBounds), ctx, ct));
        }

        var cutHints = BuildRowCutHints(rowHeights, grid.RowSpacing, contentTop);

        return new LayoutNode(grid, new Rect(originLeft, originTop, originLeft + gridWidth, originTop + gridHeight),
            children, cutHints);
    }

    /// <summary>
    ///     Scales track lengths so their sum (+ spacing) matches <paramref name="newContentSize"/> after
    ///     a Min/Max clamp on the grid border-box. Proportional to current track sizes; if all tracks
    ///     are zero, splits the new size evenly.
    /// </summary>
    private static void RedistributeTracks(float[] tracks, float spacing, float oldContentSize, float newContentSize)
    {
        if (tracks.Length == 0) return;
        if (Math.Abs(newContentSize - oldContentSize) < 0.01f) return;

        var spacingTotal = spacing * Math.Max(0, tracks.Length - 1);
        var oldSum = 0f;
        for (var i = 0; i < tracks.Length; i++)
            oldSum += tracks[i];
        var newSum = Math.Max(0f, newContentSize - spacingTotal);

        if (oldSum <= 0.01f)
        {
            var each = newSum / tracks.Length;
            for (var i = 0; i < tracks.Length; i++)
                tracks[i] = each;
            return;
        }

        var scale = newSum / oldSum;
        for (var i = 0; i < tracks.Length; i++)
            tracks[i] = Math.Max(0f, tracks[i] * scale);
    }

    private static IReadOnlyList<float> BuildRowCutHints(float[] rowHeights, float rowSpacing, float contentTop)
    {
        if (rowHeights.Length == 0) return LayoutNode.NoCuts;
        var hints = new float[rowHeights.Length];
        var y = contentTop;
        for (int i = 0; i < rowHeights.Length; i++)
        {
            y += rowHeights[i];
            hints[i] = y;
            y += rowSpacing;
        }
        return hints;
    }

    private static float[] CalculateColumnWidths(
        Grid grid, Rect sizingRect, MeasureContext ctx, CancellationToken ct, bool isAutoSize)
    {
        var columns = EffectiveColumnDefinitions(grid);
        return CalculateTracks(
            columns.Count,
            i => columns[i].Width,
            grid.ColumnSpacing,
            sizingRect.Width,
            isAutoSize,
            i => CalculateAutoColumnWidth(grid, i, sizingRect, ctx, ct));
    }

    private static float CalculateAutoColumnWidth(
        Grid grid, int columnIndex, Rect sizingRect, MeasureContext ctx, CancellationToken ct)
    {
        var maxWidth = 0f;
        var measureRect = new Rect(0, 0, sizingRect.Width, sizingRect.Height);

        foreach (var child in grid.Children)
        {
            if (GridAttached.GetColumn(child) != columnIndex || GridAttached.GetColumnSpan(child) != 1)
                continue;

            // A Fill child reports its content width when measured as Auto.
            var probe = LayoutEngine.ProbeSize(child,
                new MeasureConstraint(measureRect, WidthOverride: child.Size.Width.IsFill ? SizeLength.Auto : null),
                ctx, ct);
            maxWidth = Math.Max(maxWidth, probe.Width);
        }

        return maxWidth;
    }

    private static IReadOnlyList<RowDefinition> EffectiveRowDefinitions(Grid grid) =>
        grid.RowDefinitions.Count > 0
            ? grid.RowDefinitions
            : [new RowDefinition(grid.Size.Height.IsAuto ? GridLength.Auto : GridLength.Star())];

    private static IReadOnlyList<ColumnDefinition> EffectiveColumnDefinitions(Grid grid) =>
        grid.ColumnDefinitions.Count > 0
            ? grid.ColumnDefinitions
            : [new ColumnDefinition(grid.Size.Width.IsAuto ? GridLength.Auto : GridLength.Star())];

    private static float[] CalculateRowHeights(
        Grid grid, Rect sizingRect, MeasureContext ctx, CancellationToken ct, bool isAutoSize, float[] columnWidths)
    {
        var rows = EffectiveRowDefinitions(grid);
        return CalculateTracks(
            rows.Count,
            i => rows[i].Height,
            grid.RowSpacing,
            sizingRect.Height,
            isAutoSize,
            i => CalculateAutoRowHeight(grid, i, sizingRect, ctx, ct, columnWidths));
    }

    private static float[] CalculateTracks(
        int count,
        Func<int, GridLength> lengthOf,
        float spacing,
        float available,
        bool isAutoSize,
        Func<int, float> measureAuto)
    {
        var sizes = new float[count];
        var totalFixed = 0f;
        var totalStarWeight = 0f;

        for (var i = 0; i < count; i++)
        {
            var length = lengthOf(i);
            switch (length.Type)
            {
                case GridLengthType.Fixed:
                    sizes[i] = length.Value;
                    totalFixed += sizes[i];
                    break;
                case GridLengthType.Star:
                    if (isAutoSize)
                    {
                        sizes[i] = measureAuto(i);
                        totalFixed += sizes[i];
                    }
                    else
                    {
                        totalStarWeight += length.Value;
                    }

                    break;
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (lengthOf(i).Type != GridLengthType.Auto)
                continue;

            sizes[i] = measureAuto(i);
            totalFixed += sizes[i];
        }

        if (totalStarWeight > 0 && !isAutoSize)
        {
            var leftover = available - totalFixed - spacing * Math.Max(0, count - 1);
            var starUnit = leftover / totalStarWeight;
            for (var i = 0; i < count; i++)
            {
                var length = lengthOf(i);
                if (length.Type == GridLengthType.Star)
                    sizes[i] = length.Value * starUnit;
            }
        }

        return sizes;
    }

    private static float CalculateAutoRowHeight(
        Grid grid, int rowIndex, Rect sizingRect, MeasureContext ctx, CancellationToken ct, float[] columnWidths)
    {
        var maxHeight = 0f;

        foreach (var child in grid.Children)
        {
            if (GridAttached.GetRow(child) != rowIndex || GridAttached.GetRowSpan(child) != 1)
                continue;

            float childWidth;
            if (columnWidths.Length > 0)
            {
                var childColumn = GridAttached.GetColumn(child);
                var childColumnSpan = GridAttached.GetColumnSpan(child);
                childWidth = 0f;
                for (int col = childColumn; col < childColumn + childColumnSpan && col < columnWidths.Length; col++)
                {
                    childWidth += columnWidths[col];
                    if (col < childColumn + childColumnSpan - 1)
                        childWidth += grid.ColumnSpacing;
                }
            }
            else
            {
                childWidth = sizingRect.Width;
            }

            var measureRect = new Rect(0, 0, childWidth, sizingRect.Height);

            // Fill height → Auto (report content height); Fill width → pin to the column width
            // so text wraps against the actual cell width.
            var probe = LayoutEngine.ProbeSize(child,
                new MeasureConstraint(measureRect,
                    WidthOverride: child.Size.Width.IsFill ? SizeLength.Fixed(childWidth) : null,
                    HeightOverride: child.Size.Height.IsFill ? SizeLength.Auto : null),
                ctx, ct);
            maxHeight = Math.Max(maxHeight, probe.Height);
        }

        return maxHeight;
    }

    private static Rect GetCellBounds(
        int column, int row, int columnSpan, int rowSpan,
        float[] columnWidths, float[] rowHeights,
        float columnSpacing, float rowSpacing,
        float gridLeft, float gridTop)
    {
        var left = gridLeft;
        for (int i = 0; i < column && i < columnWidths.Length; i++)
            left += columnWidths[i] + columnSpacing;

        var top = gridTop;
        for (int i = 0; i < row && i < rowHeights.Length; i++)
            top += rowHeights[i] + rowSpacing;

        var width = 0f;
        for (int i = column; i < column + columnSpan && i < columnWidths.Length; i++)
        {
            width += columnWidths[i];
            if (i < column + columnSpan - 1) width += columnSpacing;
        }

        var height = 0f;
        for (int i = row; i < row + rowSpan && i < rowHeights.Length; i++)
        {
            height += rowHeights[i];
            if (i < row + rowSpan - 1) height += rowSpacing;
        }

        return new Rect(left, top, left + width, top + height);
    }
}
