using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static Vela.Platform.Win32.NativeMethods;

namespace Vela.Platform.Win32;

/// <summary>
/// Win32 原生窗口 + GDI DIB 呈现层。
///
/// 它只做平台转换：
/// <list type="bullet">
/// <item>创建/注册窗口，处理 DPI、尺寸、关闭；</item>
/// <item>把 WM_* 消息写入 InputBuilder；</item>
/// <item>提供 BGRA DIB，让 Skia 直接画到 Surface.Bits；</item>
/// <item>处理 IME、拖放、剪贴板、UIA 句柄。</item>
/// </list>
///
/// 它不知道 Vela 控件，也不知道 Skia；DesktopApp 负责把它们组合起来。
/// 多个实例可共存，静态字典按 HWND 路由 WndProc。
/// </summary>
public sealed class Win32Window : IDisposable
{
    private const string WindowClassName = "Vela.Win32Window";
    private static readonly Dictionary<IntPtr, Win32Window> Windows = new();
    private static readonly WndProc WindowProcedure = StaticWindowProcedure;
    private static bool _classRegistered;

    private readonly InputBuilder _input;
    private readonly ConcurrentQueue<Action> _postedActions = new();
    private Win32UiaBridge? _uia;
    private AccessibilitySnapshot? _accessibility;
    private IntPtr _handle;
    private uint _dpi;
    private int _clientWidthPx;
    private int _clientHeightPx;
    private bool _trackingMouseLeave;
    private bool _disposed;
    private bool _running;
    private CursorKind _cursor = CursorKind.Default;
    private Exception? _pendingException;

    public Win32Window(string title, int widthDip = 1280, int heightDip = 800, InputBuilder? input = null)
    {
        Title = title;
        _input = input ?? new InputBuilder();
        RegisterWindowClass();
        CreateNativeWindow(title, widthDip, heightDip);
        _uia = new Win32UiaBridge(this);
    }

    public string Title { get; private set; }
    public IntPtr Handle => _handle;
    public Win32Surface Surface { get; } = new();
    public float Scale => _dpi / 96f;
    public Size ClientSizeDip => new(
        _clientWidthPx > 0 ? _clientWidthPx / Scale : 0,
        _clientHeightPx > 0 ? _clientHeightPx / Scale : 0);
    public bool IsRunning => _running;
    public bool IsMinimized => _clientWidthPx <= 0 || _clientHeightPx <= 0;
    public int MinWidthDip { get; set; } = 720;
    public int MinHeightDip { get; set; } = 460;
    public InputBuilder Input => _input;
    public AccessibilitySnapshot? LatestAccessibility => Volatile.Read(ref _accessibility);
    public Action<Id>? FocusRequested { get; set; }
    public Action<Id>? InvokeRequested { get; set; }

    public Vec2 ClientOriginScreen
    {
        get
        {
            var point = new POINT();
            if (_handle != IntPtr.Zero)
            {
                _ = ClientToScreen(_handle, ref point);
            }

            return new Vec2(point.X, point.Y);
        }
    }

    public void SetAccessibility(AccessibilitySnapshot? snapshot) => Volatile.Write(ref _accessibility, snapshot);

    public void NotifyFocusChanged(Id id) => _uia?.RaiseFocusChanged(id);

