namespace Vela;

/// <summary>Logical keyboard keys. Text characters arrive separately through text input.</summary>
public enum Key
{
    Unknown = 0,
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Escape,
    Tab,
    CapsLock,
    Shift,
    Control,
    Alt,
    Space,
    Enter,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    Left,
    Right,
    Up,
    Down,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,
    PrintScreen,
    ScrollLock,
    Pause,
    NumLock,
    Numpad0,
    Numpad1,
    Numpad2,
    Numpad3,
    Numpad4,
    Numpad5,
    Numpad6,
    Numpad7,
    Numpad8,
    Numpad9,
    NumpadAdd,
    NumpadSubtract,
    NumpadMultiply,
    NumpadDivide,
    NumpadDecimal,
    NumpadEnter,
    Semicolon,
    Equal,
    Comma,
    Minus,
    Period,
    Slash,
    Backtick,
    LeftBracket,
    Backslash,
    RightBracket,
    Quote,
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Super = 8,
}

[Flags]
public enum PointerButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    X1 = 8,
    X2 = 16,
}

/// <summary>A keyboard transition observed in the current frame.</summary>
public readonly record struct KeyEvent(Key Key, bool IsDown, bool IsRepeat, ModifierKeys Modifiers);
