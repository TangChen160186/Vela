namespace Vela;

/// <summary>Text clipboard access. The Win32 host provides a native implementation.</summary>
public interface IClipboard
{
    string? GetText();
    void SetText(string text);
}

public sealed class NullClipboard : IClipboard
{
    public static NullClipboard Instance { get; } = new();

    public string? GetText() => null;
    public void SetText(string text)
    {
    }
}
