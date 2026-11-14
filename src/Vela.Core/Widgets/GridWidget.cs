namespace Vela;

internal sealed class GridDefinition
{
    public required IReadOnlyList<GridTrack> Columns { get; init; }
    public IReadOnlyList<GridTrack>? Rows { get; init; }
}

/// <summary>
/// 网格容器：固定/百分比/Auto/Star 轨道、显式或自动放置、跨行跨列。
///
/// 算法分两阶段：先确定列宽和行高，再测量/安排子项。Auto 轨道由子项自然
/// 尺寸决定；Star 轨道只在容器有确定尺寸时分配剩余空间。实现目标是常见
/// dashboard 布局，不是完整 CSS Grid（无 min-content/container query）。
/// </summary>
internal sealed class GridWidget : Widget
{
    public static GridWidget Instance { get; } = new();

    private sealed class GridItem
    {
        public Element Element = null!;
        public int Column;
        public int Row;
        public int ColumnSpan;
        public int RowSpan;
        public Size Desired;
        public Thickness Margin;
    }

    /// <summary>
    /// 计算网格尺寸。Measure 和 Arrange 共用 Compute，避免两套轨道算法漂移。
    /// </summary>
    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var result = Compute(element, context, constraints);
        return result.Size;
    }

    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var result = Compute(element, context, Constraints.Tight(new Size(bounds.Width, bounds.Height)), bounds.Width, bounds.Height, arrange: true);
        var insets = LayoutUtil.Insets(element.Style);
        var x = bounds.X + insets.Left;
        var y = bounds.Y + insets.Top;

        var columnOffsets = Offsets(result.ColumnWidths, element.Style.Gap);
        var rowOffsets = Offsets(result.RowHeights, element.Style.Gap);

        foreach (var item in result.Items)
        {
            var cellX = x + columnOffsets[item.Column];
            var cellY = y + rowOffsets[item.Row];
            var cellWidth = result.ColumnWidths[item.Column];
            var cellHeight = result.RowHeights[item.Row];

            for (var i = 1; i < item.ColumnSpan; i++)
            {
                cellWidth += element.Style.Gap + result.ColumnWidths[item.Column + i];
            }

            for (var i = 1; i < item.RowSpan; i++)
            {
                cellHeight += element.Style.Gap + result.RowHeights[item.Row + i];
            }

            var rect = new Rect(
                cellX + item.Margin.Left,
                cellY + item.Margin.Top,
                MathF.Max(0, cellWidth - item.Margin.HorizontalSum),
                MathF.Max(0, cellHeight - item.Margin.VerticalSum));
            context.Arrange(item.Element, rect);
        }
    }

    private readonly record struct GridResult(
        Size Size,
        List<GridItem> Items,
        float[] ColumnWidths,
        float[] RowHeights);

    /// <summary>
    /// 网格求解主函数：放置子项 → 解析列宽 → 解析行高 → 计算容器尺寸。
    /// Measure 和 Arrange 共用，避免两套轨道算法不一致。
    /// </summary>
    private static GridResult Compute(
        Element element,
        LayoutContext context,
        Constraints constraints,
        float? forcedWidth = null,
        float? forcedHeight = null,
        bool arrange = false)
    {
        var definition = element.Payload as GridDefinition
            ?? throw new InvalidOperationException("Grid element is missing its grid definition.");
        var insets = LayoutUtil.Insets(element.Style);
        var items = PlaceItems(element, definition, context, constraints);
        var columnCount = Math.Max(1, Math.Max(definition.Columns.Count, items.Count == 0 ? 0 : items.Max(i => i.Column + i.ColumnSpan)));
        var rowCount = Math.Max(1, Math.Max(definition.Rows?.Count ?? 0, items.Count == 0 ? 0 : items.Max(i => i.Row + i.RowSpan)));
        var columns = EnsureTracks(definition.Columns, columnCount);
        var rows = EnsureTracks(definition.Rows, rowCount);

        var width = forcedWidth ?? ResolveOwnWidth(element, constraints);
        var height = forcedHeight ?? ResolveOwnHeight(element, constraints);
        var availableOuterWidth = width ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 0);
        var availableInnerWidth = MathF.Max(0, availableOuterWidth - insets.HorizontalSum);
        var columnWidths = ResolveColumns(items, columns, availableInnerWidth, element.Style.Gap, width is not null, context);

        var totalColumnWidth = columnWidths.Sum() + (element.Style.Gap * Math.Max(0, columnWidths.Length - 1));
        var availableOuterHeight = height ?? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : 0);
        var availableInnerHeight = MathF.Max(0, availableOuterHeight - insets.VerticalSum);
        var rowHeights = ResolveRows(items, rows, columnWidths, element.Style.Gap, availableInnerHeight, height is not null, context);
        var totalRowHeight = rowHeights.Sum() + (element.Style.Gap * Math.Max(0, rowHeights.Length - 1));

        var resolvedWidth = width ?? (totalColumnWidth + insets.HorizontalSum);
        var resolvedHeight = height ?? (totalRowHeight + insets.VerticalSum);

        if (element.Style.AspectRatio is { } ratio && ratio > 0)
        {
            if (element.Style.Width.IsAuto)
            {
                resolvedWidth = resolvedHeight * ratio;
            }
            else if (element.Style.Height.IsAuto)
            {
                resolvedHeight = resolvedWidth / ratio;
            }
        }

        var size = new Size(
            LayoutUtil.Clamp(resolvedWidth, MathF.Max(constraints.MinWidth, element.Style.MinWidth ?? 0), MathF.Min(constraints.MaxWidth, element.Style.MaxWidth ?? float.PositiveInfinity)),
            LayoutUtil.Clamp(resolvedHeight, MathF.Max(constraints.MinHeight, element.Style.MinHeight ?? 0), MathF.Min(constraints.MaxHeight, element.Style.MaxHeight ?? float.PositiveInfinity)));

        return new GridResult(size, items, columnWidths, rowHeights);
    }

    /// <summary>
    /// 子项放置。显式 GridColumn/GridRow 优先；只给一个轴时沿另一个轴找空位；
    /// 都不给时按 row-major 找第一个能容纳 span 的空位。span 占用的单元格会
    /// 记录在 occupied，后续子项不能重叠。
    /// </summary>
    private static List<GridItem> PlaceItems(Element element, GridDefinition definition, LayoutContext context, Constraints constraints)
    {
        var items = new List<GridItem>();
        var occupied = new HashSet<(int Column, int Row)>();
        var maxColumn = Math.Max(1, definition.Columns.Count);
        var maxRow = Math.Max(1, definition.Rows?.Count ?? 0);

        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position == PositionKind.Absolute)
            {
                continue;
            }

            var columnSpan = Math.Max(1, child.Style.GridColumnSpan);
            var rowSpan = Math.Max(1, child.Style.GridRowSpan);
            var column = child.Style.GridColumn;
            var row = child.Style.GridRow;

            if (column >= 0 && row >= 0)
            {
                // Explicit placement.
            }
            else if (column >= 0)
            {
                row = FindFreeRow(occupied, column, columnSpan, rowSpan, maxRow);
            }
            else if (row >= 0)
            {
                column = FindFreeColumn(occupied, row, columnSpan, rowSpan, maxColumn);
            }
            else
            {
                (column, row) = FindFreeSlot(occupied, columnSpan, rowSpan);
            }

            column = Math.Max(0, column);
            row = Math.Max(0, row);
            MarkOccupied(occupied, column, row, columnSpan, rowSpan);
            maxColumn = Math.Max(maxColumn, column + columnSpan);
            maxRow = Math.Max(maxRow, row + rowSpan);

            var margin = child.Style.Margin;
            items.Add(new GridItem
            {
                Element = child,
                Column = column,
                Row = row,
                ColumnSpan = columnSpan,
                RowSpan = rowSpan,
                Margin = margin,
            });
        }

        return items;
    }

    private static int FindFreeRow(HashSet<(int Column, int Row)> occupied, int column, int columnSpan, int rowSpan, int maxRow)
    {
        for (var row = 0; row < Math.Max(1, maxRow) + 10000; row++)
        {
            if (IsFree(occupied, column, row, columnSpan, rowSpan))
            {
                return row;
            }
        }

        return maxRow;
    }

    private static int FindFreeColumn(HashSet<(int Column, int Row)> occupied, int row, int columnSpan, int rowSpan, int maxColumn)
    {
        for (var column = 0; column < Math.Max(1, maxColumn) + 10000; column++)
        {
            if (IsFree(occupied, column, row, columnSpan, rowSpan))
            {
                return column;
            }
        }

        return maxColumn;
    }

    private static (int Column, int Row) FindFreeSlot(HashSet<(int Column, int Row)> occupied, int columnSpan, int rowSpan)
    {
        for (var row = 0; row < 10000; row++)
        {
            for (var column = 0; column < 10000; column++)
            {
                if (IsFree(occupied, column, row, columnSpan, rowSpan))
                {
                    return (column, row);
                }
            }
        }

        return (0, 0);
    }

    private static bool IsFree(HashSet<(int Column, int Row)> occupied, int column, int row, int columnSpan, int rowSpan)
    {
        for (var c = column; c < column + columnSpan; c++)
        {
            for (var r = row; r < row + rowSpan; r++)
            {
                if (occupied.Contains((c, r)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void MarkOccupied(HashSet<(int Column, int Row)> occupied, int column, int row, int columnSpan, int rowSpan)
    {
        for (var c = column; c < column + columnSpan; c++)
        {
            for (var r = row; r < row + rowSpan; r++)
            {
                occupied.Add((c, r));
            }
        }
    }

    /// <summary>补齐缺失的轨道定义：不足的列/行按 Auto 处理，保证至少 1 条。</summary>
    private static GridTrack[] EnsureTracks(IReadOnlyList<GridTrack>? source, int count)
    {
        var result = new GridTrack[Math.Max(1, count)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = source is not null && i < source.Count ? source[i] : GridTrack.Auto;
        }

        return result;
    }

    /// <summary>
    /// 解析列宽：Points/Percent 直接算；Auto/Star 先用单列子项自然宽度求内容宽；
    /// 有确定容器宽度时，再把剩余空间按 weight 分给 Star。
    /// </summary>
    private static float[] ResolveColumns(
        List<GridItem> items,
        GridTrack[] columns,
        float available,
        float gap,
        bool definiteWidth,
        LayoutContext context)
    {
        var widths = new float[columns.Length];
        var totalGap = gap * Math.Max(0, columns.Length - 1);
        for (var i = 0; i < columns.Length; i++)
        {
            widths[i] = columns[i].Kind switch
            {
                GridTrackKind.Points => MathF.Max(0, columns[i].Value),
                GridTrackKind.Percent => MathF.Max(0, available * columns[i].Value),
                _ => 0,
            };
        }

        // Auto-track intrinsic widths from single-column items.
        for (var i = 0; i < columns.Length; i++)
        {
            if (columns[i].Kind is not (GridTrackKind.Auto or GridTrackKind.Star))
            {
                continue;
            }

            foreach (var item in items)
            {
                if (item.Column != i || item.ColumnSpan != 1)
                {
                    continue;
                }

                var constraint = new Constraints(0, float.IsFinite(available) ? available : 0, 0, float.PositiveInfinity);
                var measured = context.Measure(item.Element, constraint);
                widths[i] = MathF.Max(widths[i], measured.Width + item.Margin.HorizontalSum);
            }
        }

        if (definiteWidth)
        {
            var fixedWidth = widths.Sum();
            var free = available - totalGap - fixedWidth;
            var starWeight = columns.Where(t => t.Kind == GridTrackKind.Star).Sum(t => MathF.Max(0, t.Value));
            if (free > 0 && starWeight > 0)
            {
                for (var i = 0; i < columns.Length; i++)
                {
                    if (columns[i].Kind == GridTrackKind.Star)
                    {
                        widths[i] += free * (MathF.Max(0, columns[i].Value) / starWeight);
                    }
                }
            }
        }

        return widths;
    }

    /// <summary>
    /// 解析行高：先用最终列宽测量单行子项，得到 Auto 行高；跨行子项不足时
    /// 把差额补给最后一行；有确定高度时再把剩余分给 Star 行。
    /// </summary>
    private static float[] ResolveRows(
        List<GridItem> items,
        GridTrack[] rows,
        float[] columnWidths,
        float gap,
        float available,
        bool definiteHeight,
        LayoutContext context)
    {
        var heights = new float[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            heights[i] = rows[i].Kind switch
            {
                GridTrackKind.Points => MathF.Max(0, rows[i].Value),
                GridTrackKind.Percent => MathF.Max(0, available * rows[i].Value),
                _ => 0,
            };
        }

        // Measure single-row items at their final column widths to resolve auto rows.
        foreach (var item in items)
        {
            if (item.Row >= heights.Length || item.Column >= columnWidths.Length)
            {
                continue;
            }

            var width = columnWidths[item.Column];
            for (var c = 1; c < item.ColumnSpan && item.Column + c < columnWidths.Length; c++)
            {
                width += gap + columnWidths[item.Column + c];
            }

            var measured = context.Measure(item.Element, new Constraints(
                width,
                width,
                0,
                float.PositiveInfinity));
            item.Desired = measured;
            if (item.RowSpan == 1)
            {
                heights[item.Row] = MathF.Max(heights[item.Row], measured.Height + item.Margin.VerticalSum);
            }
        }

        // Rows spanning multiple tracks grow the last row in the span if needed.
        foreach (var item in items)
        {
            if (item.RowSpan <= 1)
            {
                continue;
            }

            var current = 0f;
            for (var r = 0; r < item.RowSpan && item.Row + r < heights.Length; r++)
            {
                current += heights[item.Row + r] + (r > 0 ? gap : 0);
            }

            var desired = item.Desired.Height + item.Margin.VerticalSum;
            if (desired > current && item.Row + item.RowSpan - 1 < heights.Length)
            {
                heights[item.Row + item.RowSpan - 1] += desired - current;
            }
        }

        if (definiteHeight)
        {
            var fixedHeight = heights.Sum() + (gap * Math.Max(0, heights.Length - 1));
            var free = available - fixedHeight;
            var starWeight = rows.Where(t => t.Kind == GridTrackKind.Star).Sum(t => MathF.Max(0, t.Value));
            if (free > 0 && starWeight > 0)
            {
                for (var i = 0; i < rows.Length; i++)
                {
                    if (rows[i].Kind == GridTrackKind.Star)
                    {
                        heights[i] += free * (MathF.Max(0, rows[i].Value) / starWeight);
                    }
                }
            }
        }

        return heights;
    }

    private static float[] Offsets(float[] sizes, float gap)
    {
        var offsets = new float[sizes.Length];
        var cursor = 0f;
        for (var i = 0; i < sizes.Length; i++)
        {
            offsets[i] = cursor;
            cursor += sizes[i] + gap;
        }

        return offsets;
    }

    private static float? ResolveOwnWidth(Element element, Constraints constraints)
    {
        if (constraints.IsTightWidth)
        {
            return constraints.MinWidth;
        }

        if (element.Style.Width.IsAuto)
        {
            return null;
        }

        var value = element.Style.Width.Kind == DimensionKind.Percent
            ? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth * element.Style.Width.Value : 0)
            : element.Style.Width.Value;
        return LayoutUtil.Clamp(value, 0, element.Style.MaxWidth ?? float.PositiveInfinity);
    }

    private static float? ResolveOwnHeight(Element element, Constraints constraints)
    {
        if (constraints.IsTightHeight)
        {
            return constraints.MinHeight;
        }

        if (element.Style.Height.IsAuto)
        {
            return null;
        }

        var value = element.Style.Height.Kind == DimensionKind.Percent
            ? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight * element.Style.Height.Value : 0)
            : element.Style.Height.Value;
        return LayoutUtil.Clamp(value, 0, element.Style.MaxHeight ?? float.PositiveInfinity);
    }
}
