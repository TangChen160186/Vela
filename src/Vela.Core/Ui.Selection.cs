namespace Vela;

public sealed partial class Ui
{
    public Response Checkbox(string label, ref bool value, string? id = null, bool disabled = false, Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(CheckboxWidget.Instance, id ?? label, style, "checkbox", interactive: true, focusable: true, activatable: true);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.CheckBox;
        element.AccessibilityName = label;
        element.AccessibilityValue = value ? "checked" : "unchecked";
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;
        if (!disabled && response.Clicked)
        {
            value = !value;
        }

        element.AccessibilityValue = value ? "checked" : "unchecked";
        element.Payload = new CheckboxPayload(value, false);
        return response;
    }

    public Response RadioButton(
        string label,
        ref int selected,
        int value,
        string? id = null,
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(CheckboxWidget.Instance, id ?? label, style, "radio", interactive: true, focusable: true, activatable: true);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.RadioButton;
        element.AccessibilityName = label;
        element.AccessibilityValue = selected == value ? "selected" : "not selected";
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;
        if (!disabled && response.Clicked)
        {
            selected = value;
        }

        element.Payload = new CheckboxPayload(selected == value, true);
        return response;
    }

    public Response Toggle(string label, ref bool value, string? id = null, bool disabled = false, Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(ToggleWidget.Instance, id ?? label, style, "toggle", interactive: true, focusable: true, activatable: true);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.Switch;
        element.AccessibilityName = label;
        element.AccessibilityValue = value ? "on" : "off";
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;
        if (!disabled && response.Clicked)
        {
            value = !value;
        }

        element.AccessibilityValue = value ? "on" : "off";
        element.Payload = new TogglePayload(value);
        return response;
    }

    public Response Switch(string label, ref bool value, string? id = null, bool disabled = false) =>
        Toggle(label, ref value, id, disabled);

    public void ProgressBar(float value, float height = 8, Color? color = null, Style? style = null)
    {
        var frame = RequireFrame();
        var resolved = style is null
            ? new Style { Height = height }
            : style with { Height = style.Height.IsAuto ? height : style.Height };
        var element = frame.AddElement(ProgressWidget.Instance, null, resolved, "progress", claimId: false);
        element.AccessibilityRole = AccessibilityRole.ProgressBar;
        element.AccessibilityName = "Progress";
        element.AccessibilityValue = $"{Math.Clamp(value, 0, 1) * 100:0}%";
        element.Payload = new ProgressPayload(Math.Clamp(value, 0, 1), color);
    }

    public void Badge(string text, BadgeTone tone = BadgeTone.Neutral, Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(BadgeWidget.Instance, null, style, "badge", claimId: false);
        element.AccessibilityRole = AccessibilityRole.StaticText;
        element.AccessibilityName = text;
        element.Text = text;
        element.Font = Theme.CaptionMedium;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap);
        element.Payload = new BadgePayload(tone);
    }

    public void Avatar(string initials, float size = 34, Color? background = null, Style? style = null)
    {
        var frame = RequireFrame();
        var resolved = style ?? new Style { Width = size, Height = size };
        var element = frame.AddElement(AvatarWidget.Instance, null, resolved, "avatar", claimId: false);
        element.Payload = new AvatarPayload(initials, background ?? Color.Transparent);
        element.Font = new FontSpec(Theme.Body.Family, MathF.Max(10, size * 0.34f), FontWeight.SemiBold);
    }
}

