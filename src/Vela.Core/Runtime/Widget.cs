namespace Vela;

/// <summary>
/// 控件的行为层。
///
/// 关键设计：Widget 是**无状态单例**（例如 <c>ButtonWidget.Instance</c>），
/// 所有差异数据放在本帧的 <see cref="Element"/> 上。因此不要在这里保存
/// 某个具体控件的文本、颜色或滚动位置。
///
/// 生命周期固定为：
/// <list type="number">
/// <item>Measure：父级给出约束，返回自身 border-box 尺寸；</item>
/// <item>Arrange：父级给出最终矩形，递归安排子级；</item>
/// <item>PaintContent：把背景/内容写入 DrawList；</item>
/// <item>PaintOverlay：在子级之后绘制焦点环、滚动条等。</item>
/// </list>
/// </summary>
internal abstract class Widget
{
    /// <summary>是否需要命中测试和指针响应。</summary>
    public virtual bool Interactive => false;

    /// <summary>是否可以被 Tab/鼠标聚焦。</summary>
    public virtual bool Focusable => false;

    /// <summary>是否响应 Enter/Space 的“点击”语义。</summary>
    public virtual bool Activatable => false;

    /// <summary>
    /// 测量控件。实现必须返回有限、非负的尺寸；不能在这里修改子级几何。
    /// 父级通过 <see cref="LayoutContext.Measure"/> 调用，以保证禁止 NaN/Infinity。
    /// </summary>
    public abstract Size Measure(Element element, LayoutContext context, Constraints constraints);

    /// <summary>
    /// 安排控件。通常实现会遍历 <see cref="Element.Children"/> 并调用
    /// <see cref="LayoutContext.Arrange"/>；容器控件必须覆盖此方法。
    /// </summary>
    public virtual void Arrange(Element element, LayoutContext context, Rect bounds)
    {
    }

    /// <summary>
    /// 绘制控件内容。只能写 DrawList 或读取状态，不应修改应用状态或布局。
    /// 背景/边框通常由 Frame 的通用绘制阶段处理。
    /// </summary>
    public virtual void PaintContent(Element element, PaintContext context)
    {
    }

    /// <summary>
    /// 在子级绘制之后调用，且不强制受内容裁剪限制。
    /// 适合焦点环、滚动条、浮出内容等需要覆盖子级的装饰。
    /// </summary>
    public virtual void PaintOverlay(Element element, PaintContext context)
    {
    }
}

/// <summary>
/// 布局阶段的上下文和唯一入口。
///
/// 父级不直接调用子级 <see cref="Widget.Measure"/>，而是通过这里：
/// 这里会过滤不可见元素、规范化约束、检查非有限尺寸，并写入
/// <see cref="Element.MeasuredSize"/>。这样所有布局实现共享同一套安全边界。
/// </summary>
internal sealed class LayoutContext
{
    public required Ui Ui { get; init; }
    public required Theme Theme { get; init; }
    public required ITextEngine Text { get; init; }

    /// <summary>本帧时间增量；布局动画/滚动使用。</summary>
    public required double DeltaTime { get; init; }

    /// <summary>
    /// 测量一个元素。不可见元素直接返回零尺寸，因此 <c>Visible=false</c>
    /// 不占布局空间；如果需要占位，请用透明度或显式尺寸而不是 Visible。
    /// </summary>
    public Size Measure(Element element, Constraints constraints)
    {
        if (!element.Style.Visible)
        {
            return Size.Zero;
        }

        // 约束规范化：NaN 视为 0，负值归零，并保证 min 不超过 max。
        var widthMax = MathF.Max(constraints.MinWidth, constraints.MaxWidth);
        var heightMax = MathF.Max(constraints.MinHeight, constraints.MaxHeight);
        var normalized = new Constraints(
            MathF.Max(0, constraints.MinWidth),
            float.IsNaN(widthMax) ? 0 : widthMax,
            MathF.Max(0, constraints.MinHeight),
            float.IsNaN(heightMax) ? 0 : heightMax);

        var size = element.Widget.Measure(element, this, normalized);
        if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height))
        {
            throw new InvalidOperationException($"Widget {element.DebugName} returned a non-finite size {size}.");
        }

        size = new Size(MathF.Max(0, size.Width), MathF.Max(0, size.Height));
        element.MeasuredSize = size;
        return size;
    }

    /// <summary>
    /// 给元素最终矩形，并写入 Bounds/ContentBounds，然后递归 Arrange 子级。
    /// Bounds 是 border-box；ContentBounds 已扣掉 padding + border。
    /// </summary>
    public void Arrange(Element element, Rect bounds)
    {
        if (bounds.Width < 0 || bounds.Height < 0)
        {
            bounds = new Rect(bounds.X, bounds.Y, MathF.Max(0, bounds.Width), MathF.Max(0, bounds.Height));
        }

        element.Bounds = bounds;
        element.ContentBounds = bounds.Deflate(LayoutUtil.Insets(element.Style));
        element.Widget.Arrange(element, this, bounds);
    }
}

/// <summary>
/// 所有布局实现共用的尺寸/裁剪辅助函数。
/// 这里集中处理“固定 / 百分比 / auto + min/max + padding/border”的解析，
/// 避免 Flex、Grid、Text、Button 各自实现一套不一致的规则。
/// </summary>
internal static class LayoutUtil
{
    /// <summary>padding + border 的合计内缩；所有内容矩形都用它计算。</summary>
    public static Thickness Insets(Style style)
    {
        var border = style.BorderWidth;
        return new Thickness(
            style.Padding.Left + border,
            style.Padding.Top + border,
            style.Padding.Right + border,
            style.Padding.Bottom + border);
    }

