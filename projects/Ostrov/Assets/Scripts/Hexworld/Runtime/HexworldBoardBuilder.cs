using System.Collections.Generic;

/// <summary>
/// Builds the starting island: it spreads the terrain, plants both Castles and
/// hands the six tiles around each Castle to its owner.
/// </summary>
/// <remarks>
/// Terrain is drawn from the randomness source of the game, so the same seed
/// always produces the same island. Tiles under a Castle and around it are
/// always grass, which keeps both starting positions fair.
/// </remarks>
public static class HexworldBoardBuilder
{
    /// <summary>
    /// Builds a starting board from a config and a randomness source.
    /// </summary>
    /// <param name="config">Balance data, which carries the radius and the Castle spots.</param>
    /// <param name="random">The randomness source used for terrain.</param>
    /// <returns>A board ready for the first turn.</returns>
    public static HexBoard Build(HexworldConfig config, IHexworldRandom random)
    {
        var board = new HexBoard(config.IslandRadius);
        var reserved = new HashSet<HexCoord>();

        for (int player = 0; player < config.CastleCoords.Length; player++)
        {
            HexCoord castle = config.CastleCoords[player];
            reserved.Add(castle);
            foreach (HexCoord neighbor in castle.GetNeighbors())
            {
                if (board.Contains(neighbor))
                {
                    reserved.Add(neighbor);
                }
            }
        }

        for (int i = 0; i < board.Tiles.Count; i++)
        {
            HexTile tile = board.Tiles[i];
            tile.Terrain = reserved.Contains(tile.Coord)
                ? HexTerrainType.Grass
                : RollTerrain(config, random);
        }

        for (int player = 0; player < config.CastleCoords.Length; player++)
        {
            HexCoord castleCoord = config.CastleCoords[player];
            HexTile castleTile = board.GetTile(castleCoord);
            if (castleTile == null)
            {
                continue;
            }

            castleTile.Owner = player;
            castleTile.Building = new HexBuilding(HexBuildingType.Castle);

            foreach (HexCoord neighbor in castleCoord.GetNeighbors())
            {
                HexTile tile = board.GetTile(neighbor);
                if (tile != null)
                {
                    tile.Owner = player;
                }
            }
        }

        return board;
    }

    /// <summary>
    /// Draws one terrain kind from the configured shares.
    /// </summary>
    /// <param name="config">Balance data, which carries the shares.</param>
    /// <param name="random">The randomness source.</param>
    /// <returns>The drawn terrain.</returns>
    private static HexTerrainType RollTerrain(HexworldConfig config, IHexworldRandom random)
    {
        int[] weights = config.TerrainWeightsPercent;
        int total = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            total += weights[i];
        }

        if (total <= 0)
        {
            return HexTerrainType.Grass;
        }

        int roll = random.NextInt(0, total);
        int running = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            running += weights[i];
            if (roll < running)
            {
                return (HexTerrainType)i;
            }
        }

        return HexTerrainType.Grass;
    }
}
