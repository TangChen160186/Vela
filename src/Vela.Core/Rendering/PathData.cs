namespace Vela;

public enum PathVerb
{
    MoveTo,
    LineTo,
    QuadraticTo,
    CubicTo,
    Close,
}

public readonly record struct PathCommand(PathVerb Verb, int PointStart, int PointCount);

/// <summary>An immutable vector path made of line, quadratic, cubic and close commands.</summary>
public sealed class PathData
{
    public static PathData Empty { get; } = new(Array.Empty<PathCommand>(), Array.Empty<Vec2>(), Rect.Empty);

    public PathData(PathCommand[] commands, Vec2[] points, Rect bounds)
    {
        Commands = commands;
        Points = points;
        Bounds = bounds;
    }

    public ReadOnlyMemory<PathCommand> Commands { get; }
    public ReadOnlyMemory<Vec2> Points { get; }
    public Rect Bounds { get; }
    public bool IsEmpty => Commands.IsEmpty;

    public static PathData ParseSvg(string pathData) => SvgPathParser.Parse(pathData);
}

/// <summary>An allocation-friendly builder that can be reused between frames.</summary>
public sealed class PathBuilder
{
    private readonly List<PathCommand> _commands = new(32);
    private readonly List<Vec2> _points = new(64);
    private Vec2 _current;
    private Vec2 _start;
    private Vec2 _lastCubicControl;
    private Vec2 _lastQuadControl;
    private Vec2 _min = new(float.PositiveInfinity, float.PositiveInfinity);
    private Vec2 _max = new(float.NegativeInfinity, float.NegativeInfinity);
    private bool _hasCurrent;

    public bool IsEmpty => _commands.Count == 0;
    public Vec2 CurrentPoint => _current;

    public PathBuilder Clear()
    {
        _commands.Clear();
        _points.Clear();
        _current = Vec2.Zero;
        _start = Vec2.Zero;
        _lastCubicControl = Vec2.Zero;
        _lastQuadControl = Vec2.Zero;
        _min = new(float.PositiveInfinity, float.PositiveInfinity);
        _max = new(float.NegativeInfinity, float.NegativeInfinity);
        _hasCurrent = false;
        return this;
    }

    public PathBuilder MoveTo(float x, float y) => MoveTo(new Vec2(x, y));

    public PathBuilder MoveTo(Vec2 point)
    {
        AddPoint(point);
        _commands.Add(new PathCommand(PathVerb.MoveTo, _points.Count - 1, 1));
        _current = point;
        _start = point;
        _lastCubicControl = point;
        _lastQuadControl = point;
        _hasCurrent = true;
        return this;
    }

    public PathBuilder LineTo(float x, float y) => LineTo(new Vec2(x, y));

    public PathBuilder LineTo(Vec2 point)
    {
        EnsureSubpath(point);
        AddPoint(point);
        _commands.Add(new PathCommand(PathVerb.LineTo, _points.Count - 1, 1));
        _current = point;
        _lastCubicControl = point;
        _lastQuadControl = point;
        return this;
    }

    public PathBuilder HorizontalLineTo(float x) => LineTo(new Vec2(x, _current.Y));
    public PathBuilder VerticalLineTo(float y) => LineTo(new Vec2(_current.X, y));

    public PathBuilder QuadraticTo(Vec2 control, Vec2 point)
    {
        EnsureSubpath(control);
        AddPoint(control);
        AddPoint(point);
        _commands.Add(new PathCommand(PathVerb.QuadraticTo, _points.Count - 2, 2));
        _lastQuadControl = control;
        _current = point;
        return this;
    }

    public PathBuilder SmoothQuadraticTo(Vec2 point)
    {
        var control = (_current * 2) - _lastQuadControl;
        return QuadraticTo(control, point);
    }

