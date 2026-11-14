namespace Vela;

/// <summary>
/// TextArea 的跨帧状态。注意它和 ScrollState 共用同一个 Element.Id，
/// 但 StateStore 按 (Id, 类型) 索引，因此两者不会互相覆盖。
/// </summary>
internal sealed class TextAreaState
{
    /// <summary>光标在原始字符串中的 UTF-16 索引。</summary>
    public int Caret { get; set; }

    /// <summary>选区锚点；与 Caret 相等表示没有选区。</summary>
    public int Anchor { get; set; }

    /// <summary>上下移动时保持的目标 x；-1 表示重新计算。</summary>
    public float PreferredX { get; set; } = -1;

    public bool WasFocused { get; set; }

    /// <summary>最后一次编辑时间，用于光标闪烁。</summary>
    public double LastActivity { get; set; }

    /// <summary>上一帧/本帧的内容矩形，用于点击命中（因此可能有一帧滞后）。</summary>
    public Rect ContentRect { get; set; }

    /// <summary>上次 Arrange 生成的可视行布局。</summary>
    public TextBlockLayout? Layout { get; set; }

    public List<EditSnapshot> Undo { get; } = new();
    public List<EditSnapshot> Redo { get; } = new();
}

internal readonly record struct TextAreaPayload(string? Placeholder, bool ReadOnly, int MaxLength);

