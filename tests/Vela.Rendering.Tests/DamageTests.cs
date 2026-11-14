using Vela.Rendering.Skia;
using Xunit;

namespace Vela.Rendering.Tests;

public sealed class DamageTests
{
    [Fact]
    public void IdenticalFramesProduceNoDamage()
    {
        var tracker = new DrawDamageTracker();
        var list = new DrawList();
        list.FillRect(new Rect(10, 10, 100, 40), new Brush(Color.Red));
        Assert.Single(tracker.Update(list, 400, 300));

        var damage = tracker.Update(list, 400, 300);
        Assert.Empty(damage);
    }

    [Fact]
    public void ChangedCommandProducesBoundedDamage()
    {
        var tracker = new DrawDamageTracker();
        var list = new DrawList();
        list.FillRect(new Rect(10, 10, 100, 40), new Brush(Color.Red));
        tracker.Update(list, 400, 300);

        list.Clear();
        list.FillRect(new Rect(10, 10, 100, 40), new Brush(Color.Blue));
        var damage = tracker.Update(list, 400, 300);

        var rect = Assert.Single(damage);
        Assert.Equal(new Rect(10, 10, 100, 40), rect);
    }
}

