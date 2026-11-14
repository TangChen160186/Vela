namespace Vela;

/// <summary>Inset values for the four edges of a rectangle.</summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public static Thickness Zero => default;
    public static Thickness All(float value) => new(value, value, value, value);
    public static Thickness Symmetric(float horizontal, float vertical) => new(horizontal, vertical, horizontal, vertical);
    public static Thickness Horizontal(float value) => new(value, 0, value, 0);
    public static Thickness Vertical(float value) => new(0, value, 0, value);

    public float HorizontalSum => Left + Right;
    public float VerticalSum => Top + Bottom;
    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    public static Thickness operator +(Thickness a, Thickness b) =>
        new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);

    public void Deconstruct(out float left, out float top, out float right, out float bottom)
    {
        left = Left;
        top = Top;
        right = Right;
        bottom = Bottom;
    }

    public override string ToString() => $"({Left:0.##}, {Top:0.##}, {Right:0.##}, {Bottom:0.##})";
}
