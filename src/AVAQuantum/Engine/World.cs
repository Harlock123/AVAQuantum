namespace AVAQuantum.Engine;

/// <summary>
/// One game's arena: the atom in the middle with its electrons, the particles loose round it, and the probe with its
/// trail. <see cref="Step"/> moves it all on a sixtieth of a second. The probe kills nothing by touching it: it catches
/// particles by drawing its trail right round them, crossing its own trail to close the loop.
/// </summary>
public sealed class World
{
    public Random Rng { get; private set; } = new(1);
    public int Wave { get; private set; }
    public int Score { get; private set; }
    public int Lives = 3;
    public int NextLife = WaveBook.FirstLife;
    public double Clock;

    public Probe Probe { get; private set; } = new();
    /// <summary>The trail, oldest point first, each with when it was laid.</summary>
    public List<(Pt P, double T)> Trail { get; } = [];
    public List<Particle> Particles { get; } = [];
    /// <summary>What's still to come out of the nucleus this wave.</summary>
    public Queue<Kind> ToCome { get; } = new();
    public double SpawnClock;

    /// <summary>The electrons' angle round their orbits.</summary>
    public double AtomClock;
    public int Electrons = 2;
    public double OrbitSpeed = 1.8;
    /// <summary>While above 0 the atom is broken and forming again: no nucleus, no electrons.</summary>
    public double AtomDown;

    public List<Catch> Catches { get; } = [];
    public List<Spark> Sparks { get; } = [];
    public List<Popup> Popups { get; } = [];
    /// <summary>Sounds asked for this step; the engine plays them and clears the list.</summary>
    public List<Sfx> Heard { get; } = [];

    /// <summary>The world behind the title: nothing can hurt or be caught, and there's no probe.</summary>
    public bool Show;

    /// <summary>Everything caught this game.</summary>
    public int Caught;
    /// <summary>The most caught in one loop this game.</summary>
    public int BestLoop;
    public int AtomsBroken;
    /// <summary>The atom has been broken already this wave: no more bonus for it until the next.</summary>
    public bool AtomBrokenThisWave;
    private int _groups;

    public double Speed => WaveBook.Speed(Wave);
    public double PulsarPeriod => WaveBook.PulsarPeriod(Wave);
    public double TrailLength => WaveBook.TrailLength(Wave);
    public bool AtomUp => AtomDown <= 0;

    /// <summary>No more particles to come and none left loose.</summary>
    public bool Cleared => ToCome.Count == 0 && Particles.All(p => p.Dead);

    // ───────────────────────────── waves ─────────────────────────────

    public void Start(int seed)
    {
        Rng = new Random(seed);
        Score = 0;
        Lives = 3;
        NextLife = WaveBook.FirstLife;
        Begin(0);
    }

    /// <summary>A wave's start: the probe at the bottom of the arena, and the wave's particles queued in the nucleus.</summary>
    public void Begin(int wave)
    {
        Wave = wave;
        Particles.Clear();
        ToCome.Clear();
        foreach (var k in WaveBook.For(wave, Rng)) ToCome.Enqueue(k);
        SpawnClock = 0.9;
        Electrons = WaveBook.Electrons(wave);
        OrbitSpeed = WaveBook.OrbitSpeed(wave);
        AtomDown = 0;
        AtomBrokenThisWave = false;
        Trail.Clear();
        Catches.Clear();
        Popups.Clear();
        Probe = new Probe { Safe = 1.2 };
    }

    public void AddScore(int points)
    {
        if (Show) return;
        Score += points;
        while (Score >= NextLife)
        {
            Lives++;
            NextLife += WaveBook.EveryLife;
            Heard.Add(Sfx.ExtraLife);
        }
    }

    // ───────────────────────────── the atom ─────────────────────────────

    /// <summary>Where electron <paramref name="k"/> is at a moment on the atom's clock: each goes round its own ellipse,
    /// the ellipses turned evenly about the nucleus and slowly turning, like the drawing of an atom.</summary>
    public Pt Electron(int k, double clock)
    {
        double tilt = k * Math.PI / Electrons + clock * 0.12;
        double phase = clock + k * 2 * Math.PI / Electrons;
        double lx = Field.OrbitA * Math.Cos(phase), ly = Field.OrbitB * Math.Sin(phase);
        double c = Math.Cos(tilt), s = Math.Sin(tilt);
        return new Pt(Field.CX + lx * c - ly * s, Field.CY + lx * s + ly * c);
    }

