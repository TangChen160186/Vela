namespace Vela;

/// <summary>
/// 跨帧状态仓库。键是 <c>(Id, typeof(T))</c>，不是单纯的 Id。
///
/// 这解决了同一个元素需要多种状态的冲突：
/// <list type="bullet">
/// <item><c>ScrollState</c>：滚动位置；</item>
/// <item><c>TextAreaState</c>：光标/选区/撤销；</item>
/// <item><c>VirtualRowsState</c>：滚动条拖动；</item>
/// <item>动画/浮层/其他控件私有状态。</item>
/// </list>
///
/// 每帧 BeginFrame 会定期清理超过 900 帧未使用的槽位，避免已消失控件的状态
/// 永久占用内存。应用不要依赖“状态会永久保留”；需要持久化时自行序列化。
/// </summary>
public sealed class StateStore
{
    private sealed class Slot
    {
        public object? Value;
        public int LastUsedFrame;
    }

    private readonly Dictionary<(ulong Id, Type Type), Slot> _slots = new(256);
    private int _frame;
    private int _sweepCountdown = 600;

    public int Count => _slots.Count;

    /// <summary>
    /// 标记新帧并偶尔扫描死状态。扫描不是每帧执行（600 帧一次），
    /// 避免遍历所有槽位成为稳定状态下的成本。
    /// </summary>
    internal void BeginFrame(int frame)
    {
        _frame = frame;
        if (--_sweepCountdown > 0)
        {
            return;
        }

        _sweepCountdown = 600;
        List<(ulong, Type)>? dead = null;
        foreach (var (key, slot) in _slots)
        {
            if (frame - slot.LastUsedFrame > 900)
            {
                (dead ??= new List<(ulong, Type)>()).Add(key);
            }
        }

        if (dead is not null)
        {
            foreach (var key in dead)
            {
                _slots.Remove(key);
            }
        }
    }

    public T GetOrCreate<T>(Id id, Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        var key = (id.Value, typeof(T));
        if (_slots.TryGetValue(key, out var existing) && existing.Value is T typed)
        {
            existing.LastUsedFrame = _frame;
            return typed;
        }

        var value = factory();
        _slots[key] = new Slot { Value = value, LastUsedFrame = _frame };
        return value;
    }

    public T GetOrCreate<T>(Id id) where T : class, new() => GetOrCreate(id, static () => new T());

    public bool TryGet<T>(Id id, out T? value) where T : class
    {
        if (_slots.TryGetValue((id.Value, typeof(T)), out var slot) && slot.Value is T typed)
        {
            slot.LastUsedFrame = _frame;
            value = typed;
            return true;
        }

        value = null;
        return false;
    }

    public T? Get<T>(Id id) where T : class => TryGet<T>(id, out var value) ? value : null;

    public void Set<T>(Id id, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        _slots[(id.Value, typeof(T))] = new Slot { Value = value, LastUsedFrame = _frame };
    }

    public bool Remove(Id id)
    {
        List<(ulong, Type)>? keys = null;
        foreach (var key in _slots.Keys)
        {
            if (key.Id == id.Value)
            {
                (keys ??= new List<(ulong, Type)>()).Add(key);
            }
        }

        if (keys is null)
        {
            return false;
        }

        foreach (var key in keys)
        {
            _slots.Remove(key);
        }

        return true;
    }

    public void Clear() => _slots.Clear();
}