    public PathBuilder CubicTo(Vec2 control1, Vec2 control2, Vec2 point)
    {
        EnsureSubpath(control1);
        AddPoint(control1);
        AddPoint(control2);
        AddPoint(point);
        _commands.Add(new PathCommand(PathVerb.CubicTo, _points.Count - 3, 3));
        _lastCubicControl = control2;
        _current = point;
        return this;
    }

    public PathBuilder SmoothCubicTo(Vec2 control2, Vec2 point)
    {
        var control1 = (_current * 2) - _lastCubicControl;
        return CubicTo(control1, control2, point);
    }

    public PathBuilder Close()
    {
        if (_commands.Count == 0)
        {
            return this;
        }

        _commands.Add(new PathCommand(PathVerb.Close, 0, 0));
        _current = _start;
        _lastCubicControl = _current;
        _lastQuadControl = _current;
        _hasCurrent = false;
        return this;
    }

    public PathBuilder AddRect(Rect rect)
    {
        MoveTo(rect.Left, rect.Top);
        LineTo(rect.Right, rect.Top);
        LineTo(rect.Right, rect.Bottom);
        LineTo(rect.Left, rect.Bottom);
        return Close();
    }

    public PathBuilder AddCircle(Vec2 center, float radius)
    {
        const float Kappa = 0.5522847498f;
        var k = radius * Kappa;
        MoveTo(center.X + radius, center.Y);
        CubicTo(new Vec2(center.X + radius, center.Y + k), new Vec2(center.X + k, center.Y + radius), new Vec2(center.X, center.Y + radius));
        CubicTo(new Vec2(center.X - k, center.Y + radius), new Vec2(center.X - radius, center.Y + k), new Vec2(center.X - radius, center.Y));
        CubicTo(new Vec2(center.X - radius, center.Y - k), new Vec2(center.X - k, center.Y - radius), new Vec2(center.X, center.Y - radius));
        CubicTo(new Vec2(center.X + k, center.Y - radius), new Vec2(center.X + radius, center.Y - k), new Vec2(center.X + radius, center.Y));
        return Close();
    }

    public PathBuilder AddArc(Vec2 center, float radius, float startAngle, float sweepAngle)
    {
        if (MathF.Abs(sweepAngle) < 1e-4f)
        {
            return this;
        }

        var segments = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweepAngle) / (MathF.PI / 2)));
        var step = sweepAngle / segments;
        var angle = startAngle;
        MoveTo(center + new Vec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
        for (var i = 0; i < segments; i++)
        {
            var next = angle + step;
            var control = 4f / 3f * MathF.Tan(step / 4);
            var p0 = center + new Vec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            var p1 = center + new Vec2(MathF.Cos(next) * radius, MathF.Sin(next) * radius);
            var c1 = p0 + new Vec2(-MathF.Sin(angle), MathF.Cos(angle)) * (control * radius);
            var c2 = p1 - new Vec2(-MathF.Sin(next), MathF.Cos(next)) * (control * radius);
            CubicTo(c1, c2, p1);
            angle = next;
        }

        return this;
    }

    internal PathBuilder ArcTo(Vec2 start, Vec2 end, float rx, float ry, float rotationDegrees, bool largeArc, bool sweep)
    {
        SvgArc.Add(this, start, end, rx, ry, rotationDegrees, largeArc, sweep);
        return this;
    }

    public PathData Build()
    {
        if (_commands.Count == 0)
        {
            return PathData.Empty;
        }

        var bounds = new Rect(_min.X, _min.Y, _max.X - _min.X, _max.Y - _min.Y);
        return new PathData(_commands.ToArray(), _points.ToArray(), bounds);
    }

    private void EnsureSubpath(Vec2 next)
    {
        if (!_hasCurrent)
        {
            MoveTo(_current);
        }
    }

    private void AddPoint(Vec2 point)
    {
        _points.Add(point);
        _min = Vec2.Min(_min, point);
        _max = Vec2.Max(_max, point);
    }
}
