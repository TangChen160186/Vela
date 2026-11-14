namespace Vela;

/// <summary>Per-corner radii, ordered clockwise from the top-left corner.</summary>
public readonly record struct CornerRadius(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    public static CornerRadius Zero => default;
    public static CornerRadius All(float radius) => new(radius, radius, radius, radius);
    public static CornerRadius Circular(float radius) => All(radius);
    public static CornerRadius Top(float radius) => new(radius, radius, 0, 0);
    public static CornerRadius Bottom(float radius) => new(0, 0, radius, radius);

    public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;

    public CornerRadius Clamp(float maxRadius)
    {
        maxRadius = MathF.Max(0, maxRadius);
        return new(
            Math.Clamp(TopLeft, 0, maxRadius),
            Math.Clamp(TopRight, 0, maxRadius),
            Math.Clamp(BottomRight, 0, maxRadius),
            Math.Clamp(BottomLeft, 0, maxRadius));
    }

    public static CornerRadius operator +(CornerRadius a, CornerRadius b) =>
        new(a.TopLeft + b.TopLeft, a.TopRight + b.TopRight, a.BottomRight + b.BottomRight, a.BottomLeft + b.BottomLeft);

    public override string ToString() => $"{TopLeft:0.##}/{TopRight:0.##}/{BottomRight:0.##}/{BottomLeft:0.##}";
}
