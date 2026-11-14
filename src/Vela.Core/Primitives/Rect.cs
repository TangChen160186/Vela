using System.Globalization;

namespace Vela;

/// <summary>An axis-aligned rectangle in device-independent pixels.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public static Rect Empty => new(0, 0, 0, 0);

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vec2 Position => new(X, Y);
    public Vec2 Size => new(Width, Height);
    public Vec2 Center => new(X + (Width * 0.5f), Y + (Height * 0.5f));
    public Vec2 TopLeft => new(X, Y);
    public Vec2 TopRight => new(Right, Y);
    public Vec2 BottomLeft => new(X, Bottom);
    public Vec2 BottomRight => new(Right, Bottom);

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Width) && float.IsFinite(Height);

    public bool Contains(Vec2 point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Contains(Rect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Intersects(Rect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public Rect Intersect(Rect other)
    {
        var left = MathF.Max(Left, other.Left);
        var top = MathF.Max(Top, other.Top);
        var right = MathF.Min(Right, other.Right);
        var bottom = MathF.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? FromLTRB(left, top, right, bottom) : Empty;
    }

    public Rect Union(Rect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return FromLTRB(
            MathF.Min(Left, other.Left),
            MathF.Min(Top, other.Top),
            MathF.Max(Right, other.Right),
            MathF.Max(Bottom, other.Bottom));
    }

    public Rect Inflate(float amount) => new(X - amount, Y - amount, Width + (amount * 2), Height + (amount * 2));

    public Rect Inflate(float horizontal, float vertical) =>
        new(X - horizontal, Y - vertical, Width + (horizontal * 2), Height + (vertical * 2));

    public Rect Deflate(Thickness thickness) => FromLTRB(
        Left + thickness.Left,
        Top + thickness.Top,
        Right - thickness.Right,
        Bottom - thickness.Bottom);

    public Rect Offset(Vec2 amount) => new(X + amount.X, Y + amount.Y, Width, Height);
    public Rect Offset(float x, float y) => new(X + x, Y + y, Width, Height);

    public Rect ClampSize(float minWidth, float maxWidth, float minHeight, float maxHeight) =>
        new(X, Y, Math.Clamp(Width, minWidth, maxWidth), Math.Clamp(Height, minHeight, maxHeight));

    public Vec2 ClampPoint(Vec2 point) => new(
        Math.Clamp(point.X, Left, Right),
        Math.Clamp(point.Y, Top, Bottom));

    public static Rect FromLTRB(float left, float top, float right, float bottom) =>
        new(left, top, MathF.Max(0, right - left), MathF.Max(0, bottom - top));

    public static Rect FromSize(float width, float height) => new(0, 0, width, height);

    public static Rect Centered(Vec2 center, float width, float height) =>
        new(center.X - (width * 0.5f), center.Y - (height * 0.5f), width, height);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"[{X:0.##}, {Y:0.##}, {Width:0.##} x {Height:0.##}]");
}
