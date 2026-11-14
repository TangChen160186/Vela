namespace Vela;

public sealed partial class Ui
{
    /// <summary>
    /// A single-line text input with selection, clipboard, undo/redo and keyboard
    /// navigation. The value is owned by the application and updated in place.
    /// </summary>
    public Response TextField(
        string id,
        ref string value,
        string? placeholder = null,
        bool password = false,
        bool disabled = false,
        int maxLength = 0,
        Style? style = null)
    {
        value ??= string.Empty;
        var frame = RequireFrame();
        var element = frame.AddElement(
            TextFieldWidget.Instance,
            id,
            style,
            "text-field",
            interactive: true,
            focusable: true,
            activatable: false);
        element.Text = value;
        element.TextInput = true;
        element.HandlesArrowKeys = true;
        element.AccessibilityRole = AccessibilityRole.TextField;
        element.AccessibilityName = placeholder ?? "Text field";
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Clip);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.IBeam;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;

        var state = State.GetOrCreate<TextFieldState>(element.Id);
        TextFieldController.Update(this, element, state, ref value, response, disabled, password, maxLength);
        element.Text = value;
        element.Payload = new TextFieldPayload(placeholder, password, maxLength);
        return response;
    }

    /// <summary>
    /// A multi-line text editor with wrapping, vertical scrolling, selection,
    /// clipboard and undo/redo. The value is owned by the application.
    /// </summary>
    public Response TextArea(
        string id,
        ref string value,
        string? placeholder = null,
        bool readOnly = false,
        bool disabled = false,
        int maxLength = 0,
        Style? style = null)
    {
        value ??= string.Empty;
        var frame = RequireFrame();
        var element = frame.AddElement(
            TextAreaWidget.Instance,
            id,
            style ?? new Style { Height = 150 },
            "text-area",
            interactive: true,
            focusable: true,
            activatable: false);
        element.Text = value;
        element.TextInput = true;
        element.HandlesArrowKeys = true;
        element.AccessibilityRole = AccessibilityRole.TextArea;
        element.AccessibilityName = placeholder ?? "Text area";
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.Wrap, TextOverflowMode.Clip);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.IBeam;
        element.Payload = new TextAreaPayload(placeholder, readOnly, maxLength);
        frame.UpdateResponse(element, disabled);
        var response = element.Response;

        var state = State.GetOrCreate<TextAreaState>(element.Id);
        TextAreaController.Update(this, element, state, ref value, response, readOnly, maxLength);
        element.Text = value;
        return response;
    }
}


