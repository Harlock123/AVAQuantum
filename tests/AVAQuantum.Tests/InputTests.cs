using Avalonia.Input;
using AVAQuantum.Engine;
using static AVAQuantum.Tests.Harness;

namespace AVAQuantum.Tests;

/// <summary>The keys and the mouse, through the input handler and into the game.</summary>
public sealed class InputTests
{
    /// <summary>Holds keys down through the handler for a while, and says where the probe went.</summary>
    private static (double Dx, double Dy) Hold(InputHandler input, params Key[] keys)
    {
        var w = Empty();
        GoTo(w, Field.CX + 160, Field.CY + 160, 40);
        foreach (var k in keys) input.OnKeyDown(k);
        var (x, y) = (w.Probe.X, w.Probe.Y);
        for (int i = 0; i < 20; i++) w.Step(input.Player);
        foreach (var k in keys) input.OnKeyUp(k);
        return (w.Probe.X - x, w.Probe.Y - y);
    }

    [Fact]
    public void Up_moves_the_probe_up_the_screen_and_down_down_it()
    {
        using var input = new InputHandler();
        foreach (var key in new[] { Key.Up, Key.W, Key.NumPad8 }) Assert.True(Hold(input, key).Dy < -20, $"{key}");
        foreach (var key in new[] { Key.Down, Key.S, Key.NumPad2 }) Assert.True(Hold(input, key).Dy > 20, $"{key}");
    }

    [Fact]
    public void Left_and_right_move_it_across()
    {
        using var input = new InputHandler();
        foreach (var key in new[] { Key.Left, Key.A, Key.NumPad4 }) Assert.True(Hold(input, key).Dx < -20, $"{key}");
        foreach (var key in new[] { Key.Right, Key.D, Key.NumPad6 }) Assert.True(Hold(input, key).Dx > 20, $"{key}");
        Assert.Equal((0.0, 0.0), Hold(input));
    }

    [Fact]
    public void Two_keys_together_go_diagonally_no_faster()
    {
        using var input = new InputHandler();
        var (dx, dy) = Hold(input, Key.Up, Key.Left);
        Assert.True(dx < -10 && dy < -10);
        var (sx, _) = Hold(input, Key.Left);
        Assert.True(Math.Sqrt(dx * dx + dy * dy) <= Math.Abs(sx) + 0.5);
    }

    [Fact]
    public void Rolling_the_mouse_sends_the_probe_to_the_pointer()
    {
        using var input = new InputHandler();
        var w = Empty();
        input.MouseAt(380, 520);
        Assert.True(input.Player.Steer);
        for (int i = 0; i < 30; i++) w.Step(input.Player);
        Assert.Equal(380, w.Probe.X, 0.5);
        Assert.Equal(520, w.Probe.Y, 0.5);
        // The keys take over until the mouse is moved again.
        input.OnKeyDown(Key.Right);
        Assert.False(input.Player.Steer);
        Assert.Equal(1, input.Player.Dx);
        input.OnKeyUp(Key.Right);
        Assert.False(input.Player.Steer, "still the keys' until the mouse moves");
        input.MouseAt(390, 520);
        Assert.True(input.Player.Steer);
    }

    [Fact]
    public void With_the_mouse_turned_off_the_pointer_does_not_steer()
    {
        using var input = new InputHandler { Mouse = false };
        input.MouseAt(100, 100);
        Assert.False(input.Player.Steer);
    }

    [Fact]
    public void Looping_with_the_mouse_catches_a_particle()
    {
        using var input = new InputHandler();
        var w = Empty();
        var t = w.Put(Kind.Triangle, 512, 600);
        for (int i = 0; i < 160 && !t.Dead; i++)
        {
            double a = Math.PI / 2 + i * 0.05;
            input.MouseAt(512 + Math.Cos(a) * 55, 600 + Math.Sin(a) * 55);
            w.Step(input.Player);
        }
        Assert.True(t.Dead);
        Assert.False(w.Probe.Down);
    }

    [Fact]
    public void Looping_with_the_keys_catches_a_particle()
    {
        using var input = new InputHandler();
        var w = Empty();
        GoTo(w, 512, 680, 30);
        var t = w.Put(Kind.Triangle, 512, 620);
        // Round it with the keys: right, up, left, down past the start, right under it, and up across it.
        foreach (var (key, steps) in new[] { (Key.Right, 14), (Key.Up, 22), (Key.Left, 26), (Key.Down, 28), (Key.Right, 18), (Key.Up, 12) })
        {
            input.OnKeyDown(key);
            for (int i = 0; i < steps; i++) w.Step(input.Player);
            input.OnKeyUp(key);
        }
        Assert.True(t.Dead, $"caught (the probe ended at {w.Probe.X:0},{w.Probe.Y:0}; trail {string.Join(" ", w.Trail.Where((_, i) => i % 6 == 0).Select(q => $"{q.P.X:0},{q.P.Y:0}"))})");
        Assert.False(w.Probe.Down);
    }

    [Fact]
    public void Enter_or_Space_starts()
    {
        using var input = new InputHandler();
        Assert.False(input.Start);
        input.OnKeyDown(Key.Enter);
        Assert.True(input.Start);
        input.OnKeyUp(Key.Enter);
        input.OnKeyDown(Key.Space);
        Assert.True(input.Start);
    }
}