    public Pt Electron(int k) => Electron(k, AtomClock);

    /// <summary>The atom's clock a little while from now.</summary>
    public double AtomClockIn(double seconds) => AtomClock + seconds * OrbitSpeed;

    /// <summary>Whether a pulsar will be charged a little while from now.</summary>
    public bool ChargedIn(Particle p, double seconds) =>
        p.Kind == Kind.Pulsar && p.Out && (p.Pulse + seconds) % PulsarPeriod > PulsarPeriod * 0.55;

    public bool Charged(Particle p) => ChargedIn(p, 0);

    /// <summary>Whether touching it would destroy the probe.</summary>
    public bool Deadly(Particle p) => !p.Dead && p.Out && p.Fresh <= 0 && p.Kind switch
    {
        Kind.Kitten => false,
        Kind.Pulsar => Charged(p),
        _ => true,
    };

    // ───────────────────────────── a step ─────────────────────────────

    public void Step(Controls c, bool live = true)
    {
        double dt = Field.Step;
        Clock += dt;
        AtomClock += dt * OrbitSpeed;
        if (AtomDown > 0)
        {
            AtomDown -= dt;
            if (AtomDown <= 0)
            {
                Heard.Add(Sfx.AtomForm);
                Burst(Field.CX, Field.CY, Palette.Electron, 24, 160);
            }
        }
        Spawn(dt);
        if (!Show && live && Probe.Free) MoveProbe(c, dt);
        else if (Probe.Down) Probe.DownTime += dt;
        foreach (var p in Particles) if (!p.Dead) MoveParticle(p, dt);
        if (!Show && Probe.Free)
        {
            LayTrail();
            Touch();
        }
        Particles.RemoveAll(p => p.Dead);
        Fade(dt);
    }

    private void Spawn(double dt)
    {
        if (ToCome.Count == 0) return;
        SpawnClock -= dt;
        if (SpawnClock > 0) return;
        SpawnClock = 0.5;
        var kind = ToCome.Dequeue();
        double angle = Rng.NextDouble() * Math.PI * 2;
        double to = Field.OrbitA + 50 + Rng.NextDouble() * 70;
        if (kind == Kind.Square)
        {
            int group = ++_groups;
            for (int i = 0; i < 4; i++)
                Particles.Add(new Particle { Kind = kind, Group = group, Slot = i, EmergeAngle = angle, EmergeTo = to, Emerge = Particle.EmergeSeconds, Cx = Field.CX, Cy = Field.CY, X = Field.CX, Y = Field.CY });
        }
        else
            Particles.Add(new Particle
            {
                Kind = kind, EmergeAngle = angle, EmergeTo = to, Emerge = Particle.EmergeSeconds, X = Field.CX, Y = Field.CY,
                Pulse = Rng.NextDouble() * PulsarPeriod * 0.4, Heading = angle,
            });
        Heard.Add(Sfx.Emerge);
    }

