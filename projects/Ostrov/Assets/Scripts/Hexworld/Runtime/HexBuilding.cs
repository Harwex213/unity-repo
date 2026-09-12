/// <summary>
/// A building instance standing on a tile. The kind never changes; only the
/// active flag does, and upkeep is what flips it.
/// </summary>
public sealed class HexBuilding
{
    /// <summary>
    /// Creates a building instance.
    /// </summary>
    /// <param name="type">Which building this is.</param>
    public HexBuilding(HexBuildingType type)
    {
        Type = type;
        IsActive = true;
    }

    /// <summary>Which building this is.</summary>
    public HexBuildingType Type { get; private set; }

    /// <summary>
    /// True when the building was fed during the last upkeep. An inactive
    /// building rolls no die and pays no income until it is fed again.
    /// </summary>
    public bool IsActive { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsActive ? Type.ToString() : Type + " (starving)";
    }
}
