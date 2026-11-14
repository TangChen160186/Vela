namespace Vela;

internal readonly record struct NavItemPayload(bool Selected, string? Badge);
internal readonly record struct SearchTriggerPayload(string Placeholder, string? Shortcut);

/// <summary>
/// 应用侧栏导航项与搜索入口。选中/激活状态由调用方传入，控件本身无状态；
/// 可访问性角色分别为 Tab 和 Button。
/// </summary>
internal sealed class NavItemWidget : Widget
{
    public static NavItemWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var theme = context.Theme;
        var textWidth = context.Text.Measure(element.Text ?? string.Empty, element.TextFormat.Font, TextMeasureOptions.SingleLine).Width;
        var badgeWidth = 0f;
        if (element.Payload is NavItemPayload { Badge: { Length: > 0 } badge })
        {
            badgeWidth = context.Text.Measure(badge, theme.Caption, TextMeasureOptions.SingleLine).Width + 18;
        }

        return LayoutUtil.ResolveLeaf(element, constraints, new Size(textWidth + 18 + theme.SpacingMd + badgeWidth + 20, 36));
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var payload = element.Payload is NavItemPayload value ? value : default;
        var response = element.Response;
        var draw = context.DrawList;
        var radius = CornerRadius.All(theme.RadiusMedium);

        if (payload.Selected)
        {
            draw.FillRect(element.Bounds, new Brush(theme.AccentSoft), radius);
        }
        else if (response.Hovered)
        {
            draw.FillRect(element.Bounds, new Brush(theme.SurfaceHover), radius);
        }

        var foreground = payload.Selected ? theme.Text : theme.TextMuted;
        var x = element.Bounds.Left + 10;
        if (element.Icon is not null)
        {
            draw.DrawIcon(element.Icon, new Rect(x, element.Bounds.Center.Y - 9, 18, 18), payload.Selected ? theme.Accent : theme.TextMuted);
            x += 18 + theme.SpacingMd;
        }

        var badgeWidth = 0f;
        if (!string.IsNullOrEmpty(payload.Badge))
        {
            var badgeWidthText = context.Text.Measure(payload.Badge, theme.CaptionMedium, TextMeasureOptions.SingleLine).Width;
            badgeWidth = badgeWidthText + 14;
            var badgeRect = new Rect(element.Bounds.Right - badgeWidth - 8, element.Bounds.Center.Y - 9, badgeWidth, 18);
            draw.FillRect(badgeRect, new Brush(payload.Selected ? theme.Accent.WithAlpha(0.22f) : theme.SurfaceActive), CornerRadius.All(9));
            draw.Text(badgeRect, payload.Badge, new TextFormat(theme.CaptionMedium, TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap), payload.Selected ? theme.Accent : theme.TextMuted);
        }

        draw.Text(
            new Rect(x, element.Bounds.Top, MathF.Max(0, element.Bounds.Right - x - badgeWidth - 10), element.Bounds.Height),
            element.Text ?? string.Empty,
            element.TextFormat with { VerticalAlignment = VerticalAlignment.Center },
            response.Disabled ? theme.TextSubtle : foreground);

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(element.Bounds.Deflate(Thickness.All(1)), theme.FocusRing.WithAlpha(0.55f), 1, radius);
        }
    }
}

internal sealed class SearchTriggerWidget : Widget
{
    public static SearchTriggerWidget Instance { get; } = new();

    public override bool Interactive => true;
    public override bool Focusable => true;
    public override bool Activatable => true;

    public override Size Measure(Element element, LayoutContext context, Constraints constraints) =>
        LayoutUtil.ResolveLeaf(element, constraints, new Size(250, 34));

    public override void PaintContent(Element element, PaintContext context)
    {
        var theme = context.Theme;
        var payload = element.Payload is SearchTriggerPayload value ? value : default;
        var response = element.Response;
        var draw = context.DrawList;
        var bounds = element.Bounds;
        var radius = CornerRadius.All(theme.RadiusMedium);
        var border = response.Hovered ? theme.BorderStrong : theme.Border;
        draw.FillRect(bounds, new Brush(theme.InputBackground), radius);
        draw.StrokeRect(bounds.Deflate(Thickness.All(0.5f)), border, 1, radius);
        draw.DrawIcon(Icons.Search, new Rect(bounds.Left + 10, bounds.Center.Y - 8, 16, 16), theme.TextSubtle);

        var right = bounds.Right - 10;
        if (!string.IsNullOrEmpty(payload.Shortcut))
        {
            var width = context.Text.Measure(payload.Shortcut, theme.Caption, TextMeasureOptions.SingleLine).Width + 12;
            var badge = new Rect(right - width, bounds.Center.Y - 9, width, 18);
            draw.FillRect(badge, new Brush(theme.SurfaceHover), CornerRadius.All(5));
            draw.Text(badge, payload.Shortcut, new TextFormat(theme.Caption, TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap), theme.TextSubtle);
            right = badge.Left - 8;
        }

        var textRect = new Rect(bounds.Left + 34, bounds.Top, MathF.Max(0, right - bounds.Left - 34), bounds.Height);
        draw.Text(textRect, string.IsNullOrEmpty(element.Text) ? payload.Placeholder : element.Text!, new TextFormat(
            element.TextFormat.Font,
            TextAlignment.Left,
            VerticalAlignment.Center,
            TextWrapMode.NoWrap,
            TextOverflowMode.Ellipsis), theme.TextSubtle);

        if (response.Focused && response.FocusVisible)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(2)), theme.FocusRing.WithAlpha(0.45f), 1, CornerRadius.All(theme.RadiusMedium + 1));
        }
    }
}

public sealed partial class Ui
{
    public Response NavItem(
        string label,
        IconData icon,
        bool selected,
        string? id = null,
        string? badge = null,
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(NavItemWidget.Instance, id ?? label, style, "nav-item", interactive: true, focusable: true, activatable: true);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.Tab;
        element.AccessibilityName = label;
        element.AccessibilityValue = selected ? "selected" : null;
        element.Icon = icon;
        element.Payload = new NavItemPayload(selected, badge);
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        return element.Response;
    }

    public Response SearchTrigger(
        string id,
        string placeholder = "Search…",
        string? shortcut = "Ctrl K",
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(SearchTriggerWidget.Instance, id, style, "search-trigger", interactive: true, focusable: true, activatable: true);
        element.Text = string.Empty;
        element.AccessibilityRole = AccessibilityRole.Button;
        element.AccessibilityName = placeholder;
        element.AccessibilityHelp = shortcut;
        element.Payload = new SearchTriggerPayload(placeholder, shortcut);
        element.Font = style?.Font ?? Theme.Small;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        return element.Response;
    }
}

