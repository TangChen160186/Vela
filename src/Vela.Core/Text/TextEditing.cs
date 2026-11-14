using System.Globalization;

namespace Vela;

/// <summary>UTF-16 helpers that keep editing operations on grapheme boundaries.</summary>
public static class TextEditing
{
    public static int NextGrapheme(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index >= text.Length)
        {
            return text.Length;
        }

        foreach (var boundary in StringInfo.ParseCombiningCharacters(text))
        {
            if (boundary > index)
            {
                return boundary;
            }
        }

        return text.Length;
    }

    public static int PreviousGrapheme(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index <= 0)
        {
            return 0;
        }

        var previous = 0;
        foreach (var boundary in StringInfo.ParseCombiningCharacters(text))
        {
            if (boundary >= index)
            {
                break;
            }

            previous = boundary;
        }

        return previous;
    }

    public static int NextWord(string text, int index)
    {
        if (index >= text.Length)
        {
            return text.Length;
        }

        var i = index;
        while (i < text.Length && !IsWordChar(text[i]))
        {
            i = NextGrapheme(text, i);
        }

        while (i < text.Length && IsWordChar(text[i]))
        {
            i = NextGrapheme(text, i);
        }

        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i = NextGrapheme(text, i);
        }

        return i;
    }

    public static int PreviousWord(string text, int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        var i = index;
        while (i > 0 && char.IsWhiteSpace(text[Math.Max(0, i - 1)]))
        {
            i = PreviousGrapheme(text, i);
        }

        if (i > 0 && IsWordChar(text[i - 1]))
        {
            while (i > 0 && IsWordChar(text[Math.Max(0, i - 1)]))
            {
                i = PreviousGrapheme(text, i);
            }
        }
        else
        {
            i = PreviousGrapheme(text, i);
        }

        return i;
    }

    public static (int Start, int End) WordAt(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (text.Length == 0)
        {
            return (0, 0);
        }

        var probe = index >= text.Length ? text.Length - 1 : index;
        var start = probe;
        var end = probe;

        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        if (start == end)
        {
            end = Math.Min(text.Length, index);
            start = index;
        }

        return (start, end);
    }

    /// <summary>Returns the line start and end (excluding the newline) containing index.</summary>
    public static (int Start, int End) LineAt(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        var start = index;
        while (start > 0 && text[start - 1] != '\n')
        {
            start--;
        }

        var end = index;
        while (end < text.Length && text[end] != '\n')
        {
            end++;
        }

        if (end > start && end <= text.Length && end - 1 < text.Length && text[end - 1] == '\r')
        {
            end--;
        }

        return (start, end);
    }

    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    public static int CountLines(string text)
    {
        var count = 1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Removes a trailing NUL and normalizes CRLF to LF for editing.</summary>
    public static string Normalize(string? text) => (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\0');
}
