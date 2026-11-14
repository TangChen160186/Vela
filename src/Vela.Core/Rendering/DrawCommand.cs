namespace Vela;

/// <summary>
/// 后端需要理解的绘制命令种类。
/// Push/Pop 三类状态（clip/transform/opacity）必须严格配对，
/// 否则后续命令会继承错误状态；Render 阶段通常用 Save/Restore 实现。
/// </summary>
public enum DrawCommandKind
{
    FillRect,
    StrokeRect,
    BoxShadow,
    Text,
    Line,
    FillPath,
    StrokePath,
    Image,
    PushClip,
    PopClip,
    PushTransform,
    PopTransform,
    PushOpacity,
    PopOpacity,
}

public enum LineCap
{
    Butt,
    Round,
    Square,
}

public enum LineJoin
{
    Miter,
    Round,
    Bevel,
}

/// <summary>
/// 一条扁平的绘制指令。
///
/// 设计：DrawList 每帧重建，命令只携带值类型和不可变 payload 引用
/// （字符串、PathData、ITexture）。后端不需要知道控件、布局或主题。
/// 字段按 Kind 复用，不相关字段保持 default，减少分配。
/// </summary>
public readonly struct DrawCommand
{
    internal DrawCommand(DrawCommandKind kind)
    {
        Kind = kind;
        Brush = default;
        Color = default;
        Text = null;
        Path = null;
        Texture = null;
        Format = default;
    }

    public DrawCommandKind Kind { get; init; }
    public Rect Rect { get; init; }
    public Rect SourceRect { get; init; }
    public Brush Brush { get; init; }
    public Color Color { get; init; }
    public CornerRadius CornerRadius { get; init; }
    public float Width { get; init; }
    public float Opacity { get; init; }
    public Vec2 P0 { get; init; }
    public Vec2 P1 { get; init; }
    public string? Text { get; init; }
    public TextFormat Format { get; init; }
    public PathData? Path { get; init; }
    public ITexture? Texture { get; init; }
    public BoxShadow Shadow { get; init; }
    public Matrix2D Transform { get; init; }
    public LineCap LineCap { get; init; }
    public LineJoin LineJoin { get; init; }
}

/// <summary>
/// 一帧的有序显示列表，是 Core 与渲染后端之间的唯一契约。
///
/// 使用约束：
/// <list type="bullet">
/// <item>命令顺序就是绘制顺序；</item>
/// <item>每个 PushClip/PushTransform/PushOpacity 必须有对应 Pop；</item>
/// <item>后端实现应支持裁剪、变换、透明度、文本、路径、图片；</item>
/// <item>DrawList 不拥有控件生命周期，只拥有命令 payload 引用。</item>
/// </list>
/// </summary>
public sealed class DrawList
{
    private readonly List<DrawCommand> _commands = new(1024);
    private int _clipDepth;
    private int _transformDepth;
    private int _opacityDepth;

    public int Count => _commands.Count;
    public int ClipDepth => _clipDepth;
    public int TransformDepth => _transformDepth;
    public int OpacityDepth => _opacityDepth;

