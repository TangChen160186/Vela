using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace Vela.Platform.Sdl;

/// <summary>
/// SDL3 跨平台窗口：窗口、DPI、输入、IME 组合文本、剪贴板、拖放、呈现。
///
/// 两种模式：
/// <list type="bullet">
/// <item>CPU：Skia 画到调用方的 BGRA 缓冲，再上传 streaming texture；</item>
/// <item>OpenGL：创建 GL 3.3 Core context，由 SkiaGpuRenderer 直接画默认 framebuffer。</item>
/// </list>
///
/// 事件处理是“平台 → InputBuilder”的单向转换；它不依赖 Vela.Core 的控件。
/// </summary>
public sealed unsafe class Sdl3Window : IDisposable
{
    private readonly InputBuilder _input;
    private readonly Dictionary<CursorKind, IntPtr> _cursors = new();
    private SDL_Window* _window;
    private SDL_Renderer* _renderer;
    private SDL_Texture* _texture;
    private SDL_GLContextState* _glContext;
    private readonly bool _useOpenGL;
    private IntPtr _bits;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _rowBytes;
    private int _dipWidth;
    private int _dipHeight;
    private float _scale = 1;
    private bool _running = true;
    private bool _disposed;
    private bool _trackingMouse;
    private CursorKind _cursor = CursorKind.Default;
    private bool _themeIsDark;