/// <summary>
/// 多行文本控件。
///
/// 读取顺序建议：
/// <list type="number">
/// <item>Measure：默认高度/宽度和 padding；</item>
/// <item>Arrange：用实际内容宽度构建 TextBlockLayout，并同步 ScrollState；</item>
/// <item>PaintContent：先裁剪，再画选区/文本/IME preedit/光标/滚动条；</item>
/// <item>TextAreaController：处理鼠标、键盘、剪贴板、撤销和输入。</item>
/// </list>
///
/// 编辑状态在 TextAreaState，滚动状态在 ScrollState；控件本身无状态。
/// </summary>
internal sealed class TextAreaWidget : Widget
{
    public static TextAreaWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var padding = element.Style.Padding.IsZero ? theme.InputPadding : element.Style.Padding;
        var naturalHeight = MathF.Max(120, context.Text.GetLineHeight(element.TextFormat.Font) * 5 + padding.VerticalSum);
        var width = constraints.IsTightWidth ? constraints.MinWidth : 260;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, naturalHeight));
    }

    /// <summary>
    /// 使用最终内容宽度重新做文本行布局。编辑发生在 Build 阶段，因此这里
    /// 能看到本帧最新的字符串；之后 Paint 直接复用 state.Layout。
    /// 同时把滚动内容高度同步给 ScrollState，保证滚轮/滚动条在下一帧正确。
    /// </summary>
    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var state = context.Ui.State.GetOrCreate<TextAreaState>(element.Id);
        var padding = element.Style.Padding.IsZero ? context.Theme.InputPadding : element.Style.Padding;
        var content = bounds.Deflate(padding);
        state.ContentRect = content;
        var layout = TextBlockLayout.Build(element.Text ?? string.Empty, element.TextFormat.Font, content.Width, context.Text);
        state.Layout = layout;

        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        scroll.SetMetrics(layout.Height, content.Height);
        scroll.Tick(context.DeltaTime);
        element.IsScrollable = scroll.IsScrollable;
        element.ScrollOffset = scroll.Offset;
        element.ScrollContentHeight = scroll.ContentHeight;
        element.ScrollViewportHeight = scroll.ViewportHeight;

        var caretPosition = layout.CaretPosition(element.Text ?? string.Empty, element.TextFormat.Font, context.Text, state.Caret);
        if (caretPosition is { } position)
        {
            var top = position.Line * layout.LineHeight;
            var bottom = top + layout.LineHeight;
            if (top < scroll.Target)
            {
                scroll.ScrollTo(MathF.Max(0, top - 2));
            }
            else if (bottom > scroll.Target + content.Height)
            {
                scroll.ScrollTo(MathF.Max(0, bottom - content.Height + 2));
            }
        }

    }

    /// <summary>
    /// 绘制顺序：输入框背景/边框 → 内容裁剪 → 选区 → 文本行 → IME preedit
    /// → 光标 → 滚动条 → 焦点环。只绘制与 content 相交的可视行。
    /// </summary>
    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var state = context.Ui.State.GetOrCreate<TextAreaState>(element.Id);
        var payload = element.Payload is TextAreaPayload value ? value : default;
        var response = element.Response;
        var disabled = response.Disabled;
        var focused = response.Focused && !disabled && !payload.ReadOnly;
        var bounds = element.Bounds;
        var radius = CornerRadius.All(element.Style.CornerRadius.IsZero ? theme.RadiusMedium : element.Style.CornerRadius.TopLeft);
        var draw = context.DrawList;
        var borderColor = disabled
            ? theme.Border
            : focused ? theme.Accent : response.Hovered ? theme.BorderStrong : theme.Border;
        draw.FillRect(bounds, new Brush(disabled ? theme.Surface : theme.InputBackground), radius);
        draw.StrokeRect(bounds.Deflate(Thickness.All(focused ? 1.25f : 0.5f)), borderColor, focused ? 2 : 1, radius);

        var padding = element.Style.Padding.IsZero ? theme.InputPadding : element.Style.Padding;
        var content = bounds.Deflate(padding);
        var layout = state.Layout ?? TextBlockLayout.Build(element.Text ?? string.Empty, element.TextFormat.Font, content.Width, context.Text);
        state.ContentRect = content;
        state.Layout = layout;
        var scroll = context.Ui.State.GetOrCreate<ScrollState>(element.Id);
        var text = element.Text ?? string.Empty;
        var drawTop = content.Top - scroll.Offset;

        draw.PushClip(content);
        if (text.Length == 0 && !string.IsNullOrEmpty(payload.Placeholder))
        {
            draw.Text(content, payload.Placeholder, element.TextFormat with { VerticalAlignment = VerticalAlignment.Top }, theme.TextSubtle);
        }
        else
        {
            var selectionStart = Math.Min(state.Caret, state.Anchor);
            var selectionEnd = Math.Max(state.Caret, state.Anchor);
            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                var y = drawTop + i * layout.LineHeight;
                if (y + layout.LineHeight < content.Top || y > content.Bottom)
                {
                    continue;
                }

                if (focused && selectionStart != selectionEnd && selectionStart < line.End && selectionEnd > line.Start)
                {
                    var from = Math.Max(selectionStart, line.Start);
                    var to = Math.Min(selectionEnd, line.End);
                    var x1 = context.Text.MeasureRange(text, element.TextFormat.Font, line.Start, from - line.Start);
                    var x2 = context.Text.MeasureRange(text, element.TextFormat.Font, line.Start, to - line.Start);
                    if (selectionEnd > line.End && line.End < text.Length && text[line.End] == '\n')
                    {
                        x2 += 6;
                    }

                    draw.FillRect(new Rect(content.Left + x1, y, MathF.Max(1, x2 - x1), layout.LineHeight), new Brush(theme.Selection), CornerRadius.All(2));
                }

                if (line.Length > 0)
                {
                    draw.Text(
                        new Rect(content.Left, y, MathF.Max(1, line.Width + 2), layout.LineHeight),
                        text.Substring(line.Start, line.Length),
                        element.TextFormat with { VerticalAlignment = VerticalAlignment.Top, Wrap = TextWrapMode.NoWrap },
                        disabled ? theme.TextSubtle : element.Foreground);
                }
            }

            if (focused)
            {
                var position = layout.CaretPosition(text, element.TextFormat.Font, context.Text, state.Caret);
                if (position is { } caret)
                {
                    var caretX = content.Left + caret.X;
                    var caretY = drawTop + caret.Line * layout.LineHeight;
                    var composition = context.Input.CompositionText;
                    if (composition.Length > 0)
                    {
                        var compositionWidth = context.Text.Measure(composition, element.TextFormat.Font, new TextMeasureOptions(float.PositiveInfinity, TextWrapMode.NoWrap)).Width;
                        draw.Text(
                            new Rect(caretX, caretY, compositionWidth + 8, layout.LineHeight),
                            composition,
                            element.TextFormat with { VerticalAlignment = VerticalAlignment.Top, Wrap = TextWrapMode.NoWrap },
                            theme.Text);
                        draw.Line(new Vec2(caretX, caretY + layout.LineHeight - 1), new Vec2(caretX + compositionWidth, caretY + layout.LineHeight - 1), theme.Accent, 1);
                        caretX += context.Text.MeasureRange(composition, element.TextFormat.Font, 0, Math.Clamp(context.Input.CompositionCursor, 0, composition.Length));
                    }

                    if (IsCaretVisible(context, state))
                    {
                        draw.FillRect(new Rect(caretX, caretY + 1, 1.5f, MathF.Max(1, layout.LineHeight - 3)), new Brush(theme.Accent), CornerRadius.All(1));
                    }
                }
            }
        }

        draw.PopClip();

        if (scroll.IsScrollable)
        {
            var track = new Rect(content.Right - 7, content.Top, 7, content.Height);
            var thumbHeight = MathF.Max(24, track.Height * Math.Clamp(scroll.ViewportHeight / MathF.Max(1, scroll.ContentHeight), 0, 1));
            var thumbY = track.Top + (scroll.Offset / MathF.Max(1, scroll.MaxOffset) * MathF.Max(0, track.Height - thumbHeight));
            draw.FillRect(new Rect(track.X + 2, thumbY, 3, thumbHeight), new Brush(theme.Scrollbar), CornerRadius.All(2));
        }

        if (focused && response.FocusVisible)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(3)), theme.FocusRing.WithAlpha(0.4f), 1, CornerRadius.All(theme.RadiusMedium + 2));
        }
    }

    private static bool IsCaretVisible(PaintContext context, TextAreaState state)
    {
        var elapsed = context.Input.Time - state.LastActivity;
        return elapsed < 0.55 || (context.Input.Time * 1.7 % 2) < 1.05;
    }
}

