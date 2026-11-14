namespace Vela;

internal readonly record struct CheckboxPayload(bool Checked, bool Radio);
internal readonly record struct TogglePayload(bool Value);
internal readonly record struct ProgressPayload(float Value, Color? Fill);
internal readonly record struct BadgePayload(BadgeTone Tone);
internal readonly record struct AvatarPayload(string Initials, Color Background);

public enum BadgeTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger,
}

/// <summary>
/// Checkbox/Radio/Toggle/Progress/Badge/Avatar 的绘制实现共用本文件。
/// 它们都是无状态 Widget；选中/开关值来自 payload，交互由 Ui.Selection 更新。
/// </summary>
internal sealed class CheckboxWidget : Widget
{
    public static CheckboxWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var textWidth = string.IsNullOrEmpty(element.Text)
            ? 0
            : context.Text.Measure(element.Text!, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var lineHeight = context.Text.GetLineHeight(element.TextFormat.Font);
        var height = MathF.Max(22, lineHeight + 8);
        var width = string.IsNullOrEmpty(element.Text) ? 18 : 18 + context.Theme.SpacingMd + textWidth;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var payload = element.Payload is CheckboxPayload value ? value : default;
        var response = element.Response;
        var disabled = response.Disabled;
        var boxSize = 18f;
        var centerY = element.Bounds.Center.Y;
        var box = new Rect(element.ContentBounds.Left, centerY - (boxSize * 0.5f), boxSize, boxSize);
        var draw = context.DrawList;
        var radius = CornerRadius.All(payload.Radio ? boxSize * 0.5f : theme.RadiusSmall);

        var border = payload.Checked
            ? (disabled ? theme.TextSubtle : theme.Accent)
            : (disabled ? theme.Border : response.Hovered ? theme.BorderStrong : theme.BorderStrong);
        var background = payload.Checked
            ? (disabled ? theme.SurfaceActive : theme.Accent)
            : Color.Transparent;

        if (background.A > 0)
        {
            draw.FillRect(box, new Brush(background), radius);
        }

        draw.StrokeRect(box.Deflate(Thickness.All(1)), border, 1.5f, radius);

        if (payload.Checked)
        {
            if (payload.Radio)
            {
                draw.FillCircle(box.Center, 4.5f, new Brush(disabled ? theme.TextMuted : theme.OnAccent));
            }
            else
            {
                draw.DrawIcon(Icons.Check, box.Deflate(Thickness.All(3.5f)), disabled ? theme.TextMuted : theme.OnAccent);
            }
        }

        if (!string.IsNullOrEmpty(element.Text))
        {
            var textRect = new Rect(box.Right + theme.SpacingMd, element.ContentBounds.Top, MathF.Max(0, element.ContentBounds.Right - box.Right - theme.SpacingMd), element.ContentBounds.Height);
            draw.Text(textRect, element.Text, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, disabled ? theme.TextSubtle : element.Foreground);
        }

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(element.Bounds.Deflate(Thickness.All(1)), theme.FocusRing.WithAlpha(0.75f), 1, CornerRadius.All(theme.RadiusSmall));
        }
    }
}

internal sealed class ToggleWidget : Widget
{
    public static ToggleWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var textWidth = string.IsNullOrEmpty(element.Text)
            ? 0
            : context.Text.Measure(element.Text!, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var lineHeight = context.Text.GetLineHeight(element.TextFormat.Font);
        var width = textWidth + (string.IsNullOrEmpty(element.Text) ? 0 : theme.SpacingXl) + 38;
        var height = MathF.Max(30, lineHeight + 8);
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var value = element.Payload is TogglePayload payload && payload.Value;
        var response = element.Response;
        var disabled = response.Disabled;
        var trackWidth = 38f;
        var trackHeight = 22f;
        var track = new Rect(element.ContentBounds.Right - trackWidth, element.Bounds.Center.Y - (trackHeight * 0.5f), trackWidth, trackHeight);
        var radius = CornerRadius.All(trackHeight * 0.5f);
        var draw = context.DrawList;

        var targetTrackColor = value
            ? (disabled ? theme.TextSubtle : theme.Accent)
            : (disabled ? theme.SurfaceActive : response.Hovered ? theme.BorderStrong : theme.SurfaceActive);
        var progress = context.Ui.Animate(Id.Combine(element.Id, "toggle-progress"), value ? 1f : 0f, 20f);
        var trackColor = context.Ui.AnimateColor(Id.Combine(element.Id, "toggle-track"), targetTrackColor, 18f);
        draw.FillRect(track, new Brush(trackColor), radius);

        var knobRadius = 8f;
        var knobX = track.Left + 3 + knobRadius + (track.Width - 6 - knobRadius * 2) * progress;
        draw.FillCircle(new Vec2(knobX, track.Center.Y), knobRadius, new Brush(disabled ? theme.TextMuted : Color.White));

        if (!string.IsNullOrEmpty(element.Text))
        {
            var textRect = new Rect(element.ContentBounds.Left, element.ContentBounds.Top, MathF.Max(0, track.Left - theme.SpacingLg - element.ContentBounds.Left), element.ContentBounds.Height);
            draw.Text(textRect, element.Text, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, disabled ? theme.TextSubtle : element.Foreground);
        }

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(element.Bounds.Deflate(Thickness.All(1)), theme.FocusRing.WithAlpha(0.75f), 1, CornerRadius.All(theme.RadiusSmall));
        }
    }
}

