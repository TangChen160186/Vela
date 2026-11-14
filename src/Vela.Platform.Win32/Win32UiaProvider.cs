using System.Runtime.InteropServices;
using Interop.UIAutomationCore;

namespace Vela.Platform.Win32;

/// <summary>
/// Win32 WM_GETOBJECT → UI Automation 的桥。
///
/// 每次 WM_GETOBJECT 把根 provider 的 COM 指针交给 UiaReturnRawElementProvider；
/// 读取的数据来自 Win32Window.LatestAccessibility（每帧更新）。
/// 焦点事件由 DesktopApp 在焦点变化时调用 RaiseFocusChanged 触发。
/// </summary>
internal sealed class Win32UiaBridge
{
    private const int UiaRootObjectId = -25;
    private const int ObjIdClient = -4;
    private readonly Win32Window _window;
    private Win32UiaProvider? _root;

    public Win32UiaBridge(Win32Window window)
    {
        _window = window;
    }

    /// <summary>
    /// 处理 WM_GETOBJECT。只接受 UIA 根对象 (-25) 和 MSAA Client (-4)；
    /// COM 指针在返回后 Release 我们的引用，UIA 端会自行 AddRef。
    /// </summary>
    public IntPtr HandleGetObject(IntPtr wParam, IntPtr lParam)
    {
        var id = lParam.ToInt32();
        if (id != UiaRootObjectId && id != ObjIdClient)
        {
            return IntPtr.Zero;
        }

        _root ??= new Win32UiaProvider(_window, -1, null);
        var pointer = Marshal.GetComInterfaceForObject(_root, typeof(IRawElementProviderSimple));
        try
        {
            return UiaReturnRawElementProvider(_window.Handle, wParam, lParam, pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    public void Disconnect()
    {
        _root = null;
    }

    /// <summary>
    /// 向 UIA 客户端发送焦点变化事件。先检查 UiaClientsAreListening，避免
    /// 无客户端时创建/封送 COM 对象；id 必须存在于最新 AccessibilitySnapshot。
    /// </summary>
    public void RaiseFocusChanged(Id id)
    {
        if (_root is null || id == Id.None)
        {
            return;
        }

        var snapshot = _window.LatestAccessibility;
        if (snapshot is null)
        {
            return;
        }

        var index = snapshot.IndexOf(id);
        if (index < 0)
        {
            return;
        }

        var provider = new Win32UiaProvider(_window, index, _root);
        var pointer = Marshal.GetComInterfaceForObject(provider, typeof(IRawElementProviderSimple));
        try
        {
            if (UiaClientsAreListening())
            {
                _ = UiaRaiseAutomationEvent(pointer, 20005); // UIA_AutomationFocusChangedEventId
            }
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    [DllImport("uiautomationcore.dll")]
    private static extern IntPtr UiaReturnRawElementProvider(IntPtr hwnd, IntPtr wParam, IntPtr lParam, IntPtr provider);

    [DllImport("uiautomationcore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UiaClientsAreListening();

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaRaiseAutomationEvent(IntPtr provider, int eventId);
}

/// <summary>
/// UIA 节点 provider。同一个类用 NodeIndex=-1 表示窗口根，>=0 表示语义节点。
///
/// 节点按需创建（每次导航新建），但都引用同一份不可变 AccessibilitySnapshot；
/// 因此 UIA 线程读取时不需要锁。BoundingRectangle 由客户区坐标 × DPI +
/// 客户区屏幕原点转换成 UIA 需要的屏幕物理像素。
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class Win32UiaProvider : IRawElementProviderSimple, IRawElementProviderFragment, IRawElementProviderFragmentRoot
{
    private const int NamePropertyId = 30005;
    private const int ControlTypePropertyId = 30003;
    private const int BoundingRectanglePropertyId = 30001;
    private const int IsEnabledPropertyId = 30010;
    private const int IsKeyboardFocusablePropertyId = 30009;
    private const int HasKeyboardFocusPropertyId = 30008;
    private const int IsOffscreenPropertyId = 30022;
    private const int AutomationIdPropertyId = 30011;
    private const int HelptextPropertyId = 30013;
    private const int IsControlElementPropertyId = 30016;
    private const int IsContentElementPropertyId = 30017;
    private const int ValueValuePropertyId = 30045;

    private const int ButtonControlType = 50000;
    private const int CheckBoxControlType = 50002;
    private const int ComboBoxControlType = 50003;
    private const int EditControlType = 50004;
    private const int ListControlType = 50008;
    private const int ListItemControlType = 50007;
    private const int MenuControlType = 50009;
    private const int MenuItemControlType = 50011;
    private const int ProgressBarControlType = 50012;
    private const int RadioButtonControlType = 50013;
    private const int SliderControlType = 50015;
    private const int TableControlType = 50010;
    private const int TextControlType = 50020;
    private const int TreeControlType = 50016;
    private const int TreeItemControlType = 50017;
    private const int TabControlType = 50018;
    private const int WindowControlType = 50032;
    private const int PaneControlType = 50033;

    private readonly Win32Window _window;
    private readonly int _nodeIndex;
    private readonly Win32UiaProvider? _root;
    private IRawElementProviderSimple? _hostProvider;

    public Win32UiaProvider(Win32Window window, int nodeIndex, Win32UiaProvider? root)
    {
        _window = window;
        _nodeIndex = nodeIndex;
        _root = root;
    }

    private Win32UiaProvider Root => _root ?? this;
    private bool IsRoot => _nodeIndex < 0;
    private AccessibilitySnapshot Snapshot => _window.LatestAccessibility ?? AccessibilitySnapshot.Empty;
    private AccessibilityNode? Node => IsRoot || _nodeIndex >= Snapshot.Nodes.Count ? null : Snapshot.Nodes[_nodeIndex];

    public ProviderOptions ProviderOptions => ProviderOptions.ProviderOptions_ServerSideProvider | ProviderOptions.ProviderOptions_UseComThreading;

    public IRawElementProviderSimple? HostRawElementProvider
    {
        get
        {
            if (!IsRoot)
            {
                return null;
            }

            if (_hostProvider is not null)
            {
                return _hostProvider;
            }

            if (UiaHostProviderFromHwnd(_window.Handle, out var pointer) >= 0 && pointer != IntPtr.Zero)
            {
                try
                {
                    _hostProvider = (IRawElementProviderSimple)Marshal.GetObjectForIUnknown(pointer);
                }
                finally
                {
                    Marshal.Release(pointer);
                }
            }

            return _hostProvider;
        }
    }

    public object? GetPatternProvider(int patternId)
    {
        if (IsRoot || Node is not { } node)
        {
            return null;
        }

        if (patternId == 10000 && node.Role is AccessibilityRole.Button or AccessibilityRole.MenuItem or AccessibilityRole.TreeItem or AccessibilityRole.Tab)
        {
            return new Win32UiaInvokePattern(_window, node.Id);
        }

        if (patternId == 10015 && node.Role is AccessibilityRole.CheckBox or AccessibilityRole.Switch or AccessibilityRole.RadioButton)
        {
            var on = string.Equals(node.Value, "checked", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(node.Value, "on", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(node.Value, "selected", StringComparison.OrdinalIgnoreCase);
            return new Win32UiaTogglePattern(_window, node.Id, on);
        }

        if (patternId == 10010 && node.Role is AccessibilityRole.Tab or AccessibilityRole.ListItem or AccessibilityRole.TreeItem)
        {
            return new Win32UiaSelectionPattern(_window, node.Id, node.Value == "selected");
        }

        return null;
    }

    public object? GetPropertyValue(int propertyId)
    {
        if (IsRoot)
        {
            return propertyId switch
            {
                NamePropertyId => _window.Title,
                ControlTypePropertyId => WindowControlType,
                BoundingRectanglePropertyId => RootRectArray(),
                IsEnabledPropertyId => true,
                IsKeyboardFocusablePropertyId => false,
                HasKeyboardFocusPropertyId => false,
                IsOffscreenPropertyId => false,
                IsControlElementPropertyId => true,
                IsContentElementPropertyId => true,
                AutomationIdPropertyId => "Vela.Window",
                _ => null,
            };
        }

        if (Node is not { } node)
        {
            return null;
        }

        return propertyId switch
        {
            NamePropertyId => node.Name,
            ControlTypePropertyId => MapRole(node.Role),
            BoundingRectanglePropertyId => RectArray(node.Bounds),
            IsEnabledPropertyId => node.IsEnabled,
            IsKeyboardFocusablePropertyId => node.IsFocusable,
            HasKeyboardFocusPropertyId => node.IsFocused,
            IsOffscreenPropertyId => false,
            AutomationIdPropertyId => node.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            HelptextPropertyId => node.HelpText,
            IsControlElementPropertyId => true,
            IsContentElementPropertyId => true,
            ValueValuePropertyId => node.Value,
            _ => null,
        };
    }

    public IRawElementProviderFragment? Navigate(NavigateDirection direction)
    {
        if (IsRoot)
        {
            var top = TopLevelNodes();
            if (top.Count == 0)
            {
                return null;
            }

            return direction switch
            {
                NavigateDirection.NavigateDirection_FirstChild => Child(top[0]),
                NavigateDirection.NavigateDirection_LastChild => Child(top[^1]),
                _ => null,
            };
        }

        var node = Node;
        if (node is null)
        {
            return null;
        }

        var siblings = SiblingIndices(node);
        var position = siblings.IndexOf(_nodeIndex);
        return direction switch
        {
            NavigateDirection.NavigateDirection_Parent => node.ParentIndex < 0 ? Root : Child(node.ParentIndex),
            NavigateDirection.NavigateDirection_FirstChild => node.Children.Count > 0 ? Child(node.Children[0]) : null,
            NavigateDirection.NavigateDirection_LastChild => node.Children.Count > 0 ? Child(node.Children[^1]) : null,
            NavigateDirection.NavigateDirection_NextSibling => position >= 0 && position + 1 < siblings.Count ? Child(siblings[position + 1]) : null,
            NavigateDirection.NavigateDirection_PreviousSibling => position > 0 ? Child(siblings[position - 1]) : null,
            _ => null,
        };
    }

    public int[] GetRuntimeId() => IsRoot ? new[] { 42 } : new[] { 42, (int)Node!.Id.Value };

    UiaRect IRawElementProviderFragment.get_BoundingRectangle() =>
        IsRoot ? RootRect() : ToUiaRect(Node?.Bounds ?? Rect.Empty);

    public IRawElementProviderFragmentRoot[]? GetEmbeddedFragmentRoots() => null;

    public void SetFocus()
    {
        if (IsRoot)
        {
            _window.Activate();
            return;
        }

        var id = Node?.Id ?? Id.None;
        _window.Post(() => _window.FocusRequested?.Invoke(id));
    }

    public IRawElementProviderFragmentRoot FragmentRoot => Root;

    public IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
    {
        var nodes = Snapshot.Nodes;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var bounds = ScreenRect(nodes[i].Bounds);
            if (x >= bounds.Left && x <= bounds.Right && y >= bounds.Top && y <= bounds.Bottom)
            {
                return new Win32UiaProvider(_window, i, Root);
            }
        }

        return null;
    }

    public IRawElementProviderFragment? GetFocus()
    {
        var nodes = Snapshot.Nodes;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].IsFocused)
            {
                return new Win32UiaProvider(_window, i, Root);
            }
        }

        return null;
    }

    private Win32UiaProvider Child(int index) => new(_window, index, Root);

    private List<int> TopLevelNodes()
    {
        var nodes = Snapshot.Nodes;
        var result = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].ParentIndex < 0)
            {
                result.Add(i);
            }
        }

        return result;
    }

    private List<int> SiblingIndices(AccessibilityNode node)
    {
        var nodes = Snapshot.Nodes;
        var result = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].ParentIndex == node.ParentIndex)
            {
                result.Add(i);
            }
        }

