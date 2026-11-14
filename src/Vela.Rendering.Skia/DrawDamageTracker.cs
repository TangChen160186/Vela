namespace Vela.Rendering.Skia;

/// <summary>
/// 比较相邻两帧 DrawList，为“保留像素”的 CPU 呈现计算安全重绘区域。
///
/// 规则：
/// <list type="bullet">
/// <item>首帧/尺寸变化 → 全屏；</item>
/// <item>命令变化 → 新旧命令包围盒的并集；</item>
/// <item>无变化 → 空数组（宿主可跳过 Present）；</item>
/// <item>变换/透明度变化保守地全屏；</item>
/// <item>结果合并为一个矩形，优先安全和简单，不是多矩形最优。</item>
/// </list>
/// 本类不接触 Skia，只做命令比较和几何合并。
/// </summary>
public sealed class DrawDamageTracker
{
    private DrawCommand[] _previous = Array.Empty<DrawCommand>();
    private float _width;
    private float _height;

    public bool HasPreviousFrame => _previous.Length > 0;

    public void Reset()
    {
        _previous = Array.Empty<DrawCommand>();
        _width = 0;
        _height = 0;
    }

    /// <summary>
    /// 计算本帧 damage，并保存当前命令作为下一帧基线。
    /// 返回空数组表示“不需要重绘”；返回 viewport 表示全屏。
    /// </summary>
    public IReadOnlyList<Rect> Update(DrawList drawList, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(drawList);
        var viewport = new Rect(0, 0, width, height);
        if (_previous.Length == 0 || MathF.Abs(width - _width) > 0.01f || MathF.Abs(height - _height) > 0.01f)
        {
            _previous = drawList.Commands.ToArray();
            _width = width;
            _height = height;
            return new[] { viewport };
        }

        var current = drawList.Commands;
        var damage = Rect.Empty;
        var changed = false;
        var count = Math.Max(current.Length, _previous.Length);
        for (var i = 0; i < count; i++)
        {
            var hasCurrent = i < current.Length;
            var hasPrevious = i < _previous.Length;
            if (hasCurrent && hasPrevious && Equivalent(current[i], _previous[i]))
            {
                continue;
            }

            changed = true;
            if (hasCurrent)
            {
                Accumulate(ref damage, BoundsOf(current[i], viewport));
            }

            if (hasPrevious)
            {
                Accumulate(ref damage, BoundsOf(_previous[i], viewport));
            }
        }

        _previous = current.ToArray();
        _width = width;
        _height = height;
        if (!changed || damage.IsEmpty)
        {
            return Array.Empty<Rect>();
        }

        var clamped = damage.Intersect(viewport);
        return clamped.IsEmpty ? Array.Empty<Rect>() : new[] { clamped };
    }

    private static void Accumulate(ref Rect union, Rect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        if (union.IsEmpty)
        {
            union = rect;
            return;
        }

        union = union.Union(rect);
    }

    private static Rect BoundsOf(in DrawCommand command, Rect viewport)
    {
        var bounds = command.Kind switch
        {
            DrawCommandKind.FillRect => command.Rect,
            DrawCommandKind.StrokeRect => command.Rect.Inflate(command.Width),
            DrawCommandKind.BoxShadow => command.Rect
                .Inflate(command.Shadow.Blur + command.Shadow.Spread + 2)
                .Offset(command.Shadow.Offset),
            DrawCommandKind.Text => command.Rect.Inflate(2),
            DrawCommandKind.Image => command.Rect,
            DrawCommandKind.Line => new Rect(
                MathF.Min(command.P0.X, command.P1.X) - command.Width,
                MathF.Min(command.P0.Y, command.P1.Y) - command.Width,
                MathF.Abs(command.P1.X - command.P0.X) + command.Width * 2,
                MathF.Abs(command.P1.Y - command.P0.Y) + command.Width * 2),
            DrawCommandKind.FillPath or DrawCommandKind.StrokePath => command.Path?.Bounds.Inflate(command.Width + 1) ?? viewport,
            DrawCommandKind.PushClip => command.Rect,
            DrawCommandKind.PushTransform => viewport,
            DrawCommandKind.PushOpacity => viewport,
            _ => Rect.Empty,
        };
        return bounds.Intersect(viewport);
    }

    private static bool Equivalent(in DrawCommand a, in DrawCommand b)
    {
        return a.Kind == b.Kind &&
               a.Rect.Equals(b.Rect) &&
               a.SourceRect.Equals(b.SourceRect) &&
               a.Color.Equals(b.Color) &&
               a.CornerRadius.Equals(b.CornerRadius) &&
               a.Width.Equals(b.Width) &&
               a.Opacity.Equals(b.Opacity) &&
               a.P0.Equals(b.P0) &&
               a.P1.Equals(b.P1) &&
               string.Equals(a.Text, b.Text, StringComparison.Ordinal) &&
               a.Format.Equals(b.Format) &&
               ReferenceEquals(a.Path, b.Path) &&
               ReferenceEquals(a.Texture, b.Texture) &&
               a.Shadow.Equals(b.Shadow) &&
               a.Transform.Equals(b.Transform) &&
               a.LineCap == b.LineCap &&
               a.LineJoin == b.LineJoin &&
               BrushEquals(a.Brush, b.Brush);
    }

    private static bool BrushEquals(Brush a, Brush b)
    {
        if (a.Gradient is null && b.Gradient is null)
        {
            return a.SolidColor.Equals(b.SolidColor);
        }

        return ReferenceEquals(a.Gradient, b.Gradient);
    }
}