internal sealed class ProgressWidget : Widget
{
    public static ProgressWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var height = element.Style.Height.IsAuto ? 8 : element.Style.Height.Value;
        var width = constraints.IsTightWidth ? constraints.MinWidth : 180;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width, height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var payload = element.Payload is ProgressPayload progress ? progress : default;
        var theme = context.Theme;
        var ratio = Math.Clamp(payload.Value, 0, 1);
        var radius = CornerRadius.All(element.Bounds.Height * 0.5f);
        context.DrawList.FillRect(element.Bounds, new Brush(theme.SurfaceActive), radius);
        var fillWidth = element.Bounds.Width * ratio;
        if (fillWidth > 0)
        {
            var fillRect = new Rect(element.Bounds.X, element.Bounds.Y, fillWidth, element.Bounds.Height);
            var brush = payload.Fill is { } fill
                ? new Brush(new LinearGradient(
                    new Vec2(0, 0),
                    new Vec2(1, 0),
                    new GradientStop(0, fill.WithAlpha(0.75f)),
                    new GradientStop(1, fill)))
                : new Brush(new LinearGradient(
                    new Vec2(0, 0),
                    new Vec2(1, 0),
                    new GradientStop(0, theme.Accent.WithAlpha(0.7f)),
                    new GradientStop(1, theme.Accent)));
            context.DrawList.FillRect(fillRect, brush, radius);
        }
    }
}

internal sealed class BadgeWidget : Widget
{
    public static BadgeWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var width = context.Text.Measure(element.Text ?? string.Empty, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var height = MathF.Max(20, context.Text.GetLineHeight(element.TextFormat.Font) + 4);
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(width + 14, height));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var tone = element.Payload is BadgePayload payload ? payload.Tone : BadgeTone.Neutral;
        var theme = context.Theme;
        var (background, foreground) = tone switch
        {
            BadgeTone.Accent => (theme.AccentSoft, theme.Accent),
            BadgeTone.Success => (theme.SuccessSoft, theme.Success),
            BadgeTone.Warning => (theme.WarningSoft, theme.Warning),
            BadgeTone.Danger => (theme.DangerSoft, theme.Danger),
            _ => (theme.SurfaceActive, theme.TextMuted),
        };

        var radius = CornerRadius.All(element.Bounds.Height * 0.5f);
        context.DrawList.FillRect(element.Bounds, new Brush(background), radius);
        var format = element.TextFormat with { Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        context.DrawList.Text(element.Bounds, element.Text ?? string.Empty, format, foreground);
    }
}

internal sealed class AvatarWidget : Widget
{
    public static AvatarWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var size = element.Payload is AvatarPayload ? element.Font.Size * 3.2f : 34;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(size, size));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        if (element.Payload is not AvatarPayload avatar)
        {
            return;
        }

        var size = MathF.Min(element.Bounds.Width, element.Bounds.Height);
        var circle = Rect.Centered(element.Bounds.Center, size, size);
        var theme = context.Theme;
        var background = avatar.Background.A > 0 ? avatar.Background : theme.AccentSoft;
        context.DrawList.FillRect(circle, new Brush(background), CornerRadius.All(size * 0.5f));
        var format = new TextFormat(
            new FontSpec(theme.Body.Family, MathF.Max(10, size * 0.34f), FontWeight.SemiBold),
            TextAlignment.Center,
            VerticalAlignment.Center,
            TextWrapMode.NoWrap,
            TextOverflowMode.Ellipsis);
        context.DrawList.Text(circle, avatar.Initials, format, theme.Text);
    }
}
