namespace Vela;

public enum FontWeight
{
    Thin = 100,
    ExtraLight = 200,
    Light = 300,
    Regular = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    Black = 900,
}

/// <summary>A font request. The text engine resolves family fallbacks and glyph fallbacks.</summary>
public readonly record struct FontSpec(
    string? Family,
    float Size,
    FontWeight Weight = FontWeight.Regular,
    bool Italic = false,
    float LineHeightMultiplier = 1.25f)
{
    public static FontSpec Default => new("Segoe UI Variable Text", 14f);

    public FontSpec WithSize(float size) => this with { Size = size };
    public FontSpec WithWeight(FontWeight weight) => this with { Weight = weight };
}

