namespace AVAQuantum.Engine;

/// <summary>
/// The controls, boiled down: a push in each direction (-1 to 1; down the screen is +1), or, with
/// <see cref="Steer"/>, a point on the screen for the probe to race to (the mouse, rolled like the trackball, and the
/// computer player).
/// </summary>
public readonly record struct Controls(double Dx = 0, double Dy = 0, bool Steer = false, double ToX = 0, double ToY = 0);

/// <summary>A point on the screen.</summary>
public readonly record struct Pt(double X, double Y)
{
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public static Pt operator *(Pt a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>
/// The screen's measures. Everything happens in the game's own 1024×768 screen, in pixels, with y down the screen.
/// The arena is a circle round the atom in the middle.
/// </summary>
public static class Field
{
    public const double Width = 1024, Height = 768;
    /// <summary>The game runs in fixed steps of a sixtieth of a second.</summary>
    public const double Step = 1.0 / 60;

    /// <summary>The middle of the arena (the nucleus) and its radius.</summary>
    public const double CX = 512, CY = 412, R = 326;

    /// <summary>The probe's size, its top speed on the keys or the stick, and how fast it races to the pointer.</summary>
    public const double ProbeR = 9, ProbeSpeed = 440, SteerSpeed = 1100;

    /// <summary>How quickly the probe picks up speed on the keys, like a spun trackball.</summary>
    public const double Grip = 9;

    /// <summary>The trail never lasts longer than this, however long it is.</summary>
    public const double TrailLife = 2.4;

    /// <summary>A loop smaller than this (in square pixels) is just a wiggle, and catches nothing.</summary>
    public const double MinLoopArea = 500;

    /// <summary>The nucleus's size, and its electrons'.</summary>
    public const double NucleusR = 20, ElectronR = 6;

    /// <summary>The electrons' orbits: ellipses this long and this wide, turned about the nucleus.</summary>
    public const double OrbitA = 96, OrbitB = 34;

    /// <summary>How long a broken atom takes to form again.</summary>
    public const double AtomDownSeconds = 9;

    public static Pt Centre => new(CX, CY);

    /// <summary>How far a point is from the middle of the arena.</summary>
    public static double FromCentre(double x, double y) => Math.Sqrt((x - CX) * (x - CX) + (y - CY) * (y - CY));
}