    public ReadOnlySpan<DrawCommand> Commands => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_commands);

    public void Clear()
    {
        _commands.Clear();
        _clipDepth = 0;
        _transformDepth = 0;
        _opacityDepth = 0;
    }

    public void FillRect(Rect rect, Brush brush, CornerRadius radius = default)
    {
        if (rect.IsEmpty || brush.IsEmpty)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.FillRect)
        {
            Rect = rect,
            Brush = brush,
            CornerRadius = radius,
        });
    }

    public void StrokeRect(Rect rect, Color color, float width = 1, CornerRadius radius = default)
    {
        if (rect.IsEmpty || width <= 0 || color.A == 0)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.StrokeRect)
        {
            Rect = rect,
            Color = color,
            Width = width,
            CornerRadius = radius,
        });
    }

    public void Shadow(Rect rect, BoxShadow shadow, CornerRadius radius = default)
    {
        if (rect.IsEmpty || shadow.Color.A == 0 || (shadow.Blur <= 0 && shadow.Spread <= 0))
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.BoxShadow)
        {
            Rect = rect,
            Shadow = shadow,
            CornerRadius = radius,
        });
    }

    public void Text(Rect rect, string text, TextFormat format, Color color)
    {
        if (rect.IsEmpty || string.IsNullOrEmpty(text) || color.A == 0)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.Text)
        {
            Rect = rect,
            Text = text,
            Format = format,
            Color = color,
        });
    }

    public void Line(Vec2 from, Vec2 to, Color color, float width = 1)
    {
        if (width <= 0 || color.A == 0 || from == to)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.Line)
        {
            P0 = from,
            P1 = to,
            Color = color,
            Width = width,
        });
    }

    public void FillPath(PathData path, Brush brush)
    {
        if (path.IsEmpty || brush.IsEmpty)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.FillPath)
        {
            Path = path,
            Brush = brush,
        });
    }

    public void StrokePath(PathData path, Color color, float width = 1, LineCap cap = LineCap.Round, LineJoin join = LineJoin.Round)
    {
        if (path.IsEmpty || width <= 0 || color.A == 0)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.StrokePath)
        {
            Path = path,
            Color = color,
            Width = width,
            LineCap = cap,
            LineJoin = join,
        });
    }

    public void Image(ITexture texture, Rect destination, Rect? source = null, Color? tint = null, CornerRadius radius = default)
    {
        if (destination.IsEmpty || texture.PixelWidth <= 0 || texture.PixelHeight <= 0)
        {
            return;
        }

        _commands.Add(new DrawCommand(DrawCommandKind.Image)
        {
            Texture = texture,
            Rect = destination,
            SourceRect = source ?? new Rect(0, 0, texture.PixelWidth, texture.PixelHeight),
            Color = tint ?? Color.White,
            CornerRadius = radius,
        });
    }

    public void FillCircle(Vec2 center, float radius, Brush brush)
    {
        if (radius <= 0)
        {
            return;
        }

        FillRect(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), brush, CornerRadius.All(radius));
    }

    public void StrokeCircle(Vec2 center, float radius, Color color, float width = 1)
    {
        if (radius <= 0)
        {
            return;
        }

        StrokeRect(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), color, width, CornerRadius.All(radius));
    }

    public void DrawIcon(IconData icon, Rect bounds, Color color)
    {
        var side = MathF.Min(bounds.Width, bounds.Height);
        if (side <= 0 || color.A == 0)
        {
            return;
        }

        var x = bounds.X + ((bounds.Width - side) * 0.5f);
        var y = bounds.Y + ((bounds.Height - side) * 0.5f);
        var scale = side / 24f;
        PushTransform(Matrix2D.Translation(x, y) * Matrix2D.Scale(scale));
        if (icon.Filled)
        {
            FillPath(icon.Path, color);
        }
        else
        {
            StrokePath(icon.Path, color, icon.StrokeWidth, LineCap.Round, LineJoin.Round);
        }

        PopTransform();
    }

    public void PushClip(Rect rect)
    {
        _commands.Add(new DrawCommand(DrawCommandKind.PushClip) { Rect = rect });
        _clipDepth++;
    }

    public void PopClip()
    {
        if (_clipDepth == 0)
        {
            throw new InvalidOperationException("PopClip without a matching PushClip.");
        }

        _commands.Add(new DrawCommand(DrawCommandKind.PopClip));
        _clipDepth--;
    }

    public void PushTransform(Matrix2D transform)
    {
        _commands.Add(new DrawCommand(DrawCommandKind.PushTransform) { Transform = transform });
        _transformDepth++;
    }

    public void PopTransform()
    {
        if (_transformDepth == 0)
        {
            throw new InvalidOperationException("PopTransform without a matching PushTransform.");
        }

        _commands.Add(new DrawCommand(DrawCommandKind.PopTransform));
        _transformDepth--;
    }

    public void PushOpacity(float opacity)
    {
        _commands.Add(new DrawCommand(DrawCommandKind.PushOpacity) { Opacity = Math.Clamp(opacity, 0, 1) });
        _opacityDepth++;
    }

    public void PopOpacity()
    {
        if (_opacityDepth == 0)
        {
            throw new InvalidOperationException("PopOpacity without a matching PushOpacity.");
        }

        _commands.Add(new DrawCommand(DrawCommandKind.PopOpacity));
        _opacityDepth--;
    }

    public void ValidateBalanced()
    {
        if (_clipDepth != 0 || _transformDepth != 0 || _opacityDepth != 0)
        {
            throw new InvalidOperationException(
                $"Unbalanced draw list (clips={_clipDepth}, transforms={_transformDepth}, opacity={_opacityDepth}). " +
                "A custom painter probably missed a Pop call.");
        }
    }
}
