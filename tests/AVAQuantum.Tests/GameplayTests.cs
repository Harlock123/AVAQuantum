using AVAQuantum.Engine;
using static AVAQuantum.Tests.Harness;

namespace AVAQuantum.Tests;

/// <summary>The rules: catching by looping the trail, the particles, the atom, lives and waves.</summary>
public sealed class GameplayTests
{
    /// <summary>A square loop round a point.</summary>
    private static Pt[] Box(double x, double y, double half) =>
        [new(x - half, y - half), new(x + half, y - half), new(x + half, y + half), new(x - half, y + half)];

    // ───────────────────────────── catching ─────────────────────────────

    [Fact]
    public void Looping_the_trail_round_a_particle_catches_it()
    {
        var w = Empty();
        var t = w.Put(Kind.Triangle, 512, 600);
        Circle(w, 512, 600, 50);
        Assert.True(t.Dead);
        Assert.Empty(w.Particles);
        Assert.Equal(100, w.Score);
        Assert.Equal(1, w.Caught);
        Assert.False(w.Probe.Down);
        Assert.Contains(Sfx.Catch, w.Heard);
    }

    [Fact]
    public void Closing_a_loop_clears_the_trail()
    {
        var w = Empty();
        w.Put(Kind.Triangle, 512, 600);
        Circle(w, 512, 600, 50, turns: 1.1);
        Assert.Empty(w.Particles);
        Assert.True(w.Trail.Count < 8, $"{w.Trail.Count} points left");
    }

    [Fact]
    public void A_loop_round_nothing_catches_nothing()
    {
        var w = Empty();
        var t = w.Put(Kind.Triangle, 512, 620);
        Circle(w, 380, 560, 50);
        Assert.False(t.Dead);
        Assert.Equal(0, w.Score);
        Assert.Contains(Sfx.Loop, w.Heard);
    }

    [Fact]
    public void A_line_that_never_crosses_itself_catches_nothing()
    {
        var w = Empty();
        var t = w.Put(Kind.Triangle, 512, 600);
        // Three-quarters of the way round, and stop.
        Circle(w, 512, 600, 50, turns: 0.75);
        Assert.False(t.Dead);
        Assert.True(w.Trail.Count > 20);
    }

    [Fact]
    public void A_little_wiggle_is_not_a_loop()
    {
        var w = Empty();
        var t = w.Put(Kind.Triangle, 512, 600);
        w.Probe.X = t.X + 60;
        w.Probe.Y = t.Y;
        // A tiny circle, crossing itself, far too small to hold anything.
        Circle(w, t.X + 60, t.Y, 8);
        Assert.False(t.Dead);
        Assert.Empty(w.Catches);
    }

    [Fact]
    public void Several_in_one_loop_multiply_the_points()
    {
        var w = Empty();
        w.Put(Kind.Triangle, 485, 625);
        w.Put(Kind.Triangle, 540, 625);
        w.Put(Kind.Triangle, 512, 590);
        Circle(w, 512, 612, 70);
        Assert.Empty(w.Particles);
        // 3 × 100, times 3.
        Assert.Equal(900, w.Score);
        Assert.Equal(3, w.BestLoop);
        Assert.Contains(Sfx.MultiCatch, w.Heard);
    }

    [Fact]
    public void The_particles_are_worth_different_points()
    {
        foreach (var (kind, points) in new[] { (Kind.Triangle, 100), (Kind.Square, 150), (Kind.Splitter, 200), (Kind.Pulsar, 300), (Kind.Butterfly, 400), (Kind.Kitten, 500) })
        {
            var w = Empty();
            w.Put(kind, 512, 620);
            w.Capture(Box(512, 620, 40));
            Assert.Equal(points, w.Score);
        }
    }

    [Fact]
    public void A_whole_foursquare_in_one_loop_earns_a_bonus()
    {
        var w = Empty();
        var four = w.PutFoursquare(512, 620);
        w.Capture(Box(512, 620, 50));
        Assert.All(four, p => Assert.True(p.Dead));
        Assert.Equal(4 * 150 * 4 + WaveBook.FoursquareBonus, w.Score);
    }

    [Fact]
    public void Part_of_a_foursquare_earns_no_bonus_and_the_rest_carry_on()
    {
        var w = Empty();
        var four = w.PutFoursquare(512, 620);
        var one = four.OrderBy(p => p.X).First();
        w.Capture(Box(one.X, one.Y, 12));
        Assert.True(one.Dead);
        Assert.Equal(150, w.Score);
        Run(w, 1);
        Assert.Equal(3, w.Particles.Count);
    }

