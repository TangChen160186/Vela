namespace Vela;

internal sealed class TextWidget : Widget
{
    public static TextWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var available = float.PositiveInfinity;
        if (!element.Style.Width.IsAuto)
        {
            available = element.Style.Width.Kind == DimensionKind.Percent
                ? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth * element.Style.Width.Value : float.PositiveInfinity)
                : element.Style.Width.Value;
        }
        else if (float.IsFinite(constraints.MaxWidth))
        {
            available = constraints.MaxWidth;
        }

        available -= insets.HorizontalSum;
        available = MathF.Min(available, LayoutUtil.MaxWidth(element));
        if (available < 0)
        {
            available = 0;
        }

        var options = new TextMeasureOptions(
            available,
            element.TextFormat.Wrap,
            element.TextFormat.Overflow,
            element.TextFormat.MaxLines);

        var metrics = context.Text.Measure(element.Text ?? string.Empty, element.TextFormat.Font, options);
        var contentWidth = metrics.Width + (metrics.Width > 0 ? 1f : 0f);
        if (float.IsFinite(available))
        {
            contentWidth = MathF.Min(contentWidth, available);
        }

        return LayoutUtil.ResolveLeaf(element, constraints, new Size(contentWidth, metrics.Height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var rect = element.ContentBounds;
        if (rect.IsEmpty)
        {
            return;
        }

        context.DrawList.Text(rect, element.Text ?? string.Empty, element.TextFormat, element.Foreground);
    }
}

internal sealed class SpacerWidget : Widget
{
    public static SpacerWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints) =>
        LayoutUtil.ResolveLeaf(element, constraints, Size.Zero);
}

internal sealed class IconWidget : Widget
{
    public static IconWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var size = element.Payload is float value ? value : element.Font.Size;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(size, size));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        if (element.Icon is null)
        {
            return;
        }

        var bounds = element.ContentBounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        context.DrawList.DrawIcon(element.Icon, bounds, element.Foreground);
    }
}

internal sealed class DividerWidget : Widget
{
    public static DividerWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var vertical = element.Payload is Axis.Vertical;
        var thickness = vertical
            ? element.Style.Width.IsAuto ? 1 : element.Style.Width.Value
            : element.Style.Height.IsAuto ? 1 : element.Style.Height.Value;

        if (vertical)
        {
            var height = constraints.IsTightHeight ? constraints.MinHeight : 0;
            return LayoutUtil.ResolveLeaf(element, constraints, new Size(thickness, height));
        }
        else
        {
            var width = constraints.IsTightWidth ? constraints.MinWidth : 0;
            return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, thickness));
        }
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var vertical = element.Payload is Axis.Vertical;
        var bounds = element.Bounds;
        var color = element.Style.BorderColor ?? context.Theme.Border;
        if (vertical)
        {
            var x = bounds.X + (bounds.Width * 0.5f);
            context.DrawList.Line(new Vec2(x, bounds.Top), new Vec2(x, bounds.Bottom), color, 1);
        }
        else
        {
            var y = bounds.Y + (bounds.Height * 0.5f);
            context.DrawList.Line(new Vec2(bounds.Left, y), new Vec2(bounds.Right, y), color, 1);
        }
    }
}