/// <summary>
/// TextArea 的编辑控制器。它不负责绘制，只把输入/响应转换为
/// 字符串、Caret、Anchor 和 Undo/Redo 的变化。
///
/// 所有位置都是原始字符串的 UTF-16 索引；grapheme 移动由 TextEditing 统一处理。
/// </summary>
internal static class TextAreaController
{
    public static void Update(
        Ui ui,
        Element element,
        TextAreaState state,
        ref string value,
        in Response response,
        bool readOnly,
        int maxLength)
    {
        value ??= string.Empty;
        state.Caret = Math.Clamp(state.Caret, 0, value.Length);
        state.Anchor = Math.Clamp(state.Anchor, 0, value.Length);
        if (!response.Focused)
        {
            state.WasFocused = false;
            return;
        }

        // 首次获得焦点：默认把光标放到末尾。随后本次 Update 中的鼠标点击
        // 仍可在同一帧覆盖这个位置，因此点击进入时不会总是跳到末尾。
        var input = ui.Input;
        if (!state.WasFocused)
        {
            state.WasFocused = true;
            state.Caret = value.Length;
            state.Anchor = value.Length;
            state.PreferredX = -1;
            state.LastActivity = input.Time;
        }

        if (readOnly)
        {
            ProcessReadOnlyNavigation(ui, element.TextFormat.Font, state, value, input);
        }
        else
        {
            ProcessMouse(ui, element.TextFormat.Font, state, value, response, input);
            ProcessShortcuts(ui, state, ref value, input, maxLength);
            ProcessKeyboard(ui, element.TextFormat.Font, state, ref value, input, maxLength);
            ProcessText(ui, state, ref value, input, maxLength);
        }

        element.Text = value;
    }

    private static void ProcessReadOnlyNavigation(Ui ui, FontSpec font, TextAreaState state, string value, FrameInput input)
    {
        if (input.WasKeyPressed(Key.Home))
        {
            state.Caret = 0;
            state.Anchor = 0;
        }

        if (input.WasKeyPressed(Key.End))
        {
            state.Caret = value.Length;
            state.Anchor = value.Length;
        }

        MoveHorizontal(state, value, input, extend: input.Shift);
        if (input.Ctrl && input.WasKeyPressed(Key.C) && state.Caret != state.Anchor)
        {
            ui.Clipboard.SetText(value[Math.Min(state.Caret, state.Anchor)..Math.Max(state.Caret, state.Anchor)]);
        }
    }

    private static void ProcessMouse(Ui ui, FontSpec font, TextAreaState state, string value, in Response response, FrameInput input)
    {
        if (state.Layout is null || state.ContentRect.Width <= 0 || !state.ContentRect.Contains(input.PointerPosition))
        {
            return;
        }

        if (response.DoubleClicked)
        {
            var (start, end) = TextEditing.WordAt(value, state.Caret);
            state.Anchor = start;
            state.Caret = end;
            state.LastActivity = input.Time;
            return;
        }

        if (response.Pressed || (response.Active && input.IsButtonDown(PointerButton.Left)))
        {
            var scroll = ui.State.GetOrCreate<ScrollState>(response.Id);
            var x = input.PointerPosition.X - state.ContentRect.Left;
            var y = input.PointerPosition.Y - state.ContentRect.Top + scroll.Offset;
            var index = state.Layout.IndexAt(x, y, value, font, ui.TextEngine);
            state.Caret = index;
            if (!input.Shift)
            {
                state.Anchor = index;
            }

            state.PreferredX = -1;
            state.LastActivity = input.Time;
        }
    }