    [Fact]
    public void The_squares_of_a_foursquare_wheel_round_their_middle_as_it_moves()
    {
        var w = Empty();
        var four = w.PutFoursquare(512, 620, vx: 40);
        var a0 = four[0].X;
        Run(w, 30);
        Assert.All(four, p => Assert.Equal(Particle.FourSpread, Math.Sqrt((p.X - p.Cx) * (p.X - p.Cx) + (p.Y - p.Cy) * (p.Y - p.Cy)), 3));
        Assert.True(four[0].Cx > 512 + 15, "the middle moved on");
        Assert.NotEqual(a0, four[0].X);
    }

    [Fact]
    public void A_splitter_splits_in_two_twice_then_goes()
    {
        var w = Empty();
        w.Put(Kind.Splitter, 512, 620, vx: 50);
        w.Capture(Box(512, 620, 40));
        Assert.Equal(200, w.Score);
        Run(w, 1);
        var halves = w.Particles.Where(p => !p.Dead).ToList();
        Assert.Equal(2, halves.Count);
        Assert.All(halves, h => Assert.Equal(1, h.Gen));
        Assert.All(halves, h => Assert.False(w.Deadly(h), "harmless for a moment, just split"));
        Assert.True(halves[0].Radius < new Particle { Kind = Kind.Splitter }.Radius);
        // They fly apart.
        Run(w, 45);
        Assert.True(Math.Sqrt(Math.Pow(halves[0].X - halves[1].X, 2) + Math.Pow(halves[0].Y - halves[1].Y, 2)) > 30);
        Assert.All(halves, h => Assert.True(w.Deadly(h), "then dangerous"));
        var h0 = halves[0];
        w.Capture(Box(h0.X, h0.Y, 15));
        Run(w, 1);
        Assert.Equal(2, w.Particles.Count(p => p.Gen == 2));
        foreach (var q in w.Particles.Where(p => p.Gen == 2).ToList()) w.Capture(Box(q.X, q.Y, 10));
        Run(w, 1);
        Assert.DoesNotContain(w.Particles, p => p.Gen > 2);
        Assert.Single(w.Particles);
    }

    // ───────────────────────────── what's deadly ─────────────────────────────

    [Fact]
    public void Touching_a_particle_destroys_the_probe()
    {
        foreach (var kind in new[] { Kind.Triangle, Kind.Square, Kind.Splitter, Kind.Butterfly })
        {
            var w = Empty();
            // A foursquare's first square sits to the right of its middle.
            if (kind == Kind.Square) w.PutFoursquare(w.Probe.X + 12 - Particle.FourSpread, w.Probe.Y);
            else w.Put(kind, w.Probe.X + 12, w.Probe.Y);
            Run(w, 1);
            Assert.True(w.Probe.Down, $"{kind}");
            Assert.Contains(Sfx.Zap, w.Heard);
        }
    }

    [Fact]
    public void A_kitten_is_harmless_to_the_probe()
    {
        var w = Empty();
        w.Put(Kind.Kitten, w.Probe.X + 5, w.Probe.Y);
        Run(w, 10);
        Assert.False(w.Probe.Down);
    }

    [Fact]
    public void A_pulsar_is_deadly_only_while_charged()
    {
        var w = Empty();
        var p = w.Put(Kind.Pulsar, w.Probe.X + 12, w.Probe.Y);
        p.Pulse = 0;
        Run(w, 1);
        Assert.False(w.Charged(p));
        Assert.False(w.Probe.Down, "dark, it's harmless");
        p.Pulse = w.PulsarPeriod * 0.7;
        (p.X, p.Y) = (w.Probe.X + 12, w.Probe.Y);
        Run(w, 1);
        Assert.True(w.Charged(p));
        Assert.True(w.Probe.Down, "charged, it kills");
    }

    [Fact]
    public void A_charged_pulsar_comes_after_the_probe()
    {
        var w = Empty();
        var p = w.Put(Kind.Pulsar, 300, 560);
        p.Pulse = w.PulsarPeriod * 0.56;
        double before = Math.Sqrt(Math.Pow(p.X - w.Probe.X, 2) + Math.Pow(p.Y - w.Probe.Y, 2));
        Run(w, 40);
        double after = Math.Sqrt(Math.Pow(p.X - w.Probe.X, 2) + Math.Pow(p.Y - w.Probe.Y, 2));
        Assert.True(after < before - 20, $"{before:0} -> {after:0}");
    }

