namespace Vela;

internal sealed class SplitterState
{
    public bool Dragging { get; set; }
    public float StartValue { get; set; }
    public Vec2 StartPointer { get; set; }
}

/// <summary>
/// SplitPane 中间的可拖分割条。它只负责 Measure/绘制，实际尺寸调整在
/// Ui.SplitPane 中：读取 Response.Pressed/Active，用 SplitterState 保存拖动起点，
/// 再修改调用方传入的 ref firstSize。
/// </summary>
internal sealed class SplitterWidget : Widget
{
    public static SplitterWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var axis = element.Payload is Axis.Horizontal ? Axis.Horizontal : Axis.Vertical;
        var thickness = axis == Axis.Horizontal
            ? (element.Style.Width.IsAuto ? 7 : element.Style.Width.Value)
            : (element.Style.Height.IsAuto ? 7 : element.Style.Height.Value);
        var content = axis == Axis.Horizontal ? new Size(thickness, 20) : new Size(20, thickness);
        return LayoutUtil.ResolveLeaf(element, constraints, content);
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var axis = element.Payload is Axis.Horizontal ? Axis.Horizontal : Axis.Vertical;
        var theme = context.Theme;
        var active = element.Response.Held || element.Response.Hovered || element.Response.Focused;
        var color = active ? theme.BorderStrong : theme.Border;
        if (element.Response.Hovered || element.Response.Held)
        {
            context.DrawList.FillRect(element.Bounds, new Brush(theme.SurfaceHover.WithAlpha(0.45f)), CornerRadius.All(4));
        }

        if (axis == Axis.Horizontal)
        {
            var x = element.Bounds.Center.X;
            context.DrawList.Line(new Vec2(x, element.Bounds.Top + 3), new Vec2(x, element.Bounds.Bottom - 3), color, 1);
        }
        else
        {
            var y = element.Bounds.Center.Y;
            context.DrawList.Line(new Vec2(element.Bounds.Left + 3, y), new Vec2(element.Bounds.Right - 3, y), color, 1);
        }

        if (element.Response.Focused && element.Response.FocusVisible)
        {
            context.DrawList.StrokeRect(element.Bounds.Deflate(Thickness.All(1)), theme.FocusRing.WithAlpha(0.5f), 1, CornerRadius.All(3));
        }
    }
}
