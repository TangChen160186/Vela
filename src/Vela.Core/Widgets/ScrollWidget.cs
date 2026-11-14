namespace Vela;

/// <summary>
/// 垂直滚动容器。结构固定为一个内容子元素（IsScrollContent）：
/// 测量时给内容无限高度，再把内容高度/视口高度写入 ScrollState。
///
/// 绘制时裁剪视口内容，滚动条在 PaintOverlay 中绘制；滚动条几何同时写入
/// Element，供下一帧 FrameSnapshot 的拖动命中使用。
/// </summary>
internal sealed class ScrollWidget : Widget
{
    public static ScrollWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var maxWidth = MathF.Max(0, constraints.MaxWidth - insets.HorizontalSum);
        var maxHeight = MathF.Max(0, constraints.MaxHeight - insets.VerticalSum);
        var content = FindContent(element);
        if (content is null)
        {
            return LayoutUtil.ResolveLeaf(element, constraints, Size.Zero);
        }

        float width;
        if (constraints.IsTightWidth)
        {
            width = constraints.MinWidth - insets.HorizontalSum;
        }
        else if (!element.Style.Width.IsAuto)
        {
            width = (element.Style.Width.Kind == DimensionKind.Percent && float.IsFinite(constraints.MaxWidth)
                ? constraints.MaxWidth * element.Style.Width.Value
                : element.Style.Width.Value) - insets.HorizontalSum;
        }
        else
        {
            width = maxWidth;
        }

        width = MathF.Max(0, width);
        var contentSize = context.Measure(content, new Constraints(width, width, 0, float.PositiveInfinity));
        float height;
        if (constraints.IsTightHeight)
        {
            height = constraints.MinHeight - insets.VerticalSum;
        }
        else if (!element.Style.Height.IsAuto)
        {
            height = (element.Style.Height.Kind == DimensionKind.Percent && float.IsFinite(constraints.MaxHeight)
                ? constraints.MaxHeight * element.Style.Height.Value
                : element.Style.Height.Value) - insets.VerticalSum;
        }
        else
        {
            height = MathF.Min(contentSize.Height, maxHeight);
        }

        height = MathF.Max(0, height);
        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        scroll.SetMetrics(contentSize.Height, height);

        var desired = new Size(width + insets.HorizontalSum, height + insets.VerticalSum);
        return LayoutUtil.Constrain(element, constraints, desired);
    }

    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var inner = new Rect(
            bounds.X + insets.Left,
            bounds.Y + insets.Top,
            MathF.Max(0, bounds.Width - insets.HorizontalSum),
            MathF.Max(0, bounds.Height - insets.VerticalSum));
        var content = FindContent(element);
        var scrollbar = FindScrollbar(element);
        if (content is null)
        {
            return;
        }

        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        var contentSize = context.Measure(content, new Constraints(inner.Width, inner.Width, 0, float.PositiveInfinity));
        scroll.SetMetrics(contentSize.Height, inner.Height);

        if (scrollbar is not null)
        {
            ScrollbarWidget.ProcessInput(scrollbar, scroll, context, inner);
        }

        scroll.Tick(context.DeltaTime);

        var contentRect = new Rect(inner.X, inner.Y - scroll.Offset, inner.Width, contentSize.Height);
        context.Arrange(content, contentRect);

        element.IsScrollable = scroll.IsScrollable;
        element.ScrollOffset = scroll.Offset;
        element.ScrollContentHeight = scroll.ContentHeight;
        element.ScrollViewportHeight = scroll.ViewportHeight;

        if (scrollbar is not null)
        {
            var track = new Rect(inner.Right - 12, inner.Y, 12, inner.Height);
            scrollbar.LayoutHidden = !scroll.IsScrollable;
            scrollbar.IsScrollable = false;
            if (scroll.IsScrollable)
            {
                context.Arrange(scrollbar, track);
                element.ScrollTrack = track;
                element.ScrollThumb = ScrollbarWidget.GetThumb(scroll, track);
                scroll.TrackBounds = track;
                scroll.ThumbBounds = element.ScrollThumb;
            }
            else
            {
                scrollbar.Bounds = Rect.Empty;
                scrollbar.ContentBounds = Rect.Empty;
                element.ScrollTrack = Rect.Empty;
                element.ScrollThumb = Rect.Empty;
                scroll.TrackBounds = Rect.Empty;
                scroll.ThumbBounds = Rect.Empty;
            }
        }
    }

    private static Element? FindContent(Element element)
    {
        foreach (var child in element.Children)
        {
            if (child.IsScrollContent)
            {
                return child;
            }
        }

        return null;
    }

    private static Element? FindScrollbar(Element element)
    {
        foreach (var child in element.Children)
        {
            if (child.IsScrollbar)
            {
                return child;
            }
        }

        return null;
    }
}

