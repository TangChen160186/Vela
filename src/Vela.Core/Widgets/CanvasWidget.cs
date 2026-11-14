namespace Vela;

/// <summary>
/// 传给 Ui.Canvas 绘制回调的只读视图：Bounds/Clip/Theme/Input/Response/DrawList。
/// 回调发生在 Paint 阶段；不能在这里创建控件或修改布局。
/// </summary>
public sealed class CanvasView
{
    internal CanvasView(Rect bounds, DrawList drawList, Ui ui, Theme theme, FrameInput input, Response response)
    {
        Bounds = bounds;
        DrawList = drawList;
        Ui = ui;
        Theme = theme;
        Input = input;
        Response = response;
    }

    public Rect Bounds { get; }
    public DrawList DrawList { get; }
    public Ui Ui { get; }
    public Theme Theme { get; }
    public FrameInput Input { get; }
    public Response Response { get; }
}

internal sealed class CanvasWidget : Widget
{
    public static CanvasWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints) =>
        LayoutUtil.ResolveLeaf(element, constraints, Size.Zero);

    public override void PaintContent(Element element, PaintContext context)
    {
        if (element.Payload is not Action<CanvasView> painter)
        {
            return;
        }

        painter(new CanvasView(element.Bounds, context.DrawList, context.Ui, context.Theme, context.Input, element.Response));
    }
}
