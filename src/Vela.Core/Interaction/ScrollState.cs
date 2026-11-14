namespace Vela;

/// <summary>
/// 滚动视图的跨帧状态。
///
/// Offset 是当前呈现位置；Target 是平滑滚动目标。ScrollBy/ScrollTo 只改 Target，
/// Tick 每帧把 Offset 逼近 Target；JumpTo 直接同步两者（滚动条拖动）。
/// ContentHeight/ViewportHeight 由 ScrollWidget 在 Arrange 中写入。
/// </summary>
public sealed class ScrollState
{
    private float _offset;
    private float _target;

    public float Offset => _offset;
    public float Target => _target;
    public float ContentHeight { get; set; }
    public float ViewportHeight { get; set; }
    public Rect TrackBounds { get; set; }
    public Rect ThumbBounds { get; set; }
    public float MaxOffset => MathF.Max(0, ContentHeight - ViewportHeight);
    public bool IsScrollable => MaxOffset > 0.5f;

    /// <summary>Scrolling speed in logical pixels per wheel notch.</summary>
    public float WheelStep { get; set; } = 58;

    public void ScrollBy(float logicalDelta)
    {
        _target = Math.Clamp(_target + logicalDelta, 0, MaxOffset);
    }

    public void ScrollTo(float offset)
    {
        _target = Math.Clamp(offset, 0, MaxOffset);
    }

    public void JumpTo(float offset)
    {
        _target = Math.Clamp(offset, 0, MaxOffset);
        _offset = _target;
    }

    public void SetMetrics(float contentHeight, float viewportHeight)
    {
        ContentHeight = MathF.Max(0, contentHeight);
        ViewportHeight = MathF.Max(0, viewportHeight);
        _target = Math.Clamp(_target, 0, MaxOffset);
        _offset = Math.Clamp(_offset, 0, MaxOffset);
    }

    /// <summary>Advances smoothing. Large jumps are snapped so the wheel stays responsive.</summary>
    public void Tick(double deltaTime)
    {
        var max = MaxOffset;
        _target = Math.Clamp(_target, 0, max);
        if (MathF.Abs(_target - _offset) < 0.25f)
        {
            _offset = _target;
            return;
        }

        var speed = Math.Clamp((float)deltaTime * 24f, 0, 1);
        _offset += (_target - _offset) * speed;
        if (MathF.Abs(_target - _offset) < 0.5f)
        {
            _offset = _target;
        }
    }
}

/// <summary>State for a scrollbar thumb drag.</summary>
public sealed class ScrollbarDragState
{
    public bool Dragging { get; set; }
    public float StartOffset { get; set; }
    public float StartPointerY { get; set; }
    public float TrackHeight { get; set; }
    public float ThumbHeight { get; set; }
    public float ContentHeight { get; set; }
    public float ViewportHeight { get; set; }
}