        return result;
    }

    private double[] RootRectArray()
    {
        var rect = RootRect();
        return new[] { rect.left, rect.top, rect.width, rect.height };
    }

    private double[] RectArray(Rect bounds)
    {
        var rect = ToUiaRect(bounds);
        return new[] { rect.left, rect.top, rect.width, rect.height };
    }

    private UiaRect RootRect()
    {
        var origin = _window.ClientOriginScreen;
        var size = _window.ClientSizeDip;
        var scale = _window.Scale;
        return new UiaRect
        {
            left = origin.X,
            top = origin.Y,
            width = size.Width * scale,
            height = size.Height * scale,
        };
    }

    private Rect ScreenRect(Rect bounds)
    {
        var origin = _window.ClientOriginScreen;
        var scale = _window.Scale;
        return new Rect(
            origin.X + bounds.X * scale,
            origin.Y + bounds.Y * scale,
            bounds.Width * scale,
            bounds.Height * scale);
    }

    private UiaRect ToUiaRect(Rect bounds)
    {
        var rect = ScreenRect(bounds);
        return new UiaRect { left = rect.Left, top = rect.Top, width = rect.Width, height = rect.Height };
    }

    private static int MapRole(AccessibilityRole role) => role switch
    {
        AccessibilityRole.Button => ButtonControlType,
        AccessibilityRole.CheckBox or AccessibilityRole.Switch => CheckBoxControlType,
        AccessibilityRole.RadioButton => RadioButtonControlType,
        AccessibilityRole.Slider => SliderControlType,
        AccessibilityRole.TextField or AccessibilityRole.TextArea => EditControlType,
        AccessibilityRole.ComboBox => ComboBoxControlType,
        AccessibilityRole.List => ListControlType,
        AccessibilityRole.ListItem or AccessibilityRole.TableRow => ListItemControlType,
        AccessibilityRole.Table => TableControlType,
        AccessibilityRole.Tree => TreeControlType,
        AccessibilityRole.TreeItem => TreeItemControlType,
        AccessibilityRole.Menu => MenuControlType,
        AccessibilityRole.MenuItem => MenuItemControlType,
        AccessibilityRole.Tab => TabControlType,
        AccessibilityRole.Dialog or AccessibilityRole.Window => WindowControlType,
        AccessibilityRole.ProgressBar => ProgressBarControlType,
        AccessibilityRole.StaticText => TextControlType,
        AccessibilityRole.Group => PaneControlType,
        _ => PaneControlType,
    };

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaHostProviderFromHwnd(IntPtr hwnd, out IntPtr provider);

}






