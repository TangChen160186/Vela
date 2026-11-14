using System.Globalization;

namespace Vela;

/// <summary>
/// 单行输入框的跨帧状态。滚动只有水平 ScrollX；多行 TextArea 使用自己的
/// TextAreaState + ScrollState。
/// </summary>
internal sealed class TextFieldState
{
    /// <summary>光标在原始字符串中的 UTF-16 索引。</summary>
    public int Caret { get; set; }

    /// <summary>选区锚点；与 Caret 相等表示无选区。</summary>
    public int SelectionAnchor { get; set; }

    /// <summary>文本水平滚动偏移（光标移出视口时自动调整）。</summary>
    public float ScrollX { get; set; }
    public bool WasFocused { get; set; }
    public double LastActivity { get; set; }
    public Rect TextRect { get; set; }
    public List<EditSnapshot> Undo { get; } = new();
    public List<EditSnapshot> Redo { get; } = new();

    public int SelectionStart => Math.Min(Caret, SelectionAnchor);
    public int SelectionEnd => Math.Max(Caret, SelectionAnchor);
    public bool HasSelection => Caret != SelectionAnchor;

    public void ResetHistory()
    {
        Undo.Clear();
        Redo.Clear();
    }
}

internal readonly record struct EditSnapshot(string Text, int Caret, int Anchor);

internal readonly record struct TextFieldPayload(string? Placeholder, bool Password, int MaxLength);

/// <summary>
/// 单行文本输入控件。编辑逻辑在 TextFieldController，本类负责尺寸和绘制。
///
/// 绘制时使用 displayText（密码会替换为圆点），但 Caret/Selection 仍以原始
/// 字符串为索引；测量、光标 x、选区矩形都通过 ITextEngine 完成，避免字体
/// 变化时光标和文本错位。
/// </summary>
internal sealed class TextFieldWidget : Widget
{
    public static TextFieldWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var padding = element.Style.Padding.IsZero ? theme.InputPadding : element.Style.Padding;
        var naturalHeight = MathF.Max(theme.InputHeight, context.Text.GetLineHeight(element.TextFormat.Font) + padding.VerticalSum + 12);
        var width = constraints.IsTightWidth ? constraints.MinWidth : 220;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, naturalHeight));
    }

    /// <summary>
    /// 绘制输入框。顺序：背景/边框 → 内容裁剪 → 选区 → 文本 → IME preedit
    /// → 光标 → 焦点环。focused 才处理自动水平滚动和闪烁。
    /// </summary>
    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var response = element.Response;
        var payload = element.Payload is TextFieldPayload value ? value : default;
        var disabled = response.Disabled;
        var focused = response.Focused && !disabled;
        var bounds = element.Bounds;
        var radius = CornerRadius.All(element.Style.CornerRadius.IsZero ? theme.RadiusMedium : element.Style.CornerRadius.TopLeft);
        var draw = context.DrawList;

        var background = disabled ? theme.Surface : theme.InputBackground;
        var border = disabled
            ? theme.Border
            : focused
                ? theme.Accent
                : response.Hovered ? theme.BorderStrong : theme.Border;
        draw.FillRect(bounds, new Brush(background), radius);
        draw.StrokeRect(bounds.Deflate(Thickness.All(focused ? 1.25f : 0.5f)), border, focused ? 2 : 1, radius);

        var padding = element.Style.Padding.IsZero ? theme.InputPadding : element.Style.Padding;
        var textRect = bounds.Deflate(padding);
        textRect = new Rect(textRect.X + 1, textRect.Y, MathF.Max(0, textRect.Width - 2), textRect.Height);
        var state = context.Ui.State.GetOrCreate<TextFieldState>(element.Id);
        state.TextRect = textRect;

        var text = element.Text ?? string.Empty;
        var displayText = payload.Password ? MaskText(text) : text;
        var font = element.TextFormat.Font;
        var lineHeight = context.Text.GetLineHeight(font);
        var textY = textRect.Y + ((textRect.Height - lineHeight) * 0.5f);
        var textWidth = context.Text.Measure(displayText, font, new TextMeasureOptions(float.PositiveInfinity, TextWrapMode.NoWrap)).Width;
        var caretX = context.Text.MeasureRange(displayText, font, 0, Math.Clamp(state.Caret, 0, displayText.Length));
        var viewport = textRect.Width;
        var maxScroll = MathF.Max(0, textWidth - viewport + 8);
        var composition = focused && !payload.Password ? context.Input.CompositionText : string.Empty;
        var compositionCursor = Math.Clamp(context.Input.CompositionCursor, 0, composition.Length);

        if (focused)
        {
            if (caretX - state.ScrollX > viewport - 4)
            {
                state.ScrollX = caretX - viewport + 4;
            }
            else if (caretX - state.ScrollX < 0)
            {
                state.ScrollX = MathF.Max(0, caretX - 2);
            }
        }

        state.ScrollX = Math.Clamp(state.ScrollX, 0, maxScroll);

        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(payload.Placeholder))
        {
            draw.Text(textRect, payload.Placeholder, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, theme.TextSubtle);
        }
        else
        {
            draw.PushClip(textRect);
            var origin = textRect.Left - state.ScrollX;
            if (focused && state.HasSelection)
            {
                var start = Math.Clamp(state.SelectionStart, 0, displayText.Length);
                var end = Math.Clamp(state.SelectionEnd, 0, displayText.Length);
                var x1 = origin + context.Text.MeasureRange(displayText, font, 0, start);
                var x2 = origin + context.Text.MeasureRange(displayText, font, 0, end);
                var selectionRect = new Rect(
                    MathF.Min(x1, x2),
                    textY,
                    MathF.Abs(x2 - x1),
                    lineHeight);
                draw.FillRect(selectionRect, new Brush(theme.Selection), CornerRadius.All(2));
            }

            var textDrawRect = new Rect(origin, textY, MathF.Max(textWidth + 8, viewport + state.ScrollX + 8), lineHeight);
            draw.Text(textDrawRect, displayText, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, disabled ? theme.TextSubtle : element.Foreground);

            var caretDrawX = origin + caretX;
            if (composition.Length > 0)
            {
                var compositionWidth = context.Text.Measure(composition, font, new TextMeasureOptions(float.PositiveInfinity, TextWrapMode.NoWrap)).Width;
                var compositionRect = new Rect(caretDrawX, textY, compositionWidth + 6, lineHeight);
                draw.Text(compositionRect, composition, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, theme.Text);
                draw.Line(new Vec2(caretDrawX, textY + lineHeight - 1), new Vec2(caretDrawX + compositionWidth, textY + lineHeight - 1), theme.Accent, 1);
                caretDrawX += context.Text.MeasureRange(composition, font, 0, compositionCursor);
            }

            if (focused && IsCaretVisible(context, state))
            {
                var caretRect = new Rect(caretDrawX, textY + 2, 1.5f, MathF.Max(1, lineHeight - 4));
                draw.FillRect(caretRect, new Brush(theme.Accent), CornerRadius.All(1));
            }

            draw.PopClip();
        }

        if (focused && response.FocusVisible)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(3)), theme.FocusRing.WithAlpha(0.45f), 1, CornerRadius.All(theme.RadiusMedium + 2));
        }
    }

    private static bool IsCaretVisible(PaintContext context, TextFieldState state)
    {
        var elapsed = context.Input.Time - state.LastActivity;
        return elapsed < 0.55 || (context.Input.Time * 1.7 % 2) < 1.05;
    }

    private static string MaskText(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return string.Create(text.Length, 0, static (span, _) => span.Fill('\u2022'));
    }
}

