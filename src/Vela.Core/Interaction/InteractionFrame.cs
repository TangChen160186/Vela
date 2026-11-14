namespace Vela;

internal sealed class InteractionFrame
{
    public ulong HoveredId { get; init; }
    public ulong ActiveId { get; init; }
    public ulong PressedId { get; init; }
    public ulong ReleasedId { get; init; }
    public ulong ClickedId { get; init; }
    public ulong DoubleClickedId { get; init; }
    public ulong RightClickedId { get; init; }
    public ulong FocusedId { get; internal set; }
    public ulong KeyboardPressedId { get; init; }
    public ulong KeyboardClickedId { get; init; }
    public bool FocusVisible { get; internal set; }
    public CursorKind Cursor { get; init; }

    public void ApplyFocus(ulong id, bool focusVisible)
    {
        FocusedId = id;
        FocusVisible = focusVisible;
    }

    public Response Get(Id id, bool disabled = false)
    {
        var value = id.Value;
        var response = new Response(
            id,
            HoveredId == value,
            PressedId == value || KeyboardPressedId == value,
            ActiveId == value,
            ReleasedId == value,
            ClickedId == value || KeyboardClickedId == value,
            DoubleClickedId == value,
            RightClickedId == value,
            ActiveId == value,
            FocusedId == value,
            FocusedId == value && FocusVisible,
            false);
        return disabled ? response.WithDisabled(true) : response;
    }
}

/// <summary>
/// 把“上一帧快照 + 本帧输入”解析成每个元素可读取的 Response，并维护
/// active/focus/double-click/scroll 等跨帧交互状态。
///
/// 为什么在 Build 之前运行：控件在本帧创建时就要知道自己是否 Hovered/Clicked，
/// 而本帧布局尚未完成。因此命中一定是对上一帧已解析几何做的。
/// </summary>
internal sealed class InteractionManager
{
    private ulong _activeId;
    private ulong _focusedId;
    private bool _focusVisible;
    private ulong _lastClickId;
    private double _lastClickTime;
    private Vec2 _lastClickPosition;
    private ulong _rightActiveId;
    private ulong _pendingClickId;
    private CursorKind _cursor = CursorKind.Default;

    public ulong FocusedId => _focusedId;

