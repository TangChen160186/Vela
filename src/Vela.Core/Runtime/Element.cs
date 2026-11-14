namespace Vela;

/// <summary>
/// 一帧内的元素实例。
///
/// 重要：Element 只活当前帧。应用不能保存它，也不应该把它当控件句柄。
/// 需要跨帧保留的数据必须放到 <see cref="StateStore"/>，以稳定 Id 为键。
///
/// 字段可以按四组理解：
/// <list type="bullet">
/// <item>身份：Widget/Id/Parent/Children/DebugName；</item>
/// <item>声明：Style/Text/Icon/Payload/字体和颜色；</item>
/// <item>交互语义：Interactive/Focusable/TextInput/HandlesArrowKeys/Accessibility；</item>
/// <item>本帧结果：Bounds/ContentBounds/MeasuredSize/Response/滚动/浮层。</item>
/// </list>
/// </summary>
internal sealed class Element
{
    // ---- 身份与树结构 -------------------------------------------------

    /// <summary>控件行为（无状态单例）。</summary>
    public required Widget Widget { get; init; }

    /// <summary>
    /// 稳定 ID。由父 Id + 本地 key 组合；滚动、焦点、文本选区、动画都依赖它。
    /// 重复 label 的循环必须显式 id 或 PushId。
    /// </summary>
    public required Id Id { get; init; }

    /// <summary>本帧样式；通常来自控件 API 的 style 参数或主题默认值。</summary>
    public required Style Style { get; set; }

    /// <summary>父元素；root 为 null。</summary>
    public required Element? Parent { get; init; }

    /// <summary>子元素，按添加顺序（也就是绘制和多数布局的顺序）。</summary>
    public List<Element> Children { get; } = new(4);

    /// <summary>诊断名，例如 button/text-field，不是业务 ID。</summary>
    public string DebugName { get; init; } = string.Empty;

    // ---- 控件声明数据 -------------------------------------------------

    public string? Text { get; set; }
    public TextFormat TextFormat { get; set; }
    public IconData? Icon { get; set; }

    /// <summary>
    /// 控件私有数据。不同 Widget 会放不同的 record（按钮变体、滚动条配置、
    /// 数据表列定义等）；Core 本身不解释它。
    /// </summary>
    public object? Payload { get; set; }

    // ---- 交互/语义标记 ------------------------------------------------

    public bool Interactive { get; set; }
    public bool Focusable { get; set; }

    /// <summary>是否响应 Enter/Space 的点击激活。</summary>
    public bool Activatable { get; set; }

    public bool Disabled { get; set; }

    /// <summary>布局隐藏（滚动区域会把视口外的元素标记为 hidden）。</summary>
    public bool LayoutHidden { get; set; }

    public bool IsScrollbar { get; set; }
    public bool IsScrollContent { get; set; }

    /// <summary>是否有内部滚动逻辑，供 InteractionManager 的滚轮查找。</summary>
    public bool IsScrollable { get; set; }

    /// <summary>文本输入控件；全局方向键导航应让位给控件自己。</summary>
    public bool TextInput { get; set; }

    /// <summary>控件自行处理方向键（滑块、分割条、虚拟列表等）。</summary>
    public bool HandlesArrowKeys { get; set; }

    // 可访问性语义；内置控件由 Ui 方法设置，自定义控件用 Ui.Semantic。
    public AccessibilityRole AccessibilityRole { get; set; }
    public string? AccessibilityName { get; set; }
    public string? AccessibilityValue { get; set; }
    public string? AccessibilityHelp { get; set; }

    // 本帧滚动快照，供 FrameSnapshot 记录给下一帧的滚轮/滚动条命中。
    public float ScrollOffset { get; set; }
    public float ScrollContentHeight { get; set; }
    public float ScrollViewportHeight { get; set; }

    // ---- 绘制样式 -----------------------------------------------------

    public CursorKind Cursor { get; set; } = CursorKind.Default;
    public Color Foreground { get; set; }
    public FontSpec Font { get; set; }

    /// <summary>
    /// Visible=false 的元素不参与测量/绘制；LayoutHidden 还额外用于滚动裁剪。
    /// 需要“不可见但占位”时应使用透明度或固定尺寸，不要用 Visible。
    /// </summary>
    public bool IsVisible => Style.Visible && !LayoutHidden;

    // ---- 本帧布局/交互输出 -------------------------------------------

    /// <summary>最终 border-box。</summary>
    public Rect Bounds { get; set; }

    /// <summary>Bounds 去掉 padding + border 后的内容矩形。</summary>
    public Rect ContentBounds { get; set; }

    public Size MeasuredSize { get; set; }

    /// <summary>
    /// 从上一帧 InteractionManager 解析出的本帧交互结果。
    /// 因此 Clicked 是“上一帧按下并释放”的结果，不是平台事件回调。
    /// </summary>
    public Response Response { get; set; }

    // ---- 浮层 ---------------------------------------------------------

    public bool IsOverlayRoot { get; set; }
    public bool OverlayPassThrough { get; set; }
    public bool OverlayTrapFocus { get; set; }
    public int OverlayLayer { get; set; }
    public OverlayPlacement? OverlayPlacement { get; set; }

    // 滚动条几何：由 ScrollWidget 在本帧写入，供下一帧拖动命中。
    public Rect ScrollTrack { get; set; }
    public Rect ScrollThumb { get; set; }

    // 绝对定位子项的最终偏移；由 Flex/Grid 在 arrange 时使用。
    public float AbsoluteX { get; set; }
    public float AbsoluteY { get; set; }

    /// <summary>元素是否属于某个浮层子树。</summary>
    public bool IsInOverlay => IsOverlayRoot || Parent is { IsOverlayRoot: true } || Parent?.IsInOverlay == true;

    public override string ToString() => $"{DebugName} {Id} {Bounds}";
}

/// <summary>
/// 浮层根节点的定位描述。所有数值使用逻辑像素（DIP）。
/// 具体解析在 Frame.ArrangeOverlay：先测量，再按 Alignment 计算位置，
/// 最后做 viewport 约束。
/// </summary>
internal sealed class OverlayPlacement
{
    public OverlayAlignment Alignment { get; init; } = OverlayAlignment.AnchorBottomStart;

    /// <summary>锚点元素 Id；0 表示使用 Point/鼠标位置。</summary>
    public ulong AnchorId { get; init; }

    public Vec2 Point { get; init; }
    public Thickness Margin { get; init; } = Thickness.All(8);
    public float? Width { get; init; }
    public float? MinWidth { get; init; }
    public float MaxWidth { get; init; } = float.PositiveInfinity;
    public float MaxHeight { get; init; } = float.PositiveInfinity;

    /// <summary>是否把最终矩形夹在 viewport 内。</summary>
    public bool ConstrainToViewport { get; init; } = true;
}

internal enum OverlayAlignment
{
    FullScreen,
    AnchorBottomStart,
    AnchorBottomEnd,
    AnchorTopStart,
    AtPoint,
    Center,
    BottomEnd,
    TopEnd,
    TopStart,
}
