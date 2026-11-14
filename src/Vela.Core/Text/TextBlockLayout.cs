namespace Vela;

/// <summary>
/// 一条可视行。Start/Length 是原字符串的 UTF-16 索引，不是渲染后的字形索引；
/// 因此选区、光标和命中测试始终能映射回应用持有的原始文本。
/// </summary>
public readonly record struct TextLineRange(int Start, int Length, float Width)
{
    public int End => Start + Length;
}

/// <summary>
/// 可编辑多行文本的可视行布局。
///
/// 关键约束：
/// <list type="bullet">
/// <item>所有范围都基于原始 UTF-16 字符串，不做字符串复制/替换；</item>
/// <item>显式 \n 优先，其次在空白处换行，最后允许在 grapheme 边界硬断；</item>
/// <item>宽度来自 ITextEngine（会包含 HarfBuzz shaping 的宽度）；</item>
/// <item>该类型只描述几何，不保存光标/选区/撤销栈，那些在 TextAreaState。</item>
/// </list>
/// </summary>
public sealed class TextBlockLayout
{
    private TextBlockLayout(List<TextLineRange> lines, float lineHeight, float width)
    {
        Lines = lines;
        LineHeight = lineHeight;
        Width = width;
    }

    public IReadOnlyList<TextLineRange> Lines { get; }
    public float LineHeight { get; }
    public float Width { get; }
    public float Height => MathF.Max(LineHeight, Lines.Count * LineHeight);
    public int LineCount => Lines.Count;

    /// <summary>
    /// 构建可视行。maxWidth 是内容区宽度；wrap=false 时每个段落只产生一行。
    /// 空文本/末尾换行也会产生一行，保证光标有可放置的视觉行。
    /// </summary>
    public static TextBlockLayout Build(
        string text,
        FontSpec font,
        float maxWidth,
        ITextEngine engine,
        bool wrap = true)
    {
        var lines = new List<TextLineRange>(Math.Max(1, TextEditing.CountLines(text)));
        var lineHeight = engine.GetLineHeight(font);
        var widest = 0f;
        var start = 0;
        while (start <= text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline >= 0 ? newline : text.Length;
            BuildParagraph(text, start, end, font, maxWidth, wrap, engine, lines);
            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
            if (start == text.Length)
            {
                lines.Add(new TextLineRange(start, 0, 0));
                break;
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(new TextLineRange(0, 0, 0));
        }

        foreach (var line in lines)
        {
            widest = MathF.Max(widest, line.Width);
        }

        return new TextBlockLayout(lines, lineHeight, widest);
    }

    /// <summary>
    /// 返回光标所在的可视行。边界规则：行尾索引属于当前行；
    /// 这样光标停在换行前时，向上/向下移动的行为稳定。
    /// </summary>
    public int LineIndexForCaret(int index)
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            if (index >= line.Start && index <= line.End)
            {
                return i;
            }
        }

        return Math.Max(0, Lines.Count - 1);
    }

    public int LineIndexAtY(float y)
    {
        if (Lines.Count == 0 || LineHeight <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)MathF.Floor(y / LineHeight), 0, Lines.Count - 1);
    }

    /// <summary>
    /// 点击命中：先按 y 找可视行，再在行内按 x 找最接近的字符边界。
    /// 行宽之外会夹到行尾，避免点到空白时把光标放到不存在的列。
    /// </summary>
    public int IndexAt(float x, float y, string text, FontSpec font, ITextEngine engine)
    {
        var lineIndex = LineIndexAtY(y);
        var line = Lines[lineIndex];
        var localX = Math.Clamp(x, 0, MathF.Max(0, line.Width + 8));
        var within = engine.HitTestTextPosition(text, font, localX, line.Start, line.Length);
        return Math.Clamp(within, line.Start, line.End);
    }

    public float CaretX(string text, FontSpec font, ITextEngine engine, int caret)
    {
        var lineIndex = LineIndexForCaret(caret);
        var line = Lines[lineIndex];
        var local = Math.Clamp(caret - line.Start, 0, line.Length);
        return engine.MeasureRange(text, font, line.Start, local);
    }

    public (int Line, float X)? CaretPosition(string text, FontSpec font, ITextEngine engine, int caret)
    {
        if (Lines.Count == 0)
        {
            return null;
        }

        var lineIndex = LineIndexForCaret(caret);
        var line = Lines[lineIndex];
        var local = Math.Clamp(caret - line.Start, 0, line.Length);
        return (lineIndex, engine.MeasureRange(text, font, line.Start, local));
    }

    /// <summary>
    /// 段落换行。贪心扫描 grapheme，记录最近的空白断点；
    /// 超过宽度时优先在空白断，否则在 grapheme 边界硬断，保证有进展。
    /// 输出行的长度不含行尾换行/被跳过的空白，但 Start 仍指向原文索引。
    /// </summary>
    private static void BuildParagraph(
        string text,
        int start,
        int end,
        FontSpec font,
        float maxWidth,
        bool wrap,
        ITextEngine engine,
        List<TextLineRange> output)
    {
        if (start == end)
        {
            output.Add(new TextLineRange(start, 0, 0));
            return;
        }

        if (!wrap || !float.IsFinite(maxWidth) || maxWidth <= 0)
        {
            var width = engine.MeasureRange(text, font, start, end - start);
            output.Add(new TextLineRange(start, end - start, width));
            return;
        }

        var lineStart = start;
        var cursor = start;
        var lastBreak = -1;

        while (cursor < end)
        {
            var next = TextEditing.NextGrapheme(text, cursor);
            if (next <= cursor)
            {
                next = cursor + 1;
            }

            if (char.IsWhiteSpace(text, cursor))
            {
                lastBreak = cursor;
            }

            cursor = Math.Min(next, end);
            var width = engine.MeasureRange(text, font, lineStart, cursor - lineStart);
            if (width <= maxWidth || cursor <= lineStart + 1)
            {
                continue;
            }

            var breakAt = lastBreak > lineStart ? lastBreak : TextEditing.PreviousGrapheme(text, cursor);
            if (breakAt <= lineStart)
            {
                breakAt = cursor;
            }

            var commitEnd = breakAt;
            while (commitEnd > lineStart && char.IsWhiteSpace(text, commitEnd - 1))
            {
                commitEnd--;
            }

            var commitWidth = engine.MeasureRange(text, font, lineStart, commitEnd - lineStart);
            output.Add(new TextLineRange(lineStart, commitEnd - lineStart, commitWidth));
            lineStart = breakAt;
            while (lineStart < end && char.IsWhiteSpace(text, lineStart))
            {
                lineStart++;
            }

            cursor = lineStart;
            lastBreak = -1;
        }

        if (lineStart < end || output.Count == 0)
        {
            var width = engine.MeasureRange(text, font, lineStart, end - lineStart);
            output.Add(new TextLineRange(lineStart, end - lineStart, width));
        }
    }
}
