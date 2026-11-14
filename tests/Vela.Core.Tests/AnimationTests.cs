using Xunit;

namespace Vela.Tests;

public sealed class AnimationTests
{
    [Fact]
    public void FloatAnimationSmoothsTowardTarget()
    {
        using var host = new UiTestHost();
        var value = 0f;
        host.Frame(ui => value = ui.Animate("progress", 1f, speed: 16f, initial: 0f));
        Assert.Equal(0f, value, 4);

        host.Frame(ui => value = ui.Animate("progress", 1f, speed: 16f));
        Assert.InRange(value, 0.01f, 0.99f);

        for (var i = 0; i < 120; i++)
        {
            host.Frame(ui => value = ui.Animate("progress", 1f, speed: 16f));
        }

        Assert.Equal(1f, value, 3);
    }

    [Fact]
    public void ColorAnimationInterpolates()
    {
        using var host = new UiTestHost();
        var value = Color.Black;
        host.Frame(ui => value = ui.AnimateColor("color", Color.White, speed: 16f, initial: Color.Black));
        Assert.Equal(Color.Black, value);

        host.Frame(ui => value = ui.AnimateColor("color", Color.White, speed: 16f));
        Assert.InRange(value.R, (byte)1, (byte)254);
    }
}
