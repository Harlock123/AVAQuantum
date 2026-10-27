namespace AVAQuantum.Engine;

/// <summary>
/// The computer player, for the demonstration and the tests. It picks a particle to catch (the nearest it can loop
/// round safely, out of reach of the atom and the wall) and runs rings round it until the loop closes. Each step it
/// looks at the moves it could make for the one nearest its ring that stays clear of everything deadly over the next
/// moments: the electrons where their orbits will take them, charged pulsars, and whatever else is coming its way.
/// Now and then, with the atom whole and nothing near it, it loops the whole atom for the bonus.
/// </summary>
public sealed class Pilot
{
    private Particle? _target;
    private double _targetTime;
    private int _turn = 1;
    private bool _atom;
    /// <summary>The wave it last went for the atom in: one try a wave.</summary>
    private int _atomWave = -1;

    /// <summary>The ring it runs round the atom: just outside the electrons' reach.</summary>
    private const double AtomRing = Field.OrbitA + Field.ElectronR + Field.ProbeR + 16;

    /// <summary>How fast it moves the probe (pixels a second): about as fast as a hand on the ball.</summary>
    public const double Pace = 430;

    public string Doing { get; private set; } = "";

    public void Reset()
    {
        _target = null;
        _targetTime = 0;
        _atom = false;
        _atomWave = -1;
    }

    public Controls Decide(GameEngine g)
    {
        var w = g.World;
        var pr = w.Probe;
        if (g.State is not (GameState.Playing or GameState.Attract) || !pr.Free) return default;
        var me = new Pt(pr.X, pr.Y);
        Pick(w, me);
        var goal = Goal(w, me);
        // The moves it could make this step: a ring of directions, and straight for the goal.
        // Quicker round a quick particle: the loop has to close before it's away.
        double pace = _target is { } tg ? Math.Clamp(Pace + 1.6 * Math.Sqrt(tg.Vx * tg.Vx + tg.Vy * tg.Vy), Pace, 820) : Pace;
        double step = pace * Field.Step;
        double bestCost = double.MaxValue;
        Pt best = me;
        var dir = goal - me;
        double dl = dir.Length;
        var moves = new List<Pt>();
        if (dl > 1e-6) moves.Add(me + dir * (Math.Min(dl, step) / dl));
        for (int i = 0; i < 24; i++)
        {
            double a = i * Math.PI * 2 / 24;
            moves.Add(me + new Pt(Math.Cos(a), Math.Sin(a)) * step);
        }
        moves.Add(me);
        foreach (var m in moves)
        {
            double cost = (m - goal).Length * 0.5 + Danger(w, me, m);
            if (Field.FromCentre(m.X, m.Y) > Field.R - Field.ProbeR - 4) cost += 20;
            if (cost < bestCost) (bestCost, best) = (cost, m);
        }
        Doing = bestCost > 400 ? "dodging" : _atom ? "atom" : _target != null ? "looping" : "waiting";
        return new Controls(0, 0, true, best.X, best.Y);
    }

    /// <summary>How much trouble a move is: running into anything deadly in the next half second, keeping on that way.</summary>
    private static double Danger(World w, Pt me, Pt m)
    {
        if (w.Probe.Safe > 0.3) return 0;
        double cost = 0;
        var v = (m - me) * (1 / Field.Step);
        foreach (double t in (double[])[0.016, 0.1, 0.2, 0.35, 0.5])
        {
            var at = m + v * t;
            double weight = 1 - t * 1.2;
            if (w.AtomUp)
            {
                double clock = w.AtomClockIn(t);
                for (int k = 0; k < w.Electrons; k++)
                {
                    double d = (w.Electron(k, clock) - at).Length;
                    if (d < Field.ElectronR + Field.ProbeR + 16) cost += 3000 * weight * (1.2 - d / (Field.ElectronR + Field.ProbeR + 16));
                }
                double n = Field.FromCentre(at.X, at.Y);
                if (n < Field.NucleusR + Field.ProbeR + 14) cost += 3000 * weight;
            }
            foreach (var p in w.Particles)
            {
                if (p.Dead || p.Kind == Kind.Kitten) continue;
                bool deadly = p.Kind == Kind.Pulsar ? w.ChargedIn(p, t) : p.Out || p.Emerge < t;
                if (!deadly) continue;
                double margin = p.Kind == Kind.Butterfly ? 26 : 14;
                var pp = new Pt(p.X + p.Vx * t, p.Y + p.Vy * t);
                double d = (pp - at).Length, r = p.Radius + Field.ProbeR + margin;
                if (d < r) cost += 3000 * weight * (1.2 - d / r);
            }
        }
        return cost;
    }

