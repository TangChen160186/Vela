using SkiaSharp;

namespace Vela.Rendering.Skia;

/// <summary>
/// 用 Skia CPU 后端消费 DrawList。
///
/// 本类完全不知道控件/布局/主题，只把 DrawCommand 翻译成 SKCanvas 调用。
/// 所有绘制方法都应保持 Push/Pop 平衡；RenderToCanvas 可以被 CPU surface
/// 和 GPU surface 共用。
/// </summary>
public sealed class SkiaRenderer : IDisposable
{
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _imagePaint = new() { IsAntialias = true };
    private readonly SKPaint _clearPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private bool _disposed;

    public SkiaRenderer(SkiaTextEngine textEngine)
    {
        TextEngine = textEngine ?? throw new ArgumentNullException(nameof(textEngine));
    }

    public SkiaTextEngine TextEngine { get; }

    /// <summary>
    /// 直接渲染到调用方拥有的 BGRA 像素缓冲（Win32 DIB / SDL texture）。
    /// damage 为 null 表示整帧；为空数组表示无需绘制；否则只在这些矩形内
    /// 清屏并重放命令，缓冲其余像素保持不变。
    /// </summary>
    public void RenderToPixels(
        DrawList drawList,
        IntPtr pixels,
        int pixelWidth,
        int pixelHeight,
        int rowBytes,
        float scale,
        Color clearColor,
        IReadOnlyList<Rect>? damage = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(drawList);
        if (pixels == IntPtr.Zero || pixelWidth <= 0 || pixelHeight <= 0 || damage is { Count: 0 })
        {
            return;
        }

        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info, pixels, rowBytes);
        if (surface is null)
        {
            throw new InvalidOperationException("Skia could not create a surface over the supplied pixel buffer.");
        }

