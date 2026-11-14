using System.Diagnostics;
using Vela.Platform.Win32;
using Vela.Rendering.Skia;

namespace Vela.Desktop;

/// <summary>
/// Win32 桌面应用的启动配置。尺寸使用逻辑像素（DIP），窗口创建时按 DPI 换算。
/// </summary>
public sealed record DesktopAppOptions
{
    public string Title { get; init; } = "Vela";
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 800;
    public Theme Theme { get; init; } = Theme.Dark();

    /// <summary>帧率上限；内部会夹到 15–240。</summary>
    public int TargetFps { get; init; } = 60;
    public bool CaptureDiagnostics { get; init; } = true;

    /// <summary>渲染多少帧后停止；0 表示一直运行到窗口关闭。主要供冒烟测试使用。</summary>
    public int MaxFrames { get; init; }
}

/// <summary>
/// 开箱即用的 Windows 桌面循环：
/// Win32 窗口 + 输入/IME/拖放 + Skia CPU + 脏区域 + 可访问性 + 帧率控制。
///
/// 一个实例持有一个 Ui/Renderer/Window，适合单窗口应用；
/// 多窗口请使用 <see cref="MultiWindowApp"/>。
/// </summary>
public sealed class DesktopApp : IDisposable
{
    private readonly DesktopAppOptions _options;
    private readonly InputBuilder _input = new();
    private readonly Win32Clipboard _clipboard = new();
    private readonly SkiaTextEngine _textEngine;
    private readonly SkiaRenderer _renderer;
    private readonly Ui _ui;
    private readonly DrawDamageTracker _damage = new();
    private Theme _windowTheme;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private Id _lastAccessibleFocus;
    private bool _disposed;

    public DesktopApp(DesktopAppOptions? options = null)
    {
        _options = options ?? new DesktopAppOptions();
        _textEngine = new SkiaTextEngine();
        _renderer = new SkiaRenderer(_textEngine);
        _ui = new Ui(new UiOptions
        {
            TextEngine = _textEngine,
            Theme = _options.Theme,
            Clipboard = _clipboard,
            CaptureDiagnostics = _options.CaptureDiagnostics,
        });
        _windowTheme = _options.Theme;
    }

    public Ui Ui => _ui;
    public Win32Window? Window { get; private set; }

    /// <summary>
    /// 启动主循环。build 每帧调用一次，负责声明整棵 UI。
    ///
    /// 每帧顺序：
    /// BeginFrame → PumpMessages → Build input → Ui.BeginFrame → build
    /// → Ui.EndFrame → damage/render → Present → sleep。
    ///
    /// 异常路径会 AbortFrame 后继续向上抛，避免下一帧复用半成品。
    /// </summary>
    public void Run(Action<Ui> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        ThrowIfDisposed();

        using var window = new Win32Window(_options.Title, _options.Width, _options.Height, _input);
        Window = window;
        window.SetTheme(_options.Theme.IsDark, _options.Theme.Background, _options.Theme.Text);
        window.FocusRequested = id => _ui.Focus(id, keyboardVisible: true);
        window.InvokeRequested = id => _ui.Invoke(id);
        var clock = Stopwatch.StartNew();
        var targetFrame = 1.0 / Math.Clamp(_options.TargetFps, 15, 240);
        var renderedFrames = 0;

        while (window.IsRunning)
        {
            // 时间驱动输入帧；PumpMessages 会把平台事件写入同一个 InputBuilder。
            var frameStart = clock.Elapsed.TotalSeconds;
            _input.BeginFrame(frameStart);
            window.PumpMessages();
            if (!window.IsRunning)
            {
                break;
            }

            var viewport = window.ClientSizeDip;
            if (viewport.Width <= 1 || viewport.Height <= 1)
            {
                Thread.Sleep(10);
                continue;
            }

            var frameInput = _input.Build(viewport);
            _ui.BeginFrame(frameInput);
            try
            {
                build(_ui);
            }
            catch
            {
                _ui.AbortFrame();
                throw;
            }

            // EndFrame 已完成布局/快照/绘制；接下来只做呈现和平台同步。
            var frame = _ui.EndFrame();
            window.SetAccessibility(frame.Accessibility);
            var accessibleFocus = frame.Accessibility.Focused?.Id ?? Id.None;
            if (accessibleFocus != _lastAccessibleFocus)
            {
                _lastAccessibleFocus = accessibleFocus;
                window.NotifyFocusChanged(accessibleFocus);
            }
            if (!ReferenceEquals(_ui.Theme, _windowTheme))
            {
                _windowTheme = _ui.Theme;
                window.SetTheme(_windowTheme.IsDark, _windowTheme.Background, _windowTheme.Text);
            }

            var surface = window.Surface;
            if (!surface.IsEmpty)
            {
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
                        window.Scale,
                        _ui.Theme.Background,
                        damage);
                    window.Present();
                }

                window.SetCursor(frame.Cursor);
            }

            var elapsed = clock.Elapsed.TotalSeconds - frameStart;
            var remaining = targetFrame - elapsed;
            if (remaining > 0.001)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(remaining, 0.010)));
            }

            renderedFrames++;
            if (_options.MaxFrames > 0 && renderedFrames >= _options.MaxFrames)
            {
                break;
            }
        }

        Window = null;
    }

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
        _renderer.Dispose();
    }
}



