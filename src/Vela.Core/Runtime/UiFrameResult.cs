namespace Vela;

/// <summary>
/// 一帧完成后的输出，宿主只需要消费这个对象：
/// DrawList 交给渲染后端，Accessibility 交给平台无障碍桥，
/// Diagnostics 供开发工具/测试，Cursor 交给窗口。
/// </summary>
public sealed class UiFrameResult
{
    public required DrawList DrawList { get; init; }
    public required CursorKind Cursor { get; init; }
    public required FrameDiagnostics? Diagnostics { get; init; }
    public required AccessibilitySnapshot Accessibility { get; init; }
    public required long FrameNumber { get; init; }
    public required double Time { get; init; }
}

