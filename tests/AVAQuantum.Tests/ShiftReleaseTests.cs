using Avalonia.Input;
using AVAQuantum.Engine;

namespace AVAQuantum.Tests;

/// <summary>With the keyboard option shift:both_capslock_cancel, X11 names a Shift's release CapsLock.</summary>
public sealed class ShiftReleaseTests
{
    [Fact]
    public void A_shift_let_go_as_caps_lock_is_let_go()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.LeftShift, PhysicalKey.ShiftLeft);
        input.OnKeyDown(Key.RightShift, PhysicalKey.ShiftRight);
        Assert.True(input.Has(Key.LeftShift) && input.Has(Key.RightShift));
        input.OnKeyUp(Key.CapsLock, PhysicalKey.ShiftLeft);
        Assert.False(input.Has(Key.LeftShift));
        Assert.True(input.Has(Key.RightShift));
        input.OnKeyUp(Key.CapsLock, PhysicalKey.ShiftRight);
        Assert.False(input.Has(Key.RightShift));
    }

    [Fact]
    public void An_arrow_let_go_as_another_key_is_let_go()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.Up, PhysicalKey.ArrowUp);
        Assert.True(input.Player.Dy < 0);
        input.OnKeyUp(Key.None, PhysicalKey.ArrowUp);
        Assert.Equal(0, input.Player.Dy);
    }
}
