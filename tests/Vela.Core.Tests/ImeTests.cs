using Xunit;

namespace Vela.Tests;

public sealed class ImeTests
{
    [Fact]
    public void CompositionIsAvailableAsPreeditWithoutCommittingText()
    {
        using var host = new UiTestHost(new Size(400, 300));
        var value = string.Empty;
        host.Frame(
            ui => ui.TextArea("notes", ref value, style: new Style { Height = 140 }),
            input => input.SetComposition("にほん", 2));

        Assert.Equal("にほん", host.Ui.Input.CompositionText);
        Assert.True(host.Ui.Input.HasComposition);
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void DroppedFilesAreExposedForOneFrame()
    {
        using var host = new UiTestHost();
        string[]? seen = null;
        host.Frame(ui => seen = ui.Input.DroppedFiles.ToArray(), input => input.AddDroppedFile("C:\\a.txt"));
        Assert.NotNull(seen);
        Assert.Single(seen);
        Assert.Equal("C:\\a.txt", seen[0]);

        host.Frame(ui => seen = ui.Input.DroppedFiles.ToArray());
        Assert.NotNull(seen);
        Assert.Empty(seen);
    }
}

