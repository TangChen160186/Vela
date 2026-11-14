namespace Vela;

internal readonly record struct MenuItemPayload(string? Shortcut, bool Danger);
internal readonly record struct DropdownPayload(string Display, bool Open);

/// <summary>
/// Popup 菜单项与 Dropdown 的绘制。菜单项点击后由 Ui.MenuItem 关闭当前 popup；
/// 本类只负责布局/颜色/shortcut/危险态。
/// </summary>
internal sealed class MenuItemWidget : Widget
{
    public static MenuItemWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var textWidth = context.Text.Measure(element.Text ?? string.Empty, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var shortcutWidth = 0f;
        if (element.Payload is MenuItemPayload { Shortcut: { Length: > 0 } shortcut })
        {
            shortcutWidth = context.Text.Measure(shortcut, theme.Caption, TextMeasureOptions.SingleLine).Width + 24;
        }

        var iconWidth = element.Icon is null ? 0 : 16 + theme.SpacingMd;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(textWidth + iconWidth + shortcutWidth + 24, 32));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var payload = element.Payload is MenuItemPayload value ? value : default;
        var response = element.Response;
        var bounds = element.Bounds;
        var draw = context.DrawList;
        var radius = CornerRadius.All(theme.RadiusSmall);

        if (response.Hovered || response.Held)
        {
            draw.FillRect(bounds, new Brush(payload.Danger ? theme.DangerSoft : theme.SurfaceHover), radius);
        }
        else if (response.Focused && response.FocusVisible)
        {
            draw.FillRect(bounds, new Brush(theme.SurfaceHover.WithAlpha(0.55f)), radius);
        }

        var foreground = payload.Danger ? theme.Danger : element.Foreground;
        var x = bounds.Left + 10;
        if (element.Icon is not null)
        {
            draw.DrawIcon(element.Icon, new Rect(x, bounds.Center.Y - 8, 16, 16), foreground);
            x += 16 + theme.SpacingMd;
        }

        var shortcut = payload.Shortcut;
        var shortcutWidth = 0f;
        if (!string.IsNullOrEmpty(shortcut))
        {
            var format = new TextFormat(theme.Caption, TextAlignment.Right, VerticalAlignment.Center, TextWrapMode.NoWrap);
            shortcutWidth = context.Text.Measure(shortcut, theme.Caption, TextMeasureOptions.SingleLine).Width;
            draw.Text(new Rect(bounds.Right - shortcutWidth - 10, bounds.Top, shortcutWidth, bounds.Height), shortcut, format, theme.TextSubtle);
            shortcutWidth += 16;
        }

        var textRect = new Rect(x, bounds.Top, MathF.Max(0, bounds.Right - x - shortcutWidth - 10), bounds.Height);
        draw.Text(textRect, element.Text ?? string.Empty, element.TextFormat with { VerticalAlignment = VerticalAlignment.Center }, foreground);
    }
}

internal sealed class DropdownWidget : Widget
{
    public static DropdownWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        return LayoutUtil.ResolveLeaf(element, constraints, new Size(220, MathF.Max(theme.InputHeight, context.Text.GetLineHeight(element.TextFormat.Font) + 16)));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var payload = element.Payload is DropdownPayload value ? value : default;
        var response = element.Response;
        var disabled = response.Disabled;
        var bounds = element.Bounds;
        var radius = CornerRadius.All(element.Style.CornerRadius.IsZero ? theme.RadiusMedium : element.Style.CornerRadius.TopLeft);
        var border = disabled ? theme.Border : payload.Open || response.Focused ? theme.Accent : response.Hovered ? theme.BorderStrong : theme.Border;
        var draw = context.DrawList;

        draw.FillRect(bounds, new Brush(disabled ? theme.Surface : theme.InputBackground), radius);
        draw.StrokeRect(bounds.Deflate(Thickness.All(0.5f)), border, 1, radius);

        var padding = element.Style.Padding.IsZero ? theme.InputPadding : element.Style.Padding;
        var content = bounds.Deflate(padding);
        var arrow = new Rect(content.Right - 16, content.Center.Y - 8, 16, 16);
        draw.DrawIcon(payload.Open ? Icons.ChevronUp : Icons.ChevronDown, arrow, disabled ? theme.TextSubtle : theme.TextMuted);
        draw.Text(
            new Rect(content.X, content.Y, MathF.Max(0, content.Width - 24), content.Height),
            payload.Display,
            element.TextFormat with { VerticalAlignment = VerticalAlignment.Center },
            disabled ? theme.TextSubtle : element.Foreground);

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(3)), theme.FocusRing.WithAlpha(0.4f), 1, CornerRadius.All(theme.RadiusMedium + 2));
        }
    }
}
