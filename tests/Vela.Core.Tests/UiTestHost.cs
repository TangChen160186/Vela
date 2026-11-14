using Xunit;

namespace Vela.Tests;

internal sealed class UiTestHost : IDisposable
{
    private readonly InputBuilder _input = new();
    private double _time;

    public UiTestHost(Size? viewport = null)
    {
        Viewport = viewport ?? new Size(800, 600);
        Text = new FakeTextEngine();
        Ui = new Ui(Text, Theme.Dark()) { CaptureDiagnostics = true };
    }

    public FakeTextEngine Text { get; }
    public Ui Ui { get; }
    public Size Viewport { get; }
    public UiFrameResult Result { get; private set; } = null!;

    public void Frame(Action<Ui> build, Action<InputBuilder>? input = null)
    {
        _input.BeginFrame(_time);
        input?.Invoke(_input);
        var frameInput = _input.Build(Viewport);
        Ui.BeginFrame(frameInput);
        build(Ui);
        Result = Ui.EndFrame();
        _time += 1.0 / 60.0;
    }

    public Rect Bounds(Id id)
    {
        var element = Result.Diagnostics?.Find(id);
        Assert.NotNull(element);
        return element!.Value.Bounds;
    }

    public ElementInfo Element(string name)
    {
        foreach (var element in Result.Diagnostics!.Elements)
        {
            if (element.Name == name)
            {
                return element;
            }
        }

        throw new Xunit.Sdk.XunitException($"Element '{name}' was not found.");
    }

    public void Dispose() => Ui.AbortFrame();
}