    private void MoveProbe(Controls c, double dt)
    {
        var p = Probe;
        if (p.Safe > 0) p.Safe -= dt;
        if (c.Steer)
        {
            // Racing to the pointer, as fast as the ball can be rolled.
            double dx = c.ToX - p.X, dy = c.ToY - p.Y, d = Math.Sqrt(dx * dx + dy * dy);
            double go = Math.Min(d, Field.SteerSpeed * dt);
            (p.Vx, p.Vy) = d < 1e-6 ? (0, 0) : (dx / d * go / dt, dy / d * go / dt);
        }
        else
        {
            // The keys and the stick spin the ball up and let it run down, like a trackball.
            double mag = Math.Sqrt(c.Dx * c.Dx + c.Dy * c.Dy);
            double ux = mag > 1 ? c.Dx / mag : c.Dx, uy = mag > 1 ? c.Dy / mag : c.Dy;
            double k = 1 - Math.Exp(-Field.Grip * dt);
            p.Vx += (ux * Field.ProbeSpeed - p.Vx) * k;
            p.Vy += (uy * Field.ProbeSpeed - p.Vy) * k;
        }
        p.X += p.Vx * dt;
        p.Y += p.Vy * dt;
        p.Spin += dt * (3 + Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) / 40);
        // The wall of the arena.
        double far = Field.FromCentre(p.X, p.Y), limit = Field.R - Field.ProbeR - 2;
        if (far > limit)
        {
            double nx = (p.X - Field.CX) / far, ny = (p.Y - Field.CY) / far;
            p.X = Field.CX + nx * limit;
            p.Y = Field.CY + ny * limit;
            double out_ = p.Vx * nx + p.Vy * ny;
            if (out_ > 0) { p.Vx -= out_ * nx; p.Vy -= out_ * ny; }
        }
    }

    // ───────────────────────────── the trail and the loop ─────────────────────────────

    private void LayTrail()
    {
        var at = new Pt(Probe.X, Probe.Y);
        if (Trail.Count > 0 && (Trail[^1].P - at).Length < 3) { Trim(); return; }
        Trail.Add((at, Clock));
        if (Trail.Count >= 4) CloseLoop();
        Trim();
    }

    /// <summary>Drops the oldest of the trail: too old, or past its length.</summary>
    private void Trim()
    {
        while (Trail.Count > 1 && Clock - Trail[0].T > Field.TrailLife) Trail.RemoveAt(0);
        double length = 0;
        for (int i = Trail.Count - 1; i > 0; i--)
        {
            length += (Trail[i].P - Trail[i - 1].P).Length;
            if (length > TrailLength) { Trail.RemoveRange(0, i - 1); break; }
        }
    }

    /// <summary>The trail's newest stretch crossing an older stretch closes a loop: whatever is inside is caught.</summary>
    private void CloseLoop()
    {
        int n = Trail.Count;
        Pt a = Trail[n - 2].P, b = Trail[n - 1].P;
        // The oldest crossing makes the biggest loop.
        for (int i = 0; i < n - 3; i++)
        {
            if (Geometry.Cross(a, b, Trail[i].P, Trail[i + 1].P) is not { } x) continue;
            var loop = new List<Pt> { x };
            for (int j = i + 1; j <= n - 2; j++) loop.Add(Trail[j].P);
            if (Geometry.Area(loop) < Field.MinLoopArea) continue;
            Capture(loop.ToArray());
            Trail.Clear();
            Trail.Add((b, Clock));
            return;
        }
    }

    /// <summary>Catches everything inside a closed loop: points for each, times how many went at once.</summary>
    public void Capture(Pt[] loop)
    {
        var inside = Particles.Where(p => !p.Dead && p.Out && Geometry.Inside(loop, p.X, p.Y)).ToList();
        bool atom = AtomUp && Geometry.Inside(loop, Field.CX, Field.CY);
        if (inside.Count == 0 && !atom)
        {
            Catches.Add(new Catch(loop, 0, Palette.Trail));
            Heard.Add(Sfx.Loop);
            return;
        }
        int count = inside.Count, basePoints = 0;
        foreach (var p in inside)
        {
            p.Dead = true;
            basePoints += p.Points;
            Burst(p.X, p.Y, Palette.Of(p.Kind), 14, 150);
            if (p.Kind == Kind.Splitter && p.Gen < 2) Split(p);
        }
        int points = basePoints * Math.Max(1, count);
        // A whole foursquare in one loop.
        int wholeFours = inside.Where(p => p.Kind == Kind.Square).GroupBy(p => p.Group).Count(g => g.Count() == 4);
        points += wholeFours * WaveBook.FoursquareBonus;
        if (atom)
        {
            // The bonus comes once a wave; breaking it again only clears it out of the way for a while.
            if (!AtomBrokenThisWave) points += WaveBook.AtomBonus(Wave);
            AtomBrokenThisWave = true;
            AtomDown = Field.AtomDownSeconds;
            AtomsBroken++;
            Burst(Field.CX, Field.CY, Palette.Nucleus, 40, 260);
            for (int k = 0; k < Electrons; k++)
            {
                var e = Electron(k);
                Burst(e.X, e.Y, Palette.Electron, 8, 120);
            }
            Heard.Add(Sfx.AtomBreak);
        }
        AddScore(points);
        Caught += count;
        BestLoop = Math.Max(BestLoop, count);
        var mid = Geometry.Middle(loop);
        string text = points.ToString();
        if (count > 1) text += $"  x{count}";
        if (atom) text = count == 0 && points == 0 ? "ATOM!" : "ATOM!  " + text;
        Popups.Add(new Popup { X = mid.X, Y = mid.Y, Text = text, Colour = atom ? Palette.Nucleus : count > 1 ? 0xFFFFF27A : Palette.Of(inside[0].Kind) });
        Catches.Add(new Catch(loop, count + (atom ? 1 : 0), atom ? Palette.Nucleus : count > 1 ? 0xFFFFF27A : Palette.Of(inside[0].Kind)));
        Heard.Add(count > 1 || atom ? Sfx.MultiCatch : Sfx.Catch);
        if (inside.Any(p => p.Kind == Kind.Splitter && p.Gen < 2)) Heard.Add(Sfx.Split);
    }

    /// <summary>A caught splitter flies apart in two smaller ones.</summary>
    private void Split(Particle p)
    {
        double speed = (95 + 30 * (p.Gen + 1)) * Speed;
        double a = Math.Atan2(p.Vy, p.Vx) + Math.PI / 2;
        for (int s = -1; s <= 1; s += 2)
        {
            double h = a + (s < 0 ? Math.PI : 0) + (Rng.NextDouble() - 0.5) * 0.6;
            Particles.Add(new Particle
            {
                Kind = Kind.Splitter, Gen = p.Gen + 1, X = p.X, Y = p.Y, Vx = Math.Cos(h) * speed, Vy = Math.Sin(h) * speed,
                Fresh = 0.7, Turn = p.Turn,
            });
        }
    }

    // ───────────────────────────── the particles ─────────────────────────────

    private void MoveParticle(Particle p, double dt)
    {
        p.Age += dt;
        p.Turn += dt * (p.Kind switch { Kind.Triangle => 1.6, Kind.Square => 1.5, Kind.Splitter => 2.5 + p.Gen, _ => 0 });
        if (p.Fresh > 0) p.Fresh -= dt;
        double s = Speed;
        if (p.Emerge > 0)
        {
            // Out of the nucleus along a straight line, slowing as it comes clear of the orbits.
            p.Emerge -= dt;
            double f = 1 - Math.Max(0, p.Emerge) / Particle.EmergeSeconds;
            double r = p.EmergeTo * (1 - (1 - f) * (1 - f));
            double ex = Math.Cos(p.EmergeAngle), ey = Math.Sin(p.EmergeAngle);
            Place(p, Field.CX + ex * r, Field.CY + ey * r);
            if (p.Emerge <= 0)
            {
                double speed = p.Kind switch { Kind.Triangle => 70, Kind.Square => 55, Kind.Splitter => 95, Kind.Pulsar => 35, Kind.Butterfly => 125, _ => 60 } * s;
                double h = p.EmergeAngle + (Rng.NextDouble() - 0.5) * 1.6 + (p.Kind == Kind.Square ? 0 : 0);
                if (p.Kind == Kind.Square)
                {
                    // All four of a foursquare set off together the same way.
                    h = p.EmergeAngle + 0.9;
                }
                (p.Vx, p.Vy) = (Math.Cos(h) * speed, Math.Sin(h) * speed);
                p.Heading = h;
            }
            return;
        }
        switch (p.Kind)
        {
            case Kind.Pulsar:
            {
                bool was = ChargedIn(p, -dt);
                p.Pulse += dt;
                bool now = Charged(p);
                if (now && !was && !Show) Heard.Add(Sfx.Charge);
                // Charged, it homes in on the probe; dark, it drifts.
                if (now && Probe.Free && !Show)
                {
                    double dx = Probe.X - p.X, dy = Probe.Y - p.Y, d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                    double k = Math.Min(1, 2.2 * dt);
                    p.Vx += (dx / d * 95 * s - p.Vx) * k;
                    p.Vy += (dy / d * 95 * s - p.Vy) * k;
                }
                else
                {
                    double v = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy), want = 35 * s;
                    if (v > want) { p.Vx *= 1 - 1.2 * dt; p.Vy *= 1 - 1.2 * dt; }
                }
                break;
            }
            case Kind.Butterfly:
            {
                // A random flutter: the heading wanders, and the speed beats with its wings.
                p.Heading += (Rng.NextDouble() - 0.5) * 14 * dt + Math.Sin(p.Age * 3.1) * 1.2 * dt;
                double v = 125 * s * (0.8 + 0.3 * Math.Abs(Math.Sin(p.Age * 9)));
                (p.Vx, p.Vy) = (Math.Cos(p.Heading) * v, Math.Sin(p.Heading) * v);
                break;
            }
            case Kind.Kitten:
            {
                // It goes for the trail, if there is one worth having near enough; otherwise it pads about.
                double v = 62 * s;
                if (!Show && Trail.Count >= 6)
                {
                    var near = Trail.MinBy(t => (t.P.X - p.X) * (t.P.X - p.X) + (t.P.Y - p.Y) * (t.P.Y - p.Y)).P;
                    double want = Math.Atan2(near.Y - p.Y, near.X - p.X);
                    if ((near - new Pt(p.X, p.Y)).Length < 320)
                    {
                        double turn = Math.IEEERemainder(want - p.Heading, Math.PI * 2);
                        p.Heading += Math.Clamp(turn, -3 * dt, 3 * dt);
                        v = 92 * s;
                    }
                }
                else p.Heading += (Rng.NextDouble() - 0.5) * 5 * dt;
                (p.Vx, p.Vy) = (Math.Cos(p.Heading) * v, Math.Sin(p.Heading) * v);
                break;
            }
        }
        double x = (p.Kind == Kind.Square ? p.Cx : p.X) + p.Vx * dt, y = (p.Kind == Kind.Square ? p.Cy : p.Y) + p.Vy * dt;
        Bounce(p, ref x, ref y);
        Place(p, x, y);
    }

    /// <summary>Puts a particle (a foursquare's middle) at a point; a square takes its place round the middle.</summary>
    private static void Place(Particle p, double x, double y)
    {
        if (p.Kind != Kind.Square) { (p.X, p.Y) = (x, y); return; }
        (p.Cx, p.Cy) = (x, y);
        double a = p.Turn + p.Slot * Math.PI / 2;
        p.X = x + Math.Cos(a) * Particle.FourSpread;
        p.Y = y + Math.Sin(a) * Particle.FourSpread;
    }

    /// <summary>Off the wall of the arena, and off the atom's orbits while it's whole.</summary>
    private void Bounce(Particle p, ref double x, ref double y)
    {
        double far = Field.FromCentre(x, y);
        if (far < 1e-6) return;
        double nx = (x - Field.CX) / far, ny = (y - Field.CY) / far;
        double outer = Field.R - p.Reach - 2, inner = Field.OrbitA + Field.ElectronR + p.Reach + 6;
        double along = p.Vx * nx + p.Vy * ny;
        bool hit = false;
        if (far > outer)
        {
            (x, y) = (Field.CX + nx * outer, Field.CY + ny * outer);
            if (along > 0) { p.Vx -= 2 * along * nx; p.Vy -= 2 * along * ny; hit = true; }
        }
        else if (AtomUp && far < inner)
        {
            (x, y) = (Field.CX + nx * inner, Field.CY + ny * inner);
            if (along < 0) { p.Vx -= 2 * along * nx; p.Vy -= 2 * along * ny; hit = true; }
        }
        if (hit) p.Heading = Math.Atan2(p.Vy, p.Vx);
    }

    // ───────────────────────────── touching ─────────────────────────────

    /// <summary>The probe against everything deadly, and the kittens against the trail.</summary>
    private void Touch()
    {
        var pr = Probe;
        foreach (var k in Particles)
        {
            if (k.Kind != Kind.Kitten || !k.Out || Trail.Count < 3) continue;
            for (int i = 0; i < Trail.Count - 2; i++)
                if (Geometry.ToSegment(k.X, k.Y, Trail[i].P, Trail[i + 1].P) < k.Radius)
                {
                    // Eaten: the trail goes, all but where the probe is now.
                    var last = Trail[^1];
                    Burst(k.X, k.Y, Palette.Trail, 10, 90);
                    Trail.Clear();
                    Trail.Add(last);
                    Heard.Add(Sfx.Mew);
                    Popups.Add(new Popup { X = k.X, Y = k.Y - 18, Text = "MEW!", Colour = Palette.Of(Kind.Kitten) });
                    break;
                }
        }
        if (pr.Safe > 0) return;
        if (AtomUp)
        {
            if (Field.FromCentre(pr.X, pr.Y) < Field.NucleusR + Field.ProbeR) { Destroy(); return; }
            for (int k = 0; k < Electrons; k++)
            {
                var e = Electron(k);
                if ((e - new Pt(pr.X, pr.Y)).Length < Field.ElectronR + Field.ProbeR) { Destroy(); return; }
            }
        }
        foreach (var p in Particles)
            if (Deadly(p) && Math.Sqrt((p.X - pr.X) * (p.X - pr.X) + (p.Y - pr.Y) * (p.Y - pr.Y)) < p.Radius * 0.85 + Field.ProbeR)
            {
                Destroy();
                return;
            }
    }

    /// <summary>The probe's hit: it breaks up and the trail goes.</summary>
    public void Destroy()
    {
        var p = Probe;
        if (p.Down) return;
        p.Down = true;
        p.DownTime = 0;
        (p.Vx, p.Vy) = (0, 0);
        Trail.Clear();
        Burst(p.X, p.Y, Palette.Probe, 30, 220);
        Burst(p.X, p.Y, Palette.Trail, 20, 120);
        Heard.Add(Sfx.Zap);
    }

    /// <summary>A new probe, at the spot on the wall furthest from anything deadly, untouchable for a moment.</summary>
    public void Revive()
    {
        double best = -1, bx = Field.CX, by = Field.CY + Field.R - 50;
        for (int i = 0; i < 24; i++)
        {
            double a = i * Math.PI * 2 / 24, x = Field.CX + Math.Cos(a) * (Field.R - 50), y = Field.CY + Math.Sin(a) * (Field.R - 50);
            double near = Particles.Where(p => p.Kind != Kind.Kitten).Select(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y))).DefaultIfEmpty(999).Min();
            if (near > best) (best, bx, by) = (near, x, y);
        }
        Probe = new Probe { X = bx, Y = by, Safe = 2 };
        Trail.Clear();
    }

    // ───────────────────────────── the show ─────────────────────────────

    private void Burst(double x, double y, uint colour, int n, double speed)
    {
        for (int i = 0; i < n; i++)
        {
            double a = Rng.NextDouble() * Math.PI * 2, v = speed * (0.3 + Rng.NextDouble() * 0.7), life = 0.4 + Rng.NextDouble() * 0.6;
            Sparks.Add(new Spark { X = x, Y = y, Vx = Math.Cos(a) * v, Vy = Math.Sin(a) * v, Life = life, Max = life, Colour = colour });
        }
    }

    private void Fade(double dt)
    {
        foreach (var s in Sparks)
        {
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;
            s.Vx *= 1 - 1.5 * dt;
            s.Vy *= 1 - 1.5 * dt;
            s.Life -= dt;
        }
        Sparks.RemoveAll(s => s.Life <= 0);
        foreach (var c in Catches) c.T += dt;
        Catches.RemoveAll(c => c.T > 0.8);
        foreach (var p in Popups)
        {
            p.T += dt;
            p.Y -= 30 * dt;
        }
        Popups.RemoveAll(p => p.T > 1.4);
    }

    // ───────────────────────────── for tests ─────────────────────────────

    /// <summary>Puts a particle loose (already out of the nucleus) at a spot, moving or still.</summary>
    public Particle Put(Kind kind, double x, double y, double vx = 0, double vy = 0, int gen = 0)
    {
        var p = new Particle { Kind = kind, X = x, Y = y, Cx = x, Cy = y, Vx = vx, Vy = vy, Gen = gen, Heading = Math.Atan2(vy, vx) };
        Particles.Add(p);
        return p;
    }

    /// <summary>A whole foursquare loose with its middle at a spot.</summary>
    public List<Particle> PutFoursquare(double x, double y, double vx = 0, double vy = 0)
    {
        int group = ++_groups;
        var list = new List<Particle>();
        for (int i = 0; i < 4; i++)
        {
            var p = new Particle { Kind = Kind.Square, Group = group, Slot = i, Vx = vx, Vy = vy };
            Place(p, x, y);
            Particles.Add(p);
            list.Add(p);
        }
        return list;
    }
}
