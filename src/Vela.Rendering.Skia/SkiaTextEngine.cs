using System.Buffers;
using System.Globalization;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Vela.Rendering.Skia;

/// <summary>
/// Skia 文本引擎：测量、换行、字体回退、HarfBuzz shaping 和绘制。
///
/// 阅读顺序：
/// <list type="number">
/// <item>GetLayout：按文本/字体/宽度生成缓存的 TextLayout；</item>
/// <item>ShapeLine：按字形可用性拆 fallback runs；</item>
/// <item>MeasureSpan/DrawText：每个 run 使用 SKShaper 保证测量与绘制一致；</item>
/// <item>GetPrimaryFace/SelectFace：字体回退。</item>
/// </list>
///
/// 单线程设计：缓存 SKTypeface/SKShaper/布局，不跨线程共享。UI 线程调用。
/// </summary>
public sealed class SkiaTextEngine : ITextEngine, IDisposable
{
    private const int MaxLayoutCacheEntries = 1536;
    private const int MaxFallbackEntries = 2048;

    private readonly SKFontManager _fontManager;
    private readonly SKPaint _measurePaint = new() { IsAntialias = true };
    private readonly SKPaint _textPaint = new() { IsAntialias = true };
    private readonly Dictionary<FontKey, FontFace> _primaryFonts = new();
    private readonly Dictionary<(FontKey Key, int Codepoint), FontFace?> _fallbackFonts = new();
    private readonly Dictionary<LayoutKey, TextLayout> _layouts = new();
    private readonly Dictionary<SKTypeface, SKShaper> _shapers = new();
    private readonly string _defaultFamily;
    private bool _disposed;

    public SkiaTextEngine(string defaultFamily = "Segoe UI")
    {
        _fontManager = SKFontManager.Default;
        _defaultFamily = defaultFamily;
    }

    public string DefaultFamily => _defaultFamily;

    /// <summary>
    /// 测量整段文本。结果包含总宽高和行数；同一组
    /// (text,font,maxWidth,wrap,overflow,maxLines) 会命中布局缓存。
    /// </summary>
    public TextMetrics Measure(string text, FontSpec font, TextMeasureOptions options)
    {
        ThrowIfDisposed();
        var layout = GetLayout(text, font, options.MaxWidth, options.Wrap, options.Overflow, options.MaxLines);
        return new TextMetrics(layout.Width, layout.Height, layout.Lines.Count);
    }

    /// <summary>
    /// 测量 [start, start+length) 的视觉宽度，用于光标/选区/行宽。
    /// 逐 Rune 选择 fallback face，再按 run 使用 shaping 宽度；
    /// 不能简单用字体逐字形 advance 相加，否则连字/kerning 会不一致。
    /// </summary>
    public float MeasureRange(string text, FontSpec font, int start, int length)
    {
        ThrowIfDisposed();
        if (text.Length == 0 || length <= 0)
        {
            return 0;
        }

        start = Math.Clamp(start, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - start);
        if (length == 0)
        {
            return 0;
        }

        var face = GetPrimaryFace(font);
        var width = 0f;
        var runStart = -1;
        SKFont? runFont = null;

        for (var offset = 0; offset < length;)
        {
            var absolute = start + offset;
            Rune.DecodeFromUtf16(text.AsSpan(absolute, start + length - absolute), out var rune, out var consumed);
            if (consumed <= 0)
            {
                consumed = 1;
            }

            var runFace = SelectFace(font, rune.Value, face);
            if (runStart < 0 || !ReferenceEquals(runFace.Font, runFont))
            {
                if (runStart >= 0 && runFont is not null)
                {
                    width += MeasureSpan(runFont, text.AsSpan(runStart, absolute - runStart));
                }

                runStart = absolute;
                runFont = runFace.Font;
            }

            offset += consumed;
        }

        if (runStart >= 0 && runFont is not null)
        {
            width += MeasureSpan(runFont, text.AsSpan(runStart, start + length - runStart));
        }

        return width;
    }