internal sealed class ScrollbarWidget : Widget
{
    public static ScrollbarWidget Instance { get; } = new();

    public override bool Interactive => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints) => Size.Zero;

    public override void PaintContent(Element element, PaintContext context)
    {
        if (element.Parent is null)
        {
            return;
        }

        var scroll = context.Ui.State.Get<ScrollState>(element.Parent.Id);
        if (scroll is null || !scroll.IsScrollable)
        {
            return;
        }

        var track = element.Bounds;
        if (track.Height <= 0)
        {
            return;
        }

        var thumb = GetThumb(scroll, track);
        var theme = context.Theme;
        var hovered = element.Response.Hovered || element.Response.Held;
        var width = hovered ? 7f : 5f;
        var thumbRect = new Rect(
            track.Center.X - (width * 0.5f),
            thumb.Y,
            width,
            MathF.Max(22, thumb.Height));
        var radius = CornerRadius.All(width * 0.5f);

        if (hovered)
        {
            context.DrawList.FillRect(track.Deflate(new Thickness(3, 2, 3, 2)), new Brush(theme.SurfaceHover.WithAlpha(0.45f)), CornerRadius.All(6));
        }

        context.DrawList.FillRect(thumbRect, new Brush(hovered ? theme.ScrollbarHover : theme.Scrollbar), radius);
    }

    public static Rect GetThumb(ScrollState state, Rect track)
    {
        if (!state.IsScrollable || track.Height <= 0)
        {
            return Rect.Empty;
        }

        var ratio = Math.Clamp(state.ViewportHeight / state.ContentHeight, 0, 1);
        var height = MathF.Max(26, track.Height * ratio);
        var maxOffset = MathF.Max(1, state.MaxOffset);
        var travel = MathF.Max(0, track.Height - height);
        var y = track.Top + (state.Offset / maxOffset * travel);
        return new Rect(track.X, y, track.Width, height);
    }

    public static void ProcessInput(Element element, ScrollState scroll, LayoutContext context, Rect viewport)
    {
        var input = context.Ui.Input;
        var response = element.Response;
        var drag = context.Ui.State.GetOrCreate<ScrollbarDragState>(element.Id);
        // Elements are rebuilt each frame, so their current Bounds are not resolved
        // yet while processing input. Use the track resolved last frame.
        var track = scroll.TrackBounds;

        if (!scroll.IsScrollable || track.Height <= 0)
        {
            drag.Dragging = false;
            return;
        }

        if (!input.IsButtonDown(PointerButton.Left))
        {
            drag.Dragging = false;
            return;
        }

        if (response.Pressed)
        {
            var thumb = GetThumb(scroll, track);
            drag.StartOffset = scroll.Offset;
            drag.StartPointerY = input.PointerPosition.Y;
            drag.TrackHeight = track.Height;
            drag.ThumbHeight = thumb.Height;
            drag.ContentHeight = scroll.ContentHeight;
            drag.ViewportHeight = scroll.ViewportHeight;
            drag.Dragging = true;

            if (!thumb.Contains(input.PointerPosition))
            {
                var travel = MathF.Max(1, track.Height - thumb.Height);
                var ratio = Math.Clamp((input.PointerPosition.Y - track.Top - (thumb.Height * 0.5f)) / travel, 0, 1);
                scroll.JumpTo(ratio * scroll.MaxOffset);
                drag.StartOffset = scroll.Offset;
            }
        }

        if (response.Active && drag.Dragging)
        {
            var travel = MathF.Max(1, drag.TrackHeight - drag.ThumbHeight);
            var maxOffset = MathF.Max(0, drag.ContentHeight - drag.ViewportHeight);
            var delta = input.PointerPosition.Y - drag.StartPointerY;
            scroll.JumpTo(drag.StartOffset + (delta / travel * maxOffset));
        }

        _ = viewport;
    }
}