    public static float MinWidth(Element e) => MathF.Max(0, e.Style.MinWidth ?? 0);
    public static float MinHeight(Element e) => MathF.Max(0, e.Style.MinHeight ?? 0);
    public static float MaxWidth(Element e, float fallback = float.PositiveInfinity) => MathF.Max(MinWidth(e), e.Style.MaxWidth ?? fallback);
    public static float MaxHeight(Element e, float fallback = float.PositiveInfinity) => MathF.Max(MinHeight(e), e.Style.MaxHeight ?? fallback);

    /// <summary>解析宽度；百分比基数是 available，auto 回退到 contentWidth。</summary>
    public static float ResolveWidth(Element e, float available, float contentWidth)
    {
        var width = e.Style.Width.Kind switch
        {
            DimensionKind.Points => e.Style.Width.Value,
            DimensionKind.Percent => float.IsFinite(available) ? available * e.Style.Width.Value : contentWidth,
            _ => contentWidth,
        };

        width = Math.Clamp(width, MinWidth(e), MaxWidth(e));
        if (e.Style.AspectRatio is { } ratio && ratio > 0 && e.Style.Height.IsAuto)
        {
            // AspectRatio 需要同时知道两个轴，具体在 ResolveLeaf/容器中应用。
        }

        return width;
    }

    /// <summary>解析高度；规则与 <see cref="ResolveWidth"/> 对称。</summary>
    public static float ResolveHeight(Element e, float available, float contentHeight)
    {
        var height = e.Style.Height.Kind switch
        {
            DimensionKind.Points => e.Style.Height.Value,
            DimensionKind.Percent => float.IsFinite(available) ? available * e.Style.Height.Value : contentHeight,
            _ => contentHeight,
        };

        return Math.Clamp(height, MinHeight(e), MaxHeight(e));
    }

    /// <summary>
    /// 叶子控件的尺寸解析：内容尺寸 + padding/border，再应用紧约束、
    /// 固定/百分比/auto、min/max 和 AspectRatio。
    ///
    /// 容器通常自己实现更复杂的宽高解析，不应直接依赖这里。
    /// </summary>
    public static Size ResolveLeaf(Element e, Constraints c, Size content)
    {
        var insets = Insets(e.Style);
        var naturalWidth = content.Width + insets.HorizontalSum;
        var naturalHeight = content.Height + insets.VerticalSum;

        float width;
        if (c.IsTightWidth)
        {
            width = c.MinWidth;
        }
        else if (!e.Style.Width.IsAuto)
        {
            width = e.Style.Width.Kind == DimensionKind.Percent && float.IsFinite(c.MaxWidth)
                ? c.MaxWidth * e.Style.Width.Value
                : e.Style.Width.Value;
        }
        else
        {
            width = naturalWidth;
        }

        float height;
        if (c.IsTightHeight)
        {
            height = c.MinHeight;
        }
        else if (!e.Style.Height.IsAuto)
        {
            height = e.Style.Height.Kind == DimensionKind.Percent && float.IsFinite(c.MaxHeight)
                ? c.MaxHeight * e.Style.Height.Value
                : e.Style.Height.Value;
        }
        else
        {
            height = naturalHeight;
        }

        if (e.Style.AspectRatio is { } ratio && ratio > 0)
        {
            if (e.Style.Width.IsAuto && e.Style.Height.IsAuto)
            {
                height = width / ratio;
            }
            else if (!e.Style.Width.IsAuto && e.Style.Height.IsAuto)
            {
                height = width / ratio;
            }
            else if (e.Style.Width.IsAuto && !e.Style.Height.IsAuto)
            {
                width = height * ratio;
            }
        }

        width = Clamp(width, MathF.Max(c.MinWidth, MinWidth(e)), MathF.Min(c.MaxWidth, MaxWidth(e)));
        height = Clamp(height, MathF.Max(c.MinHeight, MinHeight(e)), MathF.Min(c.MaxHeight, MaxHeight(e)));
        return new Size(width, height);
    }

    /// <summary>把尺寸同时夹在父约束和自身 min/max 内。</summary>
    public static Size Constrain(Element e, Constraints c, Size size)
    {
        var width = Clamp(size.Width, MathF.Max(c.MinWidth, MinWidth(e)), MathF.Min(c.MaxWidth, MaxWidth(e)));
        var height = Clamp(size.Height, MathF.Max(c.MinHeight, MinHeight(e)), MathF.Min(c.MaxHeight, MaxHeight(e)));
        return new Size(width, height);
    }

    /// <summary>当 max &lt; min 时优先保证 min，避免布局出现负空间。</summary>
    public static float Clamp(float value, float min, float max)
    {
        if (max < min)
        {
            max = min;
        }

        return Math.Clamp(value, min, max);
    }

    public static bool IsRow(FlexDirection direction) => direction is FlexDirection.Row or FlexDirection.RowReverse;
    public static bool IsReversed(FlexDirection direction) => direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;

    /// <summary>百分比布局的基数：无限约束时使用 fallback。</summary>
    public static float PercentBase(float max, float fallback) => float.IsFinite(max) ? max : fallback;
}

/// <summary>
/// 绘制阶段的只读上下文。
///
/// 控件通过它访问主题、文本引擎、当前元素、输入和父级裁剪矩形。
/// 它不提供布局修改入口，避免 Paint 阶段改变几何。
/// </summary>
internal sealed class PaintContext
{
    public required Ui Ui { get; init; }
    public required DrawList DrawList { get; init; }
    public required Theme Theme { get; init; }
    public required ITextEngine Text { get; init; }
    public required Element Element { get; init; }
    public required FrameInput Input { get; init; }
    public required Rect Clip { get; init; }
    public int Depth { get; init; }

    public FontSpec DefaultFont => Element.Font;
    public Color Foreground => Element.Foreground;
}
