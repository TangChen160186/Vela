namespace Vela;

/// <summary>
/// An immutable snapshot of input for one UI frame. Positions are in logical
/// (device-independent) pixels relative to the client area.
/// </summary>
public sealed class FrameInput
{
    private readonly HashSet<Key> _downKeys;
    private readonly HashSet<Key> _pressedKeys;
    private readonly HashSet<Key> _releasedKeys;
    private readonly HashSet<Key> _repeatedKeys;

    internal FrameInput(
        Size viewportSize,
        Vec2 pointerPosition,
        Vec2 pointerDelta,
        bool pointerInside,
        PointerButton downButtons,
        PointerButton pressedButtons,
        PointerButton releasedButtons,
        float wheelX,
        float wheelY,
        ModifierKeys modifiers,
        string textInput,
        string compositionText,
        int compositionCursor,
        IReadOnlyList<string> droppedFiles,
        IReadOnlyList<KeyEvent> keyEvents,
        HashSet<Key> downKeys,
        HashSet<Key> pressedKeys,
        HashSet<Key> releasedKeys,
        HashSet<Key> repeatedKeys,
        double time,
        double deltaTime,
        long frameNumber)
    {
        ViewportSize = viewportSize;
        PointerPosition = pointerPosition;
        PointerDelta = pointerDelta;
        PointerInside = pointerInside;
        DownButtons = downButtons;
        PressedButtons = pressedButtons;
        ReleasedButtons = releasedButtons;
        WheelX = wheelX;
        WheelY = wheelY;
        Modifiers = modifiers;
        TextInput = textInput;
        CompositionText = compositionText;
        CompositionCursor = compositionCursor;
        DroppedFiles = droppedFiles;
        KeyEvents = keyEvents;
        _downKeys = downKeys;
        _pressedKeys = pressedKeys;
        _releasedKeys = releasedKeys;
        _repeatedKeys = repeatedKeys;
        Time = time;
        DeltaTime = deltaTime;
        FrameNumber = frameNumber;
    }

    public Size ViewportSize { get; }
    public Vec2 PointerPosition { get; }
    public Vec2 PointerDelta { get; }
    public bool PointerInside { get; }
    public PointerButton DownButtons { get; }
    public PointerButton PressedButtons { get; }
    public PointerButton ReleasedButtons { get; }
    public float WheelX { get; }
    public float WheelY { get; }
    public ModifierKeys Modifiers { get; }
    public string TextInput { get; }
    public string CompositionText { get; }
    public int CompositionCursor { get; }
    public bool HasComposition => CompositionText.Length > 0;
    public IReadOnlyList<string> DroppedFiles { get; }
    public IReadOnlyList<KeyEvent> KeyEvents { get; }
    public double Time { get; }
    public double DeltaTime { get; }
    public long FrameNumber { get; }

    public bool IsButtonDown(PointerButton button) => (DownButtons & button) != 0;
    public bool WasButtonPressed(PointerButton button) => (PressedButtons & button) != 0;
    public bool WasButtonReleased(PointerButton button) => (ReleasedButtons & button) != 0;

    public bool IsKeyDown(Key key) => _downKeys.Contains(key);

    /// <summary>True only for the initial transition, not for auto-repeat.</summary>
    public bool WasKeyPressed(Key key) => _pressedKeys.Contains(key);

    public bool WasKeyReleased(Key key) => _releasedKeys.Contains(key);
    public bool WasKeyRepeated(Key key) => _repeatedKeys.Contains(key);

    public bool WasKeyPressedOrRepeated(Key key) => WasKeyPressed(key) || WasKeyRepeated(key);

    public bool HasModifier(ModifierKeys modifier) => (Modifiers & modifier) == modifier;

    public bool IsShortcut(Key key, ModifierKeys modifiers = ModifierKeys.Control)
    {
        if (!WasKeyPressed(key))
        {
            return false;
        }

        // Require exactly the requested modifier set for Shift/Alt/Super, while allowing
        // additional modifiers to be absent. This avoids Ctrl+Shift+Z matching Ctrl+Z.
        var relevant = ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Super;
        return (Modifiers & relevant) == modifiers;
    }

    public bool Ctrl => HasModifier(ModifierKeys.Control);
    public bool Shift => HasModifier(ModifierKeys.Shift);
    public bool Alt => HasModifier(ModifierKeys.Alt);
    public bool Super => HasModifier(ModifierKeys.Super);

    public static FrameInput Empty(Size viewport) => new(
        viewport,
        Vec2.Zero,
        Vec2.Zero,
        false,
        PointerButton.None,
        PointerButton.None,
        PointerButton.None,
        0,
        0,
        ModifierKeys.None,
        string.Empty,
        string.Empty,
        0,
        Array.Empty<string>(),
        Array.Empty<KeyEvent>(),
        new HashSet<Key>(),
        new HashSet<Key>(),
        new HashSet<Key>(),
        new HashSet<Key>(),
        0,
        0,
        0);
}


