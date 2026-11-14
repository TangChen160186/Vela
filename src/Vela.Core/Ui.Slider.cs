namespace Vela;

public sealed partial class Ui
{
    public Response Slider(
        string id,
        ref float value,
        float minimum = 0,
        float maximum = 1,
        float step = 0,
        string? label = null,
        Func<float, string>? format = null,
        bool disabled = false,
        Style? style = null)
    {
        if (maximum < minimum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        var frame = RequireFrame();
        var element = frame.AddElement(
            SliderWidget.Instance,
            id,
            style,
            "slider",
            interactive: true,
            focusable: true,
            activatable: false);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.Slider;
        element.AccessibilityName = label ?? "Slider";
        element.Font = style?.Font ?? Theme.SmallMedium;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;

        if (!disabled)
        {
            var state = State.GetOrCreate<SliderState>(element.Id);
            if (response.Active && Input.IsButtonDown(PointerButton.Left) && state.TrackRect.Width > 0)
            {
                var ratio = Math.Clamp((Input.PointerPosition.X - state.TrackRect.Left) / state.TrackRect.Width, 0, 1);
                value = minimum + (ratio * (maximum - minimum));
            }

            var keyboardStep = step > 0 ? step : MathF.Max(0.01f, (maximum - minimum) / 100f);
            if (Input.Shift)
            {
                keyboardStep *= 10;
            }

            if (response.Focused)
            {
                if (Input.WasKeyPressedOrRepeated(Key.Left) || Input.WasKeyPressedOrRepeated(Key.Down))
                {
                    value -= keyboardStep;
                }
                else if (Input.WasKeyPressedOrRepeated(Key.Right) || Input.WasKeyPressedOrRepeated(Key.Up))
                {
                    value += keyboardStep;
                }
            }

            if (step > 0)
            {
                value = minimum + (MathF.Round((value - minimum) / step) * step);
            }

            value = Math.Clamp(value, minimum, maximum);
        }

        element.AccessibilityValue = format?.Invoke(value) ?? value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        element.Payload = new SliderPayload(value, minimum, maximum, step, format);
        return response;
    }
}


