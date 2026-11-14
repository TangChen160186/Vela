namespace Vela;

internal sealed class SpringAnimation
{
    public float Value;
    public float Velocity;
    public bool Initialized;
    public long LastFrame = -1;
}

public sealed partial class Ui
{
    /// <summary>
    /// A physically-based spring for interactive motion. The first call snaps to
    /// <paramref name="initial"/> (or the target), then integrates with the frame
    /// delta. The state is updated once per frame for a given ID.
    /// </summary>
    public float Spring(Id id, float target, float stiffness = 180f, float damping = 18f, float? initial = null)
    {
        var state = State.GetOrCreate<SpringAnimation>(id);
        var frame = Input.FrameNumber;
        if (!state.Initialized)
        {
            state.Value = initial ?? target;
            state.Velocity = 0;
            state.Initialized = true;
            state.LastFrame = frame;
            return state.Value;
        }

        if (state.LastFrame == frame)
        {
            return state.Value;
        }

        state.LastFrame = frame;
        var dt = (float)Math.Clamp(Input.DeltaTime, 0, 0.05);
        if (dt <= 0 || stiffness <= 0)
        {
            return state.Value;
        }

        var steps = Math.Max(1, (int)MathF.Ceiling(dt / (1f / 240f)));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            var acceleration = (target - state.Value) * stiffness - state.Velocity * damping;
            state.Velocity += acceleration * h;
            state.Value += state.Velocity * h;
        }

        if (MathF.Abs(target - state.Value) < 0.0005f && MathF.Abs(state.Velocity) < 0.01f)
        {
            state.Value = target;
            state.Velocity = 0;
        }

        return state.Value;
    }

    public float Spring(string key, float target, float stiffness = 180f, float damping = 18f, float? initial = null) =>
        Spring(Id.Combine(CurrentId, "spring:" + key), target, stiffness, damping, initial);
}
