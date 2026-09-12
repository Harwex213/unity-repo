/// <summary>
/// One of the six faces of a building die. A face either pays resources or
/// shows a skull. A skull face pays nothing and adds one skull to the counter
/// of the player who rolled it.
/// </summary>
public readonly struct HexworldDiceFace
{
    /// <summary>What the face pays. A skull face pays nothing.</summary>
    public HexworldResources Yield { get; }

    /// <summary>True when the face shows a skull.</summary>
    public bool IsSkull { get; }

    /// <summary>
    /// Creates a face.
    /// </summary>
    /// <param name="yield">What the face pays.</param>
    /// <param name="isSkull">True to make it a skull face.</param>
    public HexworldDiceFace(HexworldResources yield, bool isSkull)
    {
        Yield = isSkull ? HexworldResources.Zero : yield;
        IsSkull = isSkull;
    }

    /// <summary>
    /// Creates a paying face.
    /// </summary>
    /// <param name="yield">What the face pays.</param>
    /// <returns>The face.</returns>
    public static HexworldDiceFace Pays(HexworldResources yield)
    {
        return new HexworldDiceFace(yield, false);
    }

    /// <summary>
    /// Creates a skull face.
    /// </summary>
    /// <returns>The face.</returns>
    public static HexworldDiceFace Skull()
    {
        return new HexworldDiceFace(HexworldResources.Zero, true);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsSkull ? "Skull" : Yield.ToString();
    }
}
