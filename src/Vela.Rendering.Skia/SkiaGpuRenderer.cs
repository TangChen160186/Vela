using SkiaSharp;

namespace Vela.Rendering.Skia;

/// <summary>
/// Skia OpenGL 后端。调用方必须先让 GL context 成为 current
/// （Sdl3Window 的 OpenGL 模式会做），再调用 Render。
///
/// 当前直接渲染到默认 framebuffer 0。默认 framebuffer 在 Swap 后不保证保留，
/// 因此宿主目前每帧整帧绘制；drawList 的 damage 参数保留给未来 FBO 实现。
/// </summary>
public sealed class SkiaGpuRenderer : IDisposable
{
    private readonly SkiaRenderer _renderer;
    private GRGlInterface? _gl;
    private GRContext? _context;
    private GRBackendRenderTarget? _target;
    private SKSurface? _surface;
    private int _width;
    private int _height;
    private bool _disposed;

    public SkiaGpuRenderer(SkiaTextEngine textEngine)
    {
        _renderer = new SkiaRenderer(textEngine);
        _gl = GRGlInterface.Create();
        if (_gl is null)
        {
            throw new InvalidOperationException("Skia could not create an OpenGL interface for the current context.");
        }

        _context = GRContext.CreateGl(_gl);
        if (_context is null)
        {
            _gl.Dispose();
            _gl = null;
            throw new InvalidOperationException("Skia could not create an OpenGL GRContext.");
        }
    }

    public bool IsAvailable => _context is not null;

    /// <summary>
    /// 渲染一帧到默认 framebuffer。尺寸变化时重建 GRBackendRenderTarget/SKSurface；
    /// 同一个 DrawList 复用 <see cref="SkiaRenderer.RenderToCanvas"/>。
    /// </summary>
    public void Render(
        DrawList drawList,
        int pixelWidth,
        int pixelHeight,
        float scale,
        Color clearColor,
        IReadOnlyList<Rect>? damage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_context is null || pixelWidth <= 0 || pixelHeight <= 0)
        {
            return;
        }

        if (_surface is null || pixelWidth != _width || pixelHeight != _height)
        {
            _surface?.Dispose();
            _target?.Dispose();
            _surface = null;
            _target = null;
            var info = new GRGlFramebufferInfo(0, 0x8058); // GL_RGBA8
            _target = new GRBackendRenderTarget(pixelWidth, pixelHeight, 0, 8, info);
            _surface = SKSurface.Create(_context, _target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            if (_surface is null)
            {
                throw new InvalidOperationException("Skia could not create a GPU surface for the default framebuffer.");
            }

            _width = pixelWidth;
            _height = pixelHeight;
        }

        _renderer.RenderToCanvas(_surface.Canvas, drawList, scale, clearColor, damage);
        _surface.Canvas.Flush();
        _context.Flush();
    }

    public void Reset()
    {
        _surface?.Dispose();
        _target?.Dispose();
        _surface = null;
        _target = null;
        _width = 0;
        _height = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
        _context?.Dispose();
        _context = null;
        _gl?.Dispose();
        _gl = null;
        _renderer.Dispose();
    }
}
