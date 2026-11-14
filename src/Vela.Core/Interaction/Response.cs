namespace Vela;

/// <summary>
/// 一个元素在本帧的交互结果，是值类型快照。
///
/// 语义：
/// <list type="bullet">
/// <item>Pressed：本帧刚按下；Held：按下后持续；</item>
/// <item>Clicked：在目标上按下并释放（上一帧几何命中）；</item>
/// <item>DoubleClicked：与上一次 click 的时间/距离满足阈值；</item>
/// <item>Active：当前被 capture（拖拽/按住）；</item>
/// <item>Focused/FocusVisible：键盘焦点及是否显示焦点环；</item>
/// <item>Disabled：清空所有交互状态（WithDisabled）。</item>
/// </list>
/// 新元素从下一帧开始可交互，因为命中依赖上一帧 FrameSnapshot。
/// </summary>
public readonly record struct Response(
    Id Id,
    bool Hovered,
    bool Pressed,
    bool Held,
    bool Released,
    bool Clicked,
    bool DoubleClicked,
    bool RightClicked,
    bool Active,
    bool Focused,
    bool FocusVisible,
    bool Disabled)
{
    public static Response Empty(Id id) => new(id, false, false, false, false, false, false, false, false, false, false, false);

    /// <summary>True when the pointer is pressing or holding this element.</summary>
    public bool IsPressed => Pressed || Held;

    public Response WithDisabled(bool disabled) => disabled ? this with { Disabled = true, Hovered = false, Pressed = false, Held = false, Released = false, Clicked = false, DoubleClicked = false, RightClicked = false, Active = false, Focused = false, FocusVisible = false } : this;

    public Response AsClicked() => this with { Clicked = true, Pressed = true };
}

[Flags]
internal enum NodeFlags
{
    None = 0,
    Interactive = 1 << 0,
    Focusable = 1 << 1,
    Activatable = 1 << 2,
    Scrollable = 1 << 3,
    Disabled = 1 << 4,
    Overlay = 1 << 5,
    PassThrough = 1 << 6,
    TextInput = 1 << 7,
    ArrowKeys = 1 << 8,
}

internal readonly record struct NodeSnapshot(
    ulong Id,
    Rect Bounds,
    Rect Clip,
    int ParentIndex,
    int OverlayGroup,
    NodeFlags Flags,
    CursorKind Cursor,
    float ScrollOffset,
    float ContentHeight,
    float ViewportHeight,
    Rect ScrollTrack,
    Rect ScrollThumb)
{
    public bool Contains(Vec2 point) => Bounds.Contains(point) && Clip.Contains(point);
}

internal sealed class OverlayGroupSnapshot
{
    public required Rect Bounds { get; init; }
    public required bool PassThrough { get; init; }
    public required bool TrapFocus { get; init; }
    public required int FirstNode { get; init; }
    public required int NodeCount { get; init; }
}

/// <summary>The resolved geometry and interaction metadata kept for the next frame.</summary>
internal sealed class FrameSnapshot
{
    public static FrameSnapshot Empty { get; } = new()
    {
        Nodes = Array.Empty<NodeSnapshot>(),
        OverlayGroups = Array.Empty<OverlayGroupSnapshot>(),
        Focusables = Array.Empty<int>(),
        Width = 0,
        Height = 0,
    };

    public required NodeSnapshot[] Nodes { get; init; }
    public required OverlayGroupSnapshot[] OverlayGroups { get; init; }
    public required int[] Focusables { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }

    public int FindNode(ulong id)
    {
        for (var i = 0; i < Nodes.Length; i++)
        {
            if (Nodes[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}

