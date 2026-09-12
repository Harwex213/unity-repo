using System.Collections.Generic;

/// <summary>
/// A simple heuristic opponent. It reads the public game API exactly as a human
/// player would, so it never sees anything a player could not see and never
/// changes the board behind the rules.
/// </summary>
/// <remarks>
/// The AI does no search. It rerolls the weakest dice, spends its resources
/// down a fixed priority list and attacks once it has enough soldiers. Every
/// choice it makes is deterministic given the game state, so an AI against AI
/// run with a fixed seed always plays out the same way.
/// </remarks>
public sealed class HexworldAiPlayer
{
    /// <summary>The build priority used once the urgent cases are handled.</summary>
    private static readonly HexBuildingType[] BuildPriority =
    {
        HexBuildingType.Cottage,
        HexBuildingType.Quarry,
        HexBuildingType.Barracks,
        HexBuildingType.Church,
    };

    /// <summary>How many of each kind the AI wants, in the order of <see cref="BuildPriority"/>.</summary>
    private static readonly int[] BuildLimits = { 3, 2, 2, 1 };

    /// <summary>The game the AI plays in.</summary>
    private readonly HexworldGame _game;

    /// <summary>
    /// Creates an AI for one side.
    /// </summary>
    /// <param name="game">The running game.</param>
    /// <param name="playerIndex">Which side the AI plays.</param>
    public HexworldAiPlayer(HexworldGame game, int playerIndex)
    {
        _game = game;
        PlayerIndex = playerIndex;
    }

    /// <summary>Which side the AI plays.</summary>
    public int PlayerIndex { get; private set; }

    /// <summary>How many soldiers the AI wants before it attacks an enemy tile.</summary>
    public int AttackSoldierThreshold { get; set; } = 3;

    /// <summary>Below this much food the AI builds a Farm before anything else.</summary>
    public int FoodComfortLevel { get; set; } = 4;

    /// <summary>Above this much wood the AI spends on clearing rubble.</summary>
    public int RubbleClearWoodLevel { get; set; } = 4;

    /// <summary>How many Farms the AI builds at most.</summary>
    public int FarmLimit { get; set; } = 3;

    /// <summary>
    /// Plays one full turn: harvest, build and combat, ending with the turn
    /// handed to the other player. Does nothing when it is not the turn of this
    /// AI or the game has ended.
    /// </summary>
    public void PlayTurn()
    {
        if (_game.IsGameOver || _game.CurrentPlayerIndex != PlayerIndex)
        {
            return;
        }

        PlayHarvest();
        PlayBuild();
        PlayCombat();
    }

    /// <summary>
    /// Rerolls the weakest dice while rerolls remain, then applies the harvest.
    /// </summary>
    public void PlayHarvest()
    {
        if (_game.IsGameOver || _game.CurrentPhase != HexworldPhase.Harvest)
        {
            return;
        }

        while (_game.RerollsRemaining > 0)
        {
            List<int> weakest = SelectWeakestDice();
            if (weakest.Count == 0)
            {
                break;
            }

            _game.Reroll(weakest);
        }

        _game.ConfirmHarvest();
    }

    /// <summary>
    /// Spends resources down the priority list until nothing else is
    /// affordable, then closes the build phase.
    /// </summary>
    public void PlayBuild()
    {
        if (_game.IsGameOver || _game.CurrentPhase != HexworldPhase.Build)
        {
            return;
        }

        while (TakeOneBuildAction())
        {
        }

        _game.EndBuildPhase();
    }

    /// <summary>
    /// Attacks or expands once, then ends the turn.
    /// </summary>
    public void PlayCombat()
    {
        if (_game.IsGameOver || _game.CurrentPhase != HexworldPhase.Combat)
        {
            return;
        }

        HexworldPlayerState player = _game.GetPlayer(PlayerIndex);

        if (player.Resources.Soldiers >= AttackSoldierThreshold)
        {
            HexTile target = FindEnemyTarget();
            if (target != null)
            {
                int soldiers = player.Resources.Soldiers;
                if (_game.CanCapture(target.Coord, soldiers))
                {
                    _game.Capture(target.Coord, soldiers);
                    _game.EndTurn();
                    return;
                }
            }
        }

        if (player.Resources.Soldiers >= _game.Config.NeutralCaptureSoldierCost)
        {
            HexTile neutral = FindNeutralTarget();
            if (neutral != null && _game.CanCapture(neutral.Coord, _game.Config.NeutralCaptureSoldierCost))
            {
                _game.Capture(neutral.Coord, _game.Config.NeutralCaptureSoldierCost);
            }
        }

        _game.EndTurn();
    }

