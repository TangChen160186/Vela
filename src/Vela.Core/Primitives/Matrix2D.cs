namespace Vela;

/// <summary>
/// A 2D affine matrix using the same layout as Skia and SVG:
/// [M11 M21 M31; M12 M22 M32; 0 0 1].
/// </summary>
public readonly record struct Matrix2D(float M11, float M12, float M21, float M22, float M31, float M32)
{
    public static Matrix2D Identity => new(1, 0, 0, 1, 0, 0);

    public static Matrix2D Translation(float x, float y) => new(1, 0, 0, 1, x, y);
    public static Matrix2D Translation(Vec2 offset) => Translation(offset.X, offset.Y);
    public static Matrix2D Scale(float scale) => new(scale, 0, 0, scale, 0, 0);
    public static Matrix2D Scale(float sx, float sy) => new(sx, 0, 0, sy, 0, 0);

    public static Matrix2D Rotation(float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Matrix2D(cos, sin, -sin, cos, 0, 0);
    }

    public static Matrix2D TRS(Vec2 translation, float rotation, Vec2 scale)
    {
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        return new Matrix2D(cos * scale.X, sin * scale.X, -sin * scale.Y, cos * scale.Y, translation.X, translation.Y);
    }

    public Vec2 Transform(Vec2 point) => new(
        (M11 * point.X) + (M21 * point.Y) + M31,
        (M12 * point.X) + (M22 * point.Y) + M32);

    /// <summary>Returns a * b, applying <paramref name="b"/> before <paramref name="a"/>.</summary>
    public static Matrix2D Multiply(Matrix2D a, Matrix2D b) => new(
        (a.M11 * b.M11) + (a.M21 * b.M12),
        (a.M12 * b.M11) + (a.M22 * b.M12),
        (a.M11 * b.M21) + (a.M21 * b.M22),
        (a.M12 * b.M21) + (a.M22 * b.M22),
        (a.M11 * b.M31) + (a.M21 * b.M32) + a.M31,
        (a.M12 * b.M31) + (a.M22 * b.M32) + a.M32);

    public static Matrix2D operator *(Matrix2D a, Matrix2D b) => Multiply(a, b);
}
