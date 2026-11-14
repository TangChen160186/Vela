namespace Vela;

/// <summary>
/// 语义控件角色。内置控件由 Ui 方法自动设置；自定义控件通过 Ui.Semantic 设置。
/// 这是 Core 与平台无障碍桥（Win32 UIA）之间的稳定协议，不依赖 UIA 类型。
/// </summary>
public enum AccessibilityRole
{
    None,
    Button,
    CheckBox,
    RadioButton,
    Switch,
    Slider,
    TextField,
    TextArea,
    ComboBox,
    List,
    ListItem,
    Table,
    TableRow,
    Tree,
    TreeItem,
    Menu,
    MenuItem,
    Tab,
    Dialog,
    ProgressBar,
    StaticText,
    Group,
    Window,
}

/// <summary>
/// 无障碍树节点。Bounds 是客户区逻辑像素（DIP），平台桥负责转成屏幕物理像素。
/// ParentIndex/Children 使用同一快照 Nodes 数组的下标，便于 O(1) 导航。
/// </summary>
public sealed class AccessibilityNode
{
    public required Id Id { get; init; }
    public required AccessibilityRole Role { get; init; }
    public required string Name { get; init; }
    public string? Value { get; init; }
    public string? HelpText { get; init; }
    public required Rect Bounds { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool IsFocused { get; init; }
    public required bool IsFocusable { get; init; }
    public int ParentIndex { get; init; } = -1;
    public IReadOnlyList<int> Children { get; init; } = Array.Empty<int>();
}

/// <summary>
/// 一帧的不可变无障碍快照。Frame.Complete 生成，Win32 provider 在 UIA 线程读取；
/// 不可变性保证跨线程读取无需锁。平台桥不应持有过时节点做长期缓存。
/// </summary>
public sealed class AccessibilitySnapshot
{
    public static AccessibilitySnapshot Empty { get; } = new() { Nodes = Array.Empty<AccessibilityNode>() };

    public required IReadOnlyList<AccessibilityNode> Nodes { get; init; }

    public int IndexOf(Id id)
    {
        for (var i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public AccessibilityNode? Find(Id id)
    {
        var index = IndexOf(id);
        return index < 0 ? null : Nodes[index];
    }

    public AccessibilityNode? Focused
    {
        get
        {
            foreach (var node in Nodes)
            {
                if (node.IsFocused)
                {
                    return node;
                }
            }

            return null;
        }
    }
}
