namespace Vela;

/// <summary>
/// DataTable 的列定义。Width 是固定逻辑像素；Text 提供默认文本，
/// Paint 可完全接管单元格绘制（例如进度条、彩色状态点）。
/// 当前不支持 Grow/列宽拖动/排序，排序应由应用先重排 rows。
/// </summary>
public sealed record TableColumn<T>(
    string Header,
    float Width,
    Func<T, string>? Text = null,
    Func<T, Color>? TextColor = null,
    Action<TableCellView, T>? Paint = null,
    TextAlignment Alignment = TextAlignment.Left);

/// <summary>自定义单元格绘制上下文；只在可见行调用，属于 Paint 阶段，不能建控件。</summary>
public sealed class TableCellView
{
    internal TableCellView(
        Rect bounds,
        int rowIndex,
        int columnIndex,
        DrawList drawList,
        Theme theme,
        FrameInput input,
        bool hovered,
        bool selected)
    {
        Bounds = bounds;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        DrawList = drawList;
        Theme = theme;
        Input = input;
        Hovered = hovered;
        Selected = selected;
    }

    public Rect Bounds { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public DrawList DrawList { get; }
    public Theme Theme { get; }
    public FrameInput Input { get; }
    public bool Hovered { get; }
    public bool Selected { get; }
}

/// <summary>
/// TreeView 的扁平行：调用方负责展开/折叠并重新生成列表，
/// Vela 只负责虚拟化绘制、点击 chevron 和选中。
/// Depth 用于缩进；Tag 用于应用自己的节点身份（不要依赖 Label 唯一）。
/// </summary>
public sealed record TreeRow(
    string Label,
    int Depth,
    bool HasChildren,
    bool IsExpanded,
    IconData? Icon = null,
    string? Tag = null);

public readonly record struct TreeViewResult(Response Response, int ClickedIndex, int ToggleIndex);

public sealed partial class Ui
{
    /// <summary>
    /// 虚拟化列表：只对可见行调用 paintRow；滚轮/滚动条/选中/键盘由内部处理。
    /// 返回本帧 Response；选中索引用 ref 由应用持有。
    /// </summary>
    public Response VirtualList(
        string id,
        int itemCount,
        float rowHeight,
        Action<VirtualRowView> paintRow,
        ref int selectedIndex,
        Style? style = null)
    {
        var result = VirtualRows(
            id,
            itemCount,
            rowHeight,
            paintRow,
            ref selectedIndex,
            style ?? new Style { Height = 220 },
            focusable: true,
            keyboardNavigation: true,
            accessibilityRole: AccessibilityRole.List,
            accessibilityName: "List");
        return result.Response;
    }

    /// <summary>
    /// 固定表头 + 虚拟化数据行的表格。表头只画一次；正文只对可见行
    /// 调用单元格 painter。style 控制外框/高度，通常应设置 Grow=1 或固定 Height。
    ///
    /// 选择通过 ref selectedIndex 由应用持有；点击/键盘/滚轮由内部 VirtualRows 处理。
    /// 当前列宽固定，没有排序/列宽拖动/水平滚动。
    /// </summary>
    public Response DataTable<T>(
        string id,
        IReadOnlyList<T> rows,
        IReadOnlyList<TableColumn<T>> columns,
        ref int selectedIndex,
        float rowHeight = 42,
        float headerHeight = 38,
        Style? style = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        var frame = RequireFrame();
        var outerStyle = style ?? new Style
        {
            Direction = FlexDirection.Column,
            Height = 360,
            Background = new Brush(Theme.Surface),
            BorderColor = Theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(Theme.RadiusLarge),
            ClipContent = true,
        };

        Response response = default;
        using (frame.BeginContainer(FlexWidget.Instance, id, outerStyle, "data-table"))
        {
            frame.Current.AccessibilityRole = AccessibilityRole.Table;
            frame.Current.AccessibilityName = "Data table";
            frame.Current.AccessibilityValue = $"{rows.Count} rows, {columns.Count} columns";
            Canvas($"{id}:header", view =>
            {
                var draw = view.DrawList;
                var b = view.Bounds;
                draw.FillRect(b, new Brush(Theme.SurfaceRaised));
                var x = b.Left + 12;
                foreach (var column in columns)
                {
                    draw.Text(
                        new Rect(x, b.Top, MathF.Max(0, column.Width - 12), b.Height),
                        column.Header,
                        new TextFormat(Theme.CaptionMedium, column.Alignment, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis),
                        Theme.TextMuted);
                    x += column.Width;
                }

                draw.Line(new Vec2(b.Left, b.Bottom - 0.5f), new Vec2(b.Right, b.Bottom - 0.5f), Theme.Border, 1);
            }, new Style { Height = headerHeight, Shrink = 0 });

            var result = VirtualRows(
                $"{id}:rows",
                rows.Count,
                rowHeight,
                row =>
                {
                    var cellX = row.Bounds.Left + 12;
                    for (var c = 0; c < columns.Count; c++)
                    {
                        var column = columns[c];
                        var cell = new Rect(cellX, row.Bounds.Top, MathF.Max(0, column.Width), row.Bounds.Height);
                        if (column.Paint is not null)
                        {
                            column.Paint(new TableCellView(cell, row.Index, c, row.DrawList, row.Theme, row.Input, row.Hovered, row.Selected), rows[row.Index]);
                        }
                        else if (column.Text is { } text)
                        {
                            row.DrawList.Text(
                                new Rect(cell.X, cell.Y, MathF.Max(0, cell.Width - 10), cell.Height),
                                text(rows[row.Index]),
                                new TextFormat(Theme.SmallMedium, column.Alignment, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis),
                                column.TextColor?.Invoke(rows[row.Index]) ?? Theme.Text);
                        }

                        cellX += column.Width;
                    }
                },
                ref selectedIndex,
                new Style { Grow = 1, Shrink = 1 },
                focusable: true,
                keyboardNavigation: true,
                accessibilityRole: AccessibilityRole.Group,
                accessibilityName: "Rows");
            response = result.Response;
        }

        return response;
    }

