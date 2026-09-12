/// <summary>
/// The single source of randomness for the whole game core. Every roll, every
/// pick of a random tile and the terrain generation go through this interface,
/// so a test can hand the game a scripted source and get a repeatable run.
/// </summary>
public interface IHexworldRandom
{
    /// <summary>
    /// Returns a whole number inside the given range.
    /// </summary>
    /// <param name="minInclusive">Lowest value that may be returned.</param>
    /// <param name="maxExclusive">Upper bound, never returned itself.</param>
    /// <returns>A number in the range, or the lower bound when the range is empty.</returns>
    int NextInt(int minInclusive, int maxExclusive);
}

/// <summary>
/// Convenience helpers built on top of <see cref="IHexworldRandom"/>.
/// </summary>
public static class HexworldRandomExtensions
{
    /// <summary>
    /// Rolls a six sided die.
    /// </summary>
    /// <param name="random">The randomness source.</param>
    /// <returns>A number from 1 to 6.</returns>
    public static int RollD6(this IHexworldRandom random)
    {
        return random.NextInt(1, 7);
    }

    /// <summary>
    /// Picks the index of a random entry of a list.
    /// </summary>
    /// <param name="random">The randomness source.</param>
    /// <param name="count">How many entries the list holds.</param>
    /// <returns>An index in the range 0..count-1, or -1 when the list is empty.</returns>
    public static int NextIndex(this IHexworldRandom random, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        return random.NextInt(0, count);
    }
}
