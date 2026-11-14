namespace Vela;

/// <summary>How a width or height is determined.</summary>
public enum DimensionKind
{
    /// <summary>Size to the element's content, bounded by the available space.</summary>
    Auto,
    /// <summary>A fixed number of device-independent pixels.</summary>
    Points,
    /// <summary>A fraction of the parent's inner size (1 = 100%).</summary>
    Percent,
}

/// <summary>A width or height specification.</summary>
public readonly record struct Dimension(float Value, DimensionKind Kind)
{
    public static Dimension Auto => default;
    public static Dimension Px(float points) => new(points, DimensionKind.Points);
    public static Dimension Percent(float fraction) => new(fraction, DimensionKind.Percent);

    public bool IsAuto => Kind == DimensionKind.Auto;
    public bool IsFixed => Kind == DimensionKind.Points;
    public bool IsPercent => Kind == DimensionKind.Percent;

    public float Resolve(float baseSize, float autoValue = 0) => Kind switch
    {
        DimensionKind.Points => Value,
        DimensionKind.Percent => baseSize * Value,
        _ => autoValue,
    };

    public static implicit operator Dimension(float points) => Px(points);

    public override string ToString() => Kind switch
    {
        DimensionKind.Points => $"{Value:0.##}px",
        DimensionKind.Percent => $"{Value * 100:0.##}%",
        _ => "auto",
    };
}