    [Fact]
    public void The_electrons_and_the_nucleus_destroy_the_probe()
    {
        var w = Empty();
        var e = w.Electron(0, w.AtomClockIn(Field.Step));
        (w.Probe.X, w.Probe.Y) = (e.X, e.Y);
        Run(w, 1);
        Assert.True(w.Probe.Down, "an electron");
        w = Empty();
        (w.Probe.X, w.Probe.Y) = (Field.CX + 15, Field.CY);
        Run(w, 1);
        Assert.True(w.Probe.Down, "the nucleus");
    }

    [Fact]
    public void The_electrons_go_round_their_orbits()
    {
        var w = Empty();
        var a = w.Electron(0);
        Run(w, 20);
        var b = w.Electron(0);
        Assert.True((a - b).Length > 10);
        for (int i = 0; i < 200; i++)
        {
            Run(w, 1);
            for (int k = 0; k < w.Electrons; k++)
                Assert.InRange(Field.FromCentre(w.Electron(k).X, w.Electron(k).Y), Field.OrbitB - 0.01, Field.OrbitA + 0.01);
        }
    }

    [Fact]
    public void Just_come_back_the_probe_is_untouchable_for_a_moment()
    {
        var w = Empty();
        w.Probe.Safe = 1;
        w.Put(Kind.Triangle, w.Probe.X + 5, w.Probe.Y);
        Run(w, 10);
        Assert.False(w.Probe.Down);
    }

    [Fact]
    public void A_kitten_eats_the_trail()
    {
        var w = Empty();
        GoTo(w, 380, 620, 40);
        // A trail across the bottom of the arena.
        for (int i = 0; i < 40; i++) w.Step(new Controls(0, 0, true, 380 + i * 6, 620));
        Assert.True(w.Trail.Count > 20);
        w.Put(Kind.Kitten, 470, 640);
        for (int i = 0; i < 60 && w.Trail.Count > 3; i++) w.Step(new Controls(0, 0, true, w.Probe.X, w.Probe.Y));
        Assert.True(w.Trail.Count <= 3, $"{w.Trail.Count} points left");
        Assert.Contains(Sfx.Mew, w.Heard);
        Assert.False(w.Probe.Down);
    }

    // ───────────────────────────── the atom ─────────────────────────────

    [Fact]
    public void Looping_the_whole_atom_breaks_it_for_a_bonus_once_a_wave()
    {
        var w = Empty();
        Circle(w, Field.CX, Field.CY, Field.OrbitA + 40, turns: 1.08);
        Assert.False(w.Probe.Down);
        Assert.False(w.AtomUp);
        Assert.Equal(WaveBook.AtomBonus(0), w.Score);
        Assert.Contains(Sfx.AtomBreak, w.Heard);
        // While it's broken nothing at the middle can hurt.
        (w.Probe.X, w.Probe.Y) = (Field.CX, Field.CY);
        Run(w, 5);
        Assert.False(w.Probe.Down);
        // It forms again.
        Run(w, (int)(Field.AtomDownSeconds * 60));
        Assert.True(w.AtomUp);
        Assert.Contains(Sfx.AtomForm, w.Heard);
        // Broken again the same wave: no more bonus.
        w.Capture(Box(Field.CX, Field.CY, 150));
        Assert.False(w.AtomUp);
        Assert.Equal(WaveBook.AtomBonus(0), w.Score);
        w.Begin(1);
        w.Capture(Box(Field.CX, Field.CY, 150));
        Assert.Equal(WaveBook.AtomBonus(0) + WaveBook.AtomBonus(1), w.Score);
    }

    [Fact]
    public void Particles_keep_out_of_the_orbits_and_inside_the_wall()
    {
        var w = Empty();
        w.Put(Kind.Triangle, 512, 600, vx: 0, vy: -150);
        w.Put(Kind.Triangle, 512, 600, vx: 0, vy: 150);
        w.Put(Kind.Butterfly, 300, 400);
        w.PutFoursquare(700, 420, 60, 80);
        w.Probe.Safe = 1000;
        for (int i = 0; i < 60 * 20; i++)
        {
            Run(w, 1);
            foreach (var p in w.Particles)
            {
                double far = Field.FromCentre(p.X, p.Y);
                Assert.InRange(far, Field.OrbitA + Field.ElectronR, Field.R);
            }
        }
    }

