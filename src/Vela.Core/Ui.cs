using System.Diagnostics;

namespace Vela;

/// <summary>
/// 创建 <see cref="Ui"/> 的依赖项。
/// <para>
/// <see cref="TextEngine"/> 是唯一必填项；主题和剪贴板有安全默认值。
/// 无头测试通常传入 <c>FakeTextEngine</c>，桌面宿主传入 Skia 文本引擎。
/// </para>
/// </summary>
public sealed record UiOptions
{
    public required ITextEngine TextEngine { get; init; }
    public Theme Theme { get; init; } = Theme.Dark();
    public IClipboard Clipboard { get; init; } = NullClipboard.Instance;

    /// <summary>
    /// 是否生成 FrameDiagnostics。开发/测试打开，生产可按需关闭以省一点开销。
    /// </summary>
    public bool CaptureDiagnostics { get; init; }
}

/// <summary>
/// 即时模式 UI 上下文，是应用与框架之间的长生命周期对象。
///
/// 典型调用顺序由桌面宿主完成：
/// <code>
/// ui.BeginFrame(frameInput);
/// build(ui);              // 应用每帧声明界面
/// var result = ui.EndFrame();
/// </code>
///
/// 注意：
/// <list type="bullet">
/// <item>普通 UI 方法只能在 BeginFrame/EndFrame 之间调用；</item>
/// <item>每帧的 <c>Frame</c> 和 <c>Element</c> 都是临时的；</item>
/// <item>跨帧数据在 <see cref="StateStore"/>，键为 (Id, 状态类型)；</item>
/// <item>命中/焦点使用上一帧的 FrameSnapshot，因此新元素有一帧交互延迟。</item>
/// </list>
/// </summary>
public sealed partial class Ui
{
    private static readonly Style ColumnStyle = new() { Direction = FlexDirection.Column };
    private static readonly Style RowStyle = new() { Direction = FlexDirection.Row };
    private readonly ITextEngine _textEngine;
    private readonly IClipboard _clipboard;
    private readonly InteractionManager _interaction = new();
    private FrameSnapshot _previousSnapshot = FrameSnapshot.Empty;
    private Frame? _frame;
    private long _buildStarted;
    private bool _inFrame;
    private long _frameNumber;
    private Theme _theme;
    private Theme? _pendingTheme;

    public Ui(ITextEngine textEngine, Theme? theme = null, IClipboard? clipboard = null)
    {
        _textEngine = textEngine ?? throw new ArgumentNullException(nameof(textEngine));
        _theme = theme ?? Theme.Dark();
        _clipboard = clipboard ?? NullClipboard.Instance;
    }

    public Ui(UiOptions options)
        : this(options.TextEngine, options.Theme, options.Clipboard)
    {
        CaptureDiagnostics = options.CaptureDiagnostics;
    }

    /// <summary>
    /// 当前主题。帧内修改会延迟到下一帧生效，避免同一帧的布局/绘制/控件默认值
    /// 看到不同主题，导致颜色和尺寸在帧中途变化。
    /// </summary>
    public Theme Theme
    {
        get => _frame?.Theme ?? _theme;
        set
        {
            if (_inFrame)
            {
                _pendingTheme = value;
            }
            else
            {
                _theme = value;
            }
        }
    }

    public ITextEngine TextEngine => _textEngine;
    public IClipboard Clipboard => _clipboard;

    /// <summary>跨帧状态；元素/滚动/文本/动画都存这里。</summary>
    public StateStore State { get; } = new();

    /// <summary>当前帧的绘制命令；每帧 BeginFrame 清空。</summary>
    public DrawList DrawList { get; } = new();

    public FrameDiagnostics? LastDiagnostics { get; private set; }
    public FrameInput Input { get; private set; } = FrameInput.Empty(Size.Zero);
    public Size ViewportSize { get; private set; }
    public long FrameNumber => _frameNumber;
    public bool IsInFrame => _inFrame;
    public bool CaptureDiagnostics { get; set; }

    /// <summary>当前容器 Id；在帧外返回 Id.None。</summary>
    public Id CurrentId => _frame?.CurrentId ?? Id.None;