    /// <summary>
    /// 解析一帧交互，顺序：
    /// 1) 命中测试；2) 处理 active 丢失/鼠标释放；3) press 设置 active + focus；
    /// 4) release 产生 click/double-click；5) Tab/方向键导航；
    /// 6) 焦点陷阱校验；7) 滚轮；8) 程序化 Invoke；9) 计算光标。
    /// </summary>
    public InteractionFrame BeginFrame(FrameInput input, FrameSnapshot previous, StateStore state, Theme theme)
    {
        var hoveredId = 0UL;
        var pressedId = 0UL;
        var releasedId = 0UL;
        var clickedId = 0UL;
        var doubleClickedId = 0UL;
        var rightClickedId = 0UL;
        var keyboardPressedId = 0UL;
        var keyboardClickedId = 0UL;

        var hit = HitTest(previous, input);
        if (input.PointerInside)
        {
            hoveredId = hit.TargetId;
        }

        // active 元素可能因为重新布局而消失，或平台丢失鼠标捕获却没发 release。
        // 这里主动清理，避免控件永远停在 Held。
        if (_activeId != 0 && previous.FindNode(_activeId) < 0)
        {
            _activeId = 0;
        }

        if (_activeId != 0 && !input.IsButtonDown(PointerButton.Left) && !input.WasButtonReleased(PointerButton.Left))
        {
            _activeId = 0;
        }

        if (_rightActiveId != 0 && !input.IsButtonDown(PointerButton.Right) && !input.WasButtonReleased(PointerButton.Right))
        {
            _rightActiveId = 0;
        }

        // 左键按下：capture 目标；如果目标可聚焦，同时移动焦点。
        // 点击空白区域会清除焦点。
        if (input.WasButtonPressed(PointerButton.Left))
        {
            _activeId = 0;
            _rightActiveId = 0;

            if (hoveredId != 0)
            {
                var nodeIndex = previous.FindNode(hoveredId);
                if (nodeIndex >= 0 && (previous.Nodes[nodeIndex].Flags & NodeFlags.Disabled) == 0)
                {
                    _activeId = hoveredId;
                    pressedId = hoveredId;

                    if ((previous.Nodes[nodeIndex].Flags & NodeFlags.Focusable) != 0)
                    {
                        _focusedId = hoveredId;
                        _focusVisible = false;
                    }
                    else
                    {
                        _focusedId = 0;
                        _focusVisible = false;
                    }
                }
                else
                {
                    _focusedId = 0;
                    _focusVisible = false;
                }
            }
            else
            {
                _focusedId = 0;
                _focusVisible = false;
            }
        }

        if (input.WasButtonPressed(PointerButton.Right))
        {
            _rightActiveId = hoveredId;
        }

        if (input.WasButtonReleased(PointerButton.Right))
        {
            if (_rightActiveId != 0 && _rightActiveId == hoveredId)
            {
                rightClickedId = _rightActiveId;
            }

            _rightActiveId = 0;
        }

        // 释放：只有仍在 capture 目标上才算 click；时间和距离共同判定双击。
        if (input.WasButtonReleased(PointerButton.Left) && _activeId != 0)
        {
            releasedId = _activeId;
            if (hoveredId == _activeId)
            {
                clickedId = _activeId;
                if (_lastClickId == _activeId &&
                    input.Time - _lastClickTime <= 0.45 &&
                    (_lastClickPosition - input.PointerPosition).Length <= 8)
                {
                    doubleClickedId = _activeId;
                    _lastClickId = 0;
                }
                else
                {
                    _lastClickId = _activeId;
                    _lastClickTime = input.Time;
                    _lastClickPosition = input.PointerPosition;
                }
            }

            _activeId = 0;
        }

        // Tab moves focus through the focusable nodes of the previous frame. Modals and
        // popups that declare TrapFocus exclude the application underneath.
        if (input.WasKeyPressed(Key.Tab))
        {
            MoveFocus(previous, input.Shift);
        }

        if (_focusedId != 0 && previous.FindNode(_focusedId) < 0)
        {
            _focusedId = 0;
            _focusVisible = false;
        }

        // A modal or popup that traps focus must not leave keyboard focus on the
        // application underneath. If focus is outside the trap, move it to the
        // first focusable item inside; this also gives dialogs an initial focus.
        if (HasFocusTrap(previous))
        {
            var focusedNode = _focusedId == 0 ? -1 : previous.FindNode(_focusedId);
            var focusInsideTrap = false;
            if (focusedNode >= 0)
            {
                var group = previous.Nodes[focusedNode].OverlayGroup;
                focusInsideTrap = group >= 0 && previous.OverlayGroups[group].TrapFocus;
            }

            if (!focusInsideTrap)
            {
                var first = FindFirstFocusableInTrap(previous);
                _focusedId = first;
                _focusVisible = first != 0;
            }
        }

        // Spatial arrow navigation. Text editors, sliders, splitters and virtual
        // rows consume arrows themselves and opt out through NodeFlags.ArrowKeys.
        if (_focusedId != 0 && !input.Ctrl && !input.Alt && !input.Super)
        {
            var focusedNode = previous.FindNode(_focusedId);
            if (focusedNode >= 0)
            {
                var flags = previous.Nodes[focusedNode].Flags;
                if ((flags & (NodeFlags.TextInput | NodeFlags.ArrowKeys)) == 0)
                {
                    if (input.WasKeyPressedOrRepeated(Key.Left))
                    {
                        MoveFocusDirection(previous, Key.Left);
                    }
                    else if (input.WasKeyPressedOrRepeated(Key.Right))
                    {
                        MoveFocusDirection(previous, Key.Right);
                    }
                    else if (input.WasKeyPressedOrRepeated(Key.Up))
                    {
                        MoveFocusDirection(previous, Key.Up);
                    }
                    else if (input.WasKeyPressedOrRepeated(Key.Down))
                    {
                        MoveFocusDirection(previous, Key.Down);
                    }
                }
            }
        }

        // Home/End jump to the edge of a trapped overlay (menus, dialogs, grids).
        if (_focusedId != 0 && (input.WasKeyPressed(Key.Home) || input.WasKeyPressed(Key.End)))
        {
            if (previous.FindNode(_focusedId) is var index && index >= 0)
            {
                var group = previous.Nodes[index].OverlayGroup;
                if (group >= 0 && previous.OverlayGroups[group].TrapFocus)
                {
                    var target = input.WasKeyPressed(Key.Home)
                        ? FindFirstFocusableInTrap(previous)
                        : FindLastFocusableInTrap(previous);
                    if (target != 0)
                    {
                        _focusedId = target;
                        _focusVisible = true;
                    }
                }
            }
        }

        if (_pendingClickId != 0)
        {
            var requested = _pendingClickId;
            _pendingClickId = 0;
            var nodeIndex = previous.FindNode(requested);
            if (nodeIndex >= 0 && (previous.Nodes[nodeIndex].Flags & NodeFlags.Disabled) == 0)
            {
                keyboardPressedId = requested;
                keyboardClickedId = requested;
            }
        }

        if (_focusedId != 0 && (input.WasKeyPressed(Key.Enter) || input.WasKeyPressed(Key.Space)))
        {
            var nodeIndex = previous.FindNode(_focusedId);
            if (nodeIndex >= 0)
            {
                var flags = previous.Nodes[nodeIndex].Flags;
                if ((flags & NodeFlags.Activatable) != 0 && (flags & NodeFlags.Disabled) == 0)
                {
                    keyboardPressedId = _focusedId;
                    keyboardClickedId = _focusedId;
                }
            }
        }

        if (input.WheelY != 0 || input.WheelX != 0)
        {
            ApplyWheel(input, previous, state);
        }

        // Cursor should reflect the captured element while dragging; otherwise the
        // topmost element under the pointer.
        var cursorNode = _activeId != 0 ? previous.FindNode(_activeId) : hit.TopNodeIndex;
        if (cursorNode >= 0)
        {
            _cursor = previous.Nodes[cursorNode].Cursor;
        }
        else
        {
            _cursor = CursorKind.Default;
        }

        return new InteractionFrame
        {
            HoveredId = hoveredId,
            ActiveId = _activeId,
            PressedId = pressedId,
            ReleasedId = releasedId,
            ClickedId = clickedId,
            DoubleClickedId = doubleClickedId,
            RightClickedId = rightClickedId,
            FocusedId = _focusedId,
            KeyboardPressedId = keyboardPressedId,
            KeyboardClickedId = keyboardClickedId,
            FocusVisible = _focusVisible,
            Cursor = _cursor,
        };
    }

