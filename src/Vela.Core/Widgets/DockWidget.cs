namespace Vela;

/// <summary>
/// 五区域停靠容器。子项通过 <see cref="Style.Dock"/> 声明
/// Top/Bottom/Left/Right/Fill；按添加顺序，先声明的边先占用剩余矩形。
///
/// 典型 app shell：
/// <code>
/// Topbar (Top) → Sidebar (Left) → Status (Bottom) → Content (Fill)
/// </code>
/// 容器需要确定高度（通常是 Grow=1 或固定 Height），否则 Fill 区域没有可用空间。
/// </summary>
internal sealed class DockWidget : Widget
{
    public static DockWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var width = ResolveWidth(element, constraints) ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 0);
        var height = ResolveHeight(element, constraints) ?? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : 0);
        var remaining = new Rect(insets.Left, insets.Top, MathF.Max(0, width - insets.HorizontalSum), MathF.Max(0, height - insets.VerticalSum));

        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position == PositionKind.Absolute)
            {
                continue;
            }

            var dock = child.Style.Dock ?? DockSide.Fill;
            var margin = child.Style.Margin;
            if (dock is DockSide.Top or DockSide.Bottom)
            {
                var measured = context.Measure(child, new Constraints(remaining.Width - margin.HorizontalSum, remaining.Width - margin.HorizontalSum, 0, float.PositiveInfinity));
                var edge = ResolveEdge(child, measured.Height, vertical: true) + margin.VerticalSum;
                remaining = new Rect(remaining.X, remaining.Y, remaining.Width, MathF.Max(0, remaining.Height - edge));
            }
            else if (dock is DockSide.Left or DockSide.Right)
            {
                var measured = context.Measure(child, new Constraints(0, float.PositiveInfinity, remaining.Height - margin.VerticalSum, remaining.Height - margin.VerticalSum));
                var edge = ResolveEdge(child, measured.Width, vertical: false) + margin.HorizontalSum;
                remaining = new Rect(remaining.X, remaining.Y, MathF.Max(0, remaining.Width - edge), remaining.Height);
            }
        }

        var resolvedWidth = ResolveWidth(element, constraints) ?? (width + insets.HorizontalSum);
        var resolvedHeight = ResolveHeight(element, constraints) ?? (height + insets.VerticalSum);
        return new Size(
            LayoutUtil.Clamp(resolvedWidth, MathF.Max(constraints.MinWidth, element.Style.MinWidth ?? 0), MathF.Min(constraints.MaxWidth, element.Style.MaxWidth ?? float.PositiveInfinity)),
            LayoutUtil.Clamp(resolvedHeight, MathF.Max(constraints.MinHeight, element.Style.MinHeight ?? 0), MathF.Min(constraints.MaxHeight, element.Style.MaxHeight ?? float.PositiveInfinity)));
    }

    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var remaining = new Rect(
            bounds.X + insets.Left,
            bounds.Y + insets.Top,
            MathF.Max(0, bounds.Width - insets.HorizontalSum),
            MathF.Max(0, bounds.Height - insets.VerticalSum));

        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position == PositionKind.Absolute)
            {
                continue;
            }

            var dock = child.Style.Dock ?? DockSide.Fill;
            var margin = child.Style.Margin;
            var innerRemaining = remaining.Deflate(margin);
            var measured = context.Measure(child, new Constraints(
                dock is DockSide.Top or DockSide.Bottom ? innerRemaining.Width : 0,
                dock is DockSide.Top or DockSide.Bottom ? innerRemaining.Width : float.PositiveInfinity,
                dock is DockSide.Left or DockSide.Right ? innerRemaining.Height : 0,
                dock is DockSide.Left or DockSide.Right ? innerRemaining.Height : float.PositiveInfinity));

            var rect = dock switch
            {
                DockSide.Top => new Rect(
                    remaining.X + margin.Left,
                    remaining.Y + margin.Top,
                    MathF.Max(0, remaining.Width - margin.HorizontalSum),
                    MathF.Min(innerRemaining.Height, ResolveEdge(child, measured.Height, vertical: true))),
                DockSide.Bottom => new Rect(
                    remaining.X + margin.Left,
                    remaining.Bottom - margin.Bottom - MathF.Min(innerRemaining.Height, ResolveEdge(child, measured.Height, vertical: true)),
                    MathF.Max(0, remaining.Width - margin.HorizontalSum),
                    MathF.Min(innerRemaining.Height, ResolveEdge(child, measured.Height, vertical: true))),
                DockSide.Left => new Rect(
                    remaining.X + margin.Left,
                    remaining.Y + margin.Top,
                    MathF.Min(innerRemaining.Width, ResolveEdge(child, measured.Width, vertical: false)),
                    MathF.Max(0, remaining.Height - margin.VerticalSum)),
                DockSide.Right => new Rect(
                    remaining.Right - margin.Right - MathF.Min(innerRemaining.Width, ResolveEdge(child, measured.Width, vertical: false)),
                    remaining.Y + margin.Top,
                    MathF.Min(innerRemaining.Width, ResolveEdge(child, measured.Width, vertical: false)),
                    MathF.Max(0, remaining.Height - margin.VerticalSum)),
                _ => new Rect(
                    remaining.X + margin.Left,
                    remaining.Y + margin.Top,
                    MathF.Max(0, remaining.Width - margin.HorizontalSum),
                    MathF.Max(0, remaining.Height - margin.VerticalSum)),
            };

            context.Arrange(child, rect);

            switch (dock)
            {
                case DockSide.Top:
                    remaining = new Rect(remaining.X, rect.Bottom + margin.Bottom, remaining.Width, MathF.Max(0, remaining.Height - rect.Height - margin.VerticalSum));
                    break;
                case DockSide.Bottom:
                    remaining = new Rect(remaining.X, remaining.Y, remaining.Width, MathF.Max(0, remaining.Height - rect.Height - margin.VerticalSum));
                    break;
                case DockSide.Left:
                    remaining = new Rect(rect.Right + margin.Right, remaining.Y, MathF.Max(0, remaining.Width - rect.Width - margin.HorizontalSum), remaining.Height);
                    break;
                case DockSide.Right:
                    remaining = new Rect(remaining.X, remaining.Y, MathF.Max(0, remaining.Width - rect.Width - margin.HorizontalSum), remaining.Height);
                    break;
                default:
                    remaining = Rect.Empty;
                    break;
            }
        }
    }

    private static float ResolveEdge(Element child, float measured, bool vertical)
    {
        var dimension = vertical ? child.Style.Height : child.Style.Width;
        return dimension.IsAuto ? measured : MathF.Max(0, dimension.Value);
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
