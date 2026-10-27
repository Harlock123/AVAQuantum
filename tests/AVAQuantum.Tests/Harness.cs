using AVAQuantum.Engine;

namespace AVAQuantum.Tests;

/// <summary>Worlds and games set up for the tests, with anything saved kept in a temporary folder.</summary>
internal static class Harness
{
    public static string TempFolder() => Path.Combine(Path.GetTempPath(), "avaquantum-tests-" + Guid.NewGuid().ToString("N"));

    public static GameEngine Game(int seed = 1) => new(HighScoreTable.Load(TempFolder()), seed) { Silent = true };

    /// <summary>An arena with nothing loose in it and nothing to come: the probe at the bottom, the atom whole.</summary>
    public static World Empty(int wave = 0)
    {
        var w = new World();
        w.Start(1);
        if (wave > 0) w.Begin(wave);
        w.ToCome.Clear();
        w.Particles.Clear();
        w.Probe.Safe = 0;
        return w;
    }

    public static void Run(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) w.Step(c);
    }

    /// <summary>Steers the probe to a point and holds it there.</summary>
    public static void GoTo(World w, double x, double y, int steps = 60)
    {
        for (int i = 0; i < steps; i++) w.Step(new Controls(0, 0, true, x, y));
    }

    /// <summary>Runs the probe once round a circle (a little over, so it crosses its own trail), at an easy pace.</summary>
    public static void Circle(World w, double cx, double cy, double r, double turns = 1.15, double pace = 400)
    {
        double a0 = Math.Atan2(w.Probe.Y - cy, w.Probe.X - cx);
        // Onto the circle first.
        GoTo(w, cx + Math.Cos(a0) * r, cy + Math.Sin(a0) * r, 20);
        double da = pace * Field.Step / r;
        for (double a = 0; a < turns * Math.PI * 2; a += da)
            w.Step(new Controls(0, 0, true, cx + Math.Cos(a0 + a) * r, cy + Math.Sin(a0 + a) * r));
    }
}
