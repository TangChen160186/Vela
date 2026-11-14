namespace Vela;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger,
}

/// <summary>
/// 按钮的测量/绘制。交互结果由 Ui.Button 读取；本类负责：
/// 按 variant/response 解析颜色、测量文本+图标、绘制背景/边框/内容/focus ring。
/// 背景使用 AnimateColor 做 hover 过渡，动画状态按
/// (element.Id,"button-background") 存在 StateStore。
/// </summary>
internal sealed class ButtonWidget : Widget
{
    public static ButtonWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var padding = element.Style.Padding.IsZero ? theme.ButtonPadding : element.Style.Padding;
        var text = element.Text ?? string.Empty;
        var textWidth = string.IsNullOrEmpty(text)
            ? 0
            : context.Text.Measure(text, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var textHeight = context.Text.GetLineHeight(element.TextFormat.Font);
        var iconSize = element.Icon is null ? 0 : 16;
        var gap = element.Icon is not null && !string.IsNullOrEmpty(text) ? theme.SpacingSm : 0;
        var contentWidth = textWidth + iconSize + gap;
        var contentHeight = MathF.Max(textHeight, theme.ControlHeight - 8);

        var naturalWidth = MathF.Max(theme.ControlHeight, contentWidth + padding.HorizontalSum);
        var naturalHeight = MathF.Max(theme.ControlHeight, contentHeight + padding.VerticalSum);

        var size = LayoutUtil.ResolveLeaf(element, constraints, new Size(naturalWidth, naturalHeight));
        return size;
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var response = element.Response;
        var variant = element.Payload is ButtonVariant value ? value : ButtonVariant.Secondary;
        var disabled = response.Disabled;
        var hovered = response.Hovered && !disabled;
        var pressed = (response.Held || response.Pressed) && !disabled;

        var (background, foreground, border) = ResolveColors(theme, variant, disabled, hovered, pressed);
        if (element.Style.Background is { } backgroundOverride)
        {
            background = backgroundOverride.SolidColor ?? background;
            if (hovered)
            {
                background = Color.Lerp(background, Color.White, 0.07f);
            }
            else if (pressed)
            {
                background = Color.Lerp(background, Color.Black, 0.08f);
            }
        }

        if (element.Style.Foreground is { } foregroundOverride)
        {
            foreground = foregroundOverride;
        }

        if (element.Style.BorderColor is { } borderOverride)
        {
            border = borderOverride;
        }

        background = context.Ui.AnimateColor(Id.Combine(element.Id, "button-background"), background, hovered ? 22f : 16f);

        var radius = element.Style.CornerRadius.IsZero
            ? CornerRadius.All(theme.RadiusMedium)
            : element.Style.CornerRadius;
        var bounds = element.Bounds;
        var draw = context.DrawList;

        if (!disabled && element.Style.Shadow is { } shadowOverride)
        {
            draw.Shadow(bounds, shadowOverride, radius);
        }
        else if (!disabled && variant == ButtonVariant.Primary && theme.ButtonShadow.Color.A > 0)
        {
            draw.Shadow(bounds, theme.ButtonShadow, radius);
        }

        draw.FillRect(bounds, new Brush(background), radius);
        if (border.A > 0)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(0.5f)), border, 1, radius);
        }

        var padding = element.Style.Padding.IsZero ? theme.ButtonPadding : element.Style.Padding;
        var text = element.Text ?? string.Empty;
        var textWidth = string.IsNullOrEmpty(text)
            ? 0
            : context.Text.Measure(text, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var iconSize = element.Icon is null ? 0 : 16;
        var gap = element.Icon is not null && textWidth > 0 ? theme.SpacingSm : 0;
        var total = textWidth + iconSize + gap;
        var availableContent = MathF.Max(0, bounds.Width - padding.HorizontalSum);
        if (total + 2 > availableContent)
        {
            var horizontal = MathF.Max(0, (bounds.Width - total - 2) * 0.5f);
            padding = new Thickness(horizontal, padding.Top, horizontal, padding.Bottom);
        }

        var content = bounds.Deflate(padding);
        var x = content.X + MathF.Max(0, (content.Width - total) * 0.5f);

        if (element.Icon is not null)
        {
            var iconRect = new Rect(x, content.Y + ((content.Height - iconSize) * 0.5f), iconSize, iconSize);
            draw.DrawIcon(element.Icon, iconRect, foreground);
            x += iconSize + gap;
        }

        if (!string.IsNullOrEmpty(text))
        {
            var format = element.TextFormat with { Alignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            draw.Text(new Rect(x, content.Y, MathF.Max(0, content.Right - x + 2), content.Height), text, format, foreground);
        }

        if (response.Focused && response.FocusVisible && !disabled)
        {
            var ring = bounds.Deflate(Thickness.All(1.5f));
            draw.StrokeRect(ring, theme.FocusRing.WithAlpha(0.85f), 1.5f, radius);
        }
    }

    private static (Color Background, Color Foreground, Color Border) ResolveColors(
        Theme theme,
        ButtonVariant variant,
        bool disabled,
        bool hovered,
        bool pressed)
    {
        if (disabled)
        {
            return variant == ButtonVariant.Primary || variant == ButtonVariant.Danger
                ? (theme.SurfaceActive, theme.TextSubtle, Color.Transparent)
                : (Color.Transparent, theme.TextSubtle, theme.Border);
        }

        return variant switch
        {
            ButtonVariant.Primary => (
                pressed ? theme.AccentPressed : hovered ? theme.AccentHover : theme.Accent,
                theme.OnAccent,
                Color.Transparent),
            ButtonVariant.Danger => (
                pressed ? theme.Danger.MultiplyAlpha(0.80f) : hovered ? theme.Danger : theme.Danger.WithAlpha(0.16f),
                pressed || hovered ? theme.OnAccent : theme.Danger,
                hovered || pressed ? Color.Transparent : theme.Danger.WithAlpha(0.35f)),
            ButtonVariant.Ghost => (
                pressed ? theme.SurfaceActive : hovered ? theme.SurfaceHover : Color.Transparent,
                theme.Text,
                Color.Transparent),
            _ => (
                pressed ? theme.SurfaceActive : hovered ? theme.SurfaceHover : theme.SurfaceRaised,
                theme.Text,
                theme.Border),
        };
    }
}
