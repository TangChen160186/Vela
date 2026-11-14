namespace Vela;

/// <summary>
/// Bounds passed down the layout tree. A tight constraint fixes a size; a loose
/// constraint lets the element choose a size up to the maximum.
/// </summary>
public readonly record struct Constraints(float MinWidth, float MaxWidth, float MinHeight, float MaxHeight)
{
    public static Constraints Unbounded => new(0, float.PositiveInfinity, 0, float.PositiveInfinity);

    public bool HasBoundedWidth => float.IsFinite(MaxWidth);
    public bool HasBoundedHeight => float.IsFinite(MaxHeight);
    public bool IsTightWidth => MathF.Abs(MaxWidth - MinWidth) < 1e-4f;
    public bool IsTightHeight => MathF.Abs(MaxHeight - MinHeight) < 1e-4f;

    public static Constraints Tight(float width, float height) => new(width, width, height, height);
    public static Constraints Tight(Size size) => Tight(size.Width, size.Height);
    public static Constraints Loose(float maxWidth, float maxHeight) => new(0, maxWidth, 0, maxHeight);
    public static Constraints Loose(Size size) => Loose(size.Width, size.Height);

    public Constraints Deflate(Thickness thickness) => new(
        MathF.Max(0, MinWidth - thickness.HorizontalSum),
        MathF.Max(0, MaxWidth - thickness.HorizontalSum),
        MathF.Max(0, MinHeight - thickness.VerticalSum),
        MathF.Max(0, MaxHeight - thickness.VerticalSum));

    public Constraints WithTightWidth(float width) => new(width, width, MinHeight, MaxHeight);
    public Constraints WithTightHeight(float height) => new(MinWidth, MaxWidth, height, height);
    public Constraints WithMaxWidth(float width) => new(MathF.Min(MinWidth, width), MathF.Min(MaxWidth, width), MinHeight, MaxHeight);
    public Constraints WithMaxHeight(float height) => new(MinWidth, MaxWidth, MathF.Min(MinHeight, height), MathF.Min(MaxHeight, height));
    public Constraints WithMinWidth(float width) => this with { MinWidth = width, MaxWidth = MathF.Max(MaxWidth, width) };
    public Constraints WithMinHeight(float height) => this with { MinHeight = height, MaxHeight = MathF.Max(MaxHeight, height) };

    public Size Constrain(Size size) => new(
        Math.Clamp(size.Width, MinWidth, MathF.Max(MinWidth, MaxWidth)),
        Math.Clamp(size.Height, MinHeight, MathF.Max(MinHeight, MaxHeight)));

    public override string ToString() =>
        $"w[{MinWidth:0.##}..{MaxWidth:0.##}] h[{MinHeight:0.##}..{MaxHeight:0.##}]";
}
