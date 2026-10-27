using Avalonia.Input;

namespace AVAQuantum.Engine;

/// <summary>
/// Keyboard, mouse and gamepad boiled down to the controls. The original was played with a trackball; here the mouse
/// is the ball: the probe races to wherever it's rolled. The arrows, WASD or the stick spin the probe up and let it
/// run down. Down the screen is +1.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager _pad = new(0);
    private double _mouseX = Field.CX, _mouseY = Field.CY;
    /// <summary>The mouse has been moved since the keys or the stick were last used: it's in charge.</summary>
    private bool _mouseLive;

    /// <summary>The probe follows the mouse (on unless turned off).</summary>
    public bool Mouse { get; set; } = true;

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _held.Remove(was);
            _pressedAs[physical] = key;
        }
        _held.Remove(key);
        _held.Add(key);
    }

    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _held.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _held.Remove(was);
    }

    /// <summary>The pointer, in the game's own 1024×768 screen.</summary>
    public void MouseAt(double x, double y)
    {
        if (Math.Abs(x - _mouseX) + Math.Abs(y - _mouseY) > 0.5) _mouseLive = true;
        (_mouseX, _mouseY) = (x, y);
    }

    /// <summary>Where the pointer is, in the game's own screen.</summary>
    public (double X, double Y) Pointer => (_mouseX, _mouseY);

    /// <summary>Whether the mouse is steering the probe just now.</summary>
    public bool MouseSteering => Mouse && _mouseLive;

    public bool Has(Key key) => _held.Contains(key);
    private bool Any(params Key[] keys) => keys.Any(_held.Contains);

    public void Update() => _pad.Update();

    /// <summary>
    /// The arrows or WASD (or the numeric keypad) push the probe about; on a gamepad, the left stick (as far as it's
    /// pushed) or the D-pad. With the mouse: the probe races to the pointer. The keys or the stick take over from the
    /// mouse until it's moved again.
    /// </summary>
    public Controls Player
    {
        get
        {
            var pad = _pad;
            double dx = (Any(Key.Right, Key.D, Key.NumPad6) ? 1 : 0) - (Any(Key.Left, Key.A, Key.NumPad4) ? 1 : 0);
            // Down the screen is +1, as it is in the game.
            double dy = (Any(Key.Down, Key.S, Key.NumPad2) ? 1 : 0) - (Any(Key.Up, Key.W, Key.NumPad8) ? 1 : 0);
            if (dx == 0 && dy == 0)
            {
                if (pad.DPadLeft || pad.DPadRight || pad.DPadUp || pad.DPadDown)
                {
                    dx = (pad.DPadRight ? 1 : 0) - (pad.DPadLeft ? 1 : 0);
                    dy = (pad.DPadDown ? 1 : 0) - (pad.DPadUp ? 1 : 0);
                }
                else (dx, dy) = (pad.LeftStickX, pad.LeftStickY);
            }
            if (dx != 0 || dy != 0)
            {
                _mouseLive = false;
                return new Controls(dx, dy);
            }
            if (MouseSteering) return new Controls(0, 0, true, _mouseX, _mouseY);
            return default;
        }
    }

    /// <summary>Enter, 1, Space or the start button.</summary>
    public bool Start => Any(Key.Enter, Key.D1, Key.NumPad1, Key.Space) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