    private static void ProcessShortcuts(Ui ui, TextAreaState state, ref string value, FrameInput input, int maxLength)
    {
        if (input.Ctrl && input.WasKeyPressed(Key.A))
        {
            state.Anchor = 0;
            state.Caret = value.Length;
        }

        if (input.Ctrl && input.WasKeyPressed(Key.C) && state.Caret != state.Anchor)
        {
            ui.Clipboard.SetText(value[Math.Min(state.Caret, state.Anchor)..Math.Max(state.Caret, state.Anchor)]);
        }

        if (input.Ctrl && input.WasKeyPressed(Key.X) && state.Caret != state.Anchor)
        {
            var start = Math.Min(state.Caret, state.Anchor);
            var end = Math.Max(state.Caret, state.Anchor);
            ui.Clipboard.SetText(value[start..end]);
            PushUndo(state, value);
            value = string.Concat(value.AsSpan(0, start), value.AsSpan(end));
            state.Caret = state.Anchor = start;
        }

        if (input.Ctrl && input.WasKeyPressed(Key.V) && !string.IsNullOrEmpty(ui.Clipboard.GetText()))
        {
            Insert(ui, state, ref value, Normalize(ui.Clipboard.GetText()!), maxLength, input.Time);
        }

        if (input.Ctrl && !input.Shift && input.WasKeyPressed(Key.Z))
        {
            Undo(state, ref value);
        }

        if (input.Ctrl && (input.WasKeyPressed(Key.Y) || (input.Shift && input.WasKeyPressed(Key.Z))))
        {
            Redo(state, ref value);
        }
    }

    private static void ProcessKeyboard(Ui ui, FontSpec font, TextAreaState state, ref string value, FrameInput input, int maxLength)
    {
        var shift = input.Shift;
        if (input.WasKeyPressed(Key.Left) || input.WasKeyRepeated(Key.Left))
        {
            MoveHorizontal(state, value, input, shift);
        }
        else if (input.WasKeyPressed(Key.Right) || input.WasKeyRepeated(Key.Right))
        {
            MoveHorizontal(state, value, input, shift);
        }
        else if (input.WasKeyPressed(Key.Up) || input.WasKeyRepeated(Key.Up))
        {
            MoveVertical(ui, font, state, value, -1, shift);
        }
        else if (input.WasKeyPressed(Key.Down) || input.WasKeyRepeated(Key.Down))
        {
            MoveVertical(ui, font, state, value, 1, shift);
        }
        else if (input.WasKeyPressed(Key.Home))
        {
            var line = state.Layout?.LineIndexForCaret(state.Caret) ?? 0;
            var target = input.Ctrl ? 0 : state.Layout?.Lines[line].Start ?? 0;
            SetCaret(state, target, shift);
        }
        else if (input.WasKeyPressed(Key.End))
        {
            var line = state.Layout?.LineIndexForCaret(state.Caret) ?? 0;
            var target = input.Ctrl ? value.Length : state.Layout?.Lines[line].End ?? value.Length;
            SetCaret(state, target, shift);
        }
        else if (input.WasKeyPressed(Key.Backspace) || input.WasKeyRepeated(Key.Backspace))
        {
            if (state.Caret != state.Anchor)
            {
                PushUndo(state, value);
                value = RemoveSelection(state, value);
            }
            else if (state.Caret > 0)
            {
                PushUndo(state, value);
                var start = input.Ctrl ? TextEditing.PreviousWord(value, state.Caret) : TextEditing.PreviousGrapheme(value, state.Caret);
                value = value.Remove(start, state.Caret - start);
                state.Caret = state.Anchor = start;
            }

            state.LastActivity = input.Time;
        }
        else if (input.WasKeyPressed(Key.Delete) || input.WasKeyRepeated(Key.Delete))
        {
            if (state.Caret != state.Anchor)
            {
                PushUndo(state, value);
                value = RemoveSelection(state, value);
            }
            else if (state.Caret < value.Length)
            {
                PushUndo(state, value);
                var end = input.Ctrl ? TextEditing.NextWord(value, state.Caret) : TextEditing.NextGrapheme(value, state.Caret);
                value = value.Remove(state.Caret, end - state.Caret);
            }

            state.LastActivity = input.Time;
        }
        else if (input.WasKeyPressed(Key.Enter))
        {
            Insert(ui, state, ref value, "\n", maxLength, input.Time);
        }

    }