    public float GetLineHeight(FontSpec font)
    {
        ThrowIfDisposed();
        var face = GetPrimaryFace(font);
        var metrics = face.Font.Metrics;
        var multiplier = font.LineHeightMultiplier <= 0 ? 1.25f : font.LineHeightMultiplier;
        return MathF.Max(1, (metrics.Descent - metrics.Ascent) * multiplier);
    }

    public int HitTestTextPosition(string text, FontSpec font, float x, int start = 0, int length = -1)
    {
        ThrowIfDisposed();
        if (text.Length == 0)
        {
            return start;
        }

        start = Math.Clamp(start, 0, text.Length);
        if (length < 0)
        {
            length = text.Length - start;
        }

        length = Math.Clamp(length, 0, text.Length - start);
        if (length == 0 || x <= 0)
        {
            return start;
        }

        var bestIndex = start;
        var bestDistance = MathF.Abs(x);
        var boundaries = StringInfo.ParseCombiningCharacters(text.Substring(start, length));

        foreach (var boundary in boundaries)
        {
            var prefix = MeasureRange(text, font, start, boundary);
            var distance = MathF.Abs(prefix - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = start + boundary;
            }
            else if (prefix > x)
            {
                break;
            }
        }

        var endWidth = MeasureRange(text, font, start, length);
        if (MathF.Abs(endWidth - x) < bestDistance)
        {
            bestIndex = start + length;
        }

        return bestIndex;
    }

    /// <summary>Paints text inside <paramref name="rect"/>. Used by the Skia renderer.</summary>
    /// <summary>
    /// 绘制带字体回退的文本：先得到 TextLayout，再逐行、逐 fallback run 调用
    /// DrawShapedText。对齐和省略号在布局阶段已处理，这里只负责定位/上色。
    /// </summary>
    public void DrawText(SKCanvas canvas, Rect rect, string text, TextFormat format, Color color)
    {
        ThrowIfDisposed();
        if (rect.IsEmpty || string.IsNullOrEmpty(text) || color.A == 0)
        {
            return;
        }

        var layout = GetLayout(text, format.Font, rect.Width, format.Wrap, format.Overflow, format.MaxLines);
        var offsetY = format.VerticalAlignment switch
        {
            VerticalAlignment.Center => (rect.Height - layout.Height) * 0.5f,
            VerticalAlignment.Bottom => rect.Height - layout.Height,
            _ => 0,
        };

        _textPaint.Color = new SKColor(color.R, color.G, color.B, color.A);
        _textPaint.IsAntialias = true;

        canvas.Save();
        canvas.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
        var y = rect.Top + offsetY;
        var baselineInLine = (layout.LineHeight - ((-layout.PrimaryMetrics.Ascent) + layout.PrimaryMetrics.Descent)) * 0.5f - layout.PrimaryMetrics.Ascent;

        foreach (var line in layout.Lines)
        {
            var x = rect.Left + format.Alignment switch
            {
                TextAlignment.Center => (rect.Width - line.Width) * 0.5f,
                TextAlignment.Right => rect.Width - line.Width,
                _ => 0,
            };

            var baseline = y + baselineInLine;
            foreach (var run in line.Runs)
            {
                canvas.DrawShapedText(GetShaper(run.Font.Typeface), run.Text, x + run.X, baseline, run.Font, _textPaint);
            }

            y += layout.LineHeight;
        }

        canvas.Restore();
    }

    private TextLayout GetLayout(
        string text,
        FontSpec font,
        float maxWidth,
        TextWrapMode wrap,
        TextOverflowMode overflow,
        int maxLines)
    {
        if (maxWidth < 0)
        {
            maxWidth = 0;
        }

        var key = new LayoutKey(
            text,
            font,
            float.IsFinite(maxWidth) ? MathF.Round(maxWidth * 4f) / 4f : float.PositiveInfinity,
            wrap,
            overflow,
            maxLines);
        if (_layouts.TryGetValue(key, out var existing))
        {
            return existing;
        }

        if (_layouts.Count > MaxLayoutCacheEntries)
        {
            _layouts.Clear();
        }

        var layout = BuildLayout(text, font, key.MaxWidth, wrap, overflow, maxLines);
        _layouts[key] = layout;
        return layout;
    }

