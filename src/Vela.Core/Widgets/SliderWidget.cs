namespace Vela;

internal sealed class SliderState
{
    public Rect TrackRect { get; set; }
}

internal readonly record struct SliderPayload(
    float Value,
    float Minimum,
    float Maximum,
    float Step,
    Func<float, string>? Formatter);

/// <summary>
/// 滑块绘制。值本身由 Ui.Slider 根据 Response/键盘更新，并存进 SliderPayload；
/// SliderState 只保存上一帧轨道矩形，用于鼠标位置到比例的换算。
/// </summary>
internal sealed class SliderWidget : Widget
{
    public static SliderWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var lineHeight = context.Text.GetLineHeight(element.TextFormat.Font);
        var labelWidth = string.IsNullOrEmpty(element.Text)
            ? 0
            : context.Text.Measure(element.Text!, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var height = string.IsNullOrEmpty(element.Text) ? 30 : lineHeight + 28;
        var width = MathF.Max(180, labelWidth + 80);
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var payload = element.Payload is SliderPayload value ? value : default;
        var theme = context.Theme;
        var response = element.Response;
        var disabled = response.Disabled;
        var draw = context.DrawList;
        var content = element.ContentBounds;
        var hasLabel = !string.IsNullOrEmpty(element.Text);
        var lineHeight = context.Text.GetLineHeight(element.TextFormat.Font);

        if (hasLabel)
        {
            var labelRect = new Rect(content.X, content.Y, content.Width, lineHeight);
            draw.Text(labelRect, element.Text!, element.TextFormat, disabled ? theme.TextSubtle : theme.TextMuted);
        }

        var valueText = payload.Formatter?.Invoke(payload.Value) ?? FormatValue(payload.Value, payload.Minimum, payload.Maximum);
        var valueFont = theme.SmallMedium;
        var valueWidth = context.Text.Measure(valueText, valueFont, TextMeasureOptions.SingleLine).Width;
        var valueRect = new Rect(content.Right - valueWidth, hasLabel ? content.Y : content.Center.Y - 8, valueWidth, hasLabel ? lineHeight : 16);
        draw.Text(valueRect, valueText, new TextFormat(valueFont, TextAlignment.Right, VerticalAlignment.Center, TextWrapMode.NoWrap), disabled ? theme.TextSubtle : theme.TextMuted);

        var trackCenterY = hasLabel ? content.Bottom - 8 : content.Center.Y;
        var trackLeft = content.Left + 7;
        var trackRight = content.Right - 7;
        var track = new Rect(trackLeft, trackCenterY - 3, MathF.Max(0, trackRight - trackLeft), 6);
        var state = context.Ui.State.GetOrCreate<SliderState>(element.Id);
        state.TrackRect = track;

        var ratio = payload.Maximum > payload.Minimum
            ? Math.Clamp((payload.Value - payload.Minimum) / (payload.Maximum - payload.Minimum), 0, 1)
            : 0;
        var trackRadius = CornerRadius.All(3);
        draw.FillRect(track, new Brush(disabled ? theme.SurfaceActive : theme.SurfaceActive), trackRadius);

        var thumbX = track.Left + (track.Width * ratio);
        var fillWidth = MathF.Max(0, thumbX - track.Left);
        if (fillWidth > 0)
        {
            var fill = disabled ? theme.TextSubtle : response.Hovered || response.Held ? theme.AccentHover : theme.Accent;
            draw.FillRect(new Rect(track.Left, track.Top, fillWidth, track.Height), new Brush(fill), trackRadius);
        }

        var thumbRadius = response.Hovered || response.Held ? 8f : 7f;
        var thumbCenter = new Vec2(thumbX, track.Center.Y);
        if (!disabled)
        {
            draw.Shadow(
                new Rect(thumbCenter.X - thumbRadius, thumbCenter.Y - thumbRadius, thumbRadius * 2, thumbRadius * 2),
                new BoxShadow(new Vec2(0, 2), 8, 0, Color.FromHex("#55000000")),
                CornerRadius.All(thumbRadius));
        }

        draw.FillCircle(thumbCenter, thumbRadius, new Brush(disabled ? theme.TextMuted : Color.White));
        draw.StrokeCircle(thumbCenter, thumbRadius, disabled ? theme.Border : theme.Accent, 2);

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(element.Bounds.Deflate(Thickness.All(1)), theme.FocusRing.WithAlpha(0.7f), 1, CornerRadius.All(theme.RadiusSmall));
        }
    }

    private static string FormatValue(float value, float min, float max)
    {
        var range = MathF.Abs(max - min);
        var format = range >= 100 ? "0" : range >= 10 ? "0.#" : "0.##";
        return value.ToString(format, System.Globalization.CultureInfo.CurrentCulture);
    }
}