    public void SetFocus(ulong id, bool focusVisible = false)
    {
        _focusedId = id;
        _focusVisible = focusVisible;
    }

    public void ClearFocus()
    {
        _focusedId = 0;
        _focusVisible = false;
    }

    public void RequestClick(ulong id) => _pendingClickId = id;

    private static ulong FindLastFocusableInTrap(FrameSnapshot snapshot)
    {
        for (var i = snapshot.Nodes.Length - 1; i >= 0; i--)
        {
            var node = snapshot.Nodes[i];
            var group = node.OverlayGroup;
            if (group < 0 || !snapshot.OverlayGroups[group].TrapFocus)
            {
                continue;
            }

            if ((node.Flags & NodeFlags.Focusable) != 0 && (node.Flags & NodeFlags.Disabled) == 0)
            {
                return node.Id;
            }
        }

        return 0;
    }

    private static ulong FindFirstFocusableInTrap(FrameSnapshot snapshot)
    {
        foreach (var node in snapshot.Nodes)
        {
            var group = node.OverlayGroup;
            if (group < 0 || !snapshot.OverlayGroups[group].TrapFocus)
            {
                continue;
            }

            if ((node.Flags & NodeFlags.Focusable) != 0 && (node.Flags & NodeFlags.Disabled) == 0)
            {
                return node.Id;
            }
        }

        return 0;
    }

    /// <summary>
    /// 方向键空间导航：在可聚焦节点中找“主方向最近、交叉方向偏差最小”的候选。
    /// 文本输入、滑块、分割条、虚拟列表会标记 ArrowKeys，不进入这里。
    /// </summary>
    private void MoveFocusDirection(FrameSnapshot snapshot, Key key)
    {
        if (snapshot.FindNode(_focusedId) is var currentIndex && currentIndex < 0)
        {
            return;
        }

        var current = snapshot.Nodes[currentIndex];
        var currentCenter = current.Bounds.Center;
        var trapped = false;
        foreach (var group in snapshot.OverlayGroups)
        {
            if (group.TrapFocus)
            {
                trapped = true;
                break;
            }
        }

        var bestId = 0UL;
        var bestScore = float.PositiveInfinity;
        foreach (var index in snapshot.Focusables)
        {
            var node = snapshot.Nodes[index];
            if (node.Id == _focusedId || (node.Flags & NodeFlags.Disabled) != 0)
            {
                continue;
            }

            if (trapped && (node.Flags & NodeFlags.Overlay) == 0)
            {
                continue;
            }

            var center = node.Bounds.Center;
            var dx = center.X - currentCenter.X;
            var dy = center.Y - currentCenter.Y;
            var primary = key switch
            {
                Key.Left => -dx,
                Key.Right => dx,
                Key.Up => -dy,
                _ => dy,
            };
            if (primary <= 1)
            {
                continue;
            }

            var cross = key is Key.Left or Key.Right ? MathF.Abs(dy) : MathF.Abs(dx);
            var score = primary + cross * 0.6f;
            if (score < bestScore)
            {
                bestScore = score;
                bestId = node.Id;
            }
        }

        if (bestId != 0)
        {
            _focusedId = bestId;
            _focusVisible = true;
        }
    }

