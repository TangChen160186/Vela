using System.Globalization;

namespace Vela;

/// <summary>An sRGB color with straight (non-premultiplied) alpha.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static Color Transparent => new(0, 0, 0, 0);
    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Red => new(255, 80, 80);
    public static Color Green => new(60, 200, 130);
    public static Color Blue => new(80, 140, 255);

    public bool IsTransparent => A == 0;

    public Color WithAlpha(byte alpha) => this with { A = alpha };
    public Color WithAlpha(float alpha) => this with { A = ToByte(alpha * 255f) };
    public Color WithRed(byte red) => this with { R = red };
    public Color WithGreen(byte green) => this with { G = green };
    public Color WithBlue(byte blue) => this with { B = blue };

    public Color MultiplyAlpha(float factor) => this with { A = ToByte(A * Math.Clamp(factor, 0, 1)) };

    public static Color Rgb(byte r, byte g, byte b) => new(r, g, b);
    public static Color Rgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Transparent;
        }

        var span = hex.AsSpan().Trim();
        if (span[0] == '#')
        {
            span = span[1..];
        }

        return span.Length switch
        {
            3 => new Color(Expand(span[0]), Expand(span[1]), Expand(span[2])),
            4 => new Color(Expand(span[0]), Expand(span[1]), Expand(span[2]), Expand(span[3])),
            6 => new Color(HexByte(span[0..2]), HexByte(span[2..4]), HexByte(span[4..6])),
            8 => new Color(HexByte(span[2..4]), HexByte(span[4..6]), HexByte(span[6..8]), HexByte(span[0..2])),
            _ => throw new FormatException($"'{hex}' is not a valid color. Use #RGB, #RGBA, #RRGGBB or #AARRGGBB."),
        };

        static byte Expand(char c)
        {
            var value = HexValue(c);
            return (byte)((value << 4) | value);
        }

        static byte HexByte(ReadOnlySpan<char> text) => (byte)((HexValue(text[0]) << 4) | HexValue(text[1]));

        static int HexValue(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => throw new FormatException($"'{c}' is not a hexadecimal digit."),
        };
    }

    public string ToHex(bool includeAlpha = true) => includeAlpha
        ? string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (byte)MathF.Round(a.R + ((b.R - a.R) * t)),
            (byte)MathF.Round(a.G + ((b.G - a.G) * t)),
            (byte)MathF.Round(a.B + ((b.B - a.B) * t)),
            (byte)MathF.Round(a.A + ((b.A - a.A) * t)));
    }

    public static Color FromHsv(float hue, float saturation, float value, byte alpha = 255)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var c = value * saturation;
        var x = c * (1 - MathF.Abs(((hue / 60f) % 2) - 1));
        var m = value - c;

        var (r, g, b) = (int)(hue / 60f) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x),
        };

        return new Color(ToByte((r + m) * 255), ToByte((g + m) * 255), ToByte((b + m) * 255), alpha);
    }

    /// <summary>Returns a color at the supplied alpha (0..1) over this color, preserving RGB.</summary>
    public Color Alpha(float alpha) => WithAlpha(alpha);

    /// <summary>Blends this color over <paramref name="background"/> using source-over compositing.</summary>
    public Color Over(Color background)
    {
        var alpha = A / 255f;
        var outAlpha = alpha + (background.A / 255f * (1 - alpha));
        if (outAlpha <= 0)
        {
            return Transparent;
        }

        return new Color(
            ToByte(((R * alpha) + (background.R * background.A / 255f * (1 - alpha))) / outAlpha),
            ToByte(((G * alpha) + (background.G * background.A / 255f * (1 - alpha))) / outAlpha),
            ToByte(((B * alpha) + (background.B * background.A / 255f * (1 - alpha))) / outAlpha),
            ToByte(outAlpha * 255));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0, 255);

    public override string ToString() => ToHex();
}
