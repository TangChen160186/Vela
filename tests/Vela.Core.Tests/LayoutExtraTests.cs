using Xunit;

namespace Vela.Tests;

public sealed class LayoutExtraTests
{
    [Fact]
    public void GridDistributesStarTracksAndGaps()
    {
        using var host = new UiTestHost(new Size(400, 300));
        Id first = default;
        Id second = default;
        host.Frame(ui =>
        {
            using (ui.Grid("grid", [GridTrack.Star(), GridTrack.Star()], style: new Style { Height = 100, Gap = 10 }))
            {
                using (ui.Panel("first", new Style { Height = 50 }))
                {
                    first = ui.CurrentId;
                }

                using (ui.Panel("second", new Style { Height = 50 }))
                {
                    second = ui.CurrentId;
                }
            }
        });

        Assert.Equal(new Rect(0, 0, 195, 50), host.Bounds(first));
        Assert.Equal(new Rect(205, 0, 195, 50), host.Bounds(second));
    }

    [Fact]
    public void WrapMovesOverflowingChildrenToTheNextLine()
    {
        using var host = new UiTestHost(new Size(300, 300));
        var ids = new List<Id>();
        host.Frame(ui =>
        {
            using (ui.Wrap("wrap", new Style { Gap = 5 }))
            {
                for (var i = 0; i < 3; i++)
                {
                    using (ui.Panel($"item-{i}", new Style { Width = 120, Height = 20 }))
                    {
                        ids.Add(ui.CurrentId);
                    }
                }
            }
        });

        Assert.Equal(0, host.Bounds(ids[0]).X);
        Assert.Equal(125, host.Bounds(ids[1]).X);
        Assert.Equal(0, host.Bounds(ids[2]).X);
        Assert.Equal(25, host.Bounds(ids[2]).Y);
    }

    [Fact]
    public void DockClaimsEdgesBeforeFill()
    {
        using var host = new UiTestHost(new Size(500, 400));
        Id top = default;
        Id left = default;
        Id fill = default;
        host.Frame(ui =>
        {
            using (ui.Dock("dock"))
            {
                using (ui.Panel("top", new Style { Dock = DockSide.Top, Height = 50 }))
                {
                    top = ui.CurrentId;
                }

                using (ui.Panel("left", new Style { Dock = DockSide.Left, Width = 100 }))
                {
                    left = ui.CurrentId;
                }

                using (ui.Panel("fill", new Style { Dock = DockSide.Fill }))
                {
                    fill = ui.CurrentId;
                }
            }
        });

        Assert.Equal(new Rect(0, 0, 500, 50), host.Bounds(top));
        Assert.Equal(new Rect(0, 50, 100, 350), host.Bounds(left));
        Assert.Equal(new Rect(100, 50, 400, 350), host.Bounds(fill));
    }

    [Fact]
    public void SplitPaneReservesFirstPaneAndDivider()
    {
        using var host = new UiTestHost(new Size(500, 300));
        var firstSize = 200f;
        Id first = default;
        Id second = default;
        host.Frame(ui =>
        {
            ui.SplitPane("split", Axis.Horizontal, ref firstSize,
                () =>
                {
                    using (ui.Panel("first", new Style { Grow = 1 }))
                    {
                        first = ui.CurrentId;
                    }
                },
                () =>
                {
                    using (ui.Panel("second", new Style()))
                    {
                        second = ui.CurrentId;
                    }
                });
        });

        Assert.Equal(200, host.Bounds(first).Width);
        Assert.Equal(293, host.Bounds(second).Width);
        Assert.Equal(207, host.Bounds(second).X);
    }
}