    /// <summary>
    /// A virtualized tree. The caller flattens expanded nodes and updates its
    /// expansion state from <see cref="TreeViewResult.ToggleIndex"/>.
    /// </summary>
    /// <summary>
    /// 虚拟化树。调用方每帧把“当前展开的节点”扁平化传入；Vela 绘制缩进、
    /// chevron、图标和标签，并在点击 chevron 时通过 ToggleIndex 告诉应用。
    ///
    /// ToggleIndex 只是“发生了点击”，应用负责修改自己的展开集合并重建 rows。
    /// </summary>
    public TreeViewResult TreeView(
        string id,
        IReadOnlyList<TreeRow> rows,
        ref int selectedIndex,
        Style? style = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var clicked = -1;
        var toggle = -1;
        var result = VirtualRows(
            id,
            rows.Count,
            32,
            row =>
            {
                var node = rows[row.Index];
                var draw = row.DrawList;
                var indent = 12 + node.Depth * 18;
                var chevron = new Rect(row.Bounds.Left + indent, row.Bounds.Center.Y - 8, 16, 16);
                if (node.HasChildren)
                {
                    draw.DrawIcon(node.IsExpanded ? Icons.ChevronDown : Icons.ChevronRight, chevron, Theme.TextMuted);
                }

                var x = indent + 22;
                if (node.Icon is not null)
                {
                    draw.DrawIcon(node.Icon, new Rect(row.Bounds.Left + x, row.Bounds.Center.Y - 8, 16, 16), row.Selected ? Theme.Accent : Theme.TextMuted);
                    x += 22;
                }

                draw.Text(
                    new Rect(row.Bounds.Left + x, row.Bounds.Top, MathF.Max(0, row.Bounds.Right - x - 8), row.Bounds.Height),
                    node.Label,
                    new TextFormat(Theme.Body, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis),
                    row.Selected ? Theme.Text : Theme.TextMuted);
            },
            ref selectedIndex,
            style ?? new Style { Height = 280 },
            focusable: true,
            keyboardNavigation: true,
            accessibilityRole: AccessibilityRole.Tree,
            accessibilityName: "Tree");

        clicked = result.ClickedIndex;
        if (clicked >= 0 && clicked < rows.Count)
        {
            var node = rows[clicked];
            var indent = 12 + node.Depth * 18;
            if (node.HasChildren && result.PointerX >= result.ClickedBounds.Left + indent &&
                result.PointerX <= result.ClickedBounds.Left + indent + 22)
            {
                toggle = clicked;
            }
        }

        return new TreeViewResult(result.Response, clicked, toggle);
    }

