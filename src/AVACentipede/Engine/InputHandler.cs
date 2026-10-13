using Avalonia.Input;
using AVACentipede.Models;

namespace AVACentipede.Engine;

/// <summary>
/// Keyboard, mouse and gamepad boiled down to the controls: a direction or a point to steer to, and fire.
/// The mouse works like the arcade's trackball: the shooter chases the pointer.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressed = [];
    private readonly ControllerManager _pad = new(0);
    private bool _mouseFire;
    private Vec? _pointer;
    private bool _mouseSteers;

    public void OnKeyDown(Key key)
    {
        _pressed.Add(key);
        if (key is Key.Left or Key.Right or Key.Up or Key.Down or Key.A or Key.D or Key.W or Key.S) _mouseSteers = false;
    }

    public void OnKeyUp(Key key) => _pressed.Remove(key);
    public bool Has(Key key) => _pressed.Contains(key);
    public void OnMouse(bool down) => _mouseFire = down;

    /// <summary>The pointer in playfield coordinates; moving it hands steering to the mouse.</summary>
    public void OnPointer(Vec field)
    {
        if (_pointer is { } old && Math.Abs(old.X - field.X) + Math.Abs(old.Y - field.Y) > 1) _mouseSteers = true;
        _pointer = field;
    }

    public void Update() => _pad.Update();

    public Controls Controls
    {
        get
        {
            int dx = (Has(Key.Right) || Has(Key.D) || _pad.DPadRight || _pad.LeftStickX > 0.35f ? 1 : 0)
                   - (Has(Key.Left) || Has(Key.A) || _pad.DPadLeft || _pad.LeftStickX < -0.35f ? 1 : 0);
            int dy = (Has(Key.Down) || Has(Key.S) || _pad.DPadDown || _pad.LeftStickY > 0.35f ? 1 : 0)
                   - (Has(Key.Up) || Has(Key.W) || _pad.DPadUp || _pad.LeftStickY < -0.35f ? 1 : 0);
            if (dx != 0 || dy != 0) _mouseSteers = false;
            bool fire = Has(Key.Space) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || _mouseFire
                        || _pad.ButtonA || _pad.ButtonB || _pad.ButtonX || _pad.RightTrigger > 0.3f;
            return new Controls(dx, dy, fire, _mouseSteers ? _pointer : null);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.D1) || _pad.ButtonStart;

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
