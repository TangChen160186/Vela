namespace Vela;

public sealed partial class Ui
{
    /// <summary>
    /// Adds or updates accessibility metadata for an element that was already
    /// created in the current frame (for example a custom Canvas).
    /// </summary>
    public void Semantic(
        Id id,
        string name,
        AccessibilityRole role,
        string? value = null,
        string? help = null)
    {
        var frame = RequireFrame();
        foreach (var element in frame.AllElements)
        {
            if (element.Id != id)
            {
                continue;
            }

            element.AccessibilityName = name;
            element.AccessibilityRole = role;
            element.AccessibilityValue = value;
            element.AccessibilityHelp = help;
            break;
        }
    }

    public void Semantic(
        string id,
        string name,
        AccessibilityRole role,
        string? value = null,
        string? help = null) =>
        Semantic(Id.From(id), name, role, value, help);

    /// <summary>Marks an already-created element as a text input for keyboard routing and UIA.</summary>
    public void MarkTextInput(Id id)
    {
        foreach (var element in RequireFrame().AllElements)
        {
            if (element.Id == id)
            {
                element.TextInput = true;
                element.HandlesArrowKeys = true;
                break;
            }
        }
    }
}
