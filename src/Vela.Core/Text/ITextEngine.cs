namespace Vela;

/// <summary>
/// Measures and materializes text. The core only depends on this interface;
/// the Skia renderer supplies a shaping, fallback and rasterization implementation.
/// </summary>
public interface ITextEngine
{
    TextMetrics Measure(string text, FontSpec font, TextMeasureOptions options);

    /// <summary>Returns the natural width of a range of UTF-16 text on a single line.</summary>
    float MeasureRange(string text, FontSpec font, int start, int length);

    /// <summary>Returns the line height, including the font's preferred leading and the format's multiplier.</summary>
    float GetLineHeight(FontSpec font);

    /// <summary>
    /// Returns the UTF-16 index at the horizontal offset from the start of a single line.
    /// The returned index is clamped to the string and snapped to a grapheme boundary.
    /// </summary>
    int HitTestTextPosition(string text, FontSpec font, float x, int start = 0, int length = -1);
}