    private static bool HasFocusTrap(FrameSnapshot snapshot)
    {
        foreach (var group in snapshot.OverlayGroups)
        {
            if (group.TrapFocus)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tab/Shift+Tab：按快照 Focusables 顺序循环，跳过 disabled 和陷阱外节点。</summary>
    private void MoveFocus(FrameSnapshot snapshot, bool backwards)
    {
        var candidates = snapshot.Focusables;
        if (candidates.Length == 0)
        {
            _focusedId = 0;
            _focusVisible = false;
            return;
        }

        var trapped = false;
        for (var i = 0; i < snapshot.OverlayGroups.Length; i++)
        {
            if (snapshot.OverlayGroups[i].TrapFocus)
            {
                trapped = true;
                break;
            }
        }

        var all = new List<int>(candidates.Length);
        foreach (var index in candidates)
        {
            var node = snapshot.Nodes[index];
            if ((node.Flags & NodeFlags.Disabled) != 0)
            {
                continue;
            }

            if (trapped && (node.Flags & NodeFlags.Overlay) == 0)
            {
                continue;
            }

            all.Add(index);
        }

        if (all.Count == 0)
        {
            return;
        }

        var current = all.FindIndex(i => snapshot.Nodes[i].Id == _focusedId);
        var next = current < 0
            ? (backwards ? all.Count - 1 : 0)
            : (backwards ? (current - 1 + all.Count) % all.Count : (current + 1) % all.Count);

        _focusedId = snapshot.Nodes[all[next]].Id;
        _focusVisible = true;
    }

    /// <summary>
    /// 滚轮从命中节点向上找最近的 Scrollable 祖先；找到后滚动其 ScrollState，
    /// 内层滚不动时不会自动冒泡到外层（当前行为）。
    /// </summary>
    private static void ApplyWheel(FrameInput input, FrameSnapshot previous, StateStore state)
    {
        var hit = HitTest(previous, input);
        var index = hit.TopNodeIndex;
        if (index < 0)
        {
            return;
        }

        var delta = input.Shift && input.WheelY != 0 ? input.WheelY : -input.WheelY;
        if (MathF.Abs(delta) < 0.001f)
        {
            return;
        }

        while (index >= 0)
        {
            var node = previous.Nodes[index];
            if ((node.Flags & NodeFlags.Scrollable) != 0)
            {
                var scroll = state.Get<ScrollState>(new Id(node.Id));
                if (scroll is not null)
                {
                    scroll.ScrollBy(delta * scroll.WheelStep);
                }

                return;
            }

            index = node.ParentIndex;
        }
    }

    private readonly record struct HitResult(ulong TargetId, int TopNodeIndex, bool Blocked);

    /// <summary>
    /// 命中测试：浮层从高层到低层；组内从后往前找 Interactive 且未 disabled 的节点；
    /// 没有 interactive 目标时仍返回 top node，供滚轮和光标反馈使用。
    /// </summary>
    private static HitResult HitTest(FrameSnapshot snapshot, FrameInput input)
    {
        if (!input.PointerInside || snapshot.Nodes.Length == 0)
        {
            return new HitResult(0, -1, false);
        }

        var point = input.PointerPosition;
        var groups = snapshot.OverlayGroups;
        for (var g = groups.Length - 1; g >= 0; g--)
        {
            var group = groups[g];
            if (group.PassThrough || !group.Bounds.Contains(point))
            {
                continue;
            }

            var target = 0UL;
            var topNode = -1;
            for (var i = group.FirstNode + group.NodeCount - 1; i >= group.FirstNode; i--)
            {
                if (!snapshot.Nodes[i].Contains(point))
                {
                    continue;
                }

                if (topNode < 0)
                {
                    topNode = i;
                }

                var flags = snapshot.Nodes[i].Flags;
                if ((flags & NodeFlags.Interactive) != 0 && (flags & NodeFlags.Disabled) == 0)
                {
                    target = snapshot.Nodes[i].Id;
                    break;
                }
            }

            return new HitResult(target, topNode, true);
        }

        for (var i = snapshot.Nodes.Length - 1; i >= 0; i--)
        {
            var node = snapshot.Nodes[i];
            if (node.OverlayGroup >= 0 || !node.Contains(point))
            {
                continue;
            }

            if ((node.Flags & NodeFlags.Interactive) != 0 && (node.Flags & NodeFlags.Disabled) == 0)
            {
                return new HitResult(node.Id, i, false);
            }
        }

        // No interactive target: still find the topmost node for wheel scrolling and
        // cursor feedback.
        for (var i = snapshot.Nodes.Length - 1; i >= 0; i--)
        {
            var node = snapshot.Nodes[i];
            if (node.OverlayGroup < 0 && node.Contains(point))
            {
                return new HitResult(0, i, false);
            }
        }

        return new HitResult(0, -1, false);
    }
}



