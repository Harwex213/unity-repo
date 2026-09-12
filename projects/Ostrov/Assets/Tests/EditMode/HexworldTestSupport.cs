using System.Collections.Generic;

/// <summary>
/// A randomness source driven by a list of values. Tests enqueue the exact
/// numbers the game should draw. Once the queue runs dry the source returns the
/// lowest value of every range, which keeps an unscripted draw predictable.
/// </summary>
public sealed class HexworldQueueRandom : IHexworldRandom
{
    /// <summary>The scripted values, oldest first.</summary>
    private readonly Queue<int> _values = new Queue<int>();

    /// <summary>
    /// Creates a source with an optional opening script.
    /// </summary>
    /// <param name="values">Values the source returns, in order.</param>
    public HexworldQueueRandom(params int[] values)
    {
        Enqueue(values);
    }

    /// <summary>How many draws the source has served.</summary>
    public int CallCount { get; private set; }

    /// <summary>How many scripted values are still waiting.</summary>
    public int PendingCount
    {
        get { return _values.Count; }
    }

    /// <summary>
    /// Appends values to the script.
    /// </summary>
    /// <param name="values">Values the source returns, in order.</param>
    /// <returns>The same source, so calls can be chained.</returns>
    public HexworldQueueRandom Enqueue(params int[] values)
    {
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _values.Enqueue(values[i]);
            }
        }

        return this;
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxExclusive)
    {
        CallCount++;

        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        if (_values.Count == 0)
        {
            return minInclusive;
        }

        int value = _values.Dequeue();
        if (value < minInclusive)
        {
            return minInclusive;
        }

        if (value >= maxExclusive)
        {
            return maxExclusive - 1;
        }

        return value;
    }
}

/// <summary>
/// Builds games that behave the same on every run: an all-grass island, dice
/// with known faces and a board the test shapes by hand.
/// </summary>
public static class HexworldTestFactory
{
    /// <summary>A die face that pays nothing and shows no skull.</summary>
    public static HexworldDiceFace BlankFace
    {
        get { return HexworldDiceFace.Pays(HexworldResources.Zero); }
    }

    /// <summary>
    /// Returns the default balance with the island forced to all grass, so the
    /// board setup draws no meaningful randomness.
    /// </summary>
    /// <returns>A config the test may still edit.</returns>
    public static HexworldConfig CreateConfig()
    {
        HexworldConfig config = HexworldConfig.CreateDefault();
        config.TerrainWeightsPercent = new[] { 100, 0, 0, 0 };
        return config;
    }

    /// <summary>
    /// Returns a config where no building can ever roll anything, which makes
    /// the harvest change nothing but upkeep.
    /// </summary>
    /// <returns>A config the test may still edit.</returns>
    public static HexworldConfig CreateSilentConfig()
    {
        HexworldConfig config = CreateConfig();
        foreach (HexBuildingType type in System.Enum.GetValues(typeof(HexBuildingType)))
        {
            if (type == HexBuildingType.None || config.GetBuilding(type) == null)
            {
                continue;
            }

            if (config.GetBuilding(type).ProducesDice)
            {
                SetUniformDie(config, type, BlankFace);
            }

            SetFlatIncome(config, type, HexworldResources.Zero);
        }

        return config;
    }

    /// <summary>
    /// Replaces the die of a building so that every face is the same.
    /// </summary>
    /// <param name="config">The config to change.</param>
    /// <param name="type">Which building to change.</param>
    /// <param name="face">The face that all six sides carry.</param>
    public static void SetUniformDie(HexworldConfig config, HexBuildingType type, HexworldDiceFace face)
    {
        HexworldBuildingDefinition source = config.GetBuilding(type);
        var faces = new HexworldDiceFace[6];
        for (int i = 0; i < faces.Length; i++)
        {
            faces[i] = face;
        }

        config.SetBuilding(new HexworldBuildingDefinition(
            type,
            source.Cost,
            source.FlatIncome,
            faces,
            source.IsBuildable,
            source.IsDestructibleByDisaster,
            source.RequiresUpkeep));
    }

    /// <summary>
    /// Replaces the flat income of a building.
    /// </summary>
    /// <param name="config">The config to change.</param>
    /// <param name="type">Which building to change.</param>
    /// <param name="income">The new flat income.</param>
    public static void SetFlatIncome(HexworldConfig config, HexBuildingType type, HexworldResources income)
    {
        HexworldBuildingDefinition source = config.GetBuilding(type);
        config.SetBuilding(new HexworldBuildingDefinition(
            type,
            source.Cost,
            income,
            source.Faces,
            source.IsBuildable,
            source.IsDestructibleByDisaster,
            source.RequiresUpkeep));
    }

    /// <summary>
    /// Builds a game but does not open the first turn, so the test can shape
    /// the board first and script the randomness afterwards.
    /// </summary>
    /// <param name="config">Balance data.</param>
    /// <param name="random">Randomness source.</param>
    /// <returns>An unstarted game.</returns>
    public static HexworldGame CreateUnstartedGame(HexworldConfig config, IHexworldRandom random)
    {
        return new HexworldGame(config, random, false);
    }

    /// <summary>
    /// Puts a building on a tile without paying for it.
    /// </summary>
    /// <param name="game">The game to change.</param>
    /// <param name="coord">Where the building goes.</param>
    /// <param name="type">What to place.</param>
    /// <param name="owner">Who owns the tile afterwards.</param>
    public static void PlaceBuilding(HexworldGame game, HexCoord coord, HexBuildingType type, int owner)
    {
        HexTile tile = game.Board.GetTile(coord);
        tile.Owner = owner;
        tile.Building = new HexBuilding(type);
    }

    /// <summary>
    /// Removes every building of a player except the Castle.
    /// </summary>
    /// <param name="game">The game to change.</param>
    /// <param name="playerIndex">Whose buildings to remove.</param>
    public static void RemoveBuildings(HexworldGame game, int playerIndex)
    {
        IReadOnlyList<HexTile> tiles = game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            HexTile tile = tiles[i];
            if (tile.IsOwnedBy(playerIndex) && tile.HasBuilding && tile.Building.Type != HexBuildingType.Castle)
            {
                tile.Building = null;
            }
        }
    }

    /// <summary>
    /// Overwrites the stockpile of a player.
    /// </summary>
    /// <param name="game">The game to change.</param>
    /// <param name="playerIndex">Whose stockpile to set.</param>
    /// <param name="resources">The new stockpile.</param>
    public static void SetResources(HexworldGame game, int playerIndex, HexworldResources resources)
    {
        game.GetPlayer(playerIndex).Resources = resources;
    }

    /// <summary>
    /// Collects the tiles a player owns that carry no building.
    /// </summary>
    /// <param name="game">The game to read.</param>
    /// <param name="playerIndex">Whose tiles to collect.</param>
    /// <returns>The empty tiles in stable order.</returns>
    public static List<HexTile> GetEmptyTiles(HexworldGame game, int playerIndex)
    {
        var result = new List<HexTile>();
        List<HexTile> owned = game.Board.GetOwnedTiles(playerIndex);
        for (int i = 0; i < owned.Count; i++)
        {
            if (!owned[i].HasBuilding)
            {
                result.Add(owned[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// Runs the harvest, build and combat phases of the running turn without
    /// taking any action, which hands the turn to the other player.
    /// </summary>
    /// <param name="game">The game to advance.</param>
    public static void PassTurn(HexworldGame game)
    {
        game.AdvancePhase();
        if (game.IsGameOver)
        {
            return;
        }

        game.AdvancePhase();
        if (game.IsGameOver)
        {
            return;
        }

        game.AdvancePhase();
    }
}
