namespace AVAQuantum.Engine;

/// <summary>
/// What each wave holds and how hard it is. The first eight are set out by hand to bring in one new kind of particle
/// at a time; after that each wave is a random mix that grows, and everything keeps getting quicker.
/// </summary>
public static class WaveBook
{
    /// <summary>The waves set out by hand. A <see cref="Kind.Square"/> stands for a whole foursquare.</summary>
    private static readonly Kind[][] Opening =
    [
        [Kind.Triangle, Kind.Triangle, Kind.Triangle, Kind.Triangle],
        [Kind.Triangle, Kind.Triangle, Kind.Triangle, Kind.Splitter, Kind.Splitter],
        [Kind.Triangle, Kind.Triangle, Kind.Pulsar, Kind.Pulsar, Kind.Kitten],
        [Kind.Square, Kind.Splitter, Kind.Splitter, Kind.Triangle, Kind.Triangle],
        [Kind.Butterfly, Kind.Butterfly, Kind.Triangle, Kind.Triangle, Kind.Pulsar, Kind.Kitten],
        [Kind.Pulsar, Kind.Pulsar, Kind.Pulsar, Kind.Splitter, Kind.Splitter, Kind.Square],
        [Kind.Butterfly, Kind.Butterfly, Kind.Butterfly, Kind.Kitten, Kind.Kitten, Kind.Triangle, Kind.Triangle],
        [Kind.Square, Kind.Square, Kind.Pulsar, Kind.Pulsar, Kind.Butterfly, Kind.Butterfly, Kind.Splitter],
    ];

    public static int Handmade => Opening.Length;

    /// <summary>The particles a wave brings out of the nucleus, in the order they come.</summary>
    public static List<Kind> For(int wave, Random rng)
    {
        if (wave < Opening.Length) return [.. Opening[wave]];
        int count = Math.Min(13, 6 + (wave - Opening.Length) / 2);
        var list = new List<Kind>();
        Kind[] all = [Kind.Triangle, Kind.Square, Kind.Splitter, Kind.Pulsar, Kind.Butterfly, Kind.Kitten];
        for (int i = 0; i < count; i++)
        {
            var k = all[rng.Next(all.Length)];
            // Never more than two foursquares or two kittens.
            if (k is Kind.Square or Kind.Kitten && list.Count(x => x == k) >= 2) k = Kind.Triangle;
            list.Add(k);
        }
        return list;
    }

    /// <summary>How much quicker than the first wave everything moves.</summary>
    public static double Speed(int wave) => 1 + 0.07 * Math.Min(wave, 16);

    /// <summary>How many electrons orbit the nucleus.</summary>
    public static int Electrons(int wave) => Math.Min(6, 2 + wave / 2);

    /// <summary>How fast they go round (radians a second).</summary>
    public static double OrbitSpeed(int wave) => Math.Min(4.2, 1.8 + 0.18 * wave);

    /// <summary>How long a pulsar's cycle takes, charged and dark.</summary>
    public static double PulsarPeriod(int wave) => Math.Max(2.2, 3.4 - 0.1 * wave);

    /// <summary>How long the trail can be, in pixels: it shortens as the waves go on.</summary>
    public static double TrailLength(int wave) => Math.Max(640, 980 - 25 * wave);

    /// <summary>The bonus for clearing a wave.</summary>
    public static int ClearBonus(int wave) => 1000 + 500 * wave;

    /// <summary>The bonus for breaking the atom: catching the nucleus, electrons and all, in a loop.</summary>
    public static int AtomBonus(int wave) => 2500 + 500 * wave;

    /// <summary>Catching all four of a foursquare in one loop.</summary>
    public const int FoursquareBonus = 2000;

    /// <summary>The first extra life, and every so many points after.</summary>
    public const int FirstLife = 20000, EveryLife = 30000;
}
