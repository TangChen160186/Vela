namespace Vela;

/// <summary>A single color stop in a gradient. Offsets are normalized to the painted bounds.</summary>
public readonly record struct GradientStop(float Offset, Color Color);

public abstract class Gradient
{
    protected Gradient(GradientStop[] stops)
    {
        if (stops.Length < 2)
        {
            throw new ArgumentException("A gradient needs at least two stops.", nameof(stops));
        }

        Stops = stops;
    }

    public IReadOnlyList<GradientStop> Stops { get; }

    public static LinearGradient Linear(Vec2 start, Vec2 end, params GradientStop[] stops) => new(start, end, stops);
    public static RadialGradient Radial(Vec2 center, float radius, params GradientStop[] stops) => new(center, radius, stops);
}

/// <summary>A linear gradient whose points are normalized to the painted rectangle.</summary>
public sealed class LinearGradient : Gradient
{
    public LinearGradient(Vec2 start, Vec2 end, params GradientStop[] stops) : base(stops)
    {
        Start = start;
        End = end;
    }

    public Vec2 Start { get; }
    public Vec2 End { get; }
}

/// <summary>A radial gradient whose center and radius are normalized to the painted rectangle.</summary>
public sealed class RadialGradient : Gradient
{
    public RadialGradient(Vec2 center, float radius, params GradientStop[] stops) : base(stops)
    {
        Center = center;
        Radius = radius;
    }

    public Vec2 Center { get; }
    public float Radius { get; }
}

/// <summary>A solid color or a gradient. A default brush is empty and paints nothing.</summary>
public readonly struct Brush
{
    public Brush(Color color)
    {
        SolidColor = color;
        Gradient = null;
    }

    public Brush(Gradient gradient)
    {
        SolidColor = null;
        Gradient = gradient ?? throw new ArgumentNullException(nameof(gradient));
    }

    public Color? SolidColor { get; }
    public Gradient? Gradient { get; }
    public bool IsEmpty => SolidColor is null && Gradient is null;
    public bool IsSolid => SolidColor is not null;

    public static Brush Solid(Color color) => new(color);
    public static Brush Transparent => new(Color.Transparent);

    public static implicit operator Brush(Color color) => new(color);
    public static implicit operator Brush(LinearGradient gradient) => new(gradient);
    public static implicit operator Brush(RadialGradient gradient) => new(gradient);

    public Brush WithAlpha(float alpha) => SolidColor is { } color
        ? new Brush(color.WithAlpha(color.A / 255f * alpha))
        : this;

    public override string ToString() => Gradient is not null ? "gradient" : SolidColor?.ToString() ?? "empty";
}
