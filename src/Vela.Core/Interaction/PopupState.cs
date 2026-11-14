namespace Vela;

/// <summary>Persistent open state and anchor for a named popup.</summary>
public sealed class PopupState
{
    public bool IsOpen { get; set; }
    public ulong AnchorId { get; set; }
    public Vec2 Point { get; set; }
    public bool AtPoint { get; set; }
    public double OpenedAt { get; set; }
    public ulong PreviousFocus { get; set; }
}

internal sealed class OverlayFocusState
{
    public bool WasOpen { get; set; }
    public ulong PreviousFocus { get; set; }
}

/// <summary>Hover timing for a tooltip target.</summary>
internal sealed class TooltipState
{
    public double HoverStarted { get; set; }
    public bool Visible { get; set; }
}


