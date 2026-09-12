/// <summary>
/// The default randomness source. It runs a small xorshift generator, so the
/// same seed always produces the same sequence on every platform. Neither
/// <c>UnityEngine.Random</c> nor <c>System.Random</c> is involved.
/// </summary>
public sealed class HexworldRandom : IHexworldRandom
{
    /// <summary>Fallback state used when the caller passes a seed of zero.</summary>
    private const uint FallbackState = 0x9E3779B9u;

    /// <summary>Current generator state. It is never allowed to become zero.</summary>
    private uint _state;

    /// <summary>
    /// Creates a generator from a seed.
    /// </summary>
    /// <param name="seed">The seed. Any value works; zero is replaced by a fixed constant.</param>
    public HexworldRandom(int seed)
    {
        Seed = seed;
        _state = seed == 0 ? FallbackState : unchecked((uint)seed);
    }

    /// <summary>The seed the generator was created with.</summary>
    public int Seed { get; private set; }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        uint range = unchecked((uint)(maxExclusive - minInclusive));
        return minInclusive + unchecked((int)(NextState() % range));
    }

    /// <summary>
    /// Advances the xorshift state and returns it.
    /// </summary>
    /// <returns>The new state, which is never zero.</returns>
    private uint NextState()
    {
        unchecked
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            if (_state == 0)
            {
                _state = FallbackState;
            }

            return _state;
        }
    }
}