    private static void MoveHorizontal(TextAreaState state, string value, FrameInput input, bool extend)
    {
        var index = input.Ctrl
            ? (input.WasKeyPressed(Key.Left) || input.WasKeyRepeated(Key.Left)
                ? TextEditing.PreviousWord(value, state.Caret)
                : TextEditing.NextWord(value, state.Caret))
            : (input.WasKeyPressed(Key.Left) || input.WasKeyRepeated(Key.Left)
                ? TextEditing.PreviousGrapheme(value, state.Caret)
                : TextEditing.NextGrapheme(value, state.Caret));
        SetCaret(state, index, extend);
        state.PreferredX = -1;
    }

    private static void MoveVertical(Ui ui, FontSpec font, TextAreaState state, string value, int direction, bool extend)
    {
        if (state.Layout is null)
        {
            return;
        }

        var lineIndex = state.Layout.LineIndexForCaret(state.Caret);
        var line = state.Layout.Lines[lineIndex];
        var targetLine = Math.Clamp(lineIndex + direction, 0, state.Layout.Lines.Count - 1);
        if (targetLine == lineIndex && direction != 0)
        {
            return;
        }

        var desiredX = state.PreferredX >= 0
            ? state.PreferredX
            : state.Layout.CaretX(value, font, ui.TextEngine, state.Caret);
        var target = state.Layout.Lines[targetLine];
        var index = state.Layout.IndexAt(desiredX, targetLine * state.Layout.LineHeight + 1, value, font, ui.TextEngine);
        state.Caret = index;
        if (!extend)
        {
            state.Anchor = index;
        }

        state.PreferredX = desiredX;
    }

    private static void SetCaret(TextAreaState state, int index, bool extend)
    {
        state.Caret = index;
        if (!extend)
        {
            state.Anchor = index;
        }
    }

    private static void ProcessText(Ui ui, TextAreaState state, ref string value, FrameInput input, int maxLength)
    {
        if (input.TextInput.Length == 0 || input.Ctrl || input.Alt || input.Super)
        {
            return;
        }

        Insert(ui, state, ref value, Normalize(input.TextInput), maxLength, input.Time);
    }

    private static void Insert(Ui ui, TextAreaState state, ref string value, string text, int maxLength, double time)
    {
        text = Normalize(text);
        if (text.Length == 0)
        {
            return;
        }

        PushUndo(state, value);
        var start = Math.Min(state.Caret, state.Anchor);
        var end = Math.Max(state.Caret, state.Anchor);
        var room = maxLength > 0 ? Math.Max(0, maxLength - (value.Length - (end - start))) : int.MaxValue;
        if (room <= 0)
        {
            return;
        }

        if (text.Length > room)
        {
            text = text[..room];
        }

        value = string.Concat(value.AsSpan(0, start), text, value.AsSpan(end));
        state.Caret = state.Anchor = start + text.Length;
        state.PreferredX = -1;
        state.LastActivity = time;
        _ = ui;
    }

    private static string RemoveSelection(TextAreaState state, string value)
    {
        var start = Math.Min(state.Caret, state.Anchor);
        var end = Math.Max(state.Caret, state.Anchor);
        var result = string.Concat(value.AsSpan(0, start), value.AsSpan(end));
        state.Caret = state.Anchor = start;
        return result;
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void PushUndo(TextAreaState state, string value)
    {
        state.Undo.Add(new EditSnapshot(value, state.Caret, state.Anchor));
        if (state.Undo.Count > 100)
        {
            state.Undo.RemoveAt(0);
        }

        state.Redo.Clear();
    }

    private static void Undo(TextAreaState state, ref string value)
    {
        if (state.Undo.Count == 0)
        {
            return;
        }

        state.Redo.Add(new EditSnapshot(value, state.Caret, state.Anchor));
        var snapshot = state.Undo[^1];
        state.Undo.RemoveAt(state.Undo.Count - 1);
        value = snapshot.Text;
        state.Caret = Math.Clamp(snapshot.Caret, 0, value.Length);
        state.Anchor = Math.Clamp(snapshot.Anchor, 0, value.Length);
    }

    private static void Redo(TextAreaState state, ref string value)
    {
        if (state.Redo.Count == 0)
        {
            return;
        }

        state.Undo.Add(new EditSnapshot(value, state.Caret, state.Anchor));
        var snapshot = state.Redo[^1];
        state.Redo.RemoveAt(state.Redo.Count - 1);
        value = snapshot.Text;
        state.Caret = Math.Clamp(snapshot.Caret, 0, value.Length);
        state.Anchor = Math.Clamp(snapshot.Anchor, 0, value.Length);
    }
}


