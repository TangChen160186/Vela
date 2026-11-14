using System.Globalization;

namespace Vela;

/// <summary>
/// A compact SVG path parser. Supports M, L, H, V, C, S, Q, T, A and Z in absolute
/// and relative forms. Useful for icon sets and designer-authored vector assets.
/// </summary>
public static class SvgPathParser
{
    public static PathData Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var builder = new PathBuilder();
        var parser = new Parser(data, builder);
        parser.Parse();
        return builder.Build();
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly PathBuilder _builder;
        private int _index;
        private Vec2 _current;
        private Vec2 _subpathStart;
        private Vec2 _lastCubicControl;
        private Vec2 _lastQuadControl;
        private char _previousCommand;

        public Parser(string text, PathBuilder builder)
        {
            _text = text;
            _builder = builder;
        }

        public void Parse()
        {
            SkipSeparators();
            if (_index >= _text.Length)
            {
                return;
            }

            char command = '\0';
            while (true)
            {
                SkipSeparators();
                if (_index >= _text.Length)
                {
                    break;
                }

                if (IsCommand(_text[_index]))
                {
                    command = _text[_index++];
                    if (command is 'Z' or 'z')
                    {
                        _builder.Close();
                        _current = _subpathStart;
                        _previousCommand = command;
                        command = '\0';
                        SkipSeparators();
                        continue;
                    }
                }
                else if (command == '\0')
                {
                    throw Error("Expected an SVG path command.");
                }

                var relative = char.IsLower(command);
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                    {
                        var point = ReadPoint(relative);
                        _builder.MoveTo(point);
                        _current = point;
                        _subpathStart = point;
                        _lastCubicControl = point;
                        _lastQuadControl = point;
                        command = relative ? 'l' : 'L';
                        break;
                    }

                    case 'L':
                    {
                        var point = ReadPoint(relative);
                        _builder.LineTo(point);
                        _current = point;
                        _lastCubicControl = point;
                        _lastQuadControl = point;
                        break;
                    }

                    case 'H':
                    {
                        var x = ReadNumber();
                        var point = new Vec2(relative ? _current.X + x : x, _current.Y);
                        _builder.LineTo(point);
                        _current = point;
                        _lastCubicControl = point;
                        _lastQuadControl = point;
                        break;
                    }

                    case 'V':
                    {
                        var y = ReadNumber();
                        var point = new Vec2(_current.X, relative ? _current.Y + y : y);
                        _builder.LineTo(point);
                        _current = point;
                        _lastCubicControl = point;
                        _lastQuadControl = point;
                        break;
                    }

                    case 'C':
                    {
                        var c1 = ReadPoint(relative);
                        var c2 = ReadPoint(relative);
                        var p = ReadPoint(relative);
                        _builder.CubicTo(c1, c2, p);
                        _current = p;
                        _lastCubicControl = c2;
                        break;
                    }

                    case 'S':
                    {
                        var c2 = ReadPoint(relative);
                        var p = ReadPoint(relative);
                        var c1 = _previousCommand is 'C' or 'c' or 'S' or 's'
                            ? (_current * 2) - _lastCubicControl
                            : _current;
                        _builder.CubicTo(c1, c2, p);
                        _current = p;
                        _lastCubicControl = c2;
                        break;
                    }

                    case 'Q':
                    {
                        var c = ReadPoint(relative);
                        var p = ReadPoint(relative);
                        _builder.QuadraticTo(c, p);
                        _current = p;
                        _lastQuadControl = c;
                        break;
                    }

                    case 'T':
                    {
                        var p = ReadPoint(relative);
                        var c = _previousCommand is 'Q' or 'q' or 'T' or 't'
                            ? (_current * 2) - _lastQuadControl
                            : _current;
                        _builder.QuadraticTo(c, p);
                        _current = p;
                        _lastQuadControl = c;
                        break;
                    }

                    case 'A':
                    {
                        var rx = ReadNumber();
                        var ry = ReadNumber();
                        var rotation = ReadNumber();
                        var largeArc = ReadFlag();
                        var sweep = ReadFlag();
                        var p = ReadPoint(relative);
                        SvgArc.Add(_builder, _current, p, rx, ry, rotation, largeArc, sweep);
                        _current = p;
                        _lastCubicControl = p;
                        _lastQuadControl = p;
                        break;
                    }

                    default:
                        throw Error($"Unsupported SVG path command '{command}'.");
                }

                _previousCommand = command;
                SkipSeparators();
            }
        }

        private Vec2 ReadPoint(bool relative)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return relative ? new Vec2(_current.X + x, _current.Y + y) : new Vec2(x, y);
        }

        private float ReadNumber()
        {
            SkipSeparators();
            var start = _index;
            if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
            {
                _index++;
            }

            var hasDigit = false;
            while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
            {
                _index++;
                hasDigit = true;
            }

            if (_index < _text.Length && _text[_index] == '.')
            {
                _index++;
                while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
                {
                    _index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
            {
                throw Error("Expected a number.");
            }

            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                _index++;
                if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
                {
                    _index++;
                }

                var exponentStart = _index;
                while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
                {
                    _index++;
                }

                if (exponentStart == _index)
                {
                    throw Error("Invalid exponent in number.");
                }
            }

            var span = _text.AsSpan(start, _index - start);
            if (!float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw Error($"Invalid number '{span.ToString()}'.");
            }

            return value;
        }

        private bool ReadFlag()
        {
            SkipSeparators();
            if (_index >= _text.Length)
            {
                throw Error("Expected an arc flag.");
            }

            var c = _text[_index++];
            return c switch
            {
                '0' => false,
                '1' => true,
                _ => throw Error("Arc flags must be 0 or 1."),
            };
        }

        private void SkipSeparators()
        {
            while (_index < _text.Length)
            {
                var c = _text[_index];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    _index++;
                }
                else
                {
                    break;
                }
            }
        }

        private static bool IsCommand(char c) => char.IsAsciiLetter(c);

        private FormatException Error(string message)
        {
            var near = _text[Math.Min(_index, _text.Length)..Math.Min(_text.Length, _index + 12)];
            return new FormatException($"{message} (at index {_index}, near '{near}').");
        }
    }
}

