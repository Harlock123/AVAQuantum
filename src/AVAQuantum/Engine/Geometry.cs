namespace AVAQuantum.Engine;

/// <summary>The sums behind catching things in a loop: where two lines cross, a loop's area, and what's inside it.</summary>
public static class Geometry
{
    /// <summary>Where segment a–b crosses segment c–d, if it does.</summary>
    public static Pt? Cross(Pt a, Pt b, Pt c, Pt d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        double den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-9) return null;
        double qx = c.X - a.X, qy = c.Y - a.Y;
        double t = (qx * sy - qy * sx) / den, u = (qx * ry - qy * rx) / den;
        if (t < 0 || t > 1 || u < 0 || u > 1) return null;
        return new Pt(a.X + rx * t, a.Y + ry * t);
    }

    /// <summary>The area a loop of points closes in (the shoelace sum), always positive.</summary>
    public static double Area(IReadOnlyList<Pt> loop)
    {
        double s = 0;
        for (int i = 0; i < loop.Count; i++)
        {
            var p = loop[i];
            var q = loop[(i + 1) % loop.Count];
            s += p.X * q.Y - q.X * p.Y;
        }
        return Math.Abs(s) / 2;
    }

    /// <summary>Whether a point is inside a loop (counting how many of its sides a line out to the right crosses).</summary>
    public static bool Inside(IReadOnlyList<Pt> loop, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            var a = loop[i];
            var b = loop[j];
            if (a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>How far a point is from the segment a–b.</summary>
    public static double ToSegment(double x, double y, Pt a, Pt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, len = dx * dx + dy * dy;
        double t = len < 1e-9 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / len, 0, 1);
        double px = a.X + dx * t - x, py = a.Y + dy * t - y;
        return Math.Sqrt(px * px + py * py);
    }

    /// <summary>The middle of a loop's points.</summary>
    public static Pt Middle(IReadOnlyList<Pt> loop) => new(loop.Average(p => p.X), loop.Average(p => p.Y));
}