/// <summary>
/// 单行编辑控制器：把输入事件转成字符串/Caret/Anchor/Undo 的变化。
/// 与 TextArea 共用 TextEditing 的 grapheme/word 边界工具。
/// </summary>
internal static class TextFieldController
{
    public static void Update(
        Ui ui,
        Element element,
        TextFieldState state,
        ref string value,
        in Response response,
        bool disabled,
        bool password,
        int maxLength)
    {
        value ??= string.Empty;
        state.Caret = Math.Clamp(state.Caret, 0, value.Length);
        state.SelectionAnchor = Math.Clamp(state.SelectionAnchor, 0, value.Length);

        if (disabled)
        {
            state.WasFocused = false;
            return;
        }

        if (!response.Focused)
        {
            state.WasFocused = false;
            return;
        }

        var input = ui.Input;
        if (!state.WasFocused)
        {
            state.WasFocused = true;
            state.Caret = value.Length;
            state.SelectionAnchor = value.Length;
            state.LastActivity = input.Time;
        }

        var displayText = password ? MaskTextForEditing(value) : value;
        TextRectHitTest(ui, element.TextFormat.Font, state, displayText, ref value, response, input);

        if (!response.Focused)
        {
            return;
        }

        ProcessShortcuts(ui, element, state, ref value, input, maxLength);
        ProcessKeyboard(ui, state, ref value, input, maxLength);
        ProcessText(ui, state, ref value, input, maxLength);
        element.Text = value;
    }

