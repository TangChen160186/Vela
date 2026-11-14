namespace Vela;

/// <summary>
/// 单个元素的诊断快照：Id、调试名、最终几何、深度、交互/焦点/可见状态和文本。
/// 只用于工具/测试，不参与运行时布局；可以在测试中按 Id 断言 Bounds。
/// </summary>
public readonly record struct ElementInfo(
    Id Id,
    string Name,
    Rect Bounds,
    Rect ContentBounds,
    int Depth,
    bool Interactive,
    bool Focused,
    bool Visible,
    string? Text = null);

/// <summary>
/// 最近一帧的耗时与结构信息。打开 Ui.CaptureDiagnostics 后才生成；
/// 阶段耗时分别对应 Build / Layout / Snapshot / Paint，不包含 Renderer 的 GPU/CPU 光栅化。
/// </summary>
public sealed class FrameDiagnostics
{
    public required long FrameNumber { get; init; }
    public required double BuildMilliseconds { get; init; }
    public required double LayoutMilliseconds { get; init; }
    public required double SnapshotMilliseconds { get; init; }
    public required double PaintMilliseconds { get; init; }
    public required int ElementCount { get; init; }
    public required int DrawCommandCount { get; init; }
    public required int StateCount { get; init; }
    public required int OverlayCount { get; init; }
    public required IReadOnlyList<ElementInfo> Elements { get; init; }
    public Id HoveredId { get; init; }
    public Id ActiveId { get; init; }
    public Id FocusedId { get; init; }

    public double TotalMilliseconds => BuildMilliseconds + LayoutMilliseconds + SnapshotMilliseconds + PaintMilliseconds;

    public ElementInfo? Find(Id id)
    {
        foreach (var element in Elements)
        {
            if (element.Id == id)
            {
                return element;
            }
        }

        return null;
    }
}
