using System.Globalization;

namespace Vela.Tests;

internal sealed class FakeTextEngine : ITextEngine
{
    public TextMetrics Measure(string text, FontSpec font, TextMeasureOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextMetrics(0, GetLineHeight(font), 1);
        }

        var advance = MathF.Max(1, font.Size * 0.55f);
        var lineHeight = GetLineHeight(font);
        var maxWidth = options.MaxWidth;
        if (!float.IsFinite(maxWidth) || maxWidth <= 0 || options.Wrap == TextWrapMode.NoWrap)
        {
            var lines = TextEditing.CountLines(text);
            var width = text.Split('\n').Max(line => line.Length) * advance;
            return new TextMetrics(width, lines * lineHeight, lines);
        }

        var totalLines = 0;
        var widest = 0f;
        foreach (var paragraph in text.Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                totalLines++;
                continue;
            }

            var words = paragraph.Split(' ');
            var lineWidth = 0f;
            var lineHasContent = false;
            foreach (var word in words)
            {
                var wordWidth = word.Length * advance;
                if (lineHasContent && lineWidth + advance + wordWidth > maxWidth)
                {
                    widest = MathF.Max(widest, lineWidth);
                    totalLines++;
                    lineWidth = wordWidth;
                    lineHasContent = true;
                }
                else
                {
                    lineWidth += lineHasContent ? advance + wordWidth : wordWidth;
                    lineHasContent = true;
                }
            }

            widest = MathF.Max(widest, lineWidth);
            totalLines++;
        }

        return new TextMetrics(MathF.Min(widest, maxWidth), totalLines * lineHeight, totalLines);
    }

    public float MeasureRange(string text, FontSpec font, int start, int length) =>
        MathF.Max(0, Math.Clamp(length, 0, Math.Max(0, text.Length - start))) * MathF.Max(1, font.Size * 0.55f);

    public float GetLineHeight(FontSpec font) =>
        MathF.Max(1, font.Size * (font.LineHeightMultiplier <= 0 ? 1.25f : font.LineHeightMultiplier));

    public int HitTestTextPosition(string text, FontSpec font, float x, int start = 0, int length = -1)
    {
        var advance = MathF.Max(1, font.Size * 0.55f);
        var max = length < 0 ? text.Length - start : Math.Min(length, text.Length - start);
        var index = (int)MathF.Round(x / advance, MidpointRounding.AwayFromZero);
        return Math.Clamp(start + index, start, start + Math.Max(0, max));
    }
}
