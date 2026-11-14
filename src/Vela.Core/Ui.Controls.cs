namespace Vela;

public sealed partial class Ui
{
    /// <summary>
    /// Adds a push button. The label doubles as the state ID unless <paramref name="id"/>
    /// is supplied; use explicit IDs for repeated labels or changing text.
    /// </summary>
    public Response Button(
        string label,
        string? id = null,
        ButtonVariant variant = ButtonVariant.Secondary,
        IconData? icon = null,
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(
            ButtonWidget.Instance,
            id ?? label,
            style,
            "button",
            interactive: true,
            focusable: true,
            activatable: true);

        element.Text = label ?? string.Empty;
        element.AccessibilityRole = AccessibilityRole.Button;
        element.AccessibilityName = string.IsNullOrEmpty(label) ? id : label;
        element.Icon = icon;
        element.Payload = variant;
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        element.Font = style?.Font ?? Theme.Button;
        element.TextFormat = new TextFormat(
            element.Font,
            TextAlignment.Center,
            VerticalAlignment.Center,
            TextWrapMode.NoWrap,
            TextOverflowMode.Ellipsis);
        frame.UpdateResponse(element, disabled);
        return element.Response;
    }

    public Response IconButton(
        IconData icon,
        string tooltip,
        string? id = null,
        ButtonVariant variant = ButtonVariant.Ghost,
        bool disabled = false,
        Style? style = null)
    {
        var resolved = style ?? new Style
        {
            Width = Theme.ControlHeight,
            Height = Theme.ControlHeight,
            Padding = Thickness.Zero,
        };
        return Button(string.Empty, id ?? tooltip, variant, icon, disabled, resolved);
    }
}

