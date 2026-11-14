namespace Vela;

/// <summary>
/// 虚拟化行绘制上下文。回调只对可见行调用；Bounds 已经是屏幕坐标，
/// DrawList 已处于列表内容裁剪内。
///
/// 注意：回调发生在 Paint 阶段，不能在这里增删控件或改变布局。
/// </summary>
public sealed class VirtualRowView
{
    internal VirtualRowView(
        int index,
        Rect bounds,
        DrawList drawList,
        Ui ui,
        Theme theme,
        FrameInput input,
        bool hovered,
        bool selected,
        float rowHeight)
    {
        Index = index;
        Bounds = bounds;
        DrawList = drawList;
        Ui = ui;
        Theme = theme;
        Input = input;
        Hovered = hovered;
        Selected = selected;
        RowHeight = rowHeight;
    }

    public int Index { get; }
    public Rect Bounds { get; }
    public DrawList DrawList { get; }
    public Ui Ui { get; }
    public Theme Theme { get; }
    public FrameInput Input { get; }
    public bool Hovered { get; }
    public bool Selected { get; }
    public float RowHeight { get; }
}

/// <summary>VirtualRows 每帧所需的数据快照；SelectedIndex 由包装控件更新。</summary>
internal sealed class VirtualRowsPayload
{
    public required int ItemCount { get; init; }
    public required float RowHeight { get; init; }
    public required Action<VirtualRowView> PaintRow { get; init; }
    public required int SelectedIndex { get; init; }
    public float Overscan { get; init; } = 1;
}

internal sealed class VirtualRowsState
{
    public Rect BodyRect { get; set; }
    public Rect TrackRect { get; set; }
    public Rect ThumbRect { get; set; }
    public bool Dragging { get; set; }
    public float DragStartOffset { get; set; }
    public float DragStartPointerY { get; set; }
    public float DragStartThumbY { get; set; }
    public int HoverIndex { get; set; } = -1;
}

internal readonly record struct VirtualRowsResult(
    Response Response,
    int ClickedIndex,
    int HoveredIndex,
    Rect ClickedBounds,
    float PointerX,
    float PointerY);

/// <summary>
/// 虚拟列表/表格/树的公共底座：
/// <list type="bullet">
/// <item>一个可滚动元素，内部只绘制可见行；</item>
/// <item>管滚动偏移、滚动条拖动、hover、选中、键盘移动；</item>
/// <item>通过回调画行，DataTable/TreeView 在其上做列/缩进。</item>
/// </list>
/// 行不创建 Element，因此 5000 行也只有约 20 个可见行参与绘制。
/// </summary>
internal sealed class VirtualRowsWidget : Widget
{
    public static VirtualRowsWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var width = constraints.IsTightWidth
            ? constraints.MinWidth
            : element.Style.Width.Kind switch
            {
                DimensionKind.Points => element.Style.Width.Value,
                DimensionKind.Percent => float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth * element.Style.Width.Value : 240,
                _ => 240,
            };
        var height = constraints.IsTightHeight
            ? constraints.MinHeight
            : element.Style.Height.Kind switch
            {
                DimensionKind.Points => element.Style.Height.Value,
                DimensionKind.Percent => float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight * element.Style.Height.Value : 220,
                _ => 220,
            };
        var size = LayoutUtil.ResolveLeaf(element, constraints, new Size(width, height));
        return size;
    }

    /// <summary>
    /// 同步滚动指标并 Tick 平滑滚动。IsScrollable/ScrollOffset 写入 Element，
    /// 下一帧快照用它支持滚轮和滚动条拖动命中。
    /// </summary>
    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        var payload = element.Payload as VirtualRowsPayload;
        var contentHeight = payload is null ? 0 : payload.ItemCount * payload.RowHeight;
        scroll.SetMetrics(contentHeight, element.ContentBounds.Height);
        scroll.Tick(context.DeltaTime);
        element.IsScrollable = scroll.IsScrollable;
        element.ScrollOffset = scroll.Offset;
        element.ScrollContentHeight = scroll.ContentHeight;
        element.ScrollViewportHeight = scroll.ViewportHeight;
    }

    /// <summary>
    /// 只绘制可视行：先算 first/last（含 overscan），PushClip 后逐行回调，
    /// 最后画滚动条滑块。行背景的 hover/selected 由本类先画，业务 Painter 覆盖内容。
    /// </summary>
    public override void PaintContent(Element element, PaintContext context)
    {
        if (element.Payload is not VirtualRowsPayload payload)
        {
            return;
        }

        var draw = context.DrawList;
        var theme = context.Theme;
        var body = element.ContentBounds;
        if (body.IsEmpty)
        {
            return;
        }

        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        var state = context.Ui.State.GetOrCreate<VirtualRowsState>(element.Id);
        state.BodyRect = body;
        var first = Math.Max(0, (int)MathF.Floor((scroll.Offset - payload.RowHeight) / payload.RowHeight));
        var last = Math.Min(payload.ItemCount - 1, (int)MathF.Ceiling((scroll.Offset + body.Height + payload.RowHeight) / payload.RowHeight));
        var hovered = -1;
        if (context.Input.PointerInside && body.Contains(context.Input.PointerPosition))
        {
            var row = (int)MathF.Floor((context.Input.PointerPosition.Y - body.Y + scroll.Offset) / payload.RowHeight);
            if (row >= 0 && row < payload.ItemCount)
            {
                hovered = row;
                state.HoverIndex = row;
            }
        }
        else
        {
            state.HoverIndex = -1;
        }

        if (payload.ItemCount > 0)
        {
            draw.PushClip(body);
            for (var i = first; i <= last; i++)
            {
                var rowTop = body.Y + i * payload.RowHeight - scroll.Offset;
                var rowRect = new Rect(body.X, rowTop, body.Width, payload.RowHeight);
                if (i == hovered)
                {
                    draw.FillRect(rowRect, new Brush(theme.SurfaceHover.WithAlpha(0.45f)));
                }
                else if (i == payload.SelectedIndex)
                {
                    draw.FillRect(rowRect, new Brush(theme.AccentSoft));
                }

                payload.PaintRow(new VirtualRowView(
                    i,
                    rowRect,
                    draw,
                    context.Ui,
                    theme,
                    context.Input,
                    i == hovered,
                    i == payload.SelectedIndex,
                    payload.RowHeight));
            }

            draw.PopClip();
        }

        if (scroll.IsScrollable)
        {
            var track = new Rect(body.Right - 7, body.Top, 7, body.Height);
            var ratio = Math.Clamp(scroll.ViewportHeight / Math.Max(1, scroll.ContentHeight), 0, 1);
            var thumbHeight = MathF.Max(26, track.Height * ratio);
            var maxOffset = MathF.Max(1, scroll.MaxOffset);
            var thumbY = track.Top + (scroll.Offset / maxOffset * MathF.Max(0, track.Height - thumbHeight));
            var thumb = new Rect(track.X + 2, thumbY, 3, thumbHeight);
            state.TrackRect = track;
            state.ThumbRect = new Rect(track.X, thumbY, track.Width, thumbHeight);
            draw.FillRect(thumb, new Brush(theme.Scrollbar), CornerRadius.All(2));
        }
        else
        {
            state.TrackRect = Rect.Empty;
            state.ThumbRect = Rect.Empty;
        }
    }
}
