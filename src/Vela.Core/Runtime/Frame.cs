using System.Diagnostics;

namespace Vela;

/// <summary>
/// 一帧的可变工作区。
///
/// 职责边界：
/// <list type="bullet">
/// <item>构建：维护容器作用域、创建 Element、检查重复 ID；</item>
/// <item>布局：调用 <see cref="PerformLayout"/>（位于 Frame.Rendering.cs）；</item>
/// <item>快照：把最终几何/焦点/滚动信息写成下一帧可用的 FrameSnapshot；</item>
/// <item>绘制：遍历元素树输出 DrawList；</item>
/// <item>可访问性：生成语义树；</item>
/// <item>出帧：返回 UiFrameResult。</item>
/// </list>
///
/// Frame 每帧新建、帧末丢弃，不能跨帧保存。应用状态在 Ui.State，
/// 业务状态在应用自己的字段里。
/// </summary>
internal sealed partial class Frame
{
    private static readonly Style EmptyStyle = new();

    private readonly Ui _ui;
    private readonly FrameInput _input;
    private readonly Theme _theme;
    private readonly long _buildStarted;

    /// <summary>容器栈；[0] 是 root，最后一个是当前父级。</summary>
    private readonly List<Element> _scopeStack = new(16);

    /// <summary>本帧所有元素，按创建顺序，用于诊断/可访问性/绘制查找。</summary>
    private readonly List<Element> _allElements = new(256);

    /// <summary>浮层根节点；它们不在普通父子布局流中，单独布局。</summary>
    private readonly List<Element> _overlays = new(8);

    /// <summary>自动 ID 计数：同一父级下相同 debugName 的序号。</summary>
    private readonly Dictionary<(ulong Parent, string Name), int> _autoKeys = new();

    /// <summary>已声明的有状态 ID，用于重复 ID 的早期诊断。</summary>
    private readonly Dictionary<ulong, string> _claimedIds = new();

    /// <summary>ID 作用域栈；PushId/PopId 与容器作用域共享同一套前缀。</summary>
    private readonly IdScopeStack _ids = new();

    internal Frame(Ui ui, FrameInput input, Theme theme, FrameSnapshot previous, InteractionFrame interaction, long buildStarted)
    {
        _ui = ui;
        _input = input;
        _theme = theme;
        Previous = previous;
        Interaction = interaction;
        _buildStarted = buildStarted;

        var viewport = input.ViewportSize;
        Root = new Element
        {
            Widget = FlexWidget.Instance,
            Id = Id.From("root"),
            Style = new Style
            {
                Width = Dimension.Px(viewport.Width),
                Height = Dimension.Px(viewport.Height),
                Background = new Brush(theme.Background),
                Direction = FlexDirection.Column,
            },
            Parent = null,
            DebugName = "root",
            Foreground = theme.Text,
            Font = theme.Body,
            Cursor = CursorKind.Default,
        };
        Root.TextFormat = CreateTextFormat(Root.Style, Root.Font);
        Root.Response = interaction.Get(Root.Id);
        Root.Children.Clear();
        _scopeStack.Add(Root);
        _allElements.Add(Root);
        _claimedIds[Root.Id.Value] = "root";
    }

    public Ui Ui => _ui;
    public FrameInput Input => _input;
    public Theme Theme => _theme;
    public FrameSnapshot Previous { get; }
    public InteractionFrame Interaction { get; }
    public Element Root { get; }
    public IReadOnlyList<Element> AllElements => _allElements;
    public IReadOnlyList<Element> Overlays => _overlays;
    public Id CurrentId => _ids.Current;
    public int ScopeCount => _scopeStack.Count;
    public LayoutContext Layout { get; private set; } = null!;
    public List<Element> SortedOverlays { get; } = new(8);
    public FrameSnapshot? Snapshot { get; private set; }
    public CursorKind RequestedCursor { get; private set; }
    public Element Current => _scopeStack[^1];