    /// <summary>
    /// 开始一帧。顺序很重要：
    /// 1) 保存输入；2) 让 InteractionManager 用上一帧快照解析交互；
    /// 3) 创建新的 Frame；4) 清空 DrawList。
    ///
    /// 交互在构建之前解析，这样控件在构建时就能读取本帧 Response。
    /// </summary>
    public void BeginFrame(FrameInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_inFrame)
        {
            throw new InvalidOperationException("BeginFrame was called while a frame is already open. Call EndFrame first.");
        }

        Input = input;
        ViewportSize = input.ViewportSize;
        State.BeginFrame(unchecked((int)input.FrameNumber));
        var interaction = _interaction.BeginFrame(input, _previousSnapshot, State, _theme);
        _buildStarted = Stopwatch.GetTimestamp();
        _frame = new Frame(this, input, _theme, _previousSnapshot, interaction, _buildStarted);
        _frameNumber = input.FrameNumber;
        _inFrame = true;
        DrawList.Clear();
    }

    /// <summary>
    /// 结束一帧：执行布局、快照、绘制、可访问性，返回可渲染结果。
    /// 无论成功与否都会清理当前 Frame；异常路径应调用 <see cref="AbortFrame"/>。
    /// </summary>
    public UiFrameResult EndFrame()
    {
        if (!_inFrame || _frame is null)
        {
            throw new InvalidOperationException("EndFrame was called without a matching BeginFrame.");
        }

        try
        {
            var result = _frame.Complete(_buildStarted, CaptureDiagnostics);
            _previousSnapshot = _frame.Snapshot ?? FrameSnapshot.Empty;
            LastDiagnostics = result.Diagnostics;
            return result;
        }
        finally
        {
            _frame = null;
            _inFrame = false;
            if (_pendingTheme is not null)
            {
                _theme = _pendingTheme;
                _pendingTheme = null;
            }
        }
    }

    /// <summary>应用构建抛异常时丢弃未完成帧，避免下一帧复用半成品状态。</summary>
    public void AbortFrame()
    {
        _frame = null;
        _inFrame = false;
        _pendingTheme = null;
    }

    internal void EndScope()
    {
        RequireFrame().EndScope();
    }

    /// <summary>
    /// 所有 UI 方法的统一守卫：在 BeginFrame/EndFrame 之外调用会抛出明确异常。
    /// 嵌套 Frame 不允许，因为状态和上一帧快照只有一个。
    /// </summary>
    internal Frame RequireFrame()
    {
        if (!_inFrame || _frame is null)
        {
            throw new InvalidOperationException("UI widgets can only be created between BeginFrame and EndFrame.");
        }

        return _frame;
    }

    /// <summary>压入一个局部 ID 作用域，适合循环中保证唯一性。</summary>
    public void PushId(string key) => RequireFrame().PushId(key);
    public void PushId(int index) => RequireFrame().PushId(index);
    public void PopId() => RequireFrame().PopId();

    /// <summary>
    /// 程序化聚焦。会立即同步 InteractionManager 和当前帧所有元素的 Response，
    /// 因此可以在同一帧后续代码看到 Focused 状态；如果没有当前帧，只保存到下一帧。
    /// </summary>
    public void Focus(Id id, bool keyboardVisible = false)
    {
        _interaction.SetFocus(id.Value, keyboardVisible);
        if (_frame is not null)
        {
            _frame.Interaction.ApplyFocus(id.Value, keyboardVisible);
            foreach (var element in _frame.AllElements)
            {
                element.Response = _frame.Interaction.Get(element.Id, element.Disabled);
            }
        }
    }

    public void ClearFocus() => _interaction.ClearFocus();

    /// <summary>
    /// 排入一次程序化点击，在下一帧产生 keyboard click 响应。
    /// UIA 的 Invoke 模式和测试都使用这条路径，避免复制点击语义。
    /// </summary>
    public void Invoke(Id id) => _interaction.RequestClick(id.Value);

    /// <summary>
    /// 开始一个 Column 容器。必须使用 <c>using</c> 释放作用域；
    /// 否则后续控件会成为该容器的子级。
    /// </summary>
    public UiScope Column(string? id = null, Style? style = null) =>
        RequireFrame().BeginContainer(FlexWidget.Instance, id, WithDirection(style, FlexDirection.Column), "column");

    /// <summary>开始 Row 容器；方向固定为横向。</summary>
    public UiScope Row(string? id = null, Style? style = null) =>
        RequireFrame().BeginContainer(FlexWidget.Instance, id, WithDirection(style, FlexDirection.Row), "row");

    /// <summary>通用 Flex 容器，方向由 Style.Direction 决定。</summary>
    public UiScope Panel(string? id = null, Style? style = null) =>
        RequireFrame().BeginContainer(FlexWidget.Instance, id, style ?? ColumnStyle, "panel");

    /// <summary>
    /// 主题化卡片容器：默认带 surface 背景、边框、圆角和阴影。
    /// 传入 style 会替换整套默认样式，而不是叠加。
    /// </summary>
    public UiScope Card(string? id = null, Style? style = null)
    {
        var resolved = style ?? new Style
        {
            Direction = FlexDirection.Column,
            Padding = Theme.CardPadding,
            Background = new Brush(Theme.Surface),
            BorderColor = Theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(Theme.RadiusLarge),
            Shadow = Theme.CardShadow,
        };
        return RequireFrame().BeginContainer(FlexWidget.Instance, id, resolved, "card");
    }

    /// <summary>
    /// 添加静态文本。文本不声明 ID（claimId=false），因为无交互状态；
    /// 但仍会进入可访问性树，角色为 StaticText。
    /// </summary>
    public void Text(string text, Style? style = null, FontSpec? font = null, Color? color = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(TextWidget.Instance, null, style, "text", claimId: false);
        element.Text = text ?? string.Empty;
        element.AccessibilityRole = AccessibilityRole.StaticText;
        element.AccessibilityName = text ?? string.Empty;
        if (font is { } fontOverride)
        {
            element.Font = fontOverride;
        }

        if (color is { } colorOverride)
        {
            element.Foreground = colorOverride;
        }

        element.TextFormat = Frame.CreateTextFormat(element.Style, element.Font);
    }

    public void Label(string text, Style? style = null, Color? color = null) =>
        Text(text, style, Theme.Body, color ?? Theme.TextMuted);

    public void Heading(string text, Style? style = null, Color? color = null) =>
        Text(text, style, Theme.Heading, color ?? Theme.Text);

    public void Caption(string text, Style? style = null, Color? color = null) =>
        Text(text, style, Theme.Caption, color ?? Theme.TextSubtle);

    /// <summary>图标是无交互叶子；颜色可从 style/参数覆盖。</summary>
    public void Icon(IconData icon, float size = 18, Color? color = null, Style? style = null)
    {
        var frame = RequireFrame();
        var effectiveStyle = style ?? new Style { Width = size, Height = size };
        var element = frame.AddElement(IconWidget.Instance, null, effectiveStyle, "icon", claimId: false);
        element.Icon = icon;
        element.Payload = size;
        if (color is { } value)
        {
            element.Foreground = value;
        }
    }

    public void Divider(Axis axis = Axis.Horizontal, Style? style = null)
    {
        var element = RequireFrame().AddElement(DividerWidget.Instance, null, style, "divider", claimId: false);
        element.Payload = axis;
    }

    public void Spacer(Style? style = null) =>
        RequireFrame().AddElement(SpacerWidget.Instance, null, style, "spacer", claimId: false);

    public void Space(float height) =>
        RequireFrame().AddElement(SpacerWidget.Instance, null, new Style { Height = height }, "space", claimId: false);

    /// <summary>
    /// 自定义绘制入口：不创建子控件，只把 CanvasView 回调留到 Paint 阶段。
    /// <paramref name="interactive"/> 为 true 时参与命中测试。
    /// </summary>
    public Response Canvas(string id, Action<CanvasView> paint, Style? style = null, bool interactive = false)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(CanvasWidget.Instance, id, style, "canvas", interactive, focusable: false);
        element.Payload = paint ?? throw new ArgumentNullException(nameof(paint));
        frame.UpdateResponse(element);
        return element.Response;
    }

    private static Style WithDirection(Style? style, FlexDirection direction)
    {
        if (style is null)
        {
            return direction == FlexDirection.Row ? RowStyle : ColumnStyle;
        }

        return style.Direction == direction ? style : style with { Direction = direction };
    }
}