internal static class SvgArc
{
    public static void Add(
        PathBuilder builder,
        Vec2 start,
        Vec2 end,
        float radiusX,
        float radiusY,
        float xAxisRotationDegrees,
        bool largeArc,
        bool sweep)
    {
        if ((start - end).LengthSquared < 1e-8f)
        {
            return;
        }

        radiusX = MathF.Abs(radiusX);
        radiusY = MathF.Abs(radiusY);
        if (radiusX < 1e-5f || radiusY < 1e-5f)
        {
            builder.LineTo(end);
            return;
        }

        var phi = xAxisRotationDegrees * MathF.PI / 180f;
        var cosPhi = MathF.Cos(phi);
        var sinPhi = MathF.Sin(phi);

        var dx = (start.X - end.X) * 0.5f;
        var dy = (start.Y - end.Y) * 0.5f;
        var x1 = (cosPhi * dx) + (sinPhi * dy);
        var y1 = (-sinPhi * dx) + (cosPhi * dy);

        var rx2 = radiusX * radiusX;
        var ry2 = radiusY * radiusY;
        var x1Sq = x1 * x1;
        var y1Sq = y1 * y1;
        var lambda = (x1Sq / rx2) + (y1Sq / ry2);
        if (lambda > 1)
        {
            var scale = MathF.Sqrt(lambda);
            radiusX *= scale;
            radiusY *= scale;
            rx2 = radiusX * radiusX;
            ry2 = radiusY * radiusY;
        }

        var numerator = (rx2 * ry2) - (rx2 * y1Sq) - (ry2 * x1Sq);
        var denominator = (rx2 * y1Sq) + (ry2 * x1Sq);
        var factor = denominator <= 1e-8f ? 0 : MathF.Sqrt(MathF.Max(0, numerator / denominator));
        if (largeArc == sweep)
        {
            factor = -factor;
        }

        var cx1 = factor * radiusX * y1 / radiusY;
        var cy1 = -factor * radiusY * x1 / radiusX;
        var cx = (cosPhi * cx1) - (sinPhi * cy1) + ((start.X + end.X) * 0.5f);
        var cy = (sinPhi * cx1) + (cosPhi * cy1) + ((start.Y + end.Y) * 0.5f);

        var ux = (x1 - cx1) / radiusX;
        var uy = (y1 - cy1) / radiusY;
        var vx = (-x1 - cx1) / radiusX;
        var vy = (-y1 - cy1) / radiusY;

        var startAngle = MathF.Atan2(uy, ux);
        var deltaAngle = SignedAngle(ux, uy, vx, vy);
        if (!sweep && deltaAngle > 0)
        {
            deltaAngle -= MathF.PI * 2;
        }
        else if (sweep && deltaAngle < 0)
        {
            deltaAngle += MathF.PI * 2;
        }

        var segments = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(deltaAngle) / (MathF.PI * 0.5f)));
        var step = deltaAngle / segments;

        for (var i = 0; i < segments; i++)
        {
            var a1 = startAngle + (step * i);
            var a2 = a1 + step;
            var alpha = 4f / 3f * MathF.Tan(step / 4f);

            var p1 = Point(cx, cy, radiusX, radiusY, cosPhi, sinPhi, a1);
            var p2 = Point(cx, cy, radiusX, radiusY, cosPhi, sinPhi, a2);
            var d1 = Derivative(radiusX, radiusY, cosPhi, sinPhi, a1);
            var d2 = Derivative(radiusX, radiusY, cosPhi, sinPhi, a2);

            var c1 = p1 + d1 * alpha;
            var c2 = p2 - d2 * alpha;
            builder.CubicTo(c1, c2, i == segments - 1 ? end : p2);
        }
    }

    private static Vec2 Point(float cx, float cy, float rx, float ry, float cosPhi, float sinPhi, float angle)
    {
        var x = rx * MathF.Cos(angle);
        var y = ry * MathF.Sin(angle);
        return new Vec2(
            cx + (cosPhi * x) - (sinPhi * y),
            cy + (sinPhi * x) + (cosPhi * y));
    }

    private static Vec2 Derivative(float rx, float ry, float cosPhi, float sinPhi, float angle)
    {
        var x = -rx * MathF.Sin(angle);
        var y = ry * MathF.Cos(angle);
        return new Vec2((cosPhi * x) - (sinPhi * y), (sinPhi * x) + (cosPhi * y));
    }

    private static float SignedAngle(float ux, float uy, float vx, float vy)
    {
        var dot = Math.Clamp((ux * vx) + (uy * vy), -1, 1);
        var angle = MathF.Acos(dot);
        return (ux * vy) - (uy * vx) < 0 ? -angle : angle;
    }
}
