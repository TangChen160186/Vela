using System.Diagnostics;
using Vela.Platform.Win32;
using Vela.Rendering.Skia;

namespace Vela.Desktop;

public sealed record WindowOptions
{
    public string Title { get; init; } = "Vela";
    public int Width { get; init; } = 1100;
    public int Height { get; init; } = 720;
    public Theme? Theme { get; init; }
}

/// <summary>One independent native window with its own immediate-mode UI tree.</summary>
public sealed class MultiWindow : IDisposable
{
    private readonly MultiWindowApp _owner;
    private readonly DrawDamageTracker _damage = new();
    private readonly SkiaTextEngine _textEngine;
    private readonly SkiaRenderer _renderer;
    private readonly Ui _ui;
    private readonly Action<Ui> _build;
    private Theme _theme;
    private Id _lastAccessibleFocus;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private bool _disposed;

    internal MultiWindow(MultiWindowApp owner, WindowOptions options, Action<Ui> build, IClipboard clipboard)
    {
        _owner = owner;
        _build = build;
        _theme = options.Theme ?? owner.Theme;
        _textEngine = new SkiaTextEngine();
        _renderer = new SkiaRenderer(_textEngine);
        var input = new InputBuilder();
        Native = new Win32Window(options.Title, options.Width, options.Height, input);
        Input = input;
        Native.SetTheme(_theme.IsDark, _theme.Background, _theme.Text);
        _ui = new Ui(new UiOptions
        {
            TextEngine = _textEngine,
            Theme = _theme,
            Clipboard = clipboard,
        });
        Native.FocusRequested = id => _ui.Focus(id, keyboardVisible: true);
        Native.InvokeRequested = id => _ui.Invoke(id);
    }

    public Win32Window Native { get; }
    public InputBuilder Input { get; }
    public Ui Ui => _ui;
    public bool IsOpen => Native.IsRunning;

    public void Close() => Native.Close();

    internal void Render(double time)
    {
        Input.BeginFrame(time);
        Native.PumpMessages();
        if (!Native.IsRunning)
        {
            return;
        }

        var viewport = Native.ClientSizeDip;
        if (viewport.Width <= 1 || viewport.Height <= 1)
        {
            return;
        }

        var frameInput = Input.Build(viewport);
        _ui.BeginFrame(frameInput);
        try
        {
            _build(_ui);
        }
        catch
        {
            _ui.AbortFrame();
            throw;
        }

        var frame = _ui.EndFrame();
        Native.SetAccessibility(frame.Accessibility);
        var accessibleFocus = frame.Accessibility.Focused?.Id ?? Id.None;
        if (accessibleFocus != _lastAccessibleFocus)
        {
            _lastAccessibleFocus = accessibleFocus;
            Native.NotifyFocusChanged(accessibleFocus);
        }
        if (!ReferenceEquals(_ui.Theme, _theme))
        {
            _theme = _ui.Theme;
            Native.SetTheme(_theme.IsDark, _theme.Background, _theme.Text);
        }

        var surface = Native.Surface;
        if (surface.IsEmpty)
        {
            return;
        }

        if (surface.WidthPixels != _surfaceWidth || surface.HeightPixels != _surfaceHeight)
        {
            _surfaceWidth = surface.WidthPixels;
            _surfaceHeight = surface.HeightPixels;
            _damage.Reset();
        }

        var damage = _damage.Update(frame.DrawList, viewport.Width, viewport.Height);
        if (damage.Count > 0)
        {
            _renderer.RenderToPixels(
                frame.DrawList,
                surface.Bits,
                surface.WidthPixels,
                surface.HeightPixels,
                surface.RowBytes,
                Native.Scale,
                _ui.Theme.Background,
                damage);
            Native.Present();
        }

        Native.SetCursor(frame.Cursor);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Native.Dispose();
        _renderer.Dispose();
        _textEngine.Dispose();
    }
}

/// <summary>
/// Runs any number of Win32 windows on one UI thread. Each window owns an
/// independent <see cref="Ui"/> and renderer.
/// </summary>
public sealed class MultiWindowApp : IDisposable
{
    private readonly List<MultiWindow> _windows = new();
    private readonly List<MultiWindow> _pending = new();
    private readonly Win32Clipboard _clipboard = new();
    private readonly double _targetFrame;
    private bool _disposed;
    private bool _running;

    public MultiWindowApp(Theme? theme = null, int targetFps = 60)
    {
        Theme = theme ?? Theme.Dark();
        _targetFrame = 1.0 / Math.Clamp(targetFps, 15, 240);
    }

    public Theme Theme { get; }
    public IReadOnlyList<MultiWindow> Windows => _windows;

    public void Stop() => _running = false;

    public MultiWindow CreateWindow(WindowOptions options, Action<Ui> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var window = new MultiWindow(this, options, build, _clipboard);
        if (_running)
        {
            _pending.Add(window);
        }
        else
        {
            _windows.Add(window);
        }

        return window;
    }

    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_windows.Count == 0)
        {
            return;
        }

        _running = true;
        var clock = Stopwatch.StartNew();
        while (_running)
        {
            FlushPending();
            if (_windows.Count == 0)
            {
                break;
            }

            var frameStart = clock.Elapsed.TotalSeconds;
            foreach (var window in _windows.ToArray())
            {
                if (!window.IsOpen)
                {
                    continue;
                }

                window.Render(frameStart);
            }

            _windows.RemoveAll(w => !w.IsOpen);
            var elapsed = clock.Elapsed.TotalSeconds - frameStart;
            if (_targetFrame - elapsed > 0.001)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(_targetFrame - elapsed, 0.010)));
            }
        }

        _running = false;
    }

    private void FlushPending()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        foreach (var window in _pending)
        {
            _windows.Add(window);
        }

        _pending.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _running = false;
        foreach (var window in _windows.Concat(_pending))
        {
            window.Dispose();
        }

        _windows.Clear();
        _pending.Clear();
    }
}