    private TextLayout BuildLayout(
        string text,
        FontSpec font,
        float maxWidth,
        TextWrapMode wrap,
        TextOverflowMode overflow,
        int maxLines)
    {
        var ranges = new List<LineRange>(8);
        var start = 0;
        while (start <= text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline >= 0 ? newline : text.Length;
            ranges.AddRange(BreakParagraph(text, start, end - start, font, maxWidth, wrap));
            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
            if (start == text.Length)
            {
                ranges.Add(new LineRange(start, 0));
                break;
            }
        }

        if (ranges.Count == 0)
        {
            ranges.Add(new LineRange(0, 0));
        }

        var truncated = false;
        if (maxLines > 0 && ranges.Count > maxLines)
        {
            ranges.RemoveRange(maxLines, ranges.Count - maxLines);
            truncated = true;
        }

        var lines = new List<LineLayout>(ranges.Count);
        var face = GetPrimaryFace(font);
        var metrics = face.Font.Metrics;
        var lineHeightMultiplier = font.LineHeightMultiplier <= 0 ? 1.25f : font.LineHeightMultiplier;
        var lineHeight = MathF.Max(1, (metrics.Descent - metrics.Ascent) * lineHeightMultiplier);
        var totalWidth = 0f;

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            var trimEnd = range.Start + range.Length;
            while (trimEnd > range.Start && char.IsWhiteSpace(text[trimEnd - 1]))
            {
                trimEnd--;
            }

            var lineStart = range.Start;
            var lineLength = trimEnd - lineStart;
            var isLast = i == ranges.Count - 1;
            var line = ShapeLine(text, lineStart, lineLength, font, face);

            if (isLast && (truncated || (line.Width > maxWidth && overflow == TextOverflowMode.Ellipsis)))
            {
                if (overflow == TextOverflowMode.Ellipsis && maxWidth > 0)
                {
                    line = TruncateWithEllipsis(text, lineStart, lineLength, font, face, maxWidth);
                }
            }

            if (overflow == TextOverflowMode.Ellipsis && line.Width > maxWidth && maxWidth > 0)
            {
                line = TruncateWithEllipsis(text, lineStart, lineLength, font, face, maxWidth);
            }

            totalWidth = MathF.Max(totalWidth, line.Width);
            lines.Add(line);
        }