    private static string MaskTextForEditing(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return string.Create(text.Length, 0, static (span, _) => span.Fill('\u2022'));
    }

    private static void TextRectHitTest(Ui ui, FontSpec font, TextFieldState state, string displayText, ref string value, in Response response, FrameInput input)
    {
        if (state.TextRect.Width <= 0)
        {
            return;
        }

        if (response.DoubleClicked)
        {
            var (start, end) = TextEditing.WordAt(value, state.Caret);
            state.SelectionAnchor = start;
            state.Caret = end;
            state.LastActivity = input.Time;
            EnsureCaretVisible(ui, font, state, displayText);
            return;
        }

        if (response.Pressed || (response.Active && input.IsButtonDown(PointerButton.Left)))
        {
            var x = input.PointerPosition.X - state.TextRect.Left + state.ScrollX;
            var index = ui.TextEngine.HitTestTextPosition(displayText, font, x);
            index = Math.Clamp(index, 0, value.Length);
            state.Caret = index;
            if (!input.Shift)
            {
                state.SelectionAnchor = index;
            }

            state.LastActivity = input.Time;
            EnsureCaretVisible(ui, font, state, displayText);
        }
    }

    private static void ProcessShortcuts(Ui ui, Element element, TextFieldState state, ref string value, FrameInput input, int maxLength)
    {
        if (input.Ctrl && input.WasKeyPressed(Key.A))
        {
            state.SelectionAnchor = 0;
            state.Caret = value.Length;
            state.LastActivity = input.Time;
        }

        if (input.Ctrl && input.WasKeyPressed(Key.C))
        {
            if (state.HasSelection)
            {
                ui.Clipboard.SetText(value[state.SelectionStart..state.SelectionEnd]);
            }
        }

        if (input.Ctrl && input.WasKeyPressed(Key.X))
        {
            if (state.HasSelection)
            {
                ui.Clipboard.SetText(value[state.SelectionStart..state.SelectionEnd]);
                PushUndo(state, value);
                value = RemoveSelection(value, state);
                state.LastActivity = input.Time;
            }
        }

        if (input.Ctrl && input.WasKeyPressed(Key.V))
        {
            var pasted = ui.Clipboard.GetText();
            if (!string.IsNullOrEmpty(pasted))
            {
                Insert(ui, state, ref value, NormalizeSingleLine(pasted), maxLength, input.Time);
            }
        }

        if (input.Ctrl && !input.Shift && input.WasKeyPressed(Key.Z))
        {
            Undo(ui, state, ref value, input.Time);
        }

        if (input.Ctrl && (input.WasKeyPressed(Key.Y) || (input.Shift && input.WasKeyPressed(Key.Z))))
        {
            Redo(ui, state, ref value, input.Time);
        }
    }

    private static void ProcessKeyboard(Ui ui, TextFieldState state, ref string value, FrameInput input, int maxLength)
    {
        var shift = input.Shift;
        if (input.WasKeyPressed(Key.Left) || input.WasKeyRepeated(Key.Left))
        {
            MoveCaret(ui, state, value, -1, input.Ctrl, shift);
        }

        if (input.WasKeyPressed(Key.Right) || input.WasKeyRepeated(Key.Right))
        {
            MoveCaret(ui, state, value, 1, input.Ctrl, shift);
        }

        if (input.WasKeyPressed(Key.Home))
        {
            MoveCaretTo(state, 0, shift);
        }

        if (input.WasKeyPressed(Key.End))
        {
            MoveCaretTo(state, value.Length, shift);
        }

        if (input.WasKeyPressed(Key.Backspace) || input.WasKeyRepeated(Key.Backspace))
        {
            if (state.HasSelection)
            {
                PushUndo(state, value);
                value = RemoveSelection(value, state);
            }
            else if (state.Caret > 0)
            {
                PushUndo(state, value);
                var start = input.Ctrl ? TextEditing.PreviousWord(value, state.Caret) : TextEditing.PreviousGrapheme(value, state.Caret);
                value = value.Remove(start, state.Caret - start);
                state.Caret = start;
                state.SelectionAnchor = start;
            }

            state.LastActivity = input.Time;
        }

        if (input.WasKeyPressed(Key.Delete) || input.WasKeyRepeated(Key.Delete))
        {
            if (state.HasSelection)
            {
                PushUndo(state, value);
                value = RemoveSelection(value, state);
            }
            else if (state.Caret < value.Length)
            {
                PushUndo(state, value);
                var end = input.Ctrl ? TextEditing.NextWord(value, state.Caret) : TextEditing.NextGrapheme(value, state.Caret);
                value = value.Remove(state.Caret, end - state.Caret);
            }

            state.LastActivity = input.Time;
        }

        _ = ui;
    }