    /// <summary>
    /// 创建一个元素并挂到当前容器下。这是所有控件 API 的公共落点。
    ///
    /// ID 规则：<c>parent.Id + key</c>。key 为 null 时按同父级同 debugName
    /// 自动编号，因此纯展示元素可以省略 ID；有状态控件应显式提供 key。
    /// </summary>
    public Element AddElement(
        Widget widget,
        string? key,
        Style? style,
        string debugName,
        bool interactive = false,
        bool focusable = false,
        bool activatable = false,
        bool claimId = true)
    {
        var parent = Current;
        var keyForId = key ?? NextAutoKey(debugName);
        var element = new Element
        {
            Widget = widget,
            Id = Id.Combine(parent.Id, keyForId),
            Style = style ?? EmptyStyle,
            Parent = parent,
            DebugName = debugName,
            Foreground = style?.Foreground ?? parent.Foreground,
            Font = style?.Font ?? parent.Font,
            Cursor = style?.Cursor ?? CursorKind.Default,
            Interactive = interactive,
            Focusable = focusable,
            Activatable = activatable,
        };
        element.TextFormat = CreateTextFormat(element.Style, element.Font);
        element.Response = Interaction.Get(element.Id);
        parent.Children.Add(element);
        _allElements.Add(element);

        if (claimId)
        {
            Claim(element.Id, debugName);
        }

        return element;
    }

    /// <summary>
    /// 创建容器并进入其作用域。返回的 UiScope 在 Dispose 时调用 EndScope；
    /// 因此调用方必须使用 <c>using</c>，否则后续元素会挂到错误父级。
    /// </summary>
    public UiScope BeginContainer(Widget widget, string? id, Style? style, string debugName, bool claimId = true)
    {
        var element = AddElement(widget, id, style, debugName, claimId: claimId);
        PushScope(element);
        return new UiScope(_ui);
    }

    /// <summary>进入容器：同时压入元素栈和 ID 前缀栈，保证二者始终同步。</summary>
    public void PushScope(Element element)
    {
        _scopeStack.Add(element);
        _ids.Push(element.Id);
    }

    /// <summary>
    /// 离开容器。会校验：
    /// <list type="number">
    /// <item>不能关闭 root；</item>
    /// <item>容器内手动 PushId 必须全部 PopId；</item>
    /// <item>ID 栈顶必须正好是当前容器。</item>
    /// </list>
    /// 这样漏写 using/PopId 会在出帧前得到明确错误，而不是静默错树。
    /// </summary>
    public void EndScope()
    {
        if (_scopeStack.Count <= 1)
        {
            throw new InvalidOperationException("There is no open container scope to dispose. Check for a duplicated using scope.");
        }

        var current = _scopeStack[^1];
        if (_ids.Current != current.Id)
        {
            throw new InvalidOperationException(
                $"Cannot close scope '{current.DebugName}': an ID scope opened inside it was not popped. " +
                "Use 'using' for containers and call PopId for every PushId.");
        }

        _ids.Pop();
        _scopeStack.RemoveAt(_scopeStack.Count - 1);
    }

    public void PushId(string key) => _ids.Push(key);

    public void PushId(int index) => _ids.Push(index);

    public void PopId()
    {
        if (_ids.Depth <= _scopeStack.Count)
        {
            throw new InvalidOperationException("PopId without a matching application PushId.");
        }

        _ids.Pop();
    }

    /// <summary>
    /// 创建浮层根节点。浮层不参与普通父级布局，而是在 PerformLayout 中按
    /// <see cref="OverlayPlacement"/> 单独测量/定位；命中测试会按 layer 从高到低处理。
    ///
    /// passThrough=true 的浮层（Toast/Tooltip）不阻止下层交互；
    /// trapFocus=true 的浮层（Popup/Modal）把键盘焦点限制在组内。
    /// </summary>
    public Element AddOverlay(
        string? key,
        Style? style,
        string debugName,
        OverlayPlacement placement,
        bool passThrough,
        bool trapFocus,
        int layer,
        bool addToCurrent = false)
    {
        var parent = addToCurrent ? Current : null;
        var keyForId = key ?? NextAutoKey(debugName);
        var baseId = addToCurrent ? parent!.Id : Root.Id;
        var element = new Element
        {
            Widget = FlexWidget.Instance,
            Id = Id.Combine(baseId, "__overlay__" + keyForId),
            Style = style ?? EmptyStyle,
            Parent = parent,
            DebugName = debugName,
            Foreground = style?.Foreground ?? _theme.Text,
            Font = style?.Font ?? _theme.Body,
            Cursor = CursorKind.Default,
            IsOverlayRoot = true,
            OverlayPlacement = placement,
            OverlayPassThrough = passThrough,
            OverlayTrapFocus = trapFocus,
            OverlayLayer = layer,
        };
        element.TextFormat = CreateTextFormat(element.Style, element.Font);
        element.Response = Interaction.Get(element.Id);
        _overlays.Add(element);
        _allElements.Add(element);
        Claim(element.Id, debugName);
        return element;
    }

