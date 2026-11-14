using System.Runtime.CompilerServices;

namespace Vela;

/// <summary>
/// 有状态 UI 的稳定身份。Id = 父作用域 + 本地 key 的确定性哈希。
///
/// 性质：
/// <list type="bullet">
/// <item>跨帧稳定，与元素对象生命周期无关；</item>
/// <item>同一父级下同名 key 会冲突（StateStore 共用槽位、Frame 会报重复）；</item>
/// <item>进程/重启后仍可复现，因此可用于持久化 UI 状态；</item>
/// <item>哈希是 FNV-1a，不是安全哈希，只用于身份，不用于安全边界。</item>
/// </list>
///
/// 注意：Id 稳定不代表业务状态自动持久化；持久化仍需应用自己序列化。
/// </summary>
public readonly record struct Id(ulong Value)
{
    public static Id None => default;
    public bool IsNone => Value == 0;

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static Id From(string text) => new(Hash(text));

    public static Id Combine(Id parent, string key) => new(Mix(parent.Value, Hash(key)));

    public static Id Combine(Id parent, int key) => new(Mix(parent.Value, (ulong)(uint)key + 0x9E3779B97F4A7C15UL));

    public static Id Combine(Id parent, ulong key) => new(Mix(parent.Value, key));

    public static Id Combine(Id parent, Id child) => new(Mix(parent.Value, child.Value));

    /// <summary>FNV-1a over UTF-16 code units. Small, fast and deterministic.</summary>
    public static ulong Hash(string text)
    {
        var hash = FnvOffset;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        return hash == 0 ? 1UL : hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix(ulong a, ulong b)
    {
        var value = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return value == 0 ? 1UL : value;
    }

    public override string ToString() => $"#{Value:X16}";
}

/// <summary>The active ID scope stack. Container scopes push, widgets query.</summary>
internal sealed class IdScopeStack
{
    private ulong[] _values = new ulong[32];
    private int _depth;

    public IdScopeStack()
    {
        _values[0] = Id.Hash("root");
        _depth = 1;
    }

    public Id Current => new(_values[Math.Max(0, _depth - 1)]);

    internal int Depth => _depth;

    public void Push(Id id)
    {
        if (_depth == _values.Length)
        {
            Array.Resize(ref _values, _values.Length * 2);
        }

        _values[_depth++] = id.Value;
    }

    public void Push(string key)
    {
        Push(_depth == 0 ? Id.From(key) : Id.Combine(Current, key));
    }

    public void Push(int index) => Push(Id.Combine(Current, index));

    public void Pop()
    {
        if (_depth <= 1)
        {
            throw new InvalidOperationException("Cannot pop the root ID scope.");
        }

        _depth--;
    }

    internal void Reset() => _depth = 1;

    internal ulong[] Snapshot() => _values[.._depth];
}