    public Sdl3Window(string title, int widthDip = 1280, int heightDip = 800, InputBuilder? input = null, bool useOpenGL = false)
    {
        SdlRuntime.Acquire();
        _input = input ?? new InputBuilder();
        _useOpenGL = useOpenGL;
        if (useOpenGL)
        {
            _ = SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
            _ = SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
            _ = SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_CORE);
            _ = SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DOUBLEBUFFER, 1);
        }

        _window = SDL_CreateWindow(
            title,
            Math.Max(320, widthDip),
            Math.Max(220, heightDip),
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE |
            SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY |
            (useOpenGL ? SDL_WindowFlags.SDL_WINDOW_OPENGL : 0));
        if (_window == null)
        {
            SdlRuntime.Release();
            throw new InvalidOperationException("SDL_CreateWindow failed: " + SdlError());
        }

        if (useOpenGL)
        {
            _glContext = SDL_GL_CreateContext(_window);
            if (_glContext == null)
            {
                SDL_DestroyWindow(_window);
                _window = null;
                SdlRuntime.Release();
                throw new InvalidOperationException("SDL_GL_CreateContext failed: " + SdlError());
            }

            _ = SDL_GL_MakeCurrent(_window, _glContext);
            _ = SDL_GL_SetSwapInterval(1);
        }
        else
        {
            _renderer = SDL_CreateRenderer(_window, (byte*)null);
            if (_renderer == null)
            {
                SDL_DestroyWindow(_window);
                _window = null;
                SdlRuntime.Release();
                throw new InvalidOperationException("SDL_CreateRenderer failed: " + SdlError());
            }

            _ = SDL_SetRenderVSync(_renderer, 1);
        }

        _ = SDL_StartTextInput(_window);
        _ = SDL_SetWindowMinimumSize(_window, 560, 360);
        SDL_ShowWindow(_window);
        Resize();
    }

    public string Title { get; private set; } = "Vela";
    public IntPtr NativeHandle => (IntPtr)_window;
    public InputBuilder Input => _input;
    public bool IsRunning => _running && _window != null;
    public bool IsMinimized => _pixelWidth <= 0 || _pixelHeight <= 0;
    public bool IsGpu => _useOpenGL;
    public IntPtr GlContext => (IntPtr)_glContext;
    public float Scale => _scale;
    public Size ClientSizeDip => new(_dipWidth, _dipHeight);
    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;
    public int RowBytes => _rowBytes;
    public IntPtr Pixels => _bits;

    public void SetTitle(string title)
    {
        Title = title;
        if (_window != null)
        {
            _ = SDL_SetWindowTitle(_window, title);
        }
    }

    /// <summary>
    /// 泵 SDL 事件并转换为 InputBuilder 状态。
    /// 鼠标/键盘/文本编辑/拖放全部在这里桥接；窗口关闭只置 running=false，
    /// 由宿主循环决定何时退出。OpenGL 模式不创建 SDL renderer/texture。
    /// </summary>
    public void PumpEvents()
    {
        if (_window == null)
        {
            return;
        }

        _input.SetModifiers(MapModifiers(SDL_GetModState()));
        SDL_Event evt;
        while (SDL_PollEvent(&evt))
        {
            switch (evt.Type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    _running = false;
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                    if (evt.window.windowID == SDL_GetWindowID(_window))
                    {
                        _running = false;
                    }

                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                case SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED:
                    Resize();
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                    _input.ReleaseAllInputs();
                    _trackingMouse = false;
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_ENTER:
                    _trackingMouse = true;
                    _input.SetPointerInside(true);
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE:
                    _trackingMouse = false;
                    _input.SetPointerInside(false);
                    break;
                case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                    _input.SetModifiers(MapModifiers(SDL_GetModState()));
                    _input.SetPointerPosition(new Vec2(evt.motion.x, evt.motion.y), _trackingMouse);
                    break;
                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                    {
                        _input.SetPointerPosition(new Vec2(evt.button.x, evt.button.y), true);
                        var button = evt.button.button switch
                        {
                            1 => PointerButton.Left,
                            2 => PointerButton.Middle,
                            3 => PointerButton.Right,
                            4 => PointerButton.X1,
                            5 => PointerButton.X2,
                            _ => PointerButton.None,
                        };
                        if (button != PointerButton.None)
                        {
                            _input.SetButton(button, evt.button.down);
                        }

                        break;
                    }
                case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                    if (evt.wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED)
                    {
                        _input.AddWheel(-evt.wheel.x, -evt.wheel.y);
                    }
                    else
                    {
                        _input.AddWheel(evt.wheel.x, evt.wheel.y);
                    }

                    break;
                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                case SDL_EventType.SDL_EVENT_KEY_UP:
                    {
                        var key = MapKey(evt.key.scancode);
                        if (key == Key.Unknown)
                        {
                            break;
                        }

                        _input.SetModifiers(MapModifiers(evt.key.mod));
                        if (evt.key.down)
                        {
                            _input.AddKeyDown(key, evt.key.repeat);
                        }
                        else
                        {
                            _input.AddKeyUp(key);
                        }

                        break;
                    }
                case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                    AddSdlText(evt.text.text);
                    break;
                case SDL_EventType.SDL_EVENT_TEXT_EDITING:
                    {
                        var composition = ReadUtf8(evt.edit.text);
                        _input.SetComposition(composition, Math.Clamp(evt.edit.start, 0, composition.Length));
                        break;
                    }
                case SDL_EventType.SDL_EVENT_DROP_FILE:
                    _input.AddDroppedFile(ReadUtf8(evt.drop.data));
                    break;
            }
        }
    }

    /// <summary>
    /// 呈现一帧：CPU 模式上传纹理 + RenderTexture + Present；
    /// OpenGL 模式直接 SwapWindow。只应由宿主在存在新内容时调用。
    /// </summary>
    public void Present()
    {
        if (!_running || IsMinimized)
        {
            return;
        }

        if (_useOpenGL)
        {
            _ = SDL_GL_SwapWindow(_window);
            ApplyCursor();
            return;
        }

        if (_texture == null || _bits == IntPtr.Zero)
        {
            return;
        }

        _ = SDL_UpdateTexture(_texture, null, _bits, _rowBytes);
        _ = SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        _ = SDL_RenderClear(_renderer);
        _ = SDL_RenderTexture(_renderer, _texture, null, null);
        _ = SDL_RenderPresent(_renderer);
        ApplyCursor();
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
        // SDL leaves native chrome to the OS; this is the extension point for a
        // platform-specific dark-titlebar bridge.
        _themeIsDark = dark;
        _ = background;
        _ = text;
    }

    public void Close()
    {
        _running = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var cursor in _cursors.Values)
        {
            SDL_DestroyCursor((SDL_Cursor*)cursor);
        }

        _cursors.Clear();
        if (_texture != null)
        {
            SDL_DestroyTexture(_texture);
            _texture = null;
        }

        if (_glContext != null)
        {
            SDL_GL_DestroyContext(_glContext);
            _glContext = null;
        }

        if (_renderer != null)
        {
            SDL_DestroyRenderer(_renderer);
            _renderer = null;
        }

        if (_window != null)
        {
            SDL_DestroyWindow(_window);
            _window = null;
        }

        FreePixels();
        SdlRuntime.Release();
    }

    /// <summary>
    /// 重新读取 DIP/像素尺寸并更新 scale。CPU 模式重建 DIB/纹理；
    /// OpenGL 模式只更新尺寸，surface 由 SkiaGpuRenderer 在下一帧按新尺寸重建。
    /// </summary>
    private void Resize()
    {
        if (_window == null)
        {
            return;
        }

        fixed (int* dipWidth = &_dipWidth)
        fixed (int* dipHeight = &_dipHeight)
        fixed (int* pixelWidth = &_pixelWidth)
        fixed (int* pixelHeight = &_pixelHeight)
        {
            _ = SDL_GetWindowSize(_window, dipWidth, dipHeight);
            _ = SDL_GetWindowSizeInPixels(_window, pixelWidth, pixelHeight);
        }
        if (_pixelWidth <= 0 || _pixelHeight <= 0 || _dipWidth <= 0 || _dipHeight <= 0)
        {
            return;
        }

        _scale = (float)_pixelWidth / _dipWidth;
        if (_useOpenGL)
        {
            return;
        }

        if (_texture != null)
        {
            SDL_DestroyTexture(_texture);
            _texture = null;
        }

        FreePixels();
        _rowBytes = _pixelWidth * 4;
        _bits = Marshal.AllocHGlobal(_rowBytes * _pixelHeight);
        _texture = SDL_CreateTexture(
            _renderer,
            SDL_PixelFormat.SDL_PIXELFORMAT_ARGB8888,
            SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            _pixelWidth,
            _pixelHeight);
        if (_texture == null)
        {
            throw new InvalidOperationException("SDL_CreateTexture failed: " + SdlError());
        }

        _ = SDL_SetTextureScaleMode(_texture, SDL_ScaleMode.SDL_SCALEMODE_NEAREST);
    }

    private void FreePixels()
    {
        if (_bits != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_bits);
            _bits = IntPtr.Zero;
        }
    }

    private void ApplyCursor()
    {
        if (_window == null)
        {
            return;
        }

        var system = _cursor switch
        {
            CursorKind.Hand => SDL_SystemCursor.SDL_SYSTEM_CURSOR_POINTER,
            CursorKind.IBeam => SDL_SystemCursor.SDL_SYSTEM_CURSOR_TEXT,
            CursorKind.ResizeHorizontal => SDL_SystemCursor.SDL_SYSTEM_CURSOR_EW_RESIZE,
            CursorKind.ResizeVertical => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NS_RESIZE,
            CursorKind.NotAllowed => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NOT_ALLOWED,
            _ => SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT,
        };
        if (!_cursors.TryGetValue(_cursor, out var handle))
        {
            handle = (IntPtr)SDL_CreateSystemCursor(system);
            _cursors[_cursor] = handle;
        }

        if (handle != IntPtr.Zero)
        {
            _ = SDL_SetCursor((SDL_Cursor*)handle);
        }
    }

    private void AddSdlText(byte* text)
    {
        var value = ReadUtf8(text);
        if (!string.IsNullOrEmpty(value))
        {
            _input.AddText(value);
            _input.ClearComposition();
        }
    }

    private static string ReadUtf8(byte* value) =>
        value == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)value) ?? string.Empty;

    private static ModifierKeys MapModifiers(SDL_Keymod modifiers)
    {
        var result = ModifierKeys.None;
        if ((modifiers & SDL_Keymod.SDL_KMOD_SHIFT) != 0)
        {
            result |= ModifierKeys.Shift;
        }

        if ((modifiers & SDL_Keymod.SDL_KMOD_CTRL) != 0)
        {
            result |= ModifierKeys.Control;
        }

        if ((modifiers & SDL_Keymod.SDL_KMOD_ALT) != 0)
        {
            result |= ModifierKeys.Alt;
        }

        if ((modifiers & SDL_Keymod.SDL_KMOD_GUI) != 0)
        {
            result |= ModifierKeys.Super;
        }

        return result;
    }

    private static Key MapKey(SDL_Scancode code) => code switch
    {
        SDL_Scancode.SDL_SCANCODE_A => Key.A,
        SDL_Scancode.SDL_SCANCODE_B => Key.B,
        SDL_Scancode.SDL_SCANCODE_C => Key.C,
        SDL_Scancode.SDL_SCANCODE_D => Key.D,
        SDL_Scancode.SDL_SCANCODE_E => Key.E,
        SDL_Scancode.SDL_SCANCODE_F => Key.F,
        SDL_Scancode.SDL_SCANCODE_G => Key.G,
        SDL_Scancode.SDL_SCANCODE_H => Key.H,
        SDL_Scancode.SDL_SCANCODE_I => Key.I,
        SDL_Scancode.SDL_SCANCODE_J => Key.J,
        SDL_Scancode.SDL_SCANCODE_K => Key.K,
        SDL_Scancode.SDL_SCANCODE_L => Key.L,
        SDL_Scancode.SDL_SCANCODE_M => Key.M,
        SDL_Scancode.SDL_SCANCODE_N => Key.N,
        SDL_Scancode.SDL_SCANCODE_O => Key.O,
        SDL_Scancode.SDL_SCANCODE_P => Key.P,
        SDL_Scancode.SDL_SCANCODE_Q => Key.Q,
        SDL_Scancode.SDL_SCANCODE_R => Key.R,
        SDL_Scancode.SDL_SCANCODE_S => Key.S,
        SDL_Scancode.SDL_SCANCODE_T => Key.T,
        SDL_Scancode.SDL_SCANCODE_U => Key.U,
        SDL_Scancode.SDL_SCANCODE_V => Key.V,
        SDL_Scancode.SDL_SCANCODE_W => Key.W,
        SDL_Scancode.SDL_SCANCODE_X => Key.X,
        SDL_Scancode.SDL_SCANCODE_Y => Key.Y,
        SDL_Scancode.SDL_SCANCODE_Z => Key.Z,
        SDL_Scancode.SDL_SCANCODE_0 => Key.D0,
        SDL_Scancode.SDL_SCANCODE_1 => Key.D1,
        SDL_Scancode.SDL_SCANCODE_2 => Key.D2,
        SDL_Scancode.SDL_SCANCODE_3 => Key.D3,
        SDL_Scancode.SDL_SCANCODE_4 => Key.D4,
        SDL_Scancode.SDL_SCANCODE_5 => Key.D5,
        SDL_Scancode.SDL_SCANCODE_6 => Key.D6,
        SDL_Scancode.SDL_SCANCODE_7 => Key.D7,
        SDL_Scancode.SDL_SCANCODE_8 => Key.D8,
        SDL_Scancode.SDL_SCANCODE_9 => Key.D9,
        SDL_Scancode.SDL_SCANCODE_RETURN => Key.Enter,
        SDL_Scancode.SDL_SCANCODE_ESCAPE => Key.Escape,
        SDL_Scancode.SDL_SCANCODE_BACKSPACE => Key.Backspace,
        SDL_Scancode.SDL_SCANCODE_TAB => Key.Tab,
        SDL_Scancode.SDL_SCANCODE_SPACE => Key.Space,
        SDL_Scancode.SDL_SCANCODE_DELETE => Key.Delete,
        SDL_Scancode.SDL_SCANCODE_INSERT => Key.Insert,
        SDL_Scancode.SDL_SCANCODE_HOME => Key.Home,
        SDL_Scancode.SDL_SCANCODE_END => Key.End,
        SDL_Scancode.SDL_SCANCODE_PAGEUP => Key.PageUp,
        SDL_Scancode.SDL_SCANCODE_PAGEDOWN => Key.PageDown,
        SDL_Scancode.SDL_SCANCODE_LEFT => Key.Left,
        SDL_Scancode.SDL_SCANCODE_RIGHT => Key.Right,
        SDL_Scancode.SDL_SCANCODE_UP => Key.Up,
        SDL_Scancode.SDL_SCANCODE_DOWN => Key.Down,
        SDL_Scancode.SDL_SCANCODE_CAPSLOCK => Key.CapsLock,
        SDL_Scancode.SDL_SCANCODE_LSHIFT or SDL_Scancode.SDL_SCANCODE_RSHIFT => Key.Shift,
        SDL_Scancode.SDL_SCANCODE_LCTRL or SDL_Scancode.SDL_SCANCODE_RCTRL => Key.Control,
        SDL_Scancode.SDL_SCANCODE_LALT or SDL_Scancode.SDL_SCANCODE_RALT => Key.Alt,
        SDL_Scancode.SDL_SCANCODE_F1 => Key.F1,
        SDL_Scancode.SDL_SCANCODE_F2 => Key.F2,
        SDL_Scancode.SDL_SCANCODE_F3 => Key.F3,
        SDL_Scancode.SDL_SCANCODE_F4 => Key.F4,
        SDL_Scancode.SDL_SCANCODE_F5 => Key.F5,
        SDL_Scancode.SDL_SCANCODE_F6 => Key.F6,
        SDL_Scancode.SDL_SCANCODE_F7 => Key.F7,
        SDL_Scancode.SDL_SCANCODE_F8 => Key.F8,
        SDL_Scancode.SDL_SCANCODE_F9 => Key.F9,
        SDL_Scancode.SDL_SCANCODE_F10 => Key.F10,
        SDL_Scancode.SDL_SCANCODE_F11 => Key.F11,
        SDL_Scancode.SDL_SCANCODE_F12 => Key.F12,
        SDL_Scancode.SDL_SCANCODE_SEMICOLON => Key.Semicolon,
        SDL_Scancode.SDL_SCANCODE_EQUALS => Key.Equal,
        SDL_Scancode.SDL_SCANCODE_COMMA => Key.Comma,
        SDL_Scancode.SDL_SCANCODE_MINUS => Key.Minus,
        SDL_Scancode.SDL_SCANCODE_PERIOD => Key.Period,
        SDL_Scancode.SDL_SCANCODE_SLASH => Key.Slash,
        SDL_Scancode.SDL_SCANCODE_GRAVE => Key.Backtick,
        SDL_Scancode.SDL_SCANCODE_LEFTBRACKET => Key.LeftBracket,
        SDL_Scancode.SDL_SCANCODE_BACKSLASH => Key.Backslash,
        SDL_Scancode.SDL_SCANCODE_RIGHTBRACKET => Key.RightBracket,
        SDL_Scancode.SDL_SCANCODE_APOSTROPHE => Key.Quote,
        _ => Key.Unknown,
    };

    private static string SdlError()
    {
        var value = SDL_GetError();
        return value ?? "unknown SDL error";
    }
}

internal static unsafe class SdlRuntime
{
    private static int _references;

    public static void Acquire()
    {
        if (Interlocked.Increment(ref _references) == 1)
        {
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_EVENTS))
            {
                Interlocked.Decrement(ref _references);
                throw new InvalidOperationException("SDL_Init failed: " + SDL_GetError());
            }
        }
    }

    public static void Release()
    {
        if (Interlocked.Decrement(ref _references) == 0)
        {
            SDL_Quit();
        }
    }
}