    [Fact]
    public void Particles_come_out_of_the_nucleus_harmless_until_they_are_clear()
    {
        var w = new World();
        w.Start(3);
        w.Probe.Safe = 0;
        Run(w, 60);
        var p = w.Particles.First();
        Assert.False(p.Out);
        Assert.True(Field.FromCentre(p.X, p.Y) < Field.OrbitA + 140);
        Assert.False(w.Deadly(p));
        w.Capture(Box(p.X, p.Y, 30));
        Assert.False(p.Dead, "can't be caught coming out");
        Run(w, 60);
        Assert.True(p.Out);
        Assert.True(Field.FromCentre(p.X, p.Y) > Field.OrbitA + 20);
        Assert.True(w.Deadly(p));
    }

    // ───────────────────────────── the probe ─────────────────────────────

    [Fact]
    public void The_probe_races_to_a_point_and_never_leaves_the_arena()
    {
        var w = Empty();
        GoTo(w, 400, 520, 30);
        Assert.Equal(400, w.Probe.X, 0.5);
        Assert.Equal(520, w.Probe.Y, 0.5);
        GoTo(w, 0, 0, 60);
        Assert.True(Field.FromCentre(w.Probe.X, w.Probe.Y) <= Field.R);
        Run(w, 60, new Controls(1, 1));
        Assert.True(Field.FromCentre(w.Probe.X, w.Probe.Y) <= Field.R);
    }

    [Fact]
    public void Pushed_the_probe_picks_up_speed_and_runs_down_when_let_go()
    {
        var w = Empty();
        GoTo(w, 512, 640, 30);
        w.Step(new Controls(1, 0));
        double v1 = w.Probe.Vx;
        Run(w, 30, new Controls(1, 0));
        Assert.True(w.Probe.Vx > v1 * 3, "it picks up speed");
        Assert.True(w.Probe.Vx <= Field.ProbeSpeed + 0.01);
        Run(w, 30);
        Assert.True(w.Probe.Vx < 20, "and runs down");
    }

