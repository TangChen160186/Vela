namespace Vela;

internal sealed partial class Frame
{
    /// <summary>
    /// 布局阶段：
    /// 1) root 使用 viewport 的紧约束（窗口大小是最终事实）；
    /// 2) 普通子级由 FlexWidget 递归求解；
    /// 3) 浮层按自己的 placement 单独测量/定位，不参与 root 的 flex 流。
    ///
    /// 浮层必须在普通树之后布局，因为它们依赖 viewport 和锚点几何。
    /// </summary>
    private void PerformLayout()
    {
        var viewport = new Rect(0, 0, _input.ViewportSize.Width, _input.ViewportSize.Height);
        Layout.Measure(Root, Constraints.Tight(_input.ViewportSize));
        Layout.Arrange(Root, viewport);

        SortedOverlays.Clear();
        SortedOverlays.AddRange(_overlays.OrderBy(x => x.OverlayLayer));
        foreach (var overlay in SortedOverlays)
        {
            ArrangeOverlay(overlay, viewport);
        }
    }

    /// <summary>
    /// 浮层定位。关键点：
    /// <list type="bullet">
    /// <item>先按 placement 的最大尺寸测量，得到内容自然尺寸；</item>
    /// <item>锚点使用上一帧快照（所以 popup 能对齐到按钮）；</item>
    /// <item>Center/AtPoint/Anchor 等模式只计算位置，不改变内部布局；</item>
    /// <item>最后把矩形夹在 viewport 内，避免菜单跑出窗口。</item>
    /// </list>
    /// </summary>
    private void ArrangeOverlay(Element overlay, Rect viewport)
    {
        var placement = overlay.OverlayPlacement ?? new OverlayPlacement();
        if (placement.Alignment == OverlayAlignment.FullScreen)
        {
            Layout.Measure(overlay, Constraints.Tight(new Size(viewport.Width, viewport.Height)));
            Layout.Arrange(overlay, viewport);
            return;
        }

        var maxWidth = MathF.Max(0, viewport.Width - placement.Margin.HorizontalSum);
        var maxHeight = MathF.Max(0, viewport.Height - placement.Margin.VerticalSum);
        if (placement.Width is { } fixedWidth)
        {
            maxWidth = MathF.Min(maxWidth, MathF.Max(0, fixedWidth));
        }

        maxWidth = MathF.Min(maxWidth, placement.MaxWidth);
        maxHeight = MathF.Min(maxHeight, placement.MaxHeight);

        var constraints = placement.Width is { } requestedWidth
            ? new Constraints(MathF.Min(requestedWidth, maxWidth), MathF.Min(requestedWidth, maxWidth), 0, maxHeight)
            : Constraints.Loose(maxWidth, maxHeight);

        var size = Layout.Measure(overlay, constraints);
        var width = MathF.Min(size.Width, maxWidth);
        var height = MathF.Min(size.Height, maxHeight);

        var anchorIndex = placement.AnchorId == 0 ? -1 : Previous.FindNode(placement.AnchorId);
        var anchor = anchorIndex >= 0
            ? Previous.Nodes[anchorIndex].Bounds
            : new Rect(_input.PointerPosition.X, _input.PointerPosition.Y, 0, 0);

        Vec2 position;
        switch (placement.Alignment)
        {
            case OverlayAlignment.Center:
                position = new Vec2((viewport.Width - width) * 0.5f, (viewport.Height - height) * 0.5f);
                break;
            case OverlayAlignment.AtPoint:
                position = placement.Point + new Vec2(placement.Margin.Left, placement.Margin.Top);
                break;
            case OverlayAlignment.BottomEnd:
                position = new Vec2(viewport.Width - width - placement.Margin.Right, viewport.Height - height - placement.Margin.Bottom);
                break;
            case OverlayAlignment.TopEnd:
                position = new Vec2(viewport.Width - width - placement.Margin.Right, placement.Margin.Top);
                break;
            case OverlayAlignment.TopStart:
                position = new Vec2(placement.Margin.Left, placement.Margin.Top);
                break;
            case OverlayAlignment.AnchorBottomEnd:
                position = new Vec2(anchor.Right - width, anchor.Bottom + placement.Margin.Top);
                break;
            case OverlayAlignment.AnchorTopStart:
                position = new Vec2(anchor.Left, anchor.Top - height - placement.Margin.Bottom);
                break;
            default:
                position = new Vec2(anchor.Left, anchor.Bottom + placement.Margin.Top);
                break;
        }

        if (placement.ConstrainToViewport)
        {
            var minX = placement.Margin.Left;
            var maxX = viewport.Width - width - placement.Margin.Right;
            var minY = placement.Margin.Top;
            var maxY = viewport.Height - height - placement.Margin.Bottom;
            position = new Vec2(
                LayoutUtil.Clamp(position.X, minX, MathF.Max(minX, maxX)),
                LayoutUtil.Clamp(position.Y, minY, MathF.Max(minY, maxY)));
        }

        Layout.Arrange(overlay, new Rect(position.X, position.Y, width, height));
    }