    /// <summary>
    /// 从其他线程（例如 UIA provider）把动作排回 UI 线程。
    /// 动作会在下一次 PumpMessages 开始/结束时执行。
    /// </summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _postedActions.Enqueue(action);
    }

    public void Activate()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(_handle, SW_RESTORE);
        _ = SetForegroundWindow(_handle);
    }

    /// <summary>
    /// 泵当前线程的消息队列，并执行 UIA 线程投递的回调。
    /// 多个窗口共享同一消息队列时，一个窗口的调用也可能分发其他窗口的消息；
    /// 因此 WndProc 必须通过 HWND 字典找到对应实例。
    /// </summary>
    public void PumpMessages()
    {
        while (_postedActions.TryDequeue(out var action))
        {
            action();
        }

        _input.SetModifiers(ReadModifiers());
        while (PeekMessageW(out var message, IntPtr.Zero, 0, 0, PM_REMOVE))
        {
            if (message.message == WM_QUIT)
            {
                _running = false;
                break;
            }

            TranslateMessage(ref message);
            DispatchMessageW(ref message);
            if (!_running)
            {
                break;
            }
        }

        if (_pendingException is not null)
        {
            var exception = _pendingException;
            _pendingException = null;
            throw exception;
        }
    }

    /// <summary>
    /// 把 Surface 的 DIB 内容 BitBlt 到窗口，并 ValidateRect。
    /// 只在有脏区域时由宿主调用；WM_PAINT 也会调用它来恢复被遮挡区域。
    /// </summary>
    public void Present()
    {
        if (!_running || IsMinimized || Surface.IsEmpty)
        {
            return;
        }

        Surface.Present(_handle);
        ValidateRect(_handle, IntPtr.Zero);
    }

    public void SetCursor(CursorKind cursor)
    {
        if (_cursor == cursor)
        {
            return;
        }

        _cursor = cursor;
        ApplyCursor();
    }

    public void SetTheme(bool dark, Color background, Color text)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        var useDark = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(_handle, 20, ref useDark, sizeof(int));
        var caption = ColorRef(background);
        var textColor = ColorRef(text);
        var border = ColorRef(background);
        _ = DwmSetWindowAttribute(_handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(_handle, 36, ref textColor, sizeof(int));
        _ = DwmSetWindowAttribute(_handle, 34, ref border, sizeof(int));
    }

    public void SetTitle(string title)
    {
        Title = title;
        if (_handle != IntPtr.Zero)
        {
            _ = SetWindowTextW(_handle, title);
        }
    }

    public void Close()
    {
        if (_handle != IntPtr.Zero)
        {
            PostMessageW(_handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void Minimize()
    {
        if (_handle != IntPtr.Zero)
        {
            ShowWindow(_handle, SW_MINIMIZE);
        }
    }

    public void Maximize()
    {
        if (_handle != IntPtr.Zero)
        {
            ShowWindow(_handle, SW_MAXIMIZE);
        }
    }

    public void Restore()
    {
        if (_handle != IntPtr.Zero)
        {
            ShowWindow(_handle, SW_RESTORE);
        }
    }

    private static void RegisterWindowClass()
    {
        if (_classRegistered)
        {
            return;
        }

        var instance = GetModuleHandleW(null);
        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS,
            lpfnWndProc = WindowProcedure,
            hInstance = instance,
            hCursor = LoadCursorW(IntPtr.Zero, IDC_ARROW),
            hbrBackground = IntPtr.Zero,
            lpszClassName = WindowClassName,
        };

        if (RegisterClassExW(ref wndClass) == 0)
        {
            throw new InvalidOperationException($"RegisterClassExW failed ({Marshal.GetLastWin32Error()}).");
        }

        _classRegistered = true;
    }

    private void CreateNativeWindow(string title, int widthDip, int heightDip)
    {
        _dpi = Math.Max(96, GetDpiForSystem());
        var scale = _dpi / 96f;
        var width = Math.Max(320, (int)MathF.Round(widthDip * scale));
        var height = Math.Max(240, (int)MathF.Round(heightDip * scale));

        var rect = new RECT { Right = width, Bottom = height };
        AdjustWindowRectExForDpi(ref rect, WS_OVERLAPPEDWINDOW, false, 0, _dpi);
        var windowWidth = rect.Width;
        var windowHeight = rect.Height;
        var screenWidth = GetSystemMetrics((int)SM_CXSCREEN);
        var screenHeight = GetSystemMetrics((int)SM_CYSCREEN);
        var x = Math.Max(0, (screenWidth - windowWidth) / 2);
        var y = Math.Max(0, (screenHeight - windowHeight) / 2);

        var instance = GetModuleHandleW(null);
        _handle = CreateWindowExW(
            0,
            WindowClassName,
            title,
            WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
            x,
            y,
            windowWidth,
            windowHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowExW failed ({Marshal.GetLastWin32Error()}).");
        }

        Windows[_handle] = this;
        _dpi = Math.Max(96, GetDpiForWindow(_handle));
        UpdateClientArea();
        ShowWindow(_handle, SW_SHOW);
        UpdateWindow(_handle);
        SetForegroundWindow(_handle);
        _running = true;
    }

    private void UpdateClientArea()
    {
        if (_handle == IntPtr.Zero || !GetClientRect(_handle, out var rect))
        {
            return;
        }

        _clientWidthPx = rect.Width;
        _clientHeightPx = rect.Height;
        if (_clientWidthPx > 0 && _clientHeightPx > 0)
        {
            Surface.Resize(_clientWidthPx, _clientHeightPx);
        }
    }

    private static IntPtr StaticWindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (!Windows.TryGetValue(hwnd, out var window))
        {
            if (message == WM_CREATE)
            {
                return IntPtr.Zero;
            }

            return DefWindowProcW(hwnd, message, wParam, lParam);
        }

        try
        {
            return window.HandleMessage(message, wParam, lParam);
        }
        catch (Exception exception)
        {
            window._pendingException = exception;
            DestroyWindow(hwnd);
            return IntPtr.Zero;
        }
    }

    private IntPtr HandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WM_PAINT:
                Present();
                ValidateRect(_handle, IntPtr.Zero);
                return IntPtr.Zero;

            case WM_ERASEBKGND:
                return new IntPtr(1);

            case WM_SIZE:
                _clientWidthPx = LowWord(lParam);
                _clientHeightPx = HighWord(lParam);
                if (_clientWidthPx > 0 && _clientHeightPx > 0)
                {
                    Surface.Resize(_clientWidthPx, _clientHeightPx);
                }

                return IntPtr.Zero;

            case WM_DPICHANGED:
                _dpi = (uint)LowWord(wParam);
                var suggested = Marshal.PtrToStructure<RECT>(lParam);
                SetWindowPos(_handle, IntPtr.Zero, suggested.Left, suggested.Top, suggested.Width, suggested.Height, SWP_NOZORDER | SWP_NOACTIVATE);
                UpdateClientArea();
                return IntPtr.Zero;

            case WM_GETMINMAXINFO:
                var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                info.MinTrackSize.X = (int)MathF.Round(MinWidthDip * Scale);
                info.MinTrackSize.Y = (int)MathF.Round(MinHeightDip * Scale);
                Marshal.StructureToPtr(info, lParam, false);
                return IntPtr.Zero;

            case WM_SETCURSOR:
                if (LowWord(lParam) == HTCLIENT)
                {
                    ApplyCursor();
                    return new IntPtr(1);
                }

                break;

            case WM_MOUSEMOVE:
                HandleMouseMove(lParam);
                return IntPtr.Zero;

            case WM_MOUSELEAVE:
                _trackingMouseLeave = false;
                _input.SetPointerInside(false);
                return IntPtr.Zero;

            case WM_LBUTTONDOWN:
                HandleMouseButton(PointerButton.Left, true, lParam);
                SetCapture(_handle);
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                HandleMouseButton(PointerButton.Left, false, lParam);
                if (_input.DownButtons == PointerButton.None)
                {
                    ReleaseCapture();
                }

                return IntPtr.Zero;

            case WM_RBUTTONDOWN:
                HandleMouseButton(PointerButton.Right, true, lParam);
                SetCapture(_handle);
                return IntPtr.Zero;

            case WM_RBUTTONUP:
                HandleMouseButton(PointerButton.Right, false, lParam);
                if (_input.DownButtons == PointerButton.None)
                {
                    ReleaseCapture();
                }

                return IntPtr.Zero;

            case WM_MBUTTONDOWN:
                HandleMouseButton(PointerButton.Middle, true, lParam);
                SetCapture(_handle);
                return IntPtr.Zero;

            case WM_MBUTTONUP:
                HandleMouseButton(PointerButton.Middle, false, lParam);
                if (_input.DownButtons == PointerButton.None)
                {
                    ReleaseCapture();
                }

                return IntPtr.Zero;

            case WM_XBUTTONDOWN:
                HandleMouseButton((HighWord(wParam) == 1 ? PointerButton.X1 : PointerButton.X2), true, lParam);
                SetCapture(_handle);
                return IntPtr.Zero;

            case WM_XBUTTONUP:
                HandleMouseButton((HighWord(wParam) == 1 ? PointerButton.X1 : PointerButton.X2), false, lParam);
                if (_input.DownButtons == PointerButton.None)
                {
                    ReleaseCapture();
                }

                return IntPtr.Zero;

            case WM_MOUSEWHEEL:
                HandleWheel(wParam, lParam, horizontal: false);
                return IntPtr.Zero;

            case WM_MOUSEHWHEEL:
                HandleWheel(wParam, lParam, horizontal: true);
                return IntPtr.Zero;

            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                HandleKey(wParam, down: true, lParam);
                return message == WM_SYSKEYDOWN ? DefWindowProcW(_handle, message, wParam, lParam) : IntPtr.Zero;

            case WM_KEYUP:
            case WM_SYSKEYUP:
                HandleKey(wParam, down: false, lParam);
                return message == WM_SYSKEYUP ? DefWindowProcW(_handle, message, wParam, lParam) : IntPtr.Zero;

            case WM_CHAR:
                {
                    var character = (char)LowWord(wParam);
                    if (character >= ' ' && character != '\u007F' && (ReadModifiers() & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
                    {
                        _input.AddText(character.ToString());
                    }

                    return IntPtr.Zero;
                }

            case WM_IME_STARTCOMPOSITION:
                return IntPtr.Zero;

            case WM_IME_ENDCOMPOSITION:
                _input.ClearComposition();
                return IntPtr.Zero;

            case WM_IME_COMPOSITION:
                HandleImeComposition((uint)lParam.ToInt64());
                return IntPtr.Zero;

            case WM_IME_CHAR:
                return IntPtr.Zero;

            case WM_SYSCHAR:
                return IntPtr.Zero;

            case WM_KILLFOCUS:
                _input.ReleaseAllInputs();
                return IntPtr.Zero;

            case WM_ACTIVATE:
                if (LowWord(wParam) == 0)
                {
                    _input.ReleaseAllInputs();
                }

                return IntPtr.Zero;

            case WM_CLOSE:
                DestroyWindow(_handle);
                return IntPtr.Zero;

            case WM_DROPFILES:
                HandleDropFiles(wParam);
                return IntPtr.Zero;

            case WM_GETOBJECT:
                if (_uia is not null)
                {
                    var result = _uia.HandleGetObject(wParam, lParam);
                    if (result != IntPtr.Zero)
                    {
                        return result;
                    }
                }

                break;

            case WM_DESTROY:
                _running = false;
                Windows.Remove(_handle);
                _handle = IntPtr.Zero;
                _uia?.Disconnect();
                _uia = null;
                if (Windows.Count == 0)
                {
                    PostQuitMessage(0);
                }

                return IntPtr.Zero;
        }

        return DefWindowProcW(_handle, message, wParam, lParam);
    }

    private void HandleMouseMove(IntPtr lParam)
    {
        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        _input.SetPointerPosition(new Vec2(x / Scale, y / Scale), inside: true);
        if (!_trackingMouseLeave)
        {
            var track = new TRACKMOUSEEVENT
            {
                cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags = TME_LEAVE,
                hwndTrack = _handle,
            };
            TrackMouseEvent(ref track);
            _trackingMouseLeave = true;
        }
    }

    private void HandleMouseButton(PointerButton button, bool down, IntPtr lParam)
    {
        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        _input.SetPointerPosition(new Vec2(x / Scale, y / Scale), inside: true);
        _input.SetModifiers(ReadModifiers());
        _input.SetButton(button, down);
    }

    private void HandleImeComposition(uint flags)
    {
        var himc = ImmGetContext(_handle);
        if (himc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if ((flags & GCS_RESULTSTR) != 0)
            {
                var result = ReadImeString(himc, GCS_RESULTSTR);
                if (!string.IsNullOrEmpty(result))
                {
                    _input.AddText(result);
                }

                _input.ClearComposition();
            }

            if ((flags & GCS_COMPSTR) != 0)
            {
                var composition = ReadImeString(himc, GCS_COMPSTR);
                var cursor = ImmGetCompositionStringW(himc, GCS_CURSORPOS, null, 0);
                _input.SetComposition(composition, Math.Clamp(cursor, 0, composition.Length));
                if (!string.IsNullOrEmpty(composition))
                {
                    var form = new COMPOSITIONFORM
                    {
                        dwStyle = CFS_POINT,
                        ptCurrentPos = new POINT
                        {
                            X = (int)MathF.Round(_input.PointerPosition.X * Scale),
                            Y = (int)MathF.Round(_input.PointerPosition.Y * Scale),
                        },
                    };
                    _ = ImmSetCompositionWindow(himc, ref form);
                }
            }
        }
        finally
        {
            _ = ImmReleaseContext(_handle, himc);
        }
    }

    private static string ReadImeString(IntPtr himc, uint index)
    {
        var byteCount = ImmGetCompositionStringW(himc, index, null, 0);
        if (byteCount <= 0)
        {
            return string.Empty;
        }

        var buffer = new byte[byteCount];
        var copied = ImmGetCompositionStringW(himc, index, buffer, byteCount);
        return copied > 0 ? System.Text.Encoding.Unicode.GetString(buffer, 0, copied) : string.Empty;
    }

    private void HandleDropFiles(IntPtr drop)
    {
        var count = DragQueryFileW(drop, uint.MaxValue, null, 0);
        for (uint i = 0; i < count; i++)
        {
            var length = DragQueryFileW(drop, i, null, 0);
            if (length == 0)
            {
                continue;
            }

            var buffer = new System.Text.StringBuilder((int)length + 1);
            if (DragQueryFileW(drop, i, buffer, (uint)buffer.Capacity) > 0)
            {
                _input.AddDroppedFile(buffer.ToString());
            }
        }

        DragFinish(drop);
    }

    private void HandleWheel(IntPtr wParam, IntPtr lParam, bool horizontal)
    {
        var delta = HighWord(wParam) / 120f;
        var point = new POINT
        {
            X = unchecked((short)(long)lParam),
            Y = unchecked((short)((long)lParam >> 16)),
        };
        ScreenToClient(_handle, ref point);
        _input.SetPointerPosition(new Vec2(point.X / Scale, point.Y / Scale), inside: true);
        _input.SetModifiers(ReadModifiers());
        if (horizontal)
        {
            _input.AddWheel(delta, 0);
        }
        else
        {
            _input.AddWheel(0, delta);
        }
    }

    private void HandleKey(IntPtr wParam, bool down, IntPtr lParam)
    {
        var key = MapVirtualKey(wParam.ToInt32());
        if (key == Key.Unknown)
        {
            return;
        }

        _input.SetModifiers(ReadModifiers());
        if (down)
        {
            var repeat = (lParam.ToInt64() & (1L << 30)) != 0;
            _input.AddKeyDown(key, repeat);
        }
        else
        {
            _input.AddKeyUp(key);
        }
    }

    private void ApplyCursor()
    {
        var id = _cursor switch
        {
            CursorKind.Hand => IDC_HAND,
            CursorKind.IBeam => IDC_IBEAM,
            CursorKind.ResizeHorizontal => IDC_SIZEWE,
            CursorKind.ResizeVertical => IDC_SIZENS,
            CursorKind.NotAllowed => IDC_NO,
            _ => IDC_ARROW,
        };
        NativeMethods.SetCursor(LoadCursorW(IntPtr.Zero, id));
    }

    private static ModifierKeys ReadModifiers()
    {
        var modifiers = ModifierKeys.None;
        if ((GetKeyState(0x10) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Shift;
        }

        if ((GetKeyState(0x11) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Control;
        }

        if ((GetKeyState(0x12) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Alt;
        }

        if ((GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Super;
        }

        return modifiers;
    }

    private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private static Key MapVirtualKey(int vk) => vk switch
    {
        >= 0x41 and <= 0x5A => (Key)((int)Key.A + (vk - 0x41)),
        >= 0x30 and <= 0x39 => (Key)((int)Key.D0 + (vk - 0x30)),
        >= 0x70 and <= 0x87 => (Key)((int)Key.F1 + (vk - 0x70)),
        >= 0x60 and <= 0x69 => (Key)((int)Key.Numpad0 + (vk - 0x60)),
        0x08 => Key.Backspace,
        0x09 => Key.Tab,
        0x0D => Key.Enter,
        0x10 => Key.Shift,
        0x11 => Key.Control,
        0x12 => Key.Alt,
        0x13 => Key.Pause,
        0x14 => Key.CapsLock,
        0x1B => Key.Escape,
        0x20 => Key.Space,
        0x21 => Key.PageUp,
        0x22 => Key.PageDown,
        0x23 => Key.End,
        0x24 => Key.Home,
        0x25 => Key.Left,
        0x26 => Key.Up,
        0x27 => Key.Right,
        0x28 => Key.Down,
        0x2C => Key.PrintScreen,
        0x2D => Key.Insert,
        0x2E => Key.Delete,
        0x5B => Key.Unknown,
        0x5C => Key.Unknown,
        0x5D => Key.Unknown,
        0x6A => Key.NumpadMultiply,
        0x6B => Key.NumpadAdd,
        0x6D => Key.NumpadSubtract,
        0x6E => Key.NumpadDecimal,
        0x6F => Key.NumpadDivide,
        0x90 => Key.NumLock,
        0x91 => Key.ScrollLock,
        0xBA => Key.Semicolon,
        0xBB => Key.Equal,
        0xBC => Key.Comma,
        0xBD => Key.Minus,
        0xBE => Key.Period,
        0xBF => Key.Slash,
        0xC0 => Key.Backtick,
        0xDB => Key.LeftBracket,
        0xDC => Key.Backslash,
        0xDD => Key.RightBracket,
        0xDE => Key.Quote,
        _ => Key.Unknown,
    };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _running = false;
        if (_handle != IntPtr.Zero)
        {
            Windows.Remove(_handle);
            DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }

        Surface.Dispose();
    }
}