    [Fact]
    public void The_trail_is_only_so_long_and_fades_with_time()
    {
        var w = Empty();
        GoTo(w, 512, 640, 30);
        // Back and forth along a line many times.
        for (int i = 0; i < 400; i++) w.Step(new Controls(0, 0, true, 512 + 220 * Math.Sin(i * 0.1), 640));
        double length = 0;
        for (int i = 1; i < w.Trail.Count; i++) length += (w.Trail[i].P - w.Trail[i - 1].P).Length;
        Assert.InRange(length, w.TrailLength - 40, w.TrailLength + 20);
        Run(w, (int)(Field.TrailLife * 60) + 5, new Controls(0, 0, true, w.Probe.X, w.Probe.Y));
        Assert.True(w.Trail.Count <= 1);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void Clearing_a_wave_pays_a_bonus_and_brings_the_next_harder_one()
    {
        var g = Game();
        g.Go(0);
        g.World.ToCome.Clear();
        g.World.Particles.Clear();
        g.Run(1);
        Assert.Equal(Phase.Clear, g.Phase);
        Assert.Equal(WaveBook.ClearBonus(0), g.Score);
        g.Run((int)(GameEngine.ClearSeconds * 60) + 2);
        Assert.Equal(1, g.World.Wave);
        Assert.Equal(Phase.Ready, g.Phase);
        Assert.Equal("WAVE 2", g.Message);
        Assert.True(g.World.ToCome.Count > 0);
    }

    [Fact]
    public void The_waves_get_harder()
    {
        Assert.True(WaveBook.Speed(10) > WaveBook.Speed(0));
        Assert.True(WaveBook.Electrons(10) > WaveBook.Electrons(0));
        Assert.True(WaveBook.OrbitSpeed(10) > WaveBook.OrbitSpeed(0));
        Assert.True(WaveBook.TrailLength(10) < WaveBook.TrailLength(0));
        Assert.True(WaveBook.PulsarPeriod(10) < WaveBook.PulsarPeriod(0));
        var rng = new Random(1);
        Assert.True(WaveBook.For(30, rng).Count > WaveBook.For(0, rng).Count);
        // Every kind turns up in the opening waves.
        var seen = Enumerable.Range(0, WaveBook.Handmade).SelectMany(i => WaveBook.For(i, rng)).ToHashSet();
        Assert.Equal(Enum.GetValues<Kind>().Length, seen.Count);
    }

    [Fact]
    public void A_hit_costs_a_life_and_the_probe_comes_back_untouchable()
    {
        var g = Game();
        g.Go(0);
        g.World.Destroy();
        g.Run(1);
        Assert.Equal(Phase.Down, g.Phase);
        g.Run((int)(GameEngine.DownSeconds * 60) + 2);
        Assert.Equal(2, g.World.Lives);
        Assert.Equal(Phase.Play, g.Phase);
        Assert.True(g.World.Probe.Free);
        Assert.True(g.World.Probe.Safe > 0);
    }

    [Fact]
    public void Losing_the_last_life_ends_the_game_and_a_good_score_gets_initials()
    {
        var g = Game();
        g.Go(0);
        g.World.AddScore(60000);
        g.World.Lives = 1;
        g.World.Destroy();
        g.Run((int)(GameEngine.DownSeconds * 60) + 5);
        Assert.Equal(GameState.GameOver, g.State);
        g.Run(60 * 5);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('Q');
        g.Initials.Type('E');
        g.Initials.Type('D');
        g.Run(1);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(0, g.NewEntryIndex);
        Assert.Equal("QED", g.HighScores.Entries[0].Initials);
        Assert.Equal(1, g.HighScores.Entries[0].Wave);
        // Kept for next time.
        var again = HighScoreTable.Load(g.HighScores.Folder);
        Assert.Equal(g.HighScores.Entries[0], again.Entries[0]);
    }

    [Fact]
    public void A_poor_score_goes_straight_to_the_table()
    {
        var g = Game();
        g.Go(0);
        g.World.Lives = 1;
        g.World.Destroy();
        g.Run((int)(GameEngine.DownSeconds * 60) + 5 + 60 * 5);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(-1, g.NewEntryIndex);
    }

    [Fact]
    public void Extra_lives_come_with_the_points()
    {
        var w = Empty();
        Assert.Equal(3, w.Lives);
        w.AddScore(WaveBook.FirstLife);
        Assert.Equal(4, w.Lives);
        Assert.Contains(Sfx.ExtraLife, w.Heard);
        w.AddScore(WaveBook.EveryLife - 1);
        Assert.Equal(4, w.Lives);
        w.AddScore(1);
        Assert.Equal(5, w.Lives);
    }

    [Fact]
    public void Pausing_stops_everything()
    {
        var g = Game();
        g.Go(2);
        g.Run(60);
        g.Pause();
        Assert.Equal(GameState.Paused, g.State);
        var (x, y, clock) = (g.World.Particles[0].X, g.World.Particles[0].Y, g.World.Clock);
        g.Run(60, new Controls(1, 0));
        Assert.Equal((x, y, clock), (g.World.Particles[0].X, g.World.Particles[0].Y, g.World.Clock));
        g.Resume();
        Assert.Equal(GameState.Playing, g.State);
    }

    [Fact]
    public void The_title_the_demonstration_and_the_high_scores_take_turns()
    {
        var g = Game();
        Assert.Equal(GameState.Title, g.State);
        g.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle((int)(GameEngine.AttractSeconds * 60) + 2);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(GameEngine.HighScoreSeconds * 60) + 2);
        Assert.Equal(GameState.Title, g.State);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal("WAVE 1", g.Message);
    }

    [Fact]
    public void The_computer_player_survives_and_scores()
    {
        var g = Game(2);
        g.Go(0);
        g.Pilot.Reset();
        int deaths = 0;
        bool was = false;
        for (int t = 0; t < 60 * 150 && g.State == GameState.Playing; t++)
        {
            g.Tick(g.Pilot.Decide(g));
            if (g.World.Probe.Down && !was) deaths++;
            was = g.World.Probe.Down;
        }
        Assert.Equal(GameState.Playing, g.State);
        Assert.True(deaths <= 1, $"{deaths} deaths");
        Assert.True(g.World.Caught >= 20, $"caught {g.World.Caught}");
        Assert.True(g.World.Wave >= 3, $"wave {g.World.Wave + 1}");
        Assert.True(g.Score >= 8000, $"score {g.Score}");
    }

    [Fact]
    public void The_demonstration_plays_itself()
    {
        var g = Game(4);
        g.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 40);
        Assert.Equal(GameState.Attract, g.State);
        Assert.True(g.World.Caught > 3, $"caught {g.World.Caught}");
    }

    [Fact]
    public void Every_sound_has_its_file()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(folder, s + ".wav")), $"{s}.wav");
        Assert.Equal(Enum.GetValues<Sfx>().Length - 1, Directory.GetFiles(folder, "*.wav").Length);
    }
}
