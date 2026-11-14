using static SDL.SDL3;

namespace Vela.Platform.Sdl;

/// <summary>SDL3-backed text clipboard for portable hosts.</summary>
public sealed class SdlClipboard : IClipboard
{
    public string? GetText() => SDL_HasClipboardText() ? SDL_GetClipboardText() : null;

    public void SetText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _ = SDL_SetClipboardText(text);
        }
    }
}
