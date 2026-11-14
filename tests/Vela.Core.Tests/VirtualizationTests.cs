using Xunit;

namespace Vela.Tests;

public sealed class VirtualizationTests
{
    [Fact]
    public void VirtualListOnlyPaintsVisibleRows()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var selected = 0;
        var painted = 0;
        host.Frame(ui =>
        {
            painted = 0;
            ui.VirtualList("list", 10_000, 20, _ => painted++, ref selected, new Style { Height = 200 });
        });

        Assert.InRange(painted, 1, 30);
    }

    [Fact]
    public void VirtualListSelectsRowAndRespondsToKeyboard()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var selected = 0;
        host.Frame(ui => ui.VirtualList("list", 1_000, 20, _ => { }, ref selected, new Style { Height = 200 }));
        var bounds = host.Element("virtual-rows").Bounds;

        host.Frame(ui => ui.VirtualList("list", 1_000, 20, _ => { }, ref selected, new Style { Height = 200 }), input =>
        {
            input.SetPointerPosition(new Vec2(bounds.Left + 20, bounds.Top + (5 * 20) + 10));
            input.SetButton(PointerButton.Left, true);
        });
        Assert.Equal(5, selected);

        host.Frame(ui => ui.VirtualList("list", 1_000, 20, _ => { }, ref selected, new Style { Height = 200 }), input =>
        {
            input.SetPointerPosition(new Vec2(bounds.Left + 20, bounds.Top + (5 * 20) + 10));
            input.SetButton(PointerButton.Left, false);
        });
        host.Frame(ui => ui.VirtualList("list", 1_000, 20, _ => { }, ref selected, new Style { Height = 200 }), input => input.AddKeyDown(Key.Down));
        Assert.Equal(6, selected);
    }

    [Fact]
    public void DataTableRendersHeaderAndVirtualizedRows()
    {
        using var host = new UiTestHost(new Size(600, 400));
        var rows = Enumerable.Range(0, 5000).Select(i => $"Row {i}").ToList();
        var selected = 0;
        var painted = 0;
        var columns = new[]
        {
            new TableColumn<string>("Name", 240, value => value),
            new TableColumn<string>("Index", 100, value => value[4..], Paint: (_, _) => painted++),
        };
        host.Frame(ui =>
        {
            painted = 0;
            ui.DataTable("table", rows, columns, ref selected, style: new Style { Height = 300 });
        });
        Assert.InRange(painted, 1, 20);
        Assert.Contains(host.Result.Diagnostics!.Elements, e => e.Name == "data-table");
    }

    [Fact]
    public void TreeViewReportsChevronClicks()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var rows = new List<TreeRow>
        {
            new("Root", 0, true, true),
            new("Child", 1, false, false),
        };
        var selected = 1;
        TreeViewResult result = default;
        host.Frame(ui => result = ui.TreeView("tree", rows, ref selected, new Style { Height = 160 }));
        var bounds = host.Element("virtual-rows").Bounds;
        host.Frame(ui => result = ui.TreeView("tree", rows, ref selected, new Style { Height = 160 }), input =>
        {
            input.SetPointerPosition(new Vec2(bounds.Left + 16, bounds.Top + 16));
            input.SetButton(PointerButton.Left, true);
        });
        host.Frame(ui => result = ui.TreeView("tree", rows, ref selected, new Style { Height = 160 }), input =>
        {
            input.SetPointerPosition(new Vec2(bounds.Left + 16, bounds.Top + 16));
            input.SetButton(PointerButton.Left, false);
        });
        Assert.Equal(0, result.ToggleIndex);
    }
}