        return new TextLayout(lines, totalWidth, lines.Count * lineHeight, lineHeight, metrics);
    }

    private List<LineRange> BreakParagraph(
        string text,
        int start,
        int length,
        FontSpec font,
        float maxWidth,
        TextWrapMode wrap)
    {
        var result = new List<LineRange>(4);
        var end = start + length;
        if (length == 0)
        {
            result.Add(new LineRange(start, 0));
            return result;
        }

        if (wrap == TextWrapMode.NoWrap || !float.IsFinite(maxWidth) || maxWidth <= 0)
        {
            result.Add(new LineRange(start, length));
            return result;
        }

        var lineStart = start;
        var lineEnd = start;
        var cursor = start;
        while (cursor < end)
        {
            var unitEnd = FindUnitEnd(text, cursor, end);
            var trimmedEnd = unitEnd;
            while (trimmedEnd > lineStart && char.IsWhiteSpace(text[trimmedEnd - 1]))
            {
                trimmedEnd--;
            }

            var width = MeasureSpan(text, font, lineStart, trimmedEnd - lineStart);
            if (width <= maxWidth || trimmedEnd == lineStart)
            {
                lineEnd = unitEnd;
                cursor = unitEnd;
                continue;
            }

            if (lineEnd > lineStart)
            {
                var commitEnd = lineEnd;
                while (commitEnd > lineStart && char.IsWhiteSpace(text[commitEnd - 1]))
                {
                    commitEnd--;
                }

                result.Add(new LineRange(lineStart, commitEnd - lineStart));
                lineStart = cursor;
                while (lineStart < end && char.IsWhiteSpace(text[lineStart]))
                {
                    lineStart++;
                }

                lineEnd = lineStart;
            }
            else
            {
                // A single unbreakable unit is wider than the line: split by grapheme.
                var fit = FindFittingPrefix(text, lineStart, unitEnd, font, maxWidth);
                if (fit <= lineStart)
                {
                    fit = Math.Min(unitEnd, NextGrapheme(text, lineStart));
                }

                result.Add(new LineRange(lineStart, fit - lineStart));
                lineStart = fit;
                lineEnd = lineStart;
                cursor = lineStart;
            }

            cursor = Math.Max(cursor, lineStart);
        }

        if (lineStart < end || result.Count == 0)
        {
            var commitEnd = end;
            while (commitEnd > lineStart && char.IsWhiteSpace(text[commitEnd - 1]))
            {
                commitEnd--;
            }

            result.Add(new LineRange(lineStart, commitEnd - lineStart));
        }

        return result;
    }

    private static int FindUnitEnd(string text, int start, int end)
    {
        if (start >= end)
        {
            return end;
        }

        var c = text[start];
        if (char.IsWhiteSpace(c))
        {
            var i = start;
            while (i < end && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            return i;
        }

        Rune.DecodeFromUtf16(text.AsSpan(start, end - start), out var rune, out var consumed);
        if (consumed <= 0)
        {
            consumed = 1;
        }

        if (IsCjk(rune.Value))
        {
            return start + consumed;
        }

        var cursor = start;
        while (cursor < end)
        {
            Rune.DecodeFromUtf16(text.AsSpan(cursor, end - cursor), out var current, out var size);
            if (size <= 0)
            {
                size = 1;
            }

            if (char.IsWhiteSpace(text[cursor]) || IsCjk(current.Value))
            {
                break;
            }

            cursor += size;
            if (current.Value is '-' or '\u2014')
            {
                break;
            }
        }

        return cursor;
    }

    private int FindFittingPrefix(string text, int start, int end, FontSpec font, float maxWidth)
    {
        var fit = start;
        var cursor = start;
        while (cursor < end)
        {
            var next = NextGrapheme(text, Math.Min(cursor, end));
            if (next <= cursor)
            {
                break;
            }

            var width = MeasureSpan(text, font, start, next - start);
            if (width > maxWidth && fit > start)
            {
                break;
            }

            fit = next;
            if (width > maxWidth)
            {
                break;
            }

            cursor = next;
        }

        return fit;
    }

    /// <summary>
    /// 把一个逻辑行拆成字体回退 run。run 是绘制/测量的最小单位：
    /// 同一 run 使用同一 SKFont，交给 HarfBuzz shaping；不同 run 在 x 上累加。
    /// </summary>
    private LineLayout ShapeLine(string text, int start, int length, FontSpec font, FontFace primary)
    {
        if (length <= 0)
        {
            return new LineLayout(new List<RunLayout>(), 0);
        }

        var runs = new List<RunLayout>(2);
        var runStart = start;
        FontFace? runFace = null;

        for (var offset = 0; offset < length;)
        {
            var absolute = start + offset;
            Rune.DecodeFromUtf16(text.AsSpan(absolute, start + length - absolute), out var rune, out var consumed);
            if (consumed <= 0)
            {
                consumed = 1;
            }

            var face = SelectFace(font, rune.Value, primary);
            if (runFace is null)
            {
                runFace = face;
                runStart = absolute;
            }
            else if (!ReferenceEquals(face.Font, runFace.Font))
            {
                AddRun(runs, text, runStart, absolute, runFace);
                runStart = absolute;
                runFace = face;
            }

            offset += consumed;
        }

        if (runFace is not null)
        {
            AddRun(runs, text, runStart, start + length, runFace);
        }

        var x = 0f;
        foreach (var run in runs)
        {
            run.X = x;
            x += run.Width;
        }

        return new LineLayout(runs, x);
    }

    private LineLayout TruncateWithEllipsis(
        string text,
        int start,
        int length,
        FontSpec font,
        FontFace face,
        float maxWidth)
    {
        const string Ellipsis = "\u2026";
        var ellipsisWidth = MeasureSpan(face.Font, Ellipsis);
        if (ellipsisWidth > maxWidth)
        {
            return new LineLayout(new List<RunLayout>(), 0);
        }

        var end = start + length;
        while (end > start)
        {
            end = PreviousGrapheme(text, end);
            if (end < start)
            {
                end = start;
            }

            var width = ShapeLineWidth(text, start, end - start, font, face);
            if (width + ellipsisWidth <= maxWidth)
            {
                break;
            }
        }

        var truncatedText = string.Concat(text.AsSpan(start, end - start), Ellipsis);
        return ShapeLine(truncatedText, 0, truncatedText.Length, font, face);
    }

    private float ShapeLineWidth(string text, int start, int length, FontSpec font, FontFace primary)
    {
        var line = ShapeLine(text, start, length, font, primary);
        return line.Width;
    }

    private void AddRun(List<RunLayout> runs, string text, int start, int end, FontFace face)
    {
        var value = text.Substring(start, end - start);
        var width = MeasureSpan(face.Font, value);
        runs.Add(new RunLayout(value, face.Font, width));
    }

    private float MeasureSpan(string text, FontSpec font, int start, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        var face = GetPrimaryFace(font);
        var width = 0f;
        var runStart = start;
        SKFont? runFont = null;

        for (var offset = 0; offset < length;)
        {
            var absolute = start + offset;
            Rune.DecodeFromUtf16(text.AsSpan(absolute, start + length - absolute), out var rune, out var consumed);
            if (consumed <= 0)
            {
                consumed = 1;
            }

            var runFace = SelectFace(font, rune.Value, face);
            if (runFont is null)
            {
                runFont = runFace.Font;
                runStart = absolute;
            }
            else if (!ReferenceEquals(runFont, runFace.Font))
            {
                width += MeasureSpan(runFont, text.AsSpan(runStart, absolute - runStart));
                runStart = absolute;
                runFont = runFace.Font;
            }

            offset += consumed;
        }

        if (runFont is not null)
        {
            width += MeasureSpan(runFont, text.AsSpan(runStart, start + length - runStart));
        }

        return width;
    }

    /// <summary>
    /// 单个 fallback run 的 shaping 宽度。必须与 DrawShapedText 使用同一个
    /// SKShaper，否则测量和绘制在连字/kerning 上会不一致。
    /// </summary>
    private float MeasureSpan(SKFont font, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return 0;
        }

        var shaper = GetShaper(font.Typeface);
        return shaper.Shape(text.ToString(), font).Width;
    }

    /// <summary>
    /// 按 SKTypeface 缓存 SKShaper；HarfBuzz 对象创建有成本，不能每个字形新建。
    /// Dispose 时统一释放。
    /// </summary>
    private SKShaper GetShaper(SKTypeface typeface)
    {
        if (_shapers.TryGetValue(typeface, out var existing))
        {
            return existing;
        }

        var shaper = new SKShaper(typeface);
        _shapers[typeface] = shaper;
        return shaper;
    }

    private FontFace SelectFace(FontSpec font, int codepoint, FontFace primary)
    {
        if (primary.Typeface.ContainsGlyph(codepoint))
        {
            return primary;
        }

        var key = (PrimaryKey(font), codepoint);
        if (_fallbackFonts.TryGetValue(key, out var cached))
        {
            return cached ?? primary;
        }

        if (_fallbackFonts.Count > MaxFallbackEntries)
        {
            _layouts.Clear();
            foreach (var face in _fallbackFonts.Values)
            {
                if (face is not null && !ReferenceEquals(face.Typeface, primary.Typeface))
                {
                    face.Dispose();
                }
            }

            _fallbackFonts.Clear();
        }

        SKTypeface? typeface = null;
        try
        {
            typeface = _fontManager.MatchCharacter(primary.Typeface.FamilyName, codepoint);
            if (typeface is null || !typeface.ContainsGlyph(codepoint))
            {
                typeface?.Dispose();
                typeface = _fontManager.MatchCharacter(codepoint);
            }

            if (typeface is not null && typeface.ContainsGlyph(codepoint))
            {
                var fallback = CreateFace(typeface, font);
                _fallbackFonts[key] = fallback;
                return fallback;
            }
        }
        catch
        {
            typeface?.Dispose();
            throw;
        }

        typeface?.Dispose();
        _fallbackFonts[key] = null;
        return primary;
    }

    private FontFace GetPrimaryFace(FontSpec font)
    {
        var key = PrimaryKey(font);
        if (_primaryFonts.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var style = new SKFontStyle((int)font.Weight, (int)SKFontStyleWidth.Normal, font.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var family = string.IsNullOrWhiteSpace(font.Family) ? _defaultFamily : font.Family;
        var typeface = _fontManager.MatchFamily(family, style)
            ?? _fontManager.MatchFamily("Segoe UI", style)
            ?? SKTypeface.Default;
        var face = CreateFace(typeface, font);
        _primaryFonts[key] = face;
        return face;
    }

    private static FontFace CreateFace(SKTypeface typeface, FontSpec font)
    {
        var skFont = new SKFont(typeface, MathF.Max(1, font.Size))
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
            Hinting = SKFontHinting.Slight,
        };
        return new FontFace(typeface, skFont);
    }

    private static FontKey PrimaryKey(FontSpec font) =>
        new(string.IsNullOrWhiteSpace(font.Family) ? "Segoe UI" : font.Family, font.Size, font.Weight, font.Italic);

    private static bool IsCjk(int codepoint) =>
        codepoint is (>= 0x1100 and <= 0x11FF) or
            (>= 0x2E80 and <= 0x9FFF) or
            (>= 0xA960 and <= 0xA97F) or
            (>= 0xAC00 and <= 0xD7AF) or
            (>= 0xF900 and <= 0xFAFF) or
            (>= 0xFE30 and <= 0xFE4F) or
            (>= 0xFF00 and <= 0xFFEF) or
            (>= 0x20000 and <= 0x3FFFF);

    private static int NextGrapheme(string text, int index) => TextEditing.NextGrapheme(text, index);

    private static int PreviousGrapheme(string text, int index) => TextEditing.PreviousGrapheme(text, index);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _measurePaint.Dispose();
        _textPaint.Dispose();
        foreach (var face in _primaryFonts.Values)
        {
            face.Dispose();
        }

        foreach (var face in _fallbackFonts.Values)
        {
            if (face is not null)
            {
                face.Dispose();
            }
        }

        foreach (var shaper in _shapers.Values)
        {
            shaper.Dispose();
        }

        _primaryFonts.Clear();
        _fallbackFonts.Clear();
        _layouts.Clear();
        _shapers.Clear();
    }

    private readonly record struct FontKey(string Family, float Size, FontWeight Weight, bool Italic);

    private readonly record struct LayoutKey(
        string Text,
        FontSpec Font,
        float MaxWidth,
        TextWrapMode Wrap,
        TextOverflowMode Overflow,
        int MaxLines);

    private readonly record struct LineRange(int Start, int Length);

    private sealed class FontFace : IDisposable
    {
        public FontFace(SKTypeface typeface, SKFont font)
        {
            Typeface = typeface;
            Font = font;
        }

        public SKTypeface Typeface { get; }
        public SKFont Font { get; }

        public void Dispose() => Font.Dispose();
    }

    private sealed class RunLayout
    {
        public RunLayout(string text, SKFont font, float width)
        {
            Text = text;
            Font = font;
            Width = width;
        }

        public string Text { get; }
        public SKFont Font { get; }
        public float Width { get; }
        public float X { get; set; }
    }

    private sealed class LineLayout
    {
        public LineLayout(List<RunLayout> runs, float width)
        {
            Runs = runs;
            Width = width;
        }

        public List<RunLayout> Runs { get; }
        public float Width { get; }
    }

    private sealed class TextLayout
    {
        public TextLayout(List<LineLayout> lines, float width, float height, float lineHeight, SKFontMetrics metrics)
        {
            Lines = lines;
            Width = width;
            Height = height;
            LineHeight = lineHeight;
            PrimaryMetrics = metrics;
        }

        public List<LineLayout> Lines { get; }
        public float Width { get; }
        public float Height { get; }
        public float LineHeight { get; }
        public SKFontMetrics PrimaryMetrics { get; }
    }
}