        RenderSurface(surface, drawList, scale, clearColor, damage);
    }

    /// <summary>渲染到新的 CPU bitmap；调用方负责 Dispose 返回的 bitmap。</summary>
    public SKBitmap RenderBitmap(
        DrawList drawList,
        int pixelWidth,
        int pixelHeight,
        float scale,
        Color clearColor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(drawList);
        var info = new SKImageInfo(Math.Max(1, pixelWidth), Math.Max(1, pixelHeight), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null)
        {
            throw new InvalidOperationException("Skia could not create a CPU surface.");
        }

        RenderSurface(surface, drawList, scale, clearColor, null);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    public void SavePng(
        DrawList drawList,
        string path,
        int pixelWidth,
        int pixelHeight,
        float scale,
        Color clearColor)
    {
        using var bitmap = RenderBitmap(drawList, pixelWidth, pixelHeight, scale, clearColor);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private void RenderSurface(
        SKSurface surface,
        DrawList drawList,
        float scale,
        Color clearColor,
        IReadOnlyList<Rect>? damage) =>
        RenderToCanvas(surface.Canvas, drawList, scale, clearColor, damage);

    /// <summary>
    /// 渲染到已有 Skia canvas。CPU 和 GPU surface 共用同一实现。
    /// damage 矩形使用逻辑像素坐标；canvas 的 scale 在这里统一应用。
    /// 每个 damage 区域都会清背景并重放整份 DrawList，因此命令数量多时
    /// 应尽量让 damage 保持较小。
    /// </summary>
    public void RenderToCanvas(
        SKCanvas canvas,
        DrawList drawList,
        float scale,
        Color clearColor,
        IReadOnlyList<Rect>? damage)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(drawList);
        var full = damage is null;
        if (full)
        {
            canvas.Clear(ToSkColor(clearColor));
        }

        canvas.Save();
        canvas.Scale(scale, scale);
        if (damage is null)
        {
            DrawCommands(canvas, drawList);
        }
        else
        {
            _clearPaint.Color = ToSkColor(clearColor);
            foreach (var rect in damage)
            {
                if (rect.IsEmpty)
                {
                    continue;
                }

                canvas.Save();
                canvas.ClipRect(ToSkRect(rect), SKClipOperation.Intersect, antialias: false);
                canvas.DrawRect(ToSkRect(rect), _clearPaint);
                DrawCommands(canvas, drawList);
                canvas.Restore();
            }
        }

        canvas.Restore();
        canvas.Flush();
    }

    private void DrawCommands(SKCanvas canvas, DrawList drawList)
    {
        foreach (var command in drawList.Commands)
        {
            switch (command.Kind)
            {
                case DrawCommandKind.FillRect:
                    DrawFillRect(canvas, command.Rect, command.Brush, command.CornerRadius);
                    break;
                case DrawCommandKind.StrokeRect:
                    DrawStrokeRect(canvas, command.Rect, command.Color, command.Width, command.CornerRadius);
                    break;
                case DrawCommandKind.BoxShadow:
                    DrawShadow(canvas, command.Rect, command.Shadow, command.CornerRadius);
                    break;
                case DrawCommandKind.Text:
                    TextEngine.DrawText(canvas, command.Rect, command.Text ?? string.Empty, command.Format, command.Color);
                    break;
                case DrawCommandKind.Line:
                    DrawLine(canvas, command.P0, command.P1, command.Color, command.Width);
                    break;
                case DrawCommandKind.FillPath:
                    DrawPath(canvas, command.Path, command.Brush, null, 0, LineCap.Round, LineJoin.Round);
                    break;
                case DrawCommandKind.StrokePath:
                    DrawPath(canvas, command.Path, default, command.Color, command.Width, command.LineCap, command.LineJoin);
                    break;
                case DrawCommandKind.Image:
                    DrawImage(canvas, command);
                    break;
                case DrawCommandKind.PushClip:
                    canvas.Save();
                    canvas.ClipRect(ToSkRect(command.Rect), SKClipOperation.Intersect, antialias: true);
                    break;
                case DrawCommandKind.PopClip:
                case DrawCommandKind.PopTransform:
                case DrawCommandKind.PopOpacity:
                    canvas.Restore();
                    break;
                case DrawCommandKind.PushTransform:
                    canvas.Save();
                    canvas.Concat(ToSkMatrix(command.Transform));
                    break;
                case DrawCommandKind.PushOpacity:
                    {
                        using var layerPaint = new SKPaint
                        {
                            Color = new SKColor(255, 255, 255, (byte)Math.Clamp((int)MathF.Round(command.Opacity * 255f), 0, 255)),
                        };
                        canvas.SaveLayer(layerPaint);
                        break;
                    }
            }
        }
    }

    private void DrawFillRect(SKCanvas canvas, Rect rect, Brush brush, CornerRadius radius)
    {
        if (brush.IsEmpty)
        {
            return;
        }

        var oldShader = _fillPaint.Shader;
        _fillPaint.Shader = null;
        oldShader?.Dispose();
        var oldMask = _fillPaint.MaskFilter;
        _fillPaint.MaskFilter = null;
        oldMask?.Dispose();

        SKShader? shader = null;
        if (brush.Gradient is { } gradient)
        {
            // Paint alpha modulates a shader in Skia. Reset it so a gradient that
            // follows a translucent shadow is not unexpectedly faded.
            _fillPaint.Color = SKColors.White;
            shader = CreateShader(gradient, rect);
            _fillPaint.Shader = shader;
        }
        else
        {
            _fillPaint.Color = ToSkColor(brush.SolidColor ?? Color.Transparent);
        }

        if (radius.IsZero)
        {
            canvas.DrawRect(ToSkRect(rect), _fillPaint);
        }
        else
        {
            using var roundRect = CreateRoundRect(rect, radius);
            canvas.DrawRoundRect(roundRect, _fillPaint);
        }

        _fillPaint.Shader = null;
        shader?.Dispose();
    }

    private void DrawStrokeRect(SKCanvas canvas, Rect rect, Color color, float width, CornerRadius radius)
    {
        _strokePaint.Color = ToSkColor(color);
        _strokePaint.StrokeWidth = width;
        _strokePaint.Style = SKPaintStyle.Stroke;
        if (radius.IsZero)
        {
            canvas.DrawRect(ToSkRect(rect), _strokePaint);
        }
        else
        {
            using var roundRect = CreateRoundRect(rect, radius);
            canvas.DrawRoundRect(roundRect, _strokePaint);
        }
    }

    private void DrawShadow(SKCanvas canvas, Rect rect, BoxShadow shadow, CornerRadius radius)
    {
        var sigma = MathF.Max(0.1f, shadow.Blur * 0.5f);
        using var mask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
        _fillPaint.Shader?.Dispose();
        _fillPaint.Shader = null;
        _fillPaint.MaskFilter = mask;
        _fillPaint.Color = ToSkColor(shadow.Color);

        var spread = shadow.Spread;
        var shadowRect = new Rect(
            rect.X + shadow.Offset.X - spread,
            rect.Y + shadow.Offset.Y - spread,
            rect.Width + (spread * 2),
            rect.Height + (spread * 2));

        if (radius.IsZero && spread <= 0)
        {
            canvas.DrawRect(ToSkRect(shadowRect), _fillPaint);
        }
        else
        {
            var expandedRadius = new CornerRadius(
                MathF.Max(0, radius.TopLeft + spread),
                MathF.Max(0, radius.TopRight + spread),
                MathF.Max(0, radius.BottomRight + spread),
                MathF.Max(0, radius.BottomLeft + spread));
            using var roundRect = CreateRoundRect(shadowRect, expandedRadius);
            canvas.DrawRoundRect(roundRect, _fillPaint);
        }

        _fillPaint.MaskFilter = null;
    }

    private void DrawLine(SKCanvas canvas, Vec2 from, Vec2 to, Color color, float width)
    {
        _strokePaint.Color = ToSkColor(color);
        _strokePaint.StrokeWidth = width;
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        _strokePaint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(from.X, from.Y, to.X, to.Y, _strokePaint);
        _strokePaint.StrokeCap = SKStrokeCap.Butt;
    }

    private void DrawPath(
        SKCanvas canvas,
        PathData? path,
        Brush fill,
        Color? strokeColor,
        float strokeWidth,
        LineCap cap,
        LineJoin join)
    {
        if (path is null || path.IsEmpty)
        {
            return;
        }

        using var skPath = ToSkPath(path);
        if (strokeColor is { } color)
        {
            _strokePaint.Color = ToSkColor(color);
            _strokePaint.StrokeWidth = strokeWidth;
            _strokePaint.Style = SKPaintStyle.Stroke;
            _strokePaint.StrokeCap = cap switch
            {
                LineCap.Round => SKStrokeCap.Round,
                LineCap.Square => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt,
            };
            _strokePaint.StrokeJoin = join switch
            {
                LineJoin.Round => SKStrokeJoin.Round,
                LineJoin.Bevel => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter,
            };
            canvas.DrawPath(skPath, _strokePaint);
        }
        else
        {
            var previousMask = _fillPaint.MaskFilter;
            _fillPaint.MaskFilter = null;
            previousMask?.Dispose();
            var previousShader = _fillPaint.Shader;
            _fillPaint.Shader = null;
            previousShader?.Dispose();
            if (fill.Gradient is { } gradient)
            {
                _fillPaint.Color = SKColors.White;
                using var shader = CreateShader(gradient, path.Bounds);
                _fillPaint.Shader = shader;
                canvas.DrawPath(skPath, _fillPaint);
                _fillPaint.Shader = null;
            }
            else
            {
                _fillPaint.Color = ToSkColor(fill.SolidColor ?? Color.Transparent);
                canvas.DrawPath(skPath, _fillPaint);
            }
        }
    }

    private void DrawImage(SKCanvas canvas, in DrawCommand command)
    {
        if (command.Texture is not SkiaTexture texture)
        {
            return;
        }

        var source = new SKRect(command.SourceRect.Left, command.SourceRect.Top, command.SourceRect.Right, command.SourceRect.Bottom);
        var destination = ToSkRect(command.Rect);
        _imagePaint.Color = ToSkColor(command.Color);

        var clip = !command.CornerRadius.IsZero;
        if (clip)
        {
            canvas.Save();
            using var path = new SKPath();
            using var roundRect = CreateRoundRect(command.Rect, command.CornerRadius);
            path.AddRoundRect(roundRect, SKPathDirection.Clockwise);
            canvas.ClipPath(path, SKClipOperation.Intersect, antialias: true);
        }

        canvas.DrawImage(texture.Image, source, destination, _imagePaint);

        if (clip)
        {
            canvas.Restore();
        }
    }

    private static SKShader CreateShader(Gradient gradient, Rect bounds)
    {
        var colors = new SKColor[gradient.Stops.Count];
        var positions = new float[gradient.Stops.Count];
        for (var i = 0; i < gradient.Stops.Count; i++)
        {
            var stop = gradient.Stops[i];
            colors[i] = ToSkColor(stop.Color);
            positions[i] = Math.Clamp(stop.Offset, 0, 1);
        }

        return gradient switch
        {
            LinearGradient linear => SKShader.CreateLinearGradient(
                new SKPoint(bounds.X + (linear.Start.X * bounds.Width), bounds.Y + (linear.Start.Y * bounds.Height)),
                new SKPoint(bounds.X + (linear.End.X * bounds.Width), bounds.Y + (linear.End.Y * bounds.Height)),
                colors,
                positions,
                SKShaderTileMode.Clamp),
            RadialGradient radial => SKShader.CreateRadialGradient(
                new SKPoint(bounds.X + (radial.Center.X * bounds.Width), bounds.Y + (radial.Center.Y * bounds.Height)),
                MathF.Max(0.01f, radial.Radius * MathF.Max(bounds.Width, bounds.Height)),
                colors,
                positions,
                SKShaderTileMode.Clamp),
            _ => SKShader.CreateColor(SKColors.Transparent),
        };
    }

    private static SKPath ToSkPath(PathData data)
    {
        var path = new SKPath();
        var points = data.Points.Span;
        foreach (var command in data.Commands.Span)
        {
            switch (command.Verb)
            {
                case PathVerb.MoveTo:
                    path.MoveTo(ToSkPoint(points[command.PointStart]));
                    break;
                case PathVerb.LineTo:
                    path.LineTo(ToSkPoint(points[command.PointStart]));
                    break;
                case PathVerb.QuadraticTo:
                    path.QuadTo(ToSkPoint(points[command.PointStart]), ToSkPoint(points[command.PointStart + 1]));
                    break;
                case PathVerb.CubicTo:
                    path.CubicTo(ToSkPoint(points[command.PointStart]), ToSkPoint(points[command.PointStart + 1]), ToSkPoint(points[command.PointStart + 2]));
                    break;
                case PathVerb.Close:
                    path.Close();
                    break;
            }
        }

        return path;
    }

    private static SKRoundRect CreateRoundRect(Rect rect, CornerRadius radius)
    {
        var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(
            ToSkRect(rect),
            [
                new SKPoint(radius.TopLeft, radius.TopLeft),
                new SKPoint(radius.TopRight, radius.TopRight),
                new SKPoint(radius.BottomRight, radius.BottomRight),
                new SKPoint(radius.BottomLeft, radius.BottomLeft),
            ]);
        return roundRect;
    }

    private static SKMatrix ToSkMatrix(Matrix2D matrix) => new()
    {
        ScaleX = matrix.M11,
        SkewX = matrix.M21,
        TransX = matrix.M31,
        SkewY = matrix.M12,
        ScaleY = matrix.M22,
        TransY = matrix.M32,
        Persp0 = 0,
        Persp1 = 0,
        Persp2 = 1,
    };

    private static SKPoint ToSkPoint(Vec2 point) => new(point.X, point.Y);
    private static SKRect ToSkRect(Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    private static SKColor ToSkColor(Color color) => new(color.R, color.G, color.B, color.A);

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
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _imagePaint.Dispose();
        TextEngine.Dispose();
    }
}


