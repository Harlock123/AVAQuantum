namespace AVAQuantum.Engine;

/// <summary>The kinds of particle loose in the atom.</summary>
public enum Kind
{
    /// <summary>Drifts in a straight line and bounces off the wall.</summary>
    Triangle,
    /// <summary>One of a foursquare: four squares wheeling round each other as they go. Catch all four at once for a bonus.</summary>
    Square,
    /// <summary>Caught, it splits in two smaller ones, which split again; the third size goes for good.</summary>
    Splitter,
    /// <summary>Charges up and goes dark in turn; charged, it's deadly and comes after the probe.</summary>
    Pulsar,
    /// <summary>Flutters about fast and at random.</summary>
    Butterfly,
    /// <summary>Harmless to the probe, but it goes for the trail and eats it.</summary>
    Kitten,
}

/// <summary>One particle loose in the atom.</summary>
public sealed class Particle
{
    public Kind Kind;
    public double X, Y, Vx, Vy;
    /// <summary>A foursquare's middle, which is what moves; each square sits round it at its <see cref="Slot"/>.</summary>
    public double Cx, Cy;
    public int Slot;
    /// <summary>Which foursquare a square belongs to.</summary>
    public int Group;
    /// <summary>A splitter's size: 0 whole, 1 a half, 2 a quarter.</summary>
    public int Gen;
    /// <summary>How it's turned (drawing, and the foursquare's wheel), and a butterfly's or kitten's heading.</summary>
    public double Turn, Heading;
    /// <summary>A pulsar's clock through its charge.</summary>
    public double Pulse;
    public double Age;
    /// <summary>While above 0 it's still coming out of the nucleus: harmless, and it can't be caught.</summary>
    public double Emerge;
    public double EmergeAngle, EmergeTo;
    /// <summary>Just split: harmless for a moment.</summary>
    public double Fresh;
    public bool Dead;

    public const double EmergeSeconds = 0.9;
    /// <summary>How far a foursquare's squares sit from its middle.</summary>
    public const double FourSpread = 25;

    public double Radius => Kind switch
    {
        Kind.Triangle => 16,
        Kind.Square => 11,
        Kind.Splitter => 19 - 4 * Gen,
        Kind.Pulsar => 16,
        Kind.Butterfly => 16,
        _ => 15,
    };

    /// <summary>How far out from where it moves its edge reaches: a foursquare's squares reach past its middle.</summary>
    public double Reach => Kind == Kind.Square ? FourSpread + Radius : Radius;

    public bool Out => Emerge <= 0;

    /// <summary>Points for catching it, before the multiplier.</summary>
    public int Points => Kind switch
    {
        Kind.Triangle => 100,
        Kind.Square => 150,
        Kind.Splitter => 200 - 50 * Gen,
        Kind.Pulsar => 300,
        Kind.Butterfly => 400,
        _ => 500,
    };
}

/// <summary>The player's probe.</summary>
public sealed class Probe
{
    public double X = Field.CX, Y = Field.CY + Field.R - 50, Vx, Vy;
    /// <summary>Hit: breaking up, before the life is taken.</summary>
    public bool Down;
    public double DownTime;
    /// <summary>Just come back: untouchable for a moment.</summary>
    public double Safe;
    public double Spin;
    public bool Free => !Down;
}

/// <summary>A loop just closed: it flashes and fades.</summary>
public sealed record Catch(Pt[] Loop, int Count, uint Colour)
{
    public double T;
}

/// <summary>A fleck of light thrown off when something goes.</summary>
public sealed class Spark
{
    public double X, Y, Vx, Vy, Life, Max;
    public uint Colour;
}

/// <summary>Points floating up where something was caught.</summary>
public sealed class Popup
{
    public double X, Y, T;
    public string Text = "";
    public uint Colour;
}

/// <summary>Each particle's colour, for the screen and the sparks it leaves.</summary>
public static class Palette
{
    public const uint Probe = 0xFFFFFFFF, Trail = 0xFF7AF0FF, Rim = 0xFF3A7CFF, Nucleus = 0xFFFF5A2A, Electron = 0xFFFFF27A;

    public static uint Of(Kind k) => k switch
    {
        Kind.Triangle => 0xFF5AFF6A,
        Kind.Square => 0xFFFF5AE0,
        Kind.Splitter => 0xFFFFA23A,
        Kind.Pulsar => 0xFFFF3A4A,
        Kind.Butterfly => 0xFF5AD8FF,
        _ => 0xFFFFB0D8,
    };
}
