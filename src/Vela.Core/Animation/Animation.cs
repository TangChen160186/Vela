namespace Vela;

/// <summary>
/// 单值指数平滑状态。Value/Target 都是当前帧的值；LastFrame 保证同一 ID
/// 在“构建 + 绘制”中被多次调用时，一帧只推进一次。
/// </summary>
internal sealed class FloatAnimation
{
    public float Value;
    public float Target;
    public bool Initialized;
    public long LastFrame = -1;
}

/// <summary>颜色版动画状态；与 FloatAnimation 分开，避免同一 Id 类型冲突。</summary>
internal sealed class ColorAnimation
{
    public Color Value;
    public Color Target;
    public bool Initialized;
    public long LastFrame = -1;
}

public sealed partial class Ui
{
    /// <summary>
    /// 指数平滑动画：value += (target-value) * (1 - exp(-speed*dt))。
    ///
    /// 语义：
    /// <list type="bullet">
    /// <item>首次调用吸附到 initial（未提供则直接吸附 target），不产生初始跳变；</item>
    /// <item>同一 ID 同帧多次调用只更新一次；</item>
    /// <item>dt 上限 0.1s，避免卡顿后一步跳完；</item>
    /// <item>speed&lt;=0 表示立即到目标。</item>
    /// </list>
    ///
    /// 动画状态存在 StateStore，因此同一个 Id 的控件跨帧连续。
    /// </summary>
    public float Animate(Id id, float target, float speed = 16f, float? initial = null)
    {
        var state = State.GetOrCreate<FloatAnimation>(id);
        var frame = Input.FrameNumber;
        if (!state.Initialized)
        {
            state.Value = initial ?? target;
            state.Target = target;
            state.Initialized = true;
            state.LastFrame = frame;
            return state.Value;
        }

        state.Target = target;
        if (state.LastFrame == frame)
        {
            return state.Value;
        }

        state.LastFrame = frame;
        var delta = (float)Math.Clamp(Input.DeltaTime, 0, 0.1);
        if (speed <= 0)
        {
            state.Value = target;
            return state.Value;
        }

        var t = 1f - MathF.Exp(-speed * delta);
        state.Value += (target - state.Value) * t;
        if (MathF.Abs(target - state.Value) < 0.0005f)
        {
            state.Value = target;
        }

        return state.Value;
    }

    /// <summary>按当前容器作用域 + key 组合动画 ID，适合控件内部使用。</summary>
    public float Animate(string key, float target, float speed = 16f, float? initial = null) =>
        Animate(Id.Combine(CurrentId, "anim:" + key), target, speed, initial);

    /// <summary>
    /// 颜色动画。内部 Id 追加 ":color"，所以同一个元素可以同时有
    /// 数值动画和颜色动画而不会覆盖 StateStore 槽位。
    /// </summary>
    public Color AnimateColor(Id id, Color target, float speed = 16f, Color? initial = null)
    {
        var state = State.GetOrCreate<ColorAnimation>(Id.Combine(id, "color"));
        var frame = Input.FrameNumber;
        if (!state.Initialized)
        {
            state.Value = initial ?? target;
            state.Target = target;
            state.Initialized = true;
            state.LastFrame = frame;
            return state.Value;
        }

        state.Target = target;
        if (state.LastFrame == frame)
        {
            return state.Value;
        }

        state.LastFrame = frame;
        var delta = (float)Math.Clamp(Input.DeltaTime, 0, 0.1);
        if (speed <= 0)
        {
            state.Value = target;
            return state.Value;
        }

        var t = 1f - MathF.Exp(-speed * delta);
        state.Value = Color.Lerp(state.Value, target, t);
        if (state.Value == target)
        {
            state.Value = target;
        }

        return state.Value;
    }

    public Color AnimateColor(string key, Color target, float speed = 16f, Color? initial = null) =>
        AnimateColor(Id.Combine(CurrentId, "anim:" + key), target, speed, initial);
}