    /// <summary>
    /// 声明一个“有状态 ID”。重复时立即报错，并指出已占用的控件名。
    /// 纯展示元素可 claimId=false 跳过此检查。
    /// </summary>
    public void Claim(Id id, string debugName)
    {
        if (_claimedIds.TryGetValue(id.Value, out var existing))
        {
            throw new InvalidOperationException(
                $"Duplicate stateful ID {id} for '{debugName}' (already used by '{existing}'). " +
                "Give repeated widgets or containers a unique id, or wrap repeated items in ui.PushId(item.Id).");
        }

        _claimedIds[id.Value] = debugName;
    }

    /// <summary>
    /// 用 InteractionManager 的结果刷新元素 Response。
    /// 必须在元素创建之后、应用读取 Response 之前调用；disabled 会清空交互状态。
    /// </summary>
    public void UpdateResponse(Element element, bool disabled = false)
    {
        element.Disabled = disabled;
        element.Response = Interaction.Get(element.Id, disabled);
    }

    public static TextFormat CreateTextFormat(Style style, FontSpec font) => new(
        font,
        style.TextAlignment,
        style.VerticalTextAlignment,
        style.TextWrap,
        style.TextOverflow,
        style.MaxLines);

    /// <summary>
    /// 构建收尾检查：容器作用域和 PushId 必须完全平衡。
    /// 这是把“忘记 using”从视觉错误变成可诊断异常的最后一道防线。
    /// </summary>
    public void CompleteBuild()
    {
        if (_scopeStack.Count != 1)
        {
            var names = string.Join(", ", _scopeStack.Skip(1).Select(s => s.DebugName));
            throw new InvalidOperationException(
                $"The frame ended with {_scopeStack.Count - 1} open container scope(s): {names}. Did you forget a using?");
        }

        if (_ids.Depth != 1)
        {
            throw new InvalidOperationException("The frame ended with unbalanced PushId calls.");
        }
    }

    private string NextAutoKey(string debugName)
    {
        var key = (_ids.Current.Value, debugName);
        _autoKeys.TryGetValue(key, out var count);
        _autoKeys[key] = count + 1;
        return $"##{debugName}:{count}";
    }

    /// <summary>
    /// 一帧的总入口，执行顺序不可交换：
    /// CompleteBuild（校验树）→ Layout（约束）→ Snapshot（下一帧交互数据）
    /// → Paint（DrawList）→ Accessibility（语义树）→ UiFrameResult。
    ///
    /// Snapshot 必须在 Paint 之前，因为 Paint 不应该改变几何；
    /// Accessibility 在绘制之后生成，但只读取元素数据。
    /// </summary>
    internal UiFrameResult Complete(long buildStarted, bool captureDiagnostics)
    {
        CompleteBuild();
        Layout = new LayoutContext
        {
            Ui = _ui,
            Theme = _theme,
            Text = _ui.TextEngine,
            DeltaTime = _input.DeltaTime,
        };

        var layoutStart = Stopwatch.GetTimestamp();
        PerformLayout();
        var layoutEnd = Stopwatch.GetTimestamp();

        Snapshot = BuildSnapshot();
        var snapshotEnd = Stopwatch.GetTimestamp();

        _ui.DrawList.Clear();
        PaintFrame();
        var paintEnd = Stopwatch.GetTimestamp();

        var cursor = ResolveCursor();
        var diagnostics = captureDiagnostics
            ? BuildDiagnostics(buildStarted, layoutStart, layoutEnd, snapshotEnd, paintEnd)
            : null;

        return new UiFrameResult
        {
            DrawList = _ui.DrawList,
            Cursor = cursor,
            Diagnostics = diagnostics,
            Accessibility = BuildAccessibility(),
            FrameNumber = _input.FrameNumber,
            Time = _input.Time,
        };
    }

    private CursorKind ResolveCursor()
    {
        if (!_input.PointerInside || Snapshot is null)
        {
            return CursorKind.Default;
        }

        if (Interaction.ActiveId != 0)
        {
            var active = Snapshot.FindNode(Interaction.ActiveId);
            if (active >= 0)
            {
                return Snapshot.Nodes[active].Cursor;
            }
        }

        var point = _input.PointerPosition;
        var groups = Snapshot.OverlayGroups;
        for (var g = groups.Length - 1; g >= 0; g--)
        {
            var group = groups[g];
            if (group.PassThrough || !group.Bounds.Contains(point))
            {
                continue;
            }

            for (var i = group.FirstNode + group.NodeCount - 1; i >= group.FirstNode; i--)
            {
                if (Snapshot.Nodes[i].Contains(point))
                {
                    return Snapshot.Nodes[i].Cursor;
                }
            }

            return CursorKind.Default;
        }

        for (var i = Snapshot.Nodes.Length - 1; i >= 0; i--)
        {
            var node = Snapshot.Nodes[i];
            if (node.OverlayGroup < 0 && node.Contains(point))
            {
                return node.Cursor;
            }
        }

        return CursorKind.Default;
    }

