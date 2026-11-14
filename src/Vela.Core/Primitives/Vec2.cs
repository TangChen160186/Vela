using System.Globalization;

namespace Vela;

/// <summary>A two-dimensional vector in device-independent pixels.</summary>
public readonly record struct Vec2(float X, float Y)
{
    public static Vec2 Zero => new(0, 0);
    public static Vec2 One => new(1, 1);
    public static Vec2 UnitX => new(1, 0);
    public static Vec2 UnitY => new(0, 1);

    public float Length => MathF.Sqrt((X * X) + (Y * Y));
    public float LengthSquared => (X * X) + (Y * Y);

    public Vec2 Normalized()
    {
        var length = Length;
        return length > 1e-6f ? this / length : Zero;
    }

    public float Dot(Vec2 other) => (X * other.X) + (Y * other.Y);
    public float Distance(Vec2 other) => (this - other).Length;
    public float DistanceSquared(Vec2 other) => (this - other).LengthSquared;

    public Vec2 Abs() => new(MathF.Abs(X), MathF.Abs(Y));
    public Vec2 Floor() => new(MathF.Floor(X), MathF.Floor(Y));
    public Vec2 Ceiling() => new(MathF.Ceiling(X), MathF.Ceiling(Y));
    public Vec2 Round() => new(MathF.Round(X), MathF.Round(Y));

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
    public static Vec2 Min(Vec2 a, Vec2 b) => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
    public static Vec2 Max(Vec2 a, Vec2 b) => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
    public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
    public static float Dot(Vec2 a, Vec2 b) => (a.X * b.X) + (a.Y * b.Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vec2 operator *(float scalar, Vec2 a) => a * scalar;
    public static Vec2 operator /(Vec2 a, float scalar) => new(a.X / scalar, a.Y / scalar);

    public void Deconstruct(out float x, out float y)
    {
        x = X;
        y = Y;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:0.##}, {Y:0.##})");
}