    private static void ProcessText(Ui ui, TextFieldState state, ref string value, FrameInput input, int maxLength)
    {
        if (input.TextInput.Length == 0 || input.Ctrl || input.Alt || input.Super)
        {
            return;
        }

        var text = NormalizeSingleLine(input.TextInput);
        if (text.Length > 0)
        {
            Insert(ui, state, ref value, text, maxLength, input.Time);
        }
    }

    private static void Insert(Ui ui, TextFieldState state, ref string value, string text, int maxLength, double time)
    {
        if (text.Length == 0)
        {
            return;
        }

        PushUndo(state, value);
        var start = state.SelectionStart;
        var end = state.SelectionEnd;
        var available = maxLength > 0 ? Math.Max(0, maxLength - (value.Length - (end - start))) : int.MaxValue;
        if (available <= 0)
        {
            return;
        }

        if (text.Length > available)
        {
            text = text[..available];
        }

        value = string.Concat(value.AsSpan(0, start), text, value.AsSpan(end));
        state.Caret = start + text.Length;
        state.SelectionAnchor = state.Caret;
        state.LastActivity = time;
    }

    private static void MoveCaret(Ui ui, TextFieldState state, string value, int direction, bool byWord, bool extend)
    {
        var index = direction < 0
            ? byWord ? TextEditing.PreviousWord(value, state.Caret) : TextEditing.PreviousGrapheme(value, state.Caret)
            : byWord ? TextEditing.NextWord(value, state.Caret) : TextEditing.NextGrapheme(value, state.Caret);
        MoveCaretTo(state, index, extend);
    }

    private static void MoveCaretTo(TextFieldState state, int index, bool extend)
    {
        state.Caret = Math.Clamp(index, 0, int.MaxValue);
        if (!extend)
        {
            state.SelectionAnchor = state.Caret;
        }
    }

    private static void EnsureCaretVisible(Ui ui, FontSpec font, TextFieldState state, string displayText)
    {
        var caretX = ui.TextEngine.MeasureRange(displayText, font, 0, Math.Clamp(state.Caret, 0, displayText.Length));
        var viewport = state.TextRect.Width;
        if (viewport <= 0)
        {
            return;
        }

        if (caretX - state.ScrollX > viewport - 4)
        {
            state.ScrollX = caretX - viewport + 4;
        }
        else if (caretX - state.ScrollX < 0)
        {
            state.ScrollX = MathF.Max(0, caretX);
        }
    }

    private static string RemoveSelection(string value, TextFieldState state)
    {
        if (!state.HasSelection)
        {
            return value;
        }

        var start = state.SelectionStart;
        var end = state.SelectionEnd;
        var result = string.Concat(value.AsSpan(0, start), value.AsSpan(end));
        state.Caret = start;
        state.SelectionAnchor = start;
        return result;
    }

    private static string NormalizeSingleLine(string text) =>
        text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

    private static void PushUndo(TextFieldState state, string text)
    {
        state.Undo.Add(new EditSnapshot(text, state.Caret, state.SelectionAnchor));
        if (state.Undo.Count > 100)
        {
            state.Undo.RemoveAt(0);
        }

        state.Redo.Clear();
    }

    private static void Undo(Ui ui, TextFieldState state, ref string value, double time)
    {
        if (state.Undo.Count == 0)
        {
            return;
        }

        state.Redo.Add(new EditSnapshot(value, state.Caret, state.SelectionAnchor));
        var snapshot = state.Undo[^1];
        state.Undo.RemoveAt(state.Undo.Count - 1);
        value = snapshot.Text;
        state.Caret = Math.Clamp(snapshot.Caret, 0, value.Length);
        state.SelectionAnchor = Math.Clamp(snapshot.Anchor, 0, value.Length);
        state.LastActivity = time;
    }

    private static void Redo(Ui ui, TextFieldState state, ref string value, double time)
    {
        if (state.Redo.Count == 0)
        {
            return;
        }

        state.Undo.Add(new EditSnapshot(value, state.Caret, state.SelectionAnchor));
        var snapshot = state.Redo[^1];
        state.Redo.RemoveAt(state.Redo.Count - 1);
        value = snapshot.Text;
        state.Caret = Math.Clamp(snapshot.Caret, 0, value.Length);
        state.SelectionAnchor = Math.Clamp(snapshot.Anchor, 0, value.Length);
        state.LastActivity = time;
    }
}