    /// <summary>
    /// 把当前帧的几何/标记转换为一组扁平节点。
    /// 普通树和每个浮层分别写入节点数组，浮层记录 FirstNode/NodeCount，
    /// 供下一帧的 HitTest 按层从高到低查找。
    /// </summary>
    private FrameSnapshot BuildSnapshot()
    {
        var nodes = new List<NodeSnapshot>(_allElements.Count);
        var groups = new List<OverlayGroupSnapshot>();
        var focusables = new List<int>();
        var viewport = new Rect(0, 0, _input.ViewportSize.Width, _input.ViewportSize.Height);

        AddNodeTree(Root, viewport, -1, -1, false, nodes, focusables);

        foreach (var overlay in SortedOverlays)
        {
            var first = nodes.Count;
            AddNodeTree(overlay, viewport, -1, groups.Count, true, nodes, focusables);
            var count = nodes.Count - first;
            groups.Add(new OverlayGroupSnapshot
            {
                Bounds = overlay.Bounds,
                PassThrough = overlay.OverlayPassThrough,
                TrapFocus = overlay.OverlayTrapFocus,
                FirstNode = first,
                NodeCount = count,
            });
        }

        return new FrameSnapshot
        {
            Nodes = nodes.ToArray(),
            OverlayGroups = groups.ToArray(),
            Focusables = focusables.ToArray(),
            Width = _input.ViewportSize.Width,
            Height = _input.ViewportSize.Height,
        };
    }

    /// <summary>
    /// 递归生成快照节点。Clip 会随 ClipContent 向下收缩；
    /// Flags 是下一帧交互的快速判定依据（Interactive/Focusable/Scrollable/...）。
    /// </summary>
    private static void AddNodeTree(
        Element element,
        Rect clip,
        int parentIndex,
        int overlayGroup,
        bool overlay,
        List<NodeSnapshot> nodes,
        List<int> focusables)
    {
        if (!element.IsVisible || element.Style.Opacity <= 0.001f)
        {
            return;
        }

        var flags = NodeFlags.None;
        if (element.Interactive)
        {
            flags |= NodeFlags.Interactive;
        }

        if (element.Focusable && !element.Disabled)
        {
            flags |= NodeFlags.Focusable;
        }

        if (element.Activatable)
        {
            flags |= NodeFlags.Activatable;
        }

        if (element.Disabled)
        {
            flags |= NodeFlags.Disabled;
        }

        if (overlay)
        {
            flags |= NodeFlags.Overlay;
        }

        if (element.IsScrollable)
        {
            flags |= NodeFlags.Scrollable;
        }

        if (element.TextInput)
        {
            flags |= NodeFlags.TextInput;
        }

        if (element.HandlesArrowKeys)
        {
            flags |= NodeFlags.ArrowKeys;
        }

        var index = nodes.Count;
        nodes.Add(new NodeSnapshot(
            element.Id.Value,
            element.Bounds,
            clip,
            parentIndex,
            overlayGroup,
            flags,
            element.Cursor,
            element.ScrollOffset,
            element.ScrollContentHeight,
            element.ScrollViewportHeight,
            element.ScrollTrack,
            element.ScrollThumb));

        if (element.Focusable && !element.Disabled && element.Bounds.Width > 0 && element.Bounds.Height > 0)
        {
            focusables.Add(index);
        }

        var childClip = element.Style.ClipContent ? clip.Intersect(element.ContentBounds) : clip;
        foreach (var child in element.Children)
        {
            AddNodeTree(child, childClip, index, overlayGroup, overlay, nodes, focusables);
        }
    }

    /// <summary>
    /// 从元素树抽取语义节点。只收集 AccessibilityRole != None 的元素；
    /// ParentIndex 指向最近的语义祖先，Children 用下标建立关系。
    ///
    /// 生成后是不可变快照，Win32 UIA provider 可在其他线程安全读取。
    /// </summary>
    private AccessibilitySnapshot BuildAccessibility()
    {
        var builders = new List<AccessibilityNodeBuilder>();
        CollectAccessibility(Root, -1, builders);
        if (builders.Count == 0)
        {
            return AccessibilitySnapshot.Empty;
        }

        var nodes = new List<AccessibilityNode>(builders.Count);
        for (var i = 0; i < builders.Count; i++)
        {
            var builder = builders[i];
            nodes.Add(builder.ToNode(builder.Children));
        }

        return new AccessibilitySnapshot { Nodes = nodes };
    }

