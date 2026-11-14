using Xunit;

namespace Vela.Tests;

public sealed class KeyboardAccessibilityTests
{
    [Fact]
    public void ArrowKeysNavigateSpatiallyBetweenControls()
    {
        using var host = new UiTestHost(new Size(500, 200));
        Id first = default;
        Id second = default;
        host.Frame(ui =>
        {
            using (ui.Row("row", new Style { Gap = 8, Padding = Thickness.All(12) }))
            {
                first = ui.Button("First", "first").Id;
                second = ui.Button("Second", "second").Id;
            }
        });

        host.Frame(ui =>
        {
            using (ui.Row("row", new Style { Gap = 8, Padding = Thickness.All(12) }))
            {
                first = ui.Button("First", "first").Id;
                second = ui.Button("Second", "second").Id;
            }
        }, input => input.AddKeyDown(Key.Tab));
        Assert.Equal(first, host.Result.Diagnostics!.FocusedId);

        host.Frame(ui =>
        {
            using (ui.Row("row", new Style { Gap = 8, Padding = Thickness.All(12) }))
            {
                first = ui.Button("First", "first").Id;
                second = ui.Button("Second", "second").Id;
            }
        }, input =>
        {
            input.AddKeyUp(Key.Tab);
            input.AddKeyDown(Key.Right);
        });
        Assert.Equal(second, host.Result.Diagnostics!.FocusedId);
    }

    [Fact]
    public void AccessibilityTreeContainsButtonNamesAndFocus()
    {
        using var host = new UiTestHost(new Size(500, 200));
        host.Frame(ui =>
        {
            using (ui.Row("row", new Style { Gap = 8 }))
            {
                ui.Button("Save", "save");
                ui.Button("Cancel", "cancel");
            }
        });

        Assert.Contains(host.Result.Accessibility.Nodes, node =>
            node.Role == AccessibilityRole.Button && node.Name == "Save");
        Assert.Contains(host.Result.Accessibility.Nodes, node =>
            node.Role == AccessibilityRole.Button && node.Name == "Cancel");
    }
}
