using Xunit;

namespace Vela.Tests;

public sealed class InteractionTests
{
    [Fact]
    public void ButtonClicksOnReleaseInside()
    {
        using var host = new UiTestHost();
        Response response = default;
        host.Frame(ui => response = ui.Button("Click me", "click"));
        var bounds = host.Bounds(response.Id);

        host.Frame(ui => response = ui.Button("Click me", "click"), input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, true);
        });
        Assert.True(response.Pressed);
        Assert.True(response.Active);

        host.Frame(ui => response = ui.Button("Click me", "click"), input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, false);
        });
        Assert.True(response.Clicked);
        Assert.True(response.Released);
        Assert.False(response.Active);
    }

    [Fact]
    public void ButtonDoesNotClickWhenReleaseIsOutside()
    {
        using var host = new UiTestHost();
        Response response = default;
        host.Frame(ui => response = ui.Button("Click me", "click"));
        var bounds = host.Bounds(response.Id);

        host.Frame(ui => response = ui.Button("Click me", "click"), input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, true);
        });
        host.Frame(ui => response = ui.Button("Click me", "click"), input =>
        {
            input.SetPointerPosition(new Vec2(700, 500));
            input.SetButton(PointerButton.Left, false);
        });

        Assert.False(response.Clicked);
        Assert.True(response.Released);
    }

    [Fact]
    public void TabFocusesAndEnterActivates()
    {
        using var host = new UiTestHost();
        Response response = default;
        host.Frame(ui => response = ui.Button("Focus me", "focus-button"));

        host.Frame(ui => response = ui.Button("Focus me", "focus-button"), input => input.AddKeyDown(Key.Tab));
        Assert.True(response.Focused);
        Assert.True(response.FocusVisible);
        host.Frame(ui => response = ui.Button("Focus me", "focus-button"), input => input.AddKeyUp(Key.Tab));

        host.Frame(ui => response = ui.Button("Focus me", "focus-button"), input => input.AddKeyDown(Key.Enter));
        Assert.True(response.Clicked);
    }

    [Fact]
    public void TextFieldAcceptsTypingAfterClick()
    {
        using var host = new UiTestHost();
        var text = string.Empty;
        Response response = default;
        host.Frame(ui => response = ui.TextField("name", ref text, "Your name"));
        var bounds = host.Bounds(response.Id);

        host.Frame(ui => response = ui.TextField("name", ref text, "Your name"), input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, true);
        });
        host.Frame(ui => response = ui.TextField("name", ref text, "Your name"), input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, false);
        });

        host.Frame(ui => response = ui.TextField("name", ref text, "Your name"), input => input.AddText("Hello"));
        Assert.Equal("Hello", text);
        Assert.True(response.Focused);
    }

    [Fact]
    public void WheelScrollsHoveredScrollView()
    {
        using var host = new UiTestHost(new Size(500, 400));
        host.Frame(ui =>
        {
            using var scroll = ui.ScrollArea("scroll", 0, new Style { Width = 300, Height = 160 });
            for (var i = 0; i < 30; i++)
            {
                ui.Spacer(new Style { Height = 20 });
            }
        });

        var info = host.Element("scroll");
        var state = host.Ui.State.Get<ScrollState>(info.Id)!;
        host.Frame(ui =>
        {
            using var scroll = ui.ScrollArea("scroll", 0, new Style { Width = 300, Height = 160 });
            for (var i = 0; i < 30; i++)
            {
                ui.Spacer(new Style { Height = 20 });
            }
        }, input =>
        {
            input.SetPointerPosition(new Vec2(100, 60));
            input.AddWheel(0, -1);
        });

        Assert.True(state.Target > 0);
        Assert.True(state.Offset > 0);
    }

    [Fact]
    public void PopupOpensAndClosesOnOutsideClick()
    {
        using var host = new UiTestHost();
        Response opener = default;
        void Build(Ui ui)
        {
            opener = ui.Button("Open", "popup-button");
            if (opener.Clicked)
            {
                ui.OpenPopup("test-popup", opener.Id);
            }

            ui.Popup("test-popup", () =>
            {
                ui.MenuItem("Action", "popup-action");
            }, width: 180);
        }

        host.Frame(Build);
        var bounds = host.Bounds(opener.Id);
        host.Frame(Build, input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, true);
        });
        host.Frame(Build, input =>
        {
            input.SetPointerPosition(bounds.Center);
            input.SetButton(PointerButton.Left, false);
        });

        Assert.True(host.Ui.IsPopupOpen("test-popup"));
        Assert.Contains(host.Result.Diagnostics!.Elements, e => e.Name == "popup");

        host.Frame(Build, input =>
        {
            input.SetPointerPosition(new Vec2(780, 580));
            input.SetButton(PointerButton.Left, true);
        });
        host.Frame(Build, input =>
        {
            input.SetPointerPosition(new Vec2(780, 580));
            input.SetButton(PointerButton.Left, false);
        });

        Assert.False(host.Ui.IsPopupOpen("test-popup"));
        host.Frame(Build);
        Assert.DoesNotContain(host.Result.Diagnostics!.Elements, e => e.Name == "popup");
    }
}
