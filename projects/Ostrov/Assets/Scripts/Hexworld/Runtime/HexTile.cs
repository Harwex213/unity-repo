/// <summary>
/// One island tile: where it is, what it is made of, who owns it and what
/// stands on it. A tile dropped into the Ether keeps its coordinate but is out
/// of the game for good.
/// </summary>
/// <remarks>
/// The setters exist so that the game and the tests can shape a board. Views
/// must only read tiles and must react to game events instead of polling.
/// </remarks>
public sealed class HexTile
{
    /// <summary>
    /// Creates a tile.
    /// </summary>
    /// <param name="coord">Where the tile sits.</param>
    /// <param name="terrain">What the tile is made of.</param>
    /// <param name="owner">Player index of the owner, or -1 for nobody.</param>
    public HexTile(HexCoord coord, HexTerrainType terrain, int owner)
    {
        Coord = coord;
        Terrain = terrain;
        Owner = owner;
        Building = null;
        IsVoided = false;
    }

    /// <summary>Where the tile sits on the island.</summary>
    public HexCoord Coord { get; private set; }

    /// <summary>What the tile is made of.</summary>
    public HexTerrainType Terrain { get; set; }

    /// <summary>Player index of the owner, or -1 when nobody owns the tile.</summary>
    public int Owner { get; set; }

    /// <summary>The building standing here, or null when the tile is empty.</summary>
    public HexBuilding Building { get; set; }

    /// <summary>True when the tile was dropped into the Ether and is out of the game.</summary>
    public bool IsVoided { get; set; }

    /// <summary>True when the tile carries a building.</summary>
    public bool HasBuilding
    {
        get { return Building != null; }
    }

    /// <summary>
    /// Tells whether the given player owns this tile and the tile still exists.
    /// </summary>
    /// <param name="playerIndex">Player index to test.</param>
    /// <returns>True when the player owns a tile that is not voided.</returns>
    public bool IsOwnedBy(int playerIndex)
    {
        return !IsVoided && Owner == playerIndex;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsVoided)
        {
            return Coord + " voided";
        }

        return string.Format(
            "{0} {1} owner={2} {3}", Coord, Terrain, Owner, HasBuilding ? Building.Type.ToString() : "empty");
    }
}