    /// <summary>
    /// 深度优先收集语义节点。没有语义角色的容器不会打断父子关系；
    /// 子节点会挂到最近的语义祖先下，保证 UIA 树仍然连贯。
    /// </summary>
    private static void CollectAccessibility(Element element, int parentIndex, List<AccessibilityNodeBuilder> output)
    {
        if (!element.IsVisible)
        {
            return;
        }

        var nextParent = parentIndex;
        if (element.AccessibilityRole != AccessibilityRole.None)
        {
            var index = output.Count;
            var builder = new AccessibilityNodeBuilder
            {
                Id = element.Id,
                Role = element.AccessibilityRole,
                Name = element.AccessibilityName ?? element.Text ?? string.Empty,
                Value = element.AccessibilityValue ?? (element.AccessibilityRole is AccessibilityRole.TextField or AccessibilityRole.TextArea ? element.Text : null),
                HelpText = element.AccessibilityHelp,
                Bounds = element.Bounds,
                IsEnabled = !element.Disabled,
                IsFocused = element.Response.Focused,
                IsFocusable = element.Focusable,
                ParentIndex = parentIndex,
            };
            output.Add(builder);
            if (parentIndex >= 0)
            {
                output[parentIndex].Children.Add(index);
            }

            nextParent = index;
        }

        foreach (var child in element.Children)
        {
            CollectAccessibility(child, nextParent, output);
        }
    }

    private sealed class AccessibilityNodeBuilder
    {
        public Id Id { get; init; }
        public AccessibilityRole Role { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string? HelpText { get; init; }
        public Rect Bounds { get; init; }
        public bool IsEnabled { get; init; }
        public bool IsFocused { get; init; }
        public bool IsFocusable { get; init; }
        public int ParentIndex { get; init; }
        public List<int> Children { get; } = new();

        public AccessibilityNode ToNode(IReadOnlyList<int> children) => new()
        {
            Id = Id,
            Role = Role,
            Name = Name,
            Value = Value,
            HelpText = HelpText,
            Bounds = Bounds,
            IsEnabled = IsEnabled,
            IsFocused = IsFocused,
            IsFocusable = IsFocusable,
            ParentIndex = ParentIndex,
            Children = children.ToArray(),
        };
    }

    private FrameDiagnostics BuildDiagnostics(long buildStarted, long layoutStart, long layoutEnd, long snapshotEnd, long paintEnd)
    {
        var infos = new List<ElementInfo>(_allElements.Count);
        foreach (var element in _allElements)
        {
            if (!element.IsVisible)
            {
                continue;
            }

            var depth = 0;
            for (var p = element.Parent; p is not null; p = p.Parent)
            {
                depth++;
            }

            infos.Add(new ElementInfo(
                element.Id,
                element.DebugName,
                element.Bounds,
                element.ContentBounds,
                depth,
                element.Interactive,
                Interaction.FocusedId == element.Id.Value,
                true,
                element.Text));
        }

        return new FrameDiagnostics
        {
            FrameNumber = _input.FrameNumber,
            BuildMilliseconds = ToMilliseconds(buildStarted, layoutStart),
            LayoutMilliseconds = ToMilliseconds(layoutStart, layoutEnd),
            SnapshotMilliseconds = ToMilliseconds(layoutEnd, snapshotEnd),
            PaintMilliseconds = ToMilliseconds(snapshotEnd, paintEnd),
            ElementCount = infos.Count,
            DrawCommandCount = _ui.DrawList.Count,
            StateCount = _ui.State.Count,
            OverlayCount = _overlays.Count,
            Elements = infos,
            HoveredId = new Id(Interaction.HoveredId),
            ActiveId = new Id(Interaction.ActiveId),
            FocusedId = new Id(Interaction.FocusedId),
        };
    }

    private static double ToMilliseconds(long start, long end) =>
        (end - start) * 1000.0 / Stopwatch.Frequency;
}

/// <summary>Closes an open container or overlay scope when disposed.</summary>
public readonly struct UiScope : IDisposable
{
    private readonly Ui _ui;

    internal UiScope(Ui ui)
    {
        _ui = ui;
    }

    public void Dispose() => _ui.EndScope();
}



