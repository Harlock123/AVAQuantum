using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAQuantum.Engine;
using AVAQuantum.Views;

namespace AVAQuantum.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set QUANTUM_SHOTS to a folder to save
/// them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avaquantum-ui-" + Guid.NewGuid().ToString("N"))), seed) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("QUANTUM_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAQuantum-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer player play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 120)
    {
        for (int t = 0; t < 60 * maxSeconds; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            // Kept alive for the pictures: they're of the game, not of the player's luck.
            g.World.Lives = Math.Max(g.World.Lives, 3);
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    private static void Play(string name, int wave, Func<GameEngine, bool> moment, int seconds = 120, int seed = 11)
    {
        var (w, c, g) = Open(seed);
        g.Go(wave);
        g.Pilot.Reset();
        Assert.True(PlayUntil(g, c, moment, seconds), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        for (int i = 0; i < 60 * 3; i++) c.Advance(1 / 60.0);
        Shot(w, c, "title");
        g.HighScores.Insert("ACE", 182450, 16);
        g.HighScores.Insert("AVA", 95300, 11);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 60 && g.State != GameState.HighScores; i++) g.Idle(1);
        c.Advance(1);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void A_wave_begins()
    {
        var (w, c, g) = Open();
        g.StartGame();
        g.Run(70);
        c.Advance(0.7);
        Shot(w, c, "wave-start");
    }

    [AvaloniaFact]
    public void Looping_a_particle() => Play("looping", 4, g => g.Phase == Phase.Play && g.World.Trail.Count > 40
        && g.Pilot.Doing == "looping" && g.World.Particles.Count(p => p.Out) >= 5 && g.World.Probe.Safe <= 0);

    [AvaloniaFact]
    public void A_catch() => Play("catch", 7, g => g.World.Catches.Any(k => k.Count >= 2 && k.T > 0.05) && g.World.Popups.Count > 0, seconds: 200);

    [AvaloniaFact]
    public void Breaking_the_atom() => Play("atom", 1, g => g.World.Catches.Any(k => k.T > 0.03) && !g.World.AtomUp && g.World.AtomDown > Field.AtomDownSeconds - 0.3, seconds: 200);
}
