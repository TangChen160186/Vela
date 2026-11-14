using Xunit;

namespace Vela.Tests;

public sealed class TextAreaTests
{
    [Fact]
    public void TextAreaAcceptsMultilineInput()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var value = string.Empty;
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }));
        var bounds = host.Element("text-area").Bounds;

        host.Frame(
            ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }),
            input =>
            {
                input.SetPointerPosition(new Vec2(bounds.Left + 15, bounds.Top + 15));
                input.SetButton(PointerButton.Left, true);
            });
        host.Frame(
            ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }),
            input =>
            {
                input.SetPointerPosition(new Vec2(bounds.Left + 15, bounds.Top + 15));
                input.SetButton(PointerButton.Left, false);
            });
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.AddText("hello"));
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.AddKeyDown(Key.Enter));
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.AddKeyUp(Key.Enter));
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.AddText("world"));

        Assert.Equal("hello\nworld", value);
    }

    [Fact]
    public void TextAreaSupportsUndoAndRedo()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var value = string.Empty;
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.SetPointerPosition(new Vec2(20, 20)));
        host.Frame(
            ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }),
            input =>
            {
                input.SetPointerPosition(new Vec2(20, 20));
                input.SetButton(PointerButton.Left, true);
            });
        host.Frame(
            ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }),
            input => input.SetButton(PointerButton.Left, false));
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => input.AddText("abc"));
        var ctrlSeen = false;
        var zSeen = false;
        var focusedSeen = false;
        host.Frame(ui =>
        {
            ctrlSeen = ui.Input.Ctrl;
            zSeen = ui.Input.WasKeyPressed(Key.Z);
            var response = ui.TextArea("notes", ref value, style: new Style { Height = 150 });
            focusedSeen = response.Focused;
        }, input => { input.SetModifiers(ModifierKeys.Control); input.AddKeyDown(Key.Z); });
        Assert.True(ctrlSeen);
        Assert.True(zSeen);
        Assert.True(focusedSeen);
        Assert.Equal(string.Empty, value);
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => { input.AddKeyUp(Key.Z); input.SetModifiers(ModifierKeys.None); });
        host.Frame(ui => ui.TextArea("notes", ref value, style: new Style { Height = 150 }), input => { input.SetModifiers(ModifierKeys.Control); input.AddKeyDown(Key.Y); });
        Assert.Equal("abc", value);
    }
}