    /// <summary>Chooses what to go after: the atom now and then, otherwise the handiest particle.</summary>
    private void Pick(World w, Pt me)
    {
        _targetTime += Field.Step;
        if (_atom && (!w.AtomUp || _targetTime > 7)) { _atom = false; _target = null; _targetTime = 0; }
        if (_target is { Dead: false } && _targetTime < 6 && Catchable(w, _target)) return;
        _target = null;
        _targetTime = 0;
        // The atom, when it's whole, the wave is well under way and nothing is close to its orbits.
        if (!_atom && w.AtomUp && !w.AtomBrokenThisWave && _atomWave != w.Wave && w.TrailLength > AtomRing * 7 && w.ToCome.Count == 0 && w.Particles.Count(p => p.Out) >= 1 && w.Clock % 30 > 24 &&
            !w.Particles.Any(p => p.Kind != Kind.Kitten && Field.FromCentre(p.X, p.Y) < Field.OrbitA + 80))
        {
            _atom = true;
            _atomWave = w.Wave;
            return;
        }
        if (_atom) return;
        _target = w.Particles.Where(p => !p.Dead && Catchable(w, p))
            .OrderBy(p => (new Pt(p.X, p.Y) - me).Length + (p.Kind == Kind.Butterfly ? 120 : 0) + (p.Kind == Kind.Kitten ? -80 : 0))
            .FirstOrDefault();
        _turn = -_turn;
    }

    /// <summary>Whether there's room to run a ring round it: away from the wall, and clear of the orbits.</summary>
    private static bool Catchable(World w, Particle p)
    {
        if (!p.Out) return false;
        var m = Mid(p);
        double r = Ring(p), far = Field.FromCentre(m.X, m.Y);
        if (far + r > Field.R - Field.ProbeR - 6) return false;
        if (w.AtomUp && far - r < Field.OrbitA + Field.ElectronR + Field.ProbeR + 8) return false;
        return true;
    }

    /// <summary>The middle of what it's looping: a foursquare's middle, so all four go together.</summary>
    private static Pt Mid(Particle p) => p.Kind == Kind.Square ? new Pt(p.Cx, p.Cy) : new Pt(p.X, p.Y);

    /// <summary>The size of ring to run round it.</summary>
    private static double Ring(Particle p) => p.Reach + Field.ProbeR + (p.Kind == Kind.Butterfly ? 24 : 14);

    /// <summary>The point to make for: a little way round the ring, ahead of where it is now.</summary>
    private Pt Goal(World w, Pt me)
    {
        if (_atom)
        {
            double ra = AtomRing;
            double a0 = Math.Atan2(me.Y - Field.CY, me.X - Field.CX);
            double far = Field.FromCentre(me.X, me.Y);
            double ahead = far > ra * 1.3 ? 0.2 : 0.55;
            return new Pt(Field.CX + Math.Cos(a0 + ahead) * ra, Field.CY + Math.Sin(a0 + ahead) * ra);
        }
        if (_target is not { } t)
        {
            // Nothing to go after: keep well clear of everything, out near the wall below the atom.
            return new Pt(Field.CX, Field.CY + Field.R * 0.75);
        }
        double r = Ring(t);
        // Where it will be in a moment: rings run round a moving particle have to keep up with it.
        var c = Mid(t) + new Pt(t.Vx, t.Vy) * 0.12;
        double a = Math.Atan2(me.Y - c.Y, me.X - c.X);
        double d = (me - c).Length;
        double lead = d > r * 1.5 ? 0.35 : 0.75;
        return c + new Pt(Math.Cos(a + _turn * lead), Math.Sin(a + _turn * lead)) * r;
    }
}
