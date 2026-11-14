using Xunit;

namespace Vela.Tests;

public sealed class InvokeTests
{
    [Fact]
    public void ProgrammaticInvokeClicksOnTheNextFrame()
    {
        using var host = new UiTestHost(new Size(400, 200));
        Id id = default;
        var clicked = false;
        host.Frame(ui =>
        {
            var response = ui.Button("Run", "run");
            id = response.Id;
            if (response.Clicked)
            {
                clicked = true;
            }
        });

        host.Ui.Invoke(id);
        host.Frame(ui =>
        {
            if (ui.Button("Run", "run").Clicked)
            {
                clicked = true;
            }
        });

        Assert.True(clicked);
    }
}
