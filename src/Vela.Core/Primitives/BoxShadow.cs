namespace Vela;

/// <summary>A soft drop shadow painted behind a box.</summary>
public readonly record struct BoxShadow(Vec2 Offset, float Blur, float Spread, Color Color)
{
    public static BoxShadow None => default;

    public static BoxShadow Soft(Color color, float blur = 18, float y = 6, float spread = 0) =>
        new(new Vec2(0, y), blur, spread, color);
}