[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class Win32UiaInvokePattern : IInvokeProvider
{
    private readonly Win32Window _window;
    private readonly Id _id;

    public Win32UiaInvokePattern(Win32Window window, Id id)
    {
        _window = window;
        _id = id;
    }

    public void Invoke() => _window.Post(() => _window.InvokeRequested?.Invoke(_id));
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class Win32UiaTogglePattern : IToggleProvider
{
    private readonly Win32Window _window;
    private readonly Id _id;
    private readonly bool _on;

    public Win32UiaTogglePattern(Win32Window window, Id id, bool on)
    {
        _window = window;
        _id = id;
        _on = on;
    }

    public ToggleState ToggleState => _on ? ToggleState.ToggleState_On : ToggleState.ToggleState_Off;

    public void Toggle() => _window.Post(() => _window.InvokeRequested?.Invoke(_id));
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class Win32UiaSelectionPattern : ISelectionItemProvider
{
    private readonly Win32Window _window;
    private readonly Id _id;
    private readonly bool _selected;

    public Win32UiaSelectionPattern(Win32Window window, Id id, bool selected)
    {
        _window = window;
        _id = id;
        _selected = selected;
    }

    public int IsSelected => _selected ? 1 : 0;
    public IRawElementProviderSimple? SelectionContainer => null;

    public void Select() => _window.Post(() => _window.InvokeRequested?.Invoke(_id));
    public void AddToSelection() => Select();
    public void RemoveFromSelection() { }
}




