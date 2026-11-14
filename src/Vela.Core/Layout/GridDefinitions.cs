namespace Vela;

public enum GridTrackKind
{
    Points,
    Percent,
    Auto,
    Star,
}

/// <summary>A column or row definition for <see cref="Ui.Grid"/>.</summary>
public readonly record struct GridTrack(GridTrackKind Kind, float Value = 1)
{
    public static GridTrack Px(float points) => new(GridTrackKind.Points, points);
    public static GridTrack Percent(float fraction) => new(GridTrackKind.Percent, fraction);
    public static GridTrack Auto => new(GridTrackKind.Auto, 0);
    public static GridTrack Star(float weight = 1) => new(GridTrackKind.Star, weight);
}

public enum DockSide
{
    Top,
    Bottom,
    Left,
    Right,
    Fill,
}