    /// <summary>
    /// VirtualList/DataTable/TreeView 的共同实现：创建 VirtualRowsWidget，
    /// 解析点击行、滚动条拖动、键盘选择，再把结果返回给包装方法。
    ///
    /// 注意：点击/行几何使用上一帧 BodyRect，所以滚动后点击可能有一帧滞后；
    /// 这是即时模式命中一致性的同一个取舍。
    /// </summary>
    private VirtualRowsResult VirtualRows(
        string id,
        int itemCount,
        float rowHeight,
        Action<VirtualRowView> paintRow,
        ref int selectedIndex,
        Style style,
        bool focusable,
        bool keyboardNavigation,
        AccessibilityRole accessibilityRole,
        string accessibilityName)
    {
        var frame = RequireFrame();
        itemCount = Math.Max(0, itemCount);
        rowHeight = MathF.Max(1, rowHeight);
        var element = frame.AddElement(
            VirtualRowsWidget.Instance,
            id,
            style,
            "virtual-rows",
            interactive: true,
            focusable: focusable,
            activatable: false,
            claimId: true);
        element.IsScrollable = true;
        element.HandlesArrowKeys = true;
        element.AccessibilityRole = accessibilityRole;
        element.AccessibilityName = accessibilityName;
        element.Cursor = CursorKind.Default;
        selectedIndex = itemCount == 0 ? -1 : Math.Clamp(selectedIndex, -1, itemCount - 1);
        var payload = new VirtualRowsPayload
        {
            ItemCount = itemCount,
            RowHeight = rowHeight,
            PaintRow = paintRow,
            SelectedIndex = selectedIndex,
        };
        element.Payload = payload;
        frame.UpdateResponse(element);
        var response = element.Response;
        var state = State.GetOrCreate<VirtualRowsState>(element.Id);
        var scroll = State.GetOrCreate<ScrollState>(element.Id);
        var pointer = Input.PointerPosition;
        var body = state.BodyRect;
        var clickedIndex = -1;
        var clickedBounds = Rect.Empty;
        var pointerRow = -1;

        if (body.Contains(pointer) && itemCount > 0)
        {
            pointerRow = (int)MathF.Floor((pointer.Y - body.Y + scroll.Offset) / rowHeight);
            if (pointerRow < 0 || pointerRow >= itemCount)
            {
                pointerRow = -1;
            }
        }

        if (response.Pressed && body.Contains(pointer))
        {
            var onScrollTrack = scroll.IsScrollable && pointer.X >= body.Right - 9 && state.TrackRect.Contains(pointer);
            if (onScrollTrack && state.TrackRect.Height > 0)
            {
                state.Dragging = true;
                var thumbHeight = state.ThumbRect.Height;
                if (!state.ThumbRect.Contains(pointer))
                {
                    var trackTravel = MathF.Max(1, state.TrackRect.Height - thumbHeight);
                    var ratio = Math.Clamp((pointer.Y - state.TrackRect.Top - (thumbHeight * 0.5f)) / trackTravel, 0, 1);
                    scroll.JumpTo(ratio * scroll.MaxOffset);
                }

                state.DragStartOffset = scroll.Offset;
                state.DragStartPointerY = pointer.Y;
                state.DragStartThumbY = state.ThumbRect.Y;
            }
            else if (pointerRow >= 0)
            {
                selectedIndex = pointerRow;
            }
        }

        if (state.Dragging && response.Active && Input.IsButtonDown(PointerButton.Left) && state.TrackRect.Height > 0)
        {
            var travel = MathF.Max(1, state.TrackRect.Height - state.ThumbRect.Height);
            var maxOffset = MathF.Max(0, scroll.MaxOffset);
            var delta = pointer.Y - state.DragStartPointerY;
            scroll.JumpTo(state.DragStartOffset + (delta / travel * maxOffset));
        }

        if (!Input.IsButtonDown(PointerButton.Left))
        {
            state.Dragging = false;
        }

        if (response.Clicked && body.Contains(pointer))
        {
            var row = (int)MathF.Floor((pointer.Y - body.Y + scroll.Offset) / rowHeight);
            if (row >= 0 && row < itemCount)
            {
                clickedIndex = row;
                clickedBounds = new Rect(body.X, body.Y + row * rowHeight - scroll.Offset, body.Width, rowHeight);
            }
        }

        if (keyboardNavigation && focusable && response.Focused && itemCount > 0 && !state.Dragging)
        {
            var page = Math.Max(1, (int)MathF.Floor(state.BodyRect.Height / rowHeight));
            var index = selectedIndex < 0 ? 0 : selectedIndex;
            var handled = false;
            if (Input.WasKeyPressedOrRepeated(Key.Down))
            {
                index++;
                handled = true;
            }
            else if (Input.WasKeyPressedOrRepeated(Key.Up))
            {
                index--;
                handled = true;
            }
            else if (Input.WasKeyPressed(Key.PageDown))
            {
                index += page;
                handled = true;
            }
            else if (Input.WasKeyPressed(Key.PageUp))
            {
                index -= page;
                handled = true;
            }
            else if (Input.WasKeyPressed(Key.Home))
            {
                index = 0;
                handled = true;
            }
            else if (Input.WasKeyPressed(Key.End))
            {
                index = itemCount - 1;
                handled = true;
            }

            if (handled)
            {
                selectedIndex = Math.Clamp(index, 0, itemCount - 1);
                var top = selectedIndex * rowHeight;
                var bottom = top + rowHeight;
                if (top < scroll.Offset)
                {
                    scroll.ScrollTo(top);
                }
                else if (bottom > scroll.Offset + state.BodyRect.Height)
                {
                    scroll.ScrollTo(bottom - state.BodyRect.Height);
                }
            }
        }

        element.Payload = new VirtualRowsPayload
        {
            ItemCount = itemCount,
            RowHeight = rowHeight,
            PaintRow = paintRow,
            SelectedIndex = selectedIndex,
        };

        return new VirtualRowsResult(response, clickedIndex, pointerRow, clickedBounds, pointer.X, pointer.Y);
    }
}


