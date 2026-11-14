using System.Text;

namespace Vela;

/// <summary>
/// Accumulates platform events between frames and produces an immutable
/// <see cref="FrameInput"/>. Platform backends call the Add/Set methods from the
/// UI thread; the builder is intentionally not thread-safe.
/// </summary>
public sealed class InputBuilder
{
    private readonly HashSet<Key> _downKeys = new();
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<Key> _releasedKeys = new();
    private readonly HashSet<Key> _repeatedKeys = new();
    private readonly List<KeyEvent> _events = new(32);
    private readonly StringBuilder _text = new(32);
    private readonly List<string> _droppedFiles = new(4);

    private Vec2 _pointer;
    private Vec2 _previousPointer;
    private bool _hasPointer;
    private bool _pointerInside;
    private PointerButton _buttons;
    private PointerButton _pressedButtons;
    private PointerButton _releasedButtons;
    private float _wheelX;
    private float _wheelY;
    private ModifierKeys _modifiers;
    private double _time;
    private double _lastTime;
    private double _deltaTime;
    private long _frame;
    private string _composition = string.Empty;
    private int _compositionCursor;
    private bool _hasTime;

    public bool PointerInside => _pointerInside;
    public Vec2 PointerPosition => _pointer;
    public PointerButton DownButtons => _buttons;
    public long FrameNumber => _frame;

    /// <summary>Starts a new event accumulation interval. Call once per frame.</summary>
    public void BeginFrame(double time)
    {
        _time = time;
        _deltaTime = _hasTime ? Math.Max(0, time - _lastTime) : 0;
        _lastTime = time;
        _hasTime = true;

        _pressedKeys.Clear();
        _releasedKeys.Clear();
        _repeatedKeys.Clear();
        _events.Clear();
        _text.Clear();
        _pressedButtons = PointerButton.None;
        _releasedButtons = PointerButton.None;
        _wheelX = 0;
        _wheelY = 0;
    }

    public void SetPointerPosition(Vec2 position, bool inside = true)
    {
        if (!_hasPointer)
        {
            _previousPointer = position;
            _hasPointer = true;
        }

        _pointer = position;
        _pointerInside = inside;
    }

    public void SetPointerInside(bool inside) => _pointerInside = inside;

    public void SetModifiers(ModifierKeys modifiers) => _modifiers = modifiers;

    public void SetButton(PointerButton button, bool down)
    {
        var alreadyDown = (_buttons & button) != 0;
        if (down)
        {
            _buttons |= button;
            if (!alreadyDown)
            {
                _pressedButtons |= button;
            }
        }
        else if (alreadyDown)
        {
            _buttons &= ~button;
            _releasedButtons |= button;
        }
    }

    public void AddWheel(float deltaX, float deltaY)
    {
        _wheelX += deltaX;
        _wheelY += deltaY;
    }

    public void AddKeyDown(Key key, bool isRepeat = false, ModifierKeys? modifiers = null)
    {
        var wasDown = _downKeys.Contains(key);
        _repeatedKeys.Remove(key);
        if (!wasDown)
        {
            _downKeys.Add(key);
            _pressedKeys.Add(key);
            _events.Add(new KeyEvent(key, true, false, modifiers ?? _modifiers));
        }
        else if (isRepeat)
        {
            _repeatedKeys.Add(key);
            _events.Add(new KeyEvent(key, true, true, modifiers ?? _modifiers));
        }
        else
        {
            return;
        }
    }

    public void AddKeyUp(Key key, ModifierKeys? modifiers = null)
    {
        if (_downKeys.Remove(key))
        {
            _releasedKeys.Add(key);
            _events.Add(new KeyEvent(key, false, false, modifiers ?? _modifiers));
        }
    }

    public void AddText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _text.Append(text);
        }
    }

    public void AddText(ReadOnlySpan<char> text)
    {
        if (!text.IsEmpty)
        {
            _text.Append(text);
        }
    }

    public void AddDroppedFile(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _droppedFiles.Add(path);
        }
    }

    /// <summary>Updates the platform IME composition string without committing it.</summary>
    public void SetComposition(string? text, int cursor = 0)
    {
        _composition = text ?? string.Empty;
        _compositionCursor = Math.Clamp(cursor, 0, _composition.Length);
    }

    public void ClearComposition()
    {
        _composition = string.Empty;
        _compositionCursor = 0;
    }

    /// <summary>Releases all held keys and buttons, for example when the window loses focus.</summary>
    public void ReleaseAllInputs()
    {
        _downKeys.Clear();
        _buttons = PointerButton.None;
        _pressedButtons = PointerButton.None;
        _releasedButtons = PointerButton.None;
        _pressedKeys.Clear();
        _releasedKeys.Clear();
        _repeatedKeys.Clear();
        _events.Clear();
        _text.Clear();
        _composition = string.Empty;
        _compositionCursor = 0;
        _droppedFiles.Clear();
        _pointerInside = false;
    }

    public FrameInput Build(Size viewportSize)
    {
        var delta = _hasPointer ? _pointer - _previousPointer : Vec2.Zero;
        if (!_pointerInside)
        {
            delta = Vec2.Zero;
        }

        var input = new FrameInput(
            viewportSize,
            _pointer,
            delta,
            _pointerInside,
            _buttons,
            _pressedButtons,
            _releasedButtons,
            _wheelX,
            _wheelY,
            _modifiers,
            _text.ToString(),
            _composition,
            _compositionCursor,
            _droppedFiles.ToArray(),
            _events.ToArray(),
            new HashSet<Key>(_downKeys),
            new HashSet<Key>(_pressedKeys),
            new HashSet<Key>(_releasedKeys),
            new HashSet<Key>(_repeatedKeys),
            _time,
            _deltaTime,
            _frame++);

        _previousPointer = _pointer;
        _hasPointer = true;

        // Transient state has been consumed. Held keys and buttons stay down.
        _pressedKeys.Clear();
        _releasedKeys.Clear();
        _repeatedKeys.Clear();
        _events.Clear();
        _text.Clear();
        _pressedButtons = PointerButton.None;
        _releasedButtons = PointerButton.None;
        _wheelX = 0;
        _wheelY = 0;
        _droppedFiles.Clear();

        return input;
    }
}


