using System.Collections.Generic;

/// <summary>
/// The island. It holds every tile of a hexagonal area of the configured
/// radius and answers the lookups the game and the view need.
/// </summary>
/// <remarks>
/// Tile order is stable. <see cref="Tiles"/> always walks the tiles in the same
/// order, which keeps every rule that picks a tile deterministic.
/// </remarks>
public sealed class HexBoard
{
    /// <summary>Tiles by coordinate, for fast lookup.</summary>
    private readonly Dictionary<HexCoord, HexTile> _byCoord;

    /// <summary>Tiles in a stable order.</summary>
    private readonly List<HexTile> _ordered;

    /// <summary>
    /// Creates an empty island of the given radius. Every tile is grass and
    /// belongs to nobody until the setup fills it in.
    /// </summary>
    /// <param name="radius">Radius of the island in hexagon steps.</param>
    public HexBoard(int radius)
    {
        Radius = radius;
        _byCoord = new Dictionary<HexCoord, HexTile>();
        _ordered = new List<HexTile>();

        foreach (HexCoord coord in HexGrid.Area(radius))
        {
            var tile = new HexTile(coord, HexTerrainType.Grass, HexworldConfig.NeutralOwner);
            _byCoord.Add(coord, tile);
            _ordered.Add(tile);
        }
    }

    /// <summary>Radius of the island in hexagon steps.</summary>
    public int Radius { get; private set; }

    /// <summary>How many tiles the island holds, voided ones included.</summary>
    public int TileCount
    {
        get { return _ordered.Count; }
    }

    /// <summary>Every tile in a stable order.</summary>
    public IReadOnlyList<HexTile> Tiles
    {
        get { return _ordered; }
    }

    /// <summary>
    /// Tells whether a coordinate belongs to the island.
    /// </summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <returns>True when a tile exists at that coordinate.</returns>
    public bool Contains(HexCoord coord)
    {
        return _byCoord.ContainsKey(coord);
    }

    /// <summary>
    /// Returns the tile at a coordinate.
    /// </summary>
    /// <param name="coord">The coordinate to look up.</param>
    /// <returns>The tile, or null when the coordinate is off the island.</returns>
    public HexTile GetTile(HexCoord coord)
    {
        HexTile tile;
        return _byCoord.TryGetValue(coord, out tile) ? tile : null;
    }

    /// <summary>
    /// Tries to return the tile at a coordinate.
    /// </summary>
    /// <param name="coord">The coordinate to look up.</param>
    /// <param name="tile">Receives the tile, or null.</param>
    /// <returns>True when a tile exists at that coordinate.</returns>
    public bool TryGetTile(HexCoord coord, out HexTile tile)
    {
        return _byCoord.TryGetValue(coord, out tile);
    }

    /// <summary>
    /// Walks the existing neighbours of a tile. Coordinates outside the island
    /// are skipped; voided tiles are still returned.
    /// </summary>
    /// <param name="coord">The centre tile.</param>
    /// <returns>Up to six neighbouring tiles.</returns>
    public IEnumerable<HexTile> GetNeighbors(HexCoord coord)
    {
        for (int direction = 0; direction < HexCoord.DirectionCount; direction++)
        {
            HexTile tile;
            if (_byCoord.TryGetValue(coord.Neighbor(direction), out tile))
            {
                yield return tile;
            }
        }
    }

    /// <summary>
    /// Collects every tile a player owns and that is not voided.
    /// </summary>
    /// <param name="playerIndex">Player index to filter by.</param>
    /// <returns>The owned tiles in stable order.</returns>
    public List<HexTile> GetOwnedTiles(int playerIndex)
    {
        var result = new List<HexTile>();
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].IsOwnedBy(playerIndex))
            {
                result.Add(_ordered[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// Counts the buildings of one kind a player owns.
    /// </summary>
    /// <param name="playerIndex">Player index to filter by.</param>
    /// <param name="type">Which building to count.</param>
    /// <returns>How many such buildings stand on the player tiles.</returns>
    public int CountBuildings(int playerIndex, HexBuildingType type)
    {
        int count = 0;
        for (int i = 0; i < _ordered.Count; i++)
        {
            HexTile tile = _ordered[i];
            if (tile.IsOwnedBy(playerIndex) && tile.HasBuilding && tile.Building.Type == type)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Finds the tile that carries a given building of a player.
    /// </summary>
    /// <param name="playerIndex">Player index to filter by.</param>
    /// <param name="type">Which building to find.</param>
    /// <returns>The first matching tile in stable order, or null.</returns>
    public HexTile FindBuilding(int playerIndex, HexBuildingType type)
    {
        for (int i = 0; i < _ordered.Count; i++)
        {
            HexTile tile = _ordered[i];
            if (tile.IsOwnedBy(playerIndex) && tile.HasBuilding && tile.Building.Type == type)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// Tells whether a tile touches at least one tile of the given player.
    /// </summary>
    /// <param name="coord">The tile to test.</param>
    /// <param name="playerIndex">Player index to look for.</param>
    /// <returns>True when one of the six neighbours belongs to that player.</returns>
    public bool IsAdjacentToPlayer(HexCoord coord, int playerIndex)
    {
        foreach (HexTile neighbor in GetNeighbors(coord))
        {
            if (neighbor.IsOwnedBy(playerIndex))
            {
                return true;
            }
        }

        return false;
    }
}
