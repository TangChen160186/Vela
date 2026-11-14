namespace Vela;

/// <summary>
/// 流式换行容器。子项保持自然尺寸，按可用宽度从左到右排列，越界换行；
/// 行高取该行最高子项，gap 同时用于水平/垂直间距。
/// 不支持 Grow/百分比主轴，适合标签、chips、卡片流。
/// </summary>
internal sealed class WrapWidget : Widget
{
    public static WrapWidget Instance { get; } = new();

    private sealed class WrapItem
    {
        public Element Element = null!;
        public Size Size;
        public Thickness Margin;
        public float Line;
        public float X;
        public float Y;
    }

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var maxWidth = MathF.Max(0, constraints.MaxWidth - insets.HorizontalSum);
        var width = ResolveWidth(element, constraints);
        var innerWidth = width is { } w ? MathF.Max(0, w - insets.HorizontalSum) : maxWidth;
        if (!float.IsFinite(innerWidth))
        {
            innerWidth = 10000;
        }

        var items = MeasureItems(element, context, innerWidth);
        var (usedWidth, usedHeight) = ArrangeLines(items, innerWidth, element.Style.Gap, apply: false);
        var resolvedWidth = width ?? (usedWidth + insets.HorizontalSum);
        var resolvedHeight = ResolveHeight(element, constraints) ?? (usedHeight + insets.VerticalSum);

        return new Size(
            LayoutUtil.Clamp(resolvedWidth, MathF.Max(constraints.MinWidth, element.Style.MinWidth ?? 0), MathF.Min(constraints.MaxWidth, element.Style.MaxWidth ?? float.PositiveInfinity)),
            LayoutUtil.Clamp(resolvedHeight, MathF.Max(constraints.MinHeight, element.Style.MinHeight ?? 0), MathF.Min(constraints.MaxHeight, element.Style.MaxHeight ?? float.PositiveInfinity)));
    }

    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var innerWidth = MathF.Max(0, bounds.Width - insets.HorizontalSum);
        var items = MeasureItems(element, context, innerWidth);
        ArrangeLines(items, innerWidth, element.Style.Gap, apply: true);

        foreach (var item in items)
        {
            context.Arrange(item.Element, new Rect(
                bounds.X + insets.Left + item.X + item.Margin.Left,
                bounds.Y + insets.Top + item.Y + item.Margin.Top,
                item.Size.Width,
                item.Size.Height));
        }
    }

    private static List<WrapItem> MeasureItems(Element element, LayoutContext context, float innerWidth)
    {
        var items = new List<WrapItem>(element.Children.Count);
        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position == PositionKind.Absolute)
            {
                continue;
            }

            var margin = child.Style.Margin;
            var available = MathF.Max(0, innerWidth - margin.HorizontalSum);
            var measured = context.Measure(child, new Constraints(0, available, 0, float.PositiveInfinity));
            items.Add(new WrapItem { Element = child, Size = measured, Margin = margin });
        }

        return items;
    }

    private static (float Width, float Height) ArrangeLines(List<WrapItem> items, float innerWidth, float gap, bool apply)
    {
        var cursorX = 0f;
        var cursorY = 0f;
        var lineHeight = 0f;
        var usedWidth = 0f;
        var line = 0;
        var hasItem = false;

        foreach (var item in items)
        {
            var outerWidth = item.Size.Width + item.Margin.HorizontalSum;
            var outerHeight = item.Size.Height + item.Margin.VerticalSum;
            if (hasItem && cursorX + outerWidth > innerWidth)
            {
                cursorX = 0;
                cursorY += lineHeight + gap;
                lineHeight = 0;
                line++;
                hasItem = false;
            }

            if (apply)
            {
                item.Line = line;
                item.X = cursorX;
                item.Y = cursorY;
            }

            cursorX += outerWidth + gap;
            usedWidth = MathF.Max(usedWidth, cursorX - gap);
            lineHeight = MathF.Max(lineHeight, outerHeight);
            hasItem = true;
        }

        return (usedWidth, cursorY + lineHeight);
    }

    private static float? ResolveWidth(Element element, Constraints constraints)
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

    private static float? ResolveHeight(Element element, Constraints constraints)
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
