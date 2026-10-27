using AVAQuantum.Engine;

namespace AVAQuantum.Tests;

/// <summary>
/// How the computer player gets on over whole games (set QUANTUM_PROBE to run it, for tuning: "1" from the first
/// wave, or a wave number to start there). QUANTUM_SEEDS picks the games.
/// </summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_player_does()
    {
        string? mode = Environment.GetEnvironmentVariable("QUANTUM_PROBE");
        if (mode is null) return;
        int from = int.TryParse(mode, out int n) && n > 1 ? n - 1 : 0;
        var seeds = (Environment.GetEnvironmentVariable("QUANTUM_SEEDS") ?? "1,2,3").Split(',').Select(int.Parse);
        foreach (int seed in seeds)
        {
            var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "quantum-probe-" + Guid.NewGuid().ToString("N"))), seed) { Silent = true };
            g.Go(from);
            g.Pilot.Reset();
            var deaths = new List<string>();
            int t = 0, wave = from;
            double waveAt = 0;
            var waves = new List<string>();
            bool wasDown = false;
            var doing = new Dictionary<string, int>();
            for (; t < 60 * 600 && g.State == GameState.Playing; t++)
            {
                var w = g.World;
                g.Tick(g.Pilot.Decide(g));
                doing[g.Pilot.Doing] = doing.GetValueOrDefault(g.Pilot.Doing) + 1;
                w = g.World;
                if (w.Probe.Down && !wasDown) deaths.Add($"{w.Wave + 1}:{Cause(w)}");
                wasDown = w.Probe.Down;
                if (w.Wave != wave) { waves.Add($"{t / 60.0 - waveAt:0}s"); waveAt = t / 60.0; wave = w.Wave; }
            }
            output.WriteLine($"seed {seed}: {g.State} at wave {g.World.Wave + 1} after {t / 60}s, score {g.Score}, lives {g.World.Lives}, caught {g.World.Caught}, best loop {g.World.BestLoop}, atoms {g.World.AtomsBroken}, deaths [{string.Join(" ", deaths)}], waves {string.Join(" ", waves)}; {string.Join(" ", doing.Select(d => $"{d.Key}={d.Value / 60}s"))}");
        }
    }

    /// <summary>What the probe ran into, as near as can be told.</summary>
    private static string Cause(World w)
    {
        var p = new Pt(w.Probe.X, w.Probe.Y);
        if (w.AtomUp && Field.FromCentre(p.X, p.Y) < Field.OrbitA + 20) return "atom";
        var near = w.Particles.Where(x => x.Out).MinBy(x => (new Pt(x.X, x.Y) - p).Length);
        return near?.Kind.ToString() ?? "?";
    }
}
