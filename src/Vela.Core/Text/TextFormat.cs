namespace Vela;

/// <summary>Paragraph-level formatting used for measurement and painting.</summary>
public readonly record struct TextFormat(
    FontSpec Font,
    TextAlignment Alignment = TextAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    TextWrapMode Wrap = TextWrapMode.Wrap,
    TextOverflowMode Overflow = TextOverflowMode.Clip,
    int MaxLines = 0)
{
    public static TextFormat FromFont(FontSpec font) => new(font);

    public TextFormat WithAlignment(TextAlignment alignment) => this with { Alignment = alignment };
    public TextFormat WithVerticalAlignment(VerticalAlignment alignment) => this with { VerticalAlignment = alignment };
    public TextFormat WithWrap(TextWrapMode wrap) => this with { Wrap = wrap };
    public TextFormat WithMaxLines(int maxLines) => this with { MaxLines = maxLines };
}

/// <summary>The result of measuring a paragraph.</summary>
public readonly record struct TextMetrics(float Width, float Height, int LineCount)
{
    public static TextMetrics Empty => default;
}

/// <summary>Options for measuring text. The maximum width bounds line wrapping.</summary>
public readonly record struct TextMeasureOptions(
    float MaxWidth = float.PositiveInfinity,
    TextWrapMode Wrap = TextWrapMode.Wrap,
    TextOverflowMode Overflow = TextOverflowMode.Clip,
    int MaxLines = 0)
{
    public static TextMeasureOptions SingleLine => new(float.PositiveInfinity, TextWrapMode.NoWrap);
}
