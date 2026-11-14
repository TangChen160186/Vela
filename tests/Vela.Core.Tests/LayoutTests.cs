using Xunit;

namespace Vela.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void RowDistributesFixedAndGrowWidths()
    {
        using var host = new UiTestHost(new Size(800, 600));
        Id leftId = default;
        Id rightId = default;
        host.Frame(ui =>
        {
            using (ui.Row("row", new Style { Height = 100 }))
            {
                using (ui.Panel("left", new Style { Width = 200 }))
                {
                    leftId = ui.CurrentId;
                }

                using (ui.Panel("right", new Style { Grow = 1 }))
                {
                    rightId = ui.CurrentId;
                }
            }
        });

        var left = host.Bounds(leftId);
        var right = host.Bounds(rightId);
        Assert.Equal(new Rect(0, 0, 200, 100), left);
        Assert.Equal(new Rect(200, 0, 600, 100), right);
    }

    [Fact]
    public void ColumnAppliesGapBetweenChildren()
    {
        using var host = new UiTestHost(new Size(400, 300));
        host.Frame(ui =>
        {
            using (ui.Column("column", new Style { Gap = 10 }))
            {
                ui.Spacer(new Style { Height = 20 });
                ui.Spacer(new Style { Height = 20 });
            }
        });

        var spacerInfo = host.Result.Diagnostics!.Elements.Where(e => e.Name == "spacer").ToArray();
        Assert.Equal(2, spacerInfo.Length);
        Assert.Equal(0, spacerInfo[0].Bounds.Top);
        Assert.Equal(30, spacerInfo[1].Bounds.Top);
    }

    [Fact]
    public void ScrollViewportClipsAndMeasuresContent()
    {
        using var host = new UiTestHost(new Size(500, 400));
        host.Frame(ui =>
        {
            using var scroll = ui.ScrollArea("scroll", 0, new Style { Width = 300, Height = 160 });
            for (var i = 0; i < 20; i++)
            {
                ui.Spacer(new Style { Height = 20 });
            }
        });

        var scrollInfo = host.Element("scroll");
        Assert.Equal(new Rect(0, 0, 300, 160), scrollInfo.Bounds);
        var state = host.Ui.State.Get<ScrollState>(scrollInfo.Id);
        Assert.NotNull(state);
        Assert.True(state!.ContentHeight >= 400);
        Assert.True(state.IsScrollable);
    }

    [Fact]
    public void TextWrapsWithinTightWidth()
    {
        using var host = new UiTestHost(new Size(300, 400));
        host.Frame(ui =>
        {
            ui.Text(
                "A fairly long sentence that should wrap across several lines in a narrow column.",
                new Style { MaxWidth = 160 },
                new FontSpec("Test", 12));
        });

        var textElement = host.Result.Diagnostics!.Elements.Single(e => e.Name == "text");
        Assert.True(textElement.Bounds.Height > 30);
        Assert.True(textElement.Bounds.Width <= 161);
    }
}
