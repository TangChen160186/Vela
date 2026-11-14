using System.Diagnostics;
using Vela.Platform.Sdl;
using Vela.Rendering.Skia;

namespace Vela.Desktop.Portable;

/// <summary>SDL3 便携宿主配置。尺寸为逻辑像素；UseGpu 需要 OpenGL 3.3 Core。</summary>
public sealed record PortableAppOptions
{
    public string Title { get; init; } = "Vela";
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 800;
    public Theme Theme { get; init; } = Theme.Dark();
    public int TargetFps { get; init; } = 60;
    public bool CaptureDiagnostics { get; init; }

    /// <summary>false=CPU Skia + 流式纹理；true=Skia OpenGL 默认 framebuffer。</summary>
    public bool UseGpu { get; init; }

    /// <summary>渲染固定帧数后退出；0 表示一直运行。</summary>
    public int MaxFrames { get; init; }
}

/// <summary>
/// 跨平台桌面循环（Windows/macOS/Linux）：SDL3 负责窗口/输入/IME/拖放，
/// Skia 负责绘制。CPU 模式使用流式纹理；GPU 模式使用 OpenGL 默认 framebuffer。
///
/// Win32 宿主额外提供原生文件对话框、多窗口和 UIA；这里刻意不依赖这些
/// Windows 专属能力，保证 Core/Portable 可以跨平台运行。
/// </summary>
public sealed class PortableApp : IDisposable
{
    private readonly PortableAppOptions _options;
    private readonly InputBuilder _input = new();
    private readonly SdlClipboard _clipboard = new();
    private readonly SkiaTextEngine _textEngine;
    private readonly SkiaRenderer _renderer;
    private SkiaGpuRenderer? _gpu;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private readonly Ui _ui;
    private readonly DrawDamageTracker _damage = new();
    private bool _disposed;

    public PortableApp(PortableAppOptions? options = null)
    {
        _options = options ?? new PortableAppOptions();
        _textEngine = new SkiaTextEngine();
        _renderer = new SkiaRenderer(_textEngine);
        _ui = new Ui(new UiOptions
        {
            TextEngine = _textEngine,
            Theme = _options.Theme,
            Clipboard = _clipboard,
            CaptureDiagnostics = _options.CaptureDiagnostics,
        });
    }

    public Ui Ui => _ui;
    public Sdl3Window? Window { get; private set; }

    /// <summary>
    /// 启动便携主循环。与 DesktopApp 相同的帧顺序，只是窗口/呈现换成 SDL3。
    /// GPU 模式要求当前线程已有 SDL OpenGL context；Sdl3Window 构造时已完成。
    /// </summary>
    public void Run(Action<Ui> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var window = new Sdl3Window(_options.Title, _options.Width, _options.Height, _input, _options.UseGpu);
        Window = window;
        if (_options.UseGpu)
        {
            _gpu = new SkiaGpuRenderer(_textEngine);
        }
        var clock = Stopwatch.StartNew();
        var targetFrame = 1.0 / Math.Clamp(_options.TargetFps, 15, 240);
        var rendered = 0;

        while (window.IsRunning)
        {
            var frameStart = clock.Elapsed.TotalSeconds;
            _input.BeginFrame(frameStart);
            window.PumpEvents();
            if (!window.IsRunning)
            {
                break;
            }

            var viewport = window.ClientSizeDip;
            if (viewport.Width <= 1 || viewport.Height <= 1 || (!window.IsGpu && window.Pixels == IntPtr.Zero))
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

            var frame = _ui.EndFrame();
            if (window.PixelWidth != _surfaceWidth || window.PixelHeight != _surfaceHeight)
            {
                _surfaceWidth = window.PixelWidth;
                _surfaceHeight = window.PixelHeight;
                _damage.Reset();
                _gpu?.Reset();
            }

            var damage = _damage.Update(frame.DrawList, viewport.Width, viewport.Height);
            if (_gpu is not null)
            {
                // 默认 framebuffer 在 Swap 后不保证保留，所以 GPU 现在整帧绘制。
                // damage 仍用于 CPU 路径；未来若引入持久 FBO，可把 damage 传进来。
                _gpu.Render(frame.DrawList, window.PixelWidth, window.PixelHeight, window.Scale, _ui.Theme.Background, null);
                window.Present();
            }
            else if (damage.Count > 0)
            {
                _renderer.RenderToPixels(
                    frame.DrawList,
                    window.Pixels,
                    window.PixelWidth,
                    window.PixelHeight,
                    window.RowBytes,
                    window.Scale,
                    _ui.Theme.Background,
                    damage);
                window.Present();
            }

            window.SetCursor(frame.Cursor);

            var elapsed = clock.Elapsed.TotalSeconds - frameStart;
            if (targetFrame - elapsed > 0.001)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Min(targetFrame - elapsed, 0.010)));
            }

            rendered++;
            if (_options.MaxFrames > 0 && rendered >= _options.MaxFrames)
            {
                break;
            }
        }

        Window = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gpu?.Dispose();
        _renderer.Dispose();
        _textEngine.Dispose();
    }
}







