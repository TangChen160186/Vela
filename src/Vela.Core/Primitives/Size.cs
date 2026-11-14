namespace Vela;

/// <summary>A width/height pair in device-independent pixels.</summary>
public readonly record struct Size(float Width, float Height)
{
    public static Size Zero => default;

    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
    public static Size operator *(Size a, float scale) => new(a.Width * scale, a.Height * scale);

    public Vec2 ToVec2() => new(Width, Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"{Width:0.##} x {Height:0.##}";
}
