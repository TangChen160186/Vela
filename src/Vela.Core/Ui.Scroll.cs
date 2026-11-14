namespace Vela;

/// <summary>An open scroll view. Dispose it to close the content scope.</summary>
public sealed class ScrollScope : IDisposable
{
    private readonly Ui _ui;
    private bool _disposed;

    internal ScrollScope(Ui ui, ScrollState state)
    {
        _ui = ui;
        State = state;
    }

    public ScrollState State { get; }
    public float Offset => State.Offset;
    public bool IsScrollable => State.IsScrollable;

    public void ScrollToTop() => State.JumpTo(0);
    public void ScrollToBottom() => State.JumpTo(State.MaxOffset);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ui.EndScope();
        _ui.EndScope();
    }
}

public sealed partial class Ui
{
    /// <summary>
    /// Starts a vertically scrolling region. The content column is sized to its
    /// natural height and clipped to the viewport; a wheel and draggable scrollbar
    /// are provided automatically. Dispose the returned scope to close it.
    /// </summary>
    public ScrollScope ScrollArea(string? id = null, float gap = 0, Style? style = null)
    {
        var frame = RequireFrame();
        var scrollStyle = style is null
            ? new Style { Direction = FlexDirection.Column, ClipContent = true }
            : style with { Direction = FlexDirection.Column, ClipContent = true };
        _ = frame.BeginContainer(ScrollWidget.Instance, id, scrollStyle, "scroll");
        var scrollId = frame.Current.Id;

        var contentStyle = gap == 0
            ? new Style { Direction = FlexDirection.Column }
            : new Style { Direction = FlexDirection.Column, Gap = gap };
        var content = frame.AddElement(FlexWidget.Instance, "content", contentStyle, "scroll-content", claimId: true);
        content.IsScrollContent = true;

        var scrollbar = frame.AddElement(
            ScrollbarWidget.Instance,
            "scrollbar",
            new Style { Width = 12 },
            "scrollbar",
            interactive: true,
            focusable: false,
            claimId: true);
        scrollbar.IsScrollbar = true;
        scrollbar.Cursor = CursorKind.Default;

        frame.PushScope(content);
        var state = State.GetOrCreate<ScrollState>(scrollId);
        return new ScrollScope(this, state);
    }
}
