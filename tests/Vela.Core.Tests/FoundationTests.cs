using Xunit;

namespace Vela.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void AllBuiltInIconsParse()
    {
        Assert.NotEmpty(Icons.All);
        foreach (var icon in Icons.All)
        {
            Assert.False(icon.Path.IsEmpty, icon.Name);
            Assert.True(icon.Path.Bounds.Width >= 0);
        }
    }

    [Fact]
    public void DuplicateStatefulIdsAreRejected()
    {
        using var host = new UiTestHost();
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            host.Frame(ui =>
            {
                ui.Button("Save", "same-id");
                ui.Button("Save again", "same-id");
            });
        });
        Assert.Contains("Duplicate stateful ID", exception.Message);
    }

    [Fact]
    public void IdsAreStableAcrossScopes()
    {
        var a = Id.From("root");
        var b = Id.Combine(a, "button");
        var c = Id.Combine(a, "button");
        Assert.Equal(b, c);
        Assert.NotEqual(b, Id.Combine(a, "other"));
    }

    [Fact]
    public void ScrollbarClickJumpsScrollPosition()
    {
        using var host = new UiTestHost(new Size(500, 400));
        void Build(Ui ui)
        {
            using var scroll = ui.ScrollArea("scroll", 0, new Style { Width = 300, Height = 160 });
            for (var i = 0; i < 30; i++)
            {
                ui.Spacer(new Style { Height = 20 });
            }
        }

        host.Frame(Build);
        var scrollInfo = host.Element("scroll");
        var state = host.Ui.State.Get<ScrollState>(scrollInfo.Id)!;
        var scrollbar = host.Result.Diagnostics!.Elements.First(e => e.Name == "scrollbar");
        var track = scrollbar.Bounds;
        Assert.True(track.Height > 0);

        var pointer = new Vec2(track.Center.X, track.Bottom - 8);
        host.Frame(Build, input =>
        {
            input.SetPointerPosition(pointer);
            input.SetButton(PointerButton.Left, true);
        });
        Assert.True(scrollbar.Interactive);
        Assert.Equal(scrollbar.Id, host.Result.Diagnostics!.HoveredId);
        Assert.Equal(scrollbar.Id, host.Result.Diagnostics!.ActiveId);
        var drag = host.Ui.State.Get<ScrollbarDragState>(scrollbar.Id);
        Assert.NotNull(drag);
        Assert.True(drag!.Dragging, "scrollbar drag did not start");
        host.Frame(Build, input =>
        {
            input.SetPointerPosition(pointer);
            input.SetButton(PointerButton.Left, false);
        });

        Assert.True(state.Offset > 0, $"target={state.Target} offset={state.Offset} content={state.ContentHeight} viewport={state.ViewportHeight} track={track} pointer={pointer}");
    }
}