    /// <summary>
    /// Picks the dice worth rerolling. A die is weak when its face pays one
    /// unit or less. Skull dice are locked and are never picked.
    /// </summary>
    /// <returns>The indices of the weak dice, possibly empty.</returns>
    private List<int> SelectWeakestDice()
    {
        var result = new List<int>();
        IReadOnlyList<HexworldDie> dice = _game.Dice;

        int weakestValue = int.MaxValue;
        for (int i = 0; i < dice.Count; i++)
        {
            if (!dice[i].CanReroll)
            {
                continue;
            }

            int value = dice[i].Face.Yield.Total;
            if (value < weakestValue)
            {
                weakestValue = value;
            }
        }

        if (weakestValue == int.MaxValue || weakestValue > 1)
        {
            return result;
        }

        for (int i = 0; i < dice.Count; i++)
        {
            if (dice[i].CanReroll && dice[i].Face.Yield.Total == weakestValue)
            {
                result.Add(i);
            }
        }

        return result;
    }

    /// <summary>
    /// Performs at most one build action, following the AI priority list.
    /// </summary>
    /// <returns>True when an action was performed.</returns>
    private bool TakeOneBuildAction()
    {
        HexworldPlayerState player = _game.GetPlayer(PlayerIndex);
        List<HexTile> owned = _game.Board.GetOwnedTiles(PlayerIndex);

        HexTile monumentSpot = FindBuildSpot(owned, HexBuildingType.Monument);
        if (monumentSpot != null)
        {
            _game.Build(monumentSpot.Coord, HexBuildingType.Monument);
            return true;
        }

        if (player.Resources.Wood > RubbleClearWoodLevel)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (_game.CanClearRubble(owned[i].Coord))
                {
                    _game.ClearRubble(owned[i].Coord);
                    return true;
                }
            }
        }

        if (IsSavingTheLastTileForTheMonument(owned))
        {
            return false;
        }

        if (player.Resources.Food < FoodComfortLevel
            && _game.Board.CountBuildings(PlayerIndex, HexBuildingType.Farm) < FarmLimit)
        {
            HexTile farmSpot = FindBuildSpot(owned, HexBuildingType.Farm);
            if (farmSpot != null)
            {
                _game.Build(farmSpot.Coord, HexBuildingType.Farm);
                return true;
            }
        }

        for (int i = 0; i < BuildPriority.Length; i++)
        {
            HexBuildingType type = BuildPriority[i];
            if (_game.Board.CountBuildings(PlayerIndex, type) >= BuildLimits[i])
            {
                continue;
            }

            HexTile spot = FindBuildSpot(owned, type);
            if (spot != null)
            {
                _game.Build(spot.Coord, type);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether the AI should leave its last free tile alone. Once it owns
    /// the Church and the Cottages a Monument needs, filling the last tile
    /// would lock the victory building out of the board.
    /// </summary>
    /// <param name="owned">Tiles the AI owns.</param>
    /// <returns>True when the last free tile is reserved for the Monument.</returns>
    private bool IsSavingTheLastTileForTheMonument(List<HexTile> owned)
    {
        string reason;
        if (!_game.CanBuildMonument(PlayerIndex, out reason))
        {
            return false;
        }

        int free = 0;
        for (int i = 0; i < owned.Count; i++)
        {
            HexTile tile = owned[i];
            if (!tile.HasBuilding && tile.Terrain != HexTerrainType.Rubble)
            {
                free++;
            }
        }

        return free <= 1;
    }

    /// <summary>
    /// Finds the first tile where a building may be raised.
    /// </summary>
    /// <param name="owned">Tiles the AI owns.</param>
    /// <param name="type">Which building to place.</param>
    /// <returns>The tile, or null when there is no legal spot.</returns>
    private HexTile FindBuildSpot(List<HexTile> owned, HexBuildingType type)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (_game.CanBuild(owned[i].Coord, type))
            {
                return owned[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an enemy tile to attack. Tiles that carry a building come first.
    /// </summary>
    /// <returns>The target tile, or null when no enemy tile is reachable.</returns>
    private HexTile FindEnemyTarget()
    {
        int enemy = HexworldGame.Opponent(PlayerIndex);
        HexTile fallback = null;

        IReadOnlyList<HexTile> tiles = _game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            HexTile tile = tiles[i];
            if (!tile.IsOwnedBy(enemy))
            {
                continue;
            }

            if (!_game.Board.IsAdjacentToPlayer(tile.Coord, PlayerIndex))
            {
                continue;
            }

            if (tile.HasBuilding)
            {
                return tile;
            }

            if (fallback == null)
            {
                fallback = tile;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Finds a neutral tile to occupy.
    /// </summary>
    /// <returns>The target tile, or null when no neutral tile is reachable.</returns>
    private HexTile FindNeutralTarget()
    {
        IReadOnlyList<HexTile> tiles = _game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            HexTile tile = tiles[i];
            if (tile.IsVoided || tile.Owner != HexworldConfig.NeutralOwner)
            {
                continue;
            }

            if (_game.Board.IsAdjacentToPlayer(tile.Coord, PlayerIndex))
            {
                return tile;
            }
        }

        return null;
    }
}