    /// <summary>
    /// 绘制入口：先普通树，再按 OverlayLayer 升序绘制浮层。
    /// 浮层后绘制意味着它们覆盖普通内容；与命中测试的高层优先一致。
    /// </summary>
    private void PaintFrame()
    {
        var viewport = new Rect(0, 0, _input.ViewportSize.Width, _input.ViewportSize.Height);
        PaintElement(Root, viewport, 0);
        foreach (var overlay in SortedOverlays)
        {
            PaintElement(overlay, viewport, 0);
        }

        _ui.DrawList.ValidateBalanced();
    }

    /// <summary>
    /// 递归绘制单个元素。顺序固定：
    /// 背景/边框/阴影 → 内容裁剪 → PaintContent → 子级 → PaintOverlay。
    ///
    /// 需要裁剪的元素会先向 DrawList 写入 PushClip，结束写 PopClip；
    /// 任何分支都必须保持 push/pop 平衡，否则后续命令会被错误裁剪。
    /// </summary>
    private void PaintElement(Element element, Rect clip, int depth)
    {
        if (!element.IsVisible || element.Style.Opacity <= 0.001f)
        {
            return;
        }

        if (element.Bounds.IsEmpty && element.Children.Count == 0)
        {
            return;
        }

        if (!element.Bounds.Intersects(clip) && !element.Style.ClipContent)
        {
            return;
        }

        var opacityPushed = false;
        if (element.Style.Opacity < 0.999f)
        {
            _ui.DrawList.PushOpacity(element.Style.Opacity);
            opacityPushed = true;
        }

        var radius = element.Style.CornerRadius.Clamp(MathF.Min(element.Bounds.Width, element.Bounds.Height) * 0.5f);
        if (element.Style.Shadow is { } shadow)
        {
            _ui.DrawList.Shadow(element.Bounds, shadow, radius);
        }

        if (element.Style.Background is { } background)
        {
            _ui.DrawList.FillRect(element.Bounds, background, radius);
        }

        if (element.Style.BorderWidth > 0 && element.Style.BorderColor is { } border)
        {
            var inset = element.Style.BorderWidth * 0.5f;
            _ui.DrawList.StrokeRect(element.Bounds.Deflate(Thickness.All(inset)), border, element.Style.BorderWidth, radius);
        }

        var clipPushed = false;
        var contentClip = clip;
        if (element.Style.ClipContent)
        {
            contentClip = clip.Intersect(element.ContentBounds);
            if (contentClip.IsEmpty)
            {
                if (opacityPushed)
                {
                    _ui.DrawList.PopOpacity();
                }

                return;
            }

            _ui.DrawList.PushClip(contentClip);
            clipPushed = true;
        }

        var context = new PaintContext
        {
            Ui = _ui,
            DrawList = _ui.DrawList,
            Theme = _theme,
            Text = _ui.TextEngine,
            Element = element,
            Input = _input,
            Clip = contentClip,
            Depth = depth,
        };

        element.Widget.PaintContent(element, context);

        foreach (var child in element.Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            if (!child.Bounds.Intersects(contentClip) && !child.Style.ClipContent)
            {
                continue;
            }

            PaintElement(child, contentClip, depth + 1);
        }

        if (clipPushed)
        {
            _ui.DrawList.PopClip();
        }

        element.Widget.PaintOverlay(element, context);

        if (opacityPushed)
        {
            _ui.DrawList.PopOpacity();
        }
    }
}
