using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAQuantum.Engine;

namespace AVAQuantum.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAQuantum/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        width = Math.Round(width * 4) / 4;
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        return p;
    }

    private const uint White = 0xFFEAF4FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF7F8CB8, Cyan = 0xFF6AE8FF, Pink = 0xFFFF6AD8;

    private const double W = Field.Width, H = Field.Height;

    private bool InGame => Engine.State is GameState.Attract or GameState.Playing or GameState.Paused or GameState.GameOver;

    /// <summary>Text with a soft glow behind it, like the beam on a vector screen.</summary>
    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool glow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        var pos = new Point(x, at.Y - ft.Height / 2);
        if (glow)
        {
            var halo = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour & 0x30FFFFFF));
            foreach (var (ox, oy) in new[] { (-1.5, 0.0), (1.5, 0.0), (0.0, -1.5), (0.0, 1.5) }) dc.DrawText(halo, new Point(pos.X + ox, pos.Y + oy));
        }
        dc.DrawText(ft, pos);
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    private static uint Mix(uint a, uint b, double f)
    {
        f = Math.Clamp(f, 0, 1);
        uint C(int shift) => (uint)((((a >> shift) & 0xFF) * (1 - f) + ((b >> shift) & 0xFF) * f)) << shift;
        return 0xFF000000 | C(16) | C(8) | C(0);
    }

    /// <summary>A colour round the rainbow, for the trail and the logo.</summary>
    private static uint Hue(double h)
    {
        h = (h % 1 + 1) % 1 * 6;
        double f = h - Math.Floor(h);
        (double r, double g, double b) = (int)h switch
        {
            0 => (1.0, f, 0.0),
            1 => (1 - f, 1, 0),
            2 => (0, 1, f),
            3 => (0, 1 - f, 1),
            4 => (f, 0, 1),
            _ => (1, 0, 1 - f),
        };
        // Lifted towards white a little: phosphor, not paint.
        uint C(double v) => (uint)(80 + v * 175);
        return 0xFF000000 | C(r) << 16 | C(g) << 8 | C(b);
    }

    private static Point P(Pt p) => new(p.X, p.Y);

    /// <summary>A line as the beam draws it: a wide faint halo, a narrower one, and the bright core.</summary>
    private static void Beam(DrawingContext dc, Point a, Point b, uint colour, double bright = 1, double core = 1.6)
    {
        dc.DrawLine(Pen(Alpha(colour, 0.12 * bright), core * 5.5), a, b);
        dc.DrawLine(Pen(Alpha(colour, 0.3 * bright), core * 2.6), a, b);
        dc.DrawLine(Pen(Alpha(Mix(colour, 0xFFFFFFFF, 0.35), bright), core), a, b);
    }

    /// <summary>A run of joined lines as the beam draws them, closed back to the start or not.</summary>
    private static void Beams(DrawingContext dc, IReadOnlyList<Point> pts, uint colour, double bright = 1, double core = 1.6, bool closed = true)
    {
        if (pts.Count < 2) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], false);
            for (int i = 1; i < pts.Count; i++) c.LineTo(pts[i]);
            c.EndFigure(closed);
        }
        dc.DrawGeometry(null, Pen(Alpha(colour, 0.12 * bright), core * 5.5), g);
        dc.DrawGeometry(null, Pen(Alpha(colour, 0.3 * bright), core * 2.6), g);
        dc.DrawGeometry(null, Pen(Alpha(Mix(colour, 0xFFFFFFFF, 0.35), bright), core), g);
    }

    /// <summary>A dot of light with its bloom.</summary>
    private static void Dot(DrawingContext dc, Point at, double r, uint colour, double bright = 1)
    {
        dc.DrawEllipse(Brush(Alpha(colour, 0.12 * bright)), null, at, r * 3, r * 3);
        dc.DrawEllipse(Brush(Alpha(colour, 0.3 * bright)), null, at, r * 1.8, r * 1.8);
        dc.DrawEllipse(Brush(Alpha(Mix(colour, 0xFFFFFFFF, 0.5), bright)), null, at, r, r);
    }

    /// <summary>A loop of points round a centre: a regular shape (or an ellipse) turned to an angle.</summary>
    private static Point[] Ring(double x, double y, double rx, double ry, double turn, int n)
    {
        double c = Math.Cos(turn), s = Math.Sin(turn);
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double a = i * Math.PI * 2 / n, px = Math.Cos(a) * rx, py = Math.Sin(a) * ry;
            pts[i] = new Point(x + px * c - py * s, y + px * s + py * c);
        }
        return pts;
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF02020A), new Rect(0, 0, W, H));
        var w = InGame ? g.World : _show;
        DrawArena(dc, w);
        DrawAtom(dc, w);
        DrawCatches(dc, w);
        foreach (var p in w.Particles) DrawParticle(dc, w, p);
        if (InGame) DrawTrail(dc, w);
        DrawSparks(dc, w);
        if (InGame && !w.Show) DrawProbe(dc, w.Probe);
        foreach (var p in w.Popups)
            Text(dc, p.Text, new Point(p.X, p.Y), 26, Alpha(p.Colour, Math.Min(1, (1.4 - p.T) * 2)), center: true);
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default:
                DrawHud(dc);
                DrawMessages(dc);
                break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, W, H));
            Text(dc, "PAUSED", new Point(W / 2, 340), 80, Yellow, center: true);
            Text(dc, "ANY KEY OR CLICK TO CARRY ON - ESC TO QUIT", new Point(W / 2, 400), 26, White, center: true);
        }
        if (g.State == GameState.GameOver)
            Text(dc, "GAME OVER", new Point(W / 2, 300), 100, Red, center: true);
        if (g.State == GameState.Attract)
        {
            Text(dc, "DEMONSTRATION - PRESS ENTER OR CLICK TO PLAY", new Point(W / 2, H - 16), 24, Yellow, center: true);
        }
        if (_toast != null && _toastTime > 0)
        {
            dc.FillRectangle(Brush(0xC0000000), new Rect(W / 2 - 330, H - 92, 660, 40), 8);
            Text(dc, _toast, new Point(W / 2, H - 72), 26, Cyan, center: true);
        }
    }

    // ───────────────────────────── the arena ─────────────────────────────

    private void DrawArena(DrawingContext dc, World w)
    {
        double cx = Field.CX, cy = Field.CY, r = Field.R;
        // A faint polar grid, like the graticule on a scope.
        var faint = Pen(0x1A3A7CFF, 1);
        for (int k = 1; k <= 3; k++) dc.DrawEllipse(null, faint, new Point(cx, cy), r * k / 4, r * k / 4);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            dc.DrawLine(faint, new Point(cx + Math.Cos(a) * (Field.OrbitA + 20), cy + Math.Sin(a) * (Field.OrbitA + 20)), new Point(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r));
        }
        // The wall: a double ring with marks turning slowly round it.
        double shimmer = 0.75 + 0.15 * Math.Sin(_clock * 1.7);
        Beams(dc, Ring(cx, cy, r + 4, r + 4, 0, 96), Palette.Rim, shimmer, 1.8);
        Beams(dc, Ring(cx, cy, r + 12, r + 12, 0, 96), Palette.Rim, shimmer * 0.4, 1.2);
        for (int i = 0; i < 36; i++)
        {
            double a = i * Math.PI * 2 / 36 + w.Clock * 0.08;
            Beam(dc, new Point(cx + Math.Cos(a) * (r + 4), cy + Math.Sin(a) * (r + 4)), new Point(cx + Math.Cos(a) * (r + 12), cy + Math.Sin(a) * (r + 12)), Palette.Rim, shimmer * 0.6, 1.1);
        }
    }

    // ───────────────────────────── the atom ─────────────────────────────

    private void DrawAtom(DrawingContext dc, World w)
    {
        double cx = Field.CX, cy = Field.CY;
        double forming = w.AtomUp ? 1 : 0;
        if (!w.AtomUp)
        {
            // Broken: a faint flicker where it was, gathering again in its last moments.
            double left = w.AtomDown;
            if (left > 1.5)
            {
                Beams(dc, Ring(cx, cy, 8, 8, w.Clock, 6), Palette.Nucleus, 0.25, 1);
                return;
            }
            forming = (int)(left * 12) % 2 == 0 ? 0.6 : 0.2;
        }
        // The orbits, faint, each ellipse turned about the nucleus.
        for (int k = 0; k < w.Electrons; k++)
        {
            double tilt = k * Math.PI / w.Electrons + w.AtomClock * 0.12;
            Beams(dc, Ring(cx, cy, Field.OrbitA, Field.OrbitB, tilt, 40), 0xFF5A6AFF, 0.28 * forming, 1);
        }
        // The nucleus: a knot of protons and neutrons, throbbing.
        double beat = 1 + 0.08 * Math.Sin(_clock * 6);
        Dot(dc, new Point(cx, cy), 9 * beat, Palette.Nucleus, 0.5 * forming);
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3 + w.Clock * 0.7;
            uint c = i % 2 == 0 ? Palette.Nucleus : 0xFFFFA05A;
            Beams(dc, Ring(cx + Math.Cos(a) * 10 * beat, cy + Math.Sin(a) * 10 * beat, 6.5, 6.5, a, 8), c, forming, 1.3);
        }
        Beams(dc, Ring(cx, cy, 6, 6, 0, 8), 0xFFFFE0A0, forming, 1.3);
        // The electrons, with short tails along their orbits.
        for (int k = 0; k < w.Electrons; k++)
        {
            for (int t = 1; t <= 4; t++)
                Dot(dc, P(w.Electron(k, w.AtomClock - t * 0.07)), Field.ElectronR * (1 - t * 0.17), Palette.Electron, 0.35 * forming * (1 - t * 0.2));
            Dot(dc, P(w.Electron(k)), Field.ElectronR * 0.8, Palette.Electron, forming);
        }
    }

    // ───────────────────────────── the particles ─────────────────────────────

    private void DrawParticle(DrawingContext dc, World w, Particle p)
    {
        uint colour = Palette.Of(p.Kind);
        double bright = 1, scale = 1;
        if (!p.Out)
        {
            double f = 1 - p.Emerge / Particle.EmergeSeconds;
            scale = 0.3 + 0.7 * f;
            bright = 0.4 + 0.5 * f;
        }
        if (p.Fresh > 0 && (int)(p.Fresh * 16) % 2 == 0) bright *= 0.4;
        double x = p.X, y = p.Y, r = p.Radius * scale;
        switch (p.Kind)
        {
            case Kind.Triangle:
                Beams(dc, Ring(x, y, r, r, p.Turn, 3), colour, bright, 1.8);
                Beams(dc, Ring(x, y, r * 0.45, r * 0.45, -p.Turn * 1.5, 3), colour, bright * 0.6, 1.2);
                break;
            case Kind.Square:
                Beams(dc, Ring(x, y, r * 1.2, r * 1.2, p.Turn * 2 + Math.PI / 4, 4), colour, bright, 1.8);
                Dot(dc, new Point(x, y), 1.8, colour, bright);
                break;
            case Kind.Splitter:
            {
                // A diamond split down the middle, the line it breaks along.
                var d = Ring(x, y, r, r * 0.75, p.Turn, 4);
                Beams(dc, d, colour, bright, 1.8);
                Beam(dc, d[1], d[3], colour, bright * 0.7, 1.2);
                for (int i = 0; i < p.Gen; i++) Dot(dc, new Point(x, y), 1.5 + i, colour, bright);
                break;
            }
            case Kind.Pulsar:
            {
                // A star whose rays grow as it charges; charged it blazes red and white.
                bool charged = w.Charged(p);
                double period = w.PulsarPeriod, phase = p.Pulse % period / period;
                double grow = charged ? 1 + 0.25 * Math.Sin(_clock * 30) : 0.45 + 0.5 * Math.Clamp(phase / 0.55, 0, 1);
                uint c = charged ? ((int)(_clock * 20) % 2 == 0 ? 0xFFFFFFFF : colour) : 0xFF6A5AFF;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4 + p.Age * 0.8;
                    double len = r * grow * (i % 2 == 0 ? 1.25 : 0.8);
                    Beam(dc, new Point(x + Math.Cos(a) * 3, y + Math.Sin(a) * 3), new Point(x + Math.Cos(a) * len, y + Math.Sin(a) * len), c, bright * (charged ? 1 : 0.75), 1.6);
                }
                Dot(dc, new Point(x, y), charged ? 4 : 2.5, c, bright);
                break;
            }
            case Kind.Butterfly:
            {
                // Two pairs of wings beating, pointing the way it flies.
                double flap = 0.35 + 0.65 * Math.Abs(Math.Sin(p.Age * 14));
                double h = p.Heading, c = Math.Cos(h), s = Math.Sin(h);
                double k = r / 14;
                Point At(double fx, double sx) => new(x + (fx * c - sx * s) * k, y + (fx * s + sx * c) * k);
                for (int side = -1; side <= 1; side += 2)
                {
                    Beams(dc, [At(4, 0), At(10, side * 15 * flap), At(-2, side * 12 * flap)], colour, bright, 1.6);
                    Beams(dc, [At(-2, 0), At(-12, side * 10 * flap), At(-8, side * 2)], 0xFFFFF27A, bright * 0.8, 1.3);
                }
                Beam(dc, At(-9, 0), At(8, 0), White, bright, 1.4);
                break;
            }
            case Kind.Kitten:
            {
                // A cat's face: a round head, two ears, eyes and whiskers.
                double h = Math.Sin(p.Age * 2) * 0.15, k = r / 12;
                Point At(double fx, double fy) => new(x + (fx * Math.Cos(h) - fy * Math.Sin(h)) * k, y + (fx * Math.Sin(h) + fy * Math.Cos(h)) * k);
                Beams(dc, Ring(x, y + k, 10 * k, 9 * k, h, 14), colour, bright, 1.6);
                Beams(dc, [At(-9, -3), At(-8, -14), At(-2, -8)], colour, bright, 1.6, closed: false);
                Beams(dc, [At(9, -3), At(8, -14), At(2, -8)], colour, bright, 1.6, closed: false);
                bool blink = (int)(p.Age * 1.3) % 4 == 0 && p.Age % 0.77 < 0.12;
                if (!blink)
                {
                    Dot(dc, At(-4, -1), 1.6 * k, 0xFF7AFFB0, bright);
                    Dot(dc, At(4, -1), 1.6 * k, 0xFF7AFFB0, bright);
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int i = -1; i <= 1; i++)
                        Beam(dc, At(side * 4, 4), At(side * 16, 3 + i * 3), White, bright * 0.5, 0.8);
                break;
            }
        }
    }

    // ───────────────────────────── the probe and its trail ─────────────────────────────

    private void DrawTrail(DrawingContext dc, World w)
    {
        var t = w.Trail;
        if (t.Count < 2) return;
        double length = 0, total = w.TrailLength;
        // Newest first: bright at the probe, fading to the oldest.
        for (int i = t.Count - 1; i > 0; i--)
        {
            var a = t[i].P;
            var b = t[i - 1].P;
            length += (a - b).Length;
            double age = Math.Max(length / total, (w.Clock - t[i].T) / Field.TrailLife);
            double bright = Math.Clamp(1.05 - age, 0.12, 1);
            uint c = Hue(w.Clock * 0.35 - length / 700);
            Beam(dc, P(a), P(b), c, bright, 2);
        }
    }

    private void DrawProbe(DrawingContext dc, Probe p)
    {
        if (p.Down) return;
        if (p.Safe > 0 && (int)(p.Safe * 10) % 2 == 1) return;
        var at = new Point(p.X, p.Y);
        // A spinning four-pointed star inside a ring: the probe.
        double s = p.Spin;
        Beams(dc, Ring(p.X, p.Y, Field.ProbeR + 2, Field.ProbeR + 2, s * 0.5, 10), Cyan, 0.9, 1.4);
        for (int i = 0; i < 4; i++)
        {
            double a = s + i * Math.PI / 2;
            Beam(dc, new Point(p.X + Math.Cos(a) * 3, p.Y + Math.Sin(a) * 3), new Point(p.X + Math.Cos(a) * (Field.ProbeR + 6), p.Y + Math.Sin(a) * (Field.ProbeR + 6)), White, 1, 1.8);
        }
        Dot(dc, at, 2.5, White);
    }

    private void DrawCatches(DrawingContext dc, World w)
    {
        foreach (var c in w.Catches)
        {
            double f = 1 - c.T / 0.8;
            var pts = c.Loop.Select(P).ToArray();
            if (c.Count > 0)
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(pts[0], true);
                    foreach (var q in pts.Skip(1)) ctx.LineTo(q);
                    ctx.EndFigure(true);
                }
                dc.DrawGeometry(Brush(Alpha(c.Colour, 0.22 * f)), null, g);
            }
            bool flash = (int)(c.T * 20) % 2 == 0;
            Beams(dc, pts, flash ? 0xFFFFFFFF : c.Colour, f, 2.2);
        }
    }

    private void DrawSparks(DrawingContext dc, World w)
    {
        foreach (var s in w.Sparks)
        {
            double f = s.Life / s.Max;
            Beam(dc, new Point(s.X, s.Y), new Point(s.X - s.Vx * 0.03, s.Y - s.Vy * 0.03), s.Colour, f, 1.4);
        }
    }

    // ───────────────────────────── the score and the calls ─────────────────────────────

    private static string Points(int n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");

    private void DrawHud(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        Text(dc, "SCORE", new Point(24, 20), 22, Pink);
        Text(dc, Points(g.Score), new Point(24, 48), 36, White);
        Text(dc, "HIGH SCORE", new Point(W / 2, 20), 22, Yellow, center: true);
        Text(dc, Points(Math.Max(g.HighScores.TopScore, g.Score)), new Point(W / 2, 48), 36, White, center: true);
        Text(dc, "WAVE", new Point(W - 24, 20), 22, Cyan, right: true);
        Text(dc, (w.Wave + 1).ToString(), new Point(W - 24, 48), 36, White, right: true);
        // The lives in hand: a small probe for each.
        int lives = w.Lives - (g.Phase == Phase.Down ? 1 : 0);
        for (int i = 0; i < Math.Min(8, lives - 1); i++)
        {
            double x = 34 + i * 30, y = H - 30;
            Beams(dc, Ring(x, y, 9, 9, 0, 10), Cyan, 0.8, 1.2);
            for (int k = 0; k < 4; k++)
            {
                double a = k * Math.PI / 2 + Math.PI / 4;
                Beam(dc, new Point(x, y), new Point(x + Math.Cos(a) * 12, y + Math.Sin(a) * 12), White, 0.8, 1.2);
            }
        }
        if (Input.MouseSteering && g.State == GameState.Playing) Text(dc, "MOUSE", new Point(W - 24, H - 30), 20, Dim, right: true);
    }

    private void DrawMessages(DrawingContext dc)
    {
        var g = Engine;
        if (g.Message is not { } msg) return;
        double cx = W / 2, cy = 200;
        double size = msg.Length > 14 ? 64 : 84;
        uint c = (int)(_clock * 8) % 2 == 0 ? Yellow : 0xFFFFF4A0;
        Text(dc, msg, new Point(cx, cy), size, c, center: true);
        if (g.SubMessage is { } sub)
        {
            var ft = new FormattedText(sub, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, 28, Brush(White));
            dc.FillRectangle(Brush(0xB0000008), new Rect(cx - ft.Width / 2 - 14, cy + size * 0.55 + 2, ft.Width + 28, 36), 6);
            Text(dc, sub, new Point(cx, cy + size * 0.55 + 20), 28, White, center: true);
        }
    }

    // ───────────────────────────── the vector logo ─────────────────────────────

    /// <summary>The logo's letters, as the beam strokes them on a 4-by-6 grid.</summary>
    private static readonly Dictionary<char, double[][]> Strokes = new()
    {
        ['Q'] = [[1, 0, 3, 0, 4, 1, 4, 5, 3, 6, 1, 6, 0, 5, 0, 1, 1, 0], [2.4, 4.4, 4.4, 6.6]],
        ['U'] = [[0, 0, 0, 5, 1, 6, 3, 6, 4, 5, 4, 0]],
        ['A'] = [[0, 6, 0, 2, 2, 0, 4, 2, 4, 6], [0, 3.6, 4, 3.6]],
        ['N'] = [[0, 6, 0, 0, 4, 6, 4, 0]],
        ['T'] = [[0, 0, 4, 0], [2, 0, 2, 6]],
        ['M'] = [[0, 6, 0, 0, 2, 3, 4, 0, 4, 6]],
        ['V'] = [[0, 0, 2, 6, 4, 0]],
    };

    private void Logo(DrawingContext dc, string word, double cy, double size, double core)
    {
        double cell = size / 6, step = cell * 6.2, x0 = W / 2 - (word.Length * step - cell * 2.2) / 2;
        for (int i = 0; i < word.Length; i++)
        {
            if (!Strokes.TryGetValue(word[i], out var strokes)) continue;
            uint colour = Hue(i / (double)word.Length + _clock * 0.08);
            double ox = x0 + i * step, oy = cy - size / 2 + Math.Sin(_clock * 2.2 + i * 0.6) * 3;
            foreach (var s in strokes)
            {
                var pts = new Point[s.Length / 2];
                for (int k = 0; k < pts.Length; k++) pts[k] = new Point(ox + s[2 * k] * cell, oy + s[2 * k + 1] * cell);
                Beams(dc, pts, colour, 1, core, closed: false);
            }
        }
    }

    // ───────────────────────────── title and scores ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = W / 2;
        dc.FillRectangle(Brush(0x60000006), new Rect(0, 0, W, H));
        Logo(dc, "AVA", 40, 34, 1.6);
        Logo(dc, "QUANTUM", 128, 104, 3.2);
        Text(dc, "IN THE STYLE OF THE 1982 COLOUR VECTOR ARCADE GAME", new Point(cx, 206), 24, 0xFFC8D0FF, center: true);
        dc.FillRectangle(Brush(0xB0000008), new Rect(110, 486, W - 220, 150), 10);
        Text(dc, "STEER THE PROBE.  LOOP ITS TRAIL RIGHT ROUND THE PARTICLES TO CATCH THEM.", new Point(cx, 506), 24, White, center: true);
        Text(dc, "CATCH SEVERAL IN ONE LOOP: THE POINTS ARE MULTIPLIED BY HOW MANY.", new Point(cx, 532), 24, White, center: true);
        Text(dc, "DON'T TOUCH THEM, THE NUCLEUS OR ITS ELECTRONS.  KITTENS ARE HARMLESS BUT EAT YOUR TRAIL.", new Point(cx, 558), 24, White, center: true);
        // A key to the particles and their points.
        (Kind K, string Name)[] key = [(Kind.Triangle, "TRIANGLE 100"), (Kind.Square, "SQUARE 150"), (Kind.Splitter, "SPLITTER 200"), (Kind.Pulsar, "PULSAR 300"), (Kind.Butterfly, "BUTTERFLY 400"), (Kind.Kitten, "KITTEN 500")];
        for (int i = 0; i < key.Length; i++)
        {
            double x = 160 + i * 141;
            Text(dc, key[i].Name, new Point(x, 604), 20, Palette.Of(key[i].K), center: true);
        }
        dc.FillRectangle(Brush(0xB0000000), new Rect(0, H - 110, W, 110));
        if ((int)(_clock * 2) % 2 == 0) Text(dc, "PRESS ENTER OR CLICK TO START", new Point(cx, H - 84), 44, Yellow, center: true);
        Text(dc, "MOUSE: ROLL THE PROBE LIKE THE TRACKBALL   ARROWS/WASD OR STICK: STEER   TAB: MOUSE ON/OFF   P: PAUSE   M: SOUND", new Point(cx, H - 46), 20, 0xFFFFF0E0, center: true);
        Text(dc, "HIGH SCORE " + Points(Engine.HighScores.TopScore), new Point(cx, H - 20), 22, Cyan, center: true);
    }

    private void Panel(DrawingContext dc)
    {
        dc.FillRectangle(Brush(0x90000008), new Rect(0, 0, W, H));
        dc.DrawRectangle(Brush(0xD0040418), Pen(0xFF3A7CFF, 3), new Rect(W / 2 - 330, 60, 660, 650), 18, 18);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = W / 2;
        Panel(dc);
        Text(dc, "QUANTUM PHYSICISTS", new Point(cx, 112), 60, Yellow, center: true);
        Text(dc, "RANK NAME        SCORE   WAVE", new Point(cx, 178), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,12:N0}   {e.Wave,4}".Replace(",", " "), new Point(cx, 224 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 672), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = W / 2;
        Panel(dc);
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, Points(g.Score), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    /// <summary>A rounded rectangle with a radius worked out in doubles.</summary>
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
