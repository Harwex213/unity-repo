using System;
using System.Collections.Generic;

/// <summary>
/// The whole game. It owns the board, both players and the turn order, and it
/// is the only entry point a view or a UI needs.
/// </summary>
/// <remarks>
/// <para>
/// The class is plain C#. It touches no scene, no MonoBehaviour and no Unity
/// randomness, so it runs inside an EditMode test as happily as inside a build.
/// Every random draw goes through <see cref="IHexworldRandom"/>, which makes a
/// run with a fixed seed repeat exactly.
/// </para>
/// <para>
/// A turn runs Harvest, then Build, then Combat. Call
/// <see cref="AdvancePhase"/> to close the running phase, or call the named
/// method of that phase. Every player action comes as a pair: a
/// <c>Can...</c> query that a UI uses to grey a button out, and a method that
/// performs the action and throws when the query would have said no.
/// </para>
/// <para>
/// Changes are announced through plain C# events. A view subscribes once and
/// reacts to the facts; it never polls the board. Events are raised as the
/// change happens, so the board already carries the new state when a handler
/// runs.
/// </para>
/// </remarks>
public sealed class HexworldGame
{
    /// <summary>Balance data.</summary>
    private readonly HexworldConfig _config;

    /// <summary>The single randomness source of the game.</summary>
    private readonly IHexworldRandom _random;

    /// <summary>The island.</summary>
    private readonly HexBoard _board;

    /// <summary>Both player states, indexed by player number.</summary>
    private readonly HexworldPlayerState[] _players;

    /// <summary>Dice of the running harvest.</summary>
    private readonly List<HexworldDie> _dice;

    /// <summary>
    /// Starts a game from a config and a randomness source. The board is built
    /// and the first Harvest phase of player 0 begins right away.
    /// </summary>
    /// <param name="config">Balance data, or null for <see cref="HexworldConfig.CreateDefault"/>.</param>
    /// <param name="random">Randomness source, or null for a source seeded with 1.</param>
    public HexworldGame(HexworldConfig config, IHexworldRandom random)
        : this(config, random, true)
    {
    }

    /// <summary>
    /// Builds a game and decides whether the first turn starts at once. Pass
    /// false when the view wants to subscribe to the events before the opening
    /// roll happens, then call <see cref="Start"/>.
    /// </summary>
    /// <param name="config">Balance data, or null for <see cref="HexworldConfig.CreateDefault"/>.</param>
    /// <param name="random">Randomness source, or null for a source seeded with 1.</param>
    /// <param name="startImmediately">True to open the first turn inside the constructor.</param>
    public HexworldGame(HexworldConfig config, IHexworldRandom random, bool startImmediately)
    {
        _config = config ?? HexworldConfig.CreateDefault();
        _random = random ?? new HexworldRandom(1);
        _dice = new List<HexworldDie>();

        _board = HexworldBoardBuilder.Build(_config, _random);

        _players = new HexworldPlayerState[HexworldConfig.PlayerCount];
        for (int i = 0; i < _players.Length; i++)
        {
            _players[i] = new HexworldPlayerState(i, _config.StartingResources);
        }

        Winner = HexworldConfig.NeutralOwner;
        WinReason = HexworldWinReason.None;
        CurrentPlayerIndex = 0;
        TurnNumber = 0;
        CurrentPhase = HexworldPhase.Harvest;

        if (startImmediately)
        {
            Start();
        }
    }

    /// <summary>
    /// Starts a game with the default balance and a seeded randomness source.
    /// </summary>
    /// <param name="seed">Seed of the randomness source.</param>
    public HexworldGame(int seed)
        : this(HexworldConfig.CreateDefault(), new HexworldRandom(seed))
    {
    }

    /// <summary>Raised whenever the stockpile of a player changes.</summary>
    public event Action<HexworldResourcesChangedEvent> ResourcesChanged;

    /// <summary>Raised whenever a building appears on the board.</summary>
    public event Action<HexworldBuildingBuiltEvent> BuildingBuilt;

    /// <summary>Raised whenever a building leaves the board.</summary>
    public event Action<HexworldBuildingDestroyedEvent> BuildingDestroyed;

    /// <summary>Raised whenever a tile changes hands.</summary>
    public event Action<HexworldTileOwnerChangedEvent> TileOwnerChanged;

    /// <summary>Raised whenever a tile drops into the Ether.</summary>
    public event Action<HexworldTileVoidedEvent> TileVoided;

    /// <summary>Raised whenever the terrain of a tile changes.</summary>
    public event Action<HexworldTerrainChangedEvent> TerrainChanged;

    /// <summary>Raised on the opening harvest roll and on every reroll.</summary>
    public event Action<HexworldDiceRolledEvent> DiceRolled;

    /// <summary>Raised whenever a new phase starts, the first phase of a turn included.</summary>
    public event Action<HexworldPhaseChangedEvent> PhaseChanged;

    /// <summary>Raised when a skull disaster strikes at the end of a harvest.</summary>
    public event Action<HexworldDisasterEvent> DisasterStruck;

    /// <summary>Raised after every capture attempt, won or lost.</summary>
    public event Action<HexworldCombatResult> CombatResolved;

    /// <summary>Raised once, when the game ends.</summary>
    public event Action<HexworldGameOverEvent> GameOver;

    /// <summary>Balance data this game runs on.</summary>
    public HexworldConfig Config
    {
        get { return _config; }
    }

    /// <summary>The island.</summary>
    public HexBoard Board
    {
        get { return _board; }
    }

    /// <summary>Both player states, indexed by player number.</summary>
    public IReadOnlyList<HexworldPlayerState> Players
    {
        get { return _players; }
    }

    /// <summary>Index of the player whose turn is running.</summary>
    public int CurrentPlayerIndex { get; private set; }

    /// <summary>State of the player whose turn is running.</summary>
    public HexworldPlayerState CurrentPlayer
    {
        get { return _players[CurrentPlayerIndex]; }
    }

    /// <summary>The running phase.</summary>
    public HexworldPhase CurrentPhase { get; private set; }

    /// <summary>How many turns have started in total, counting both players.</summary>
    public int TurnNumber { get; private set; }

    /// <summary>The dice of the running harvest, empty outside the harvest phase.</summary>
    public IReadOnlyList<HexworldDie> Dice
    {
        get { return _dice; }
    }

    /// <summary>How many rerolls the running harvest still has.</summary>
    public int RerollsRemaining { get; private set; }

    /// <summary>True when the game has ended.</summary>
    public bool IsGameOver
    {
        get { return CurrentPhase == HexworldPhase.GameOver; }
    }

    /// <summary>Player index of the winner, or -1 while the game runs.</summary>
    public int Winner { get; private set; }

    /// <summary>How the game was won, or None while it runs.</summary>
    public HexworldWinReason WinReason { get; private set; }

    /// <summary>True once the first turn has been opened.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Opens the first turn: player 0 enters the harvest phase and the opening
    /// dice are rolled. Calling it a second time does nothing.
    /// </summary>
    public void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;
        BeginTurn(0);
    }

    /// <summary>
    /// Returns the state of one player.
    /// </summary>
    /// <param name="playerIndex">Player index, 0 or 1.</param>
    /// <returns>The player state.</returns>
    public HexworldPlayerState GetPlayer(int playerIndex)
    {
        return _players[playerIndex];
    }

    /// <summary>
    /// Returns the index of the other player.
    /// </summary>
    /// <param name="playerIndex">Player index, 0 or 1.</param>
    /// <returns>The opponent index.</returns>
    public static int Opponent(int playerIndex)
    {
        return 1 - playerIndex;
    }

    /// <summary>
    /// Closes the running phase and opens the next one. In the harvest phase
    /// this applies the dice, in the build phase it moves on to combat, and in
    /// the combat phase it ends the turn.
    /// </summary>
    public void AdvancePhase()
    {
        switch (CurrentPhase)
        {
            case HexworldPhase.Harvest:
                ConfirmHarvest();
                break;
            case HexworldPhase.Build:
                EndBuildPhase();
                break;
            case HexworldPhase.Combat:
                EndTurn();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Harvest phase
    // ------------------------------------------------------------------

    /// <summary>
    /// Tells whether the listed dice may be rerolled right now.
    /// </summary>
    /// <param name="diceIndices">Indices into <see cref="Dice"/>.</param>
    /// <param name="reason">Receives why the reroll is refused, or an empty string.</param>
    /// <returns>True when <see cref="Reroll"/> would succeed.</returns>
    public bool CanReroll(IReadOnlyList<int> diceIndices, out string reason)
    {
        reason = string.Empty;

        if (CurrentPhase != HexworldPhase.Harvest)
        {
            reason = "Dice can only be rerolled during the harvest phase.";
            return false;
        }

        if (RerollsRemaining <= 0)
        {
            reason = "No rerolls left.";
            return false;
        }

        if (diceIndices == null || diceIndices.Count == 0)
        {
            reason = "No dice were selected.";
            return false;
        }

        for (int i = 0; i < diceIndices.Count; i++)
        {
            int index = diceIndices[i];
            if (index < 0 || index >= _dice.Count)
            {
                reason = "Die index " + index + " does not exist.";
                return false;
            }

            if (!_dice[index].CanReroll)
            {
                reason = "Die " + index + " shows a skull and is locked.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tells whether the listed dice may be rerolled right now.
    /// </summary>
    /// <param name="diceIndices">Indices into <see cref="Dice"/>.</param>
    /// <returns>True when <see cref="Reroll"/> would succeed.</returns>
    public bool CanReroll(IReadOnlyList<int> diceIndices)
    {
        string reason;
        return CanReroll(diceIndices, out reason);
    }

    /// <summary>
    /// Rerolls the listed dice and spends one reroll. Dice showing a skull are
    /// locked and may not be listed.
    /// </summary>
    /// <param name="diceIndices">Indices into <see cref="Dice"/>.</param>
    /// <exception cref="InvalidOperationException">The reroll is not allowed.</exception>
    public void Reroll(IReadOnlyList<int> diceIndices)
    {
        string reason;
        if (!CanReroll(diceIndices, out reason))
        {
            throw new InvalidOperationException(reason);
        }

        for (int i = 0; i < diceIndices.Count; i++)
        {
            RollDie(_dice[diceIndices[i]]);
        }

        RerollsRemaining--;
        RaiseDiceRolled(true);
    }

    /// <summary>
    /// Rerolls the listed dice and spends one reroll.
    /// </summary>
    /// <param name="diceIndices">Indices into <see cref="Dice"/>.</param>
    public void Reroll(params int[] diceIndices)
    {
        Reroll((IReadOnlyList<int>)diceIndices);
    }

    /// <summary>
    /// Applies the harvest: dice pay out, flat income and terrain bonuses are
    /// added, skulls are counted, upkeep is charged and disasters strike. The
    /// turn then moves to the build phase.
    /// </summary>
    /// <exception cref="InvalidOperationException">The harvest phase is not running.</exception>
    public void ConfirmHarvest()
    {
        if (CurrentPhase != HexworldPhase.Harvest)
        {
            throw new InvalidOperationException("The harvest phase is not running.");
        }

        HexworldPlayerState player = CurrentPlayer;
        HexworldResources gained = HexworldResources.Zero;
        int skulls = 0;

        for (int i = 0; i < _dice.Count; i++)
        {
            HexworldDie die = _dice[i];
            if (die.IsSkull)
            {
                skulls++;
            }
            else
            {
                gained = gained + die.Face.Yield;
            }
        }

        List<HexTile> owned = _board.GetOwnedTiles(player.Index);
        for (int i = 0; i < owned.Count; i++)
        {
            HexTile tile = owned[i];
            if (!tile.HasBuilding || !tile.Building.IsActive)
            {
                continue;
            }

            HexworldBuildingDefinition definition = _config.GetBuilding(tile.Building.Type);
            if (definition == null)
            {
                continue;
            }

            gained = gained + definition.FlatIncome;
            gained = gained + _config.GetTerrainBonus(tile.Terrain);
        }

        player.Skulls += skulls;
        AddResources(player, gained);

        ApplyUpkeep(player);
        ApplyDisasters(player);

        SetPhase(HexworldPhase.Build);
    }

    /// <summary>
    /// Rolls the dice of every active building the current player owns.
    /// </summary>
    private void RollHarvestDice()
    {
        _dice.Clear();

        List<HexTile> owned = _board.GetOwnedTiles(CurrentPlayerIndex);
        for (int i = 0; i < owned.Count; i++)
        {
            HexTile tile = owned[i];
            if (!tile.HasBuilding || !tile.Building.IsActive)
            {
                continue;
            }

            HexworldBuildingDefinition definition = _config.GetBuilding(tile.Building.Type);
            if (definition == null || !definition.ProducesDice)
            {
                continue;
            }

            var die = new HexworldDie(_dice.Count, tile.Coord, tile.Building.Type);
            RollDie(die);
            _dice.Add(die);
        }

        RerollsRemaining = _config.RerollsPerHarvest;
        RaiseDiceRolled(false);
    }

    /// <summary>
    /// Draws one face for a die.
    /// </summary>
    /// <param name="die">The die to roll.</param>
    private void RollDie(HexworldDie die)
    {
        HexworldBuildingDefinition definition = _config.GetBuilding(die.BuildingType);
        if (definition == null || !definition.ProducesDice)
        {
            return;
        }

        int faceIndex = _random.NextInt(0, definition.Faces.Count);
        die.SetFace(faceIndex, definition.Faces[faceIndex]);
    }

    /// <summary>
    /// Feeds the buildings of a player. A building that gets its food is active
    /// for the next harvest; a building left hungry goes inactive. Food never
    /// drops below zero.
    /// </summary>
    /// <param name="player">The player who pays upkeep.</param>
    private void ApplyUpkeep(HexworldPlayerState player)
    {
        List<HexTile> owned = _board.GetOwnedTiles(player.Index);
        int available = player.Resources.Food;
        int spent = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            HexTile tile = owned[i];
            if (!tile.HasBuilding)
            {
                continue;
            }

            HexworldBuildingDefinition definition = _config.GetBuilding(tile.Building.Type);
            if (definition == null || !definition.RequiresUpkeep)
            {
                tile.Building.IsActive = true;
                continue;
            }

            int cost = _config.UpkeepFoodPerBuilding;
            if (available - spent >= cost)
            {
                spent += cost;
                tile.Building.IsActive = true;
            }
            else
            {
                tile.Building.IsActive = false;
            }
        }

        if (spent > 0)
        {
            AddResources(player, HexworldResources.FromFood(-spent));
        }
    }

    /// <summary>
    /// Applies the skull disasters of the running turn.
    /// </summary>
    /// <param name="player">The player who rolled the skulls.</param>
    private void ApplyDisasters(HexworldPlayerState player)
    {
        int skulls = player.Skulls;
        if (skulls < _config.SkullsToDestroyBuilding)
        {
            return;
        }

        bool destroyedBuilding = false;
        bool voidedTile = false;

        var destructible = new List<HexTile>();
        List<HexTile> owned = _board.GetOwnedTiles(player.Index);
        for (int i = 0; i < owned.Count; i++)
        {
            HexTile tile = owned[i];
            if (!tile.HasBuilding)
            {
                continue;
            }

            HexworldBuildingDefinition definition = _config.GetBuilding(tile.Building.Type);
            if (definition != null && definition.IsDestructibleByDisaster)
            {
                destructible.Add(tile);
            }
        }

        if (destructible.Count > 0)
        {
            int pick = _random.NextIndex(destructible.Count);
            DestroyBuilding(destructible[pick], HexworldBuildingLossReason.Disaster);
            destroyedBuilding = true;
        }

        if (skulls >= _config.SkullsToVoidTile)
        {
            var borderTiles = new List<HexTile>();
            List<HexTile> remaining = _board.GetOwnedTiles(player.Index);
            for (int i = 0; i < remaining.Count; i++)
            {
                if (HexGrid.IsBorder(remaining[i].Coord, _board.Radius))
                {
                    borderTiles.Add(remaining[i]);
                }
            }

            if (borderTiles.Count > 0)
            {
                int pick = _random.NextIndex(borderTiles.Count);
                VoidTile(borderTiles[pick]);
                voidedTile = true;
            }
        }

        Action<HexworldDisasterEvent> handler = DisasterStruck;
        if (handler != null)
        {
            handler(new HexworldDisasterEvent(player.Index, skulls, destroyedBuilding, voidedTile));
        }
    }

    // ------------------------------------------------------------------
    // Build phase
    // ------------------------------------------------------------------

    /// <summary>
    /// Tells whether the current player may build the given building on the
    /// given tile right now.
    /// </summary>
    /// <param name="coord">Where to build.</param>
    /// <param name="type">What to build.</param>
    /// <param name="reason">Receives why building is refused, or an empty string.</param>
    /// <returns>True when <see cref="Build"/> would succeed.</returns>
    public bool CanBuild(HexCoord coord, HexBuildingType type, out string reason)
    {
        reason = string.Empty;

        if (CurrentPhase != HexworldPhase.Build)
        {
            reason = "Buildings can only be raised during the build phase.";
            return false;
        }

        HexworldBuildingDefinition definition = _config.GetBuilding(type);
        if (definition == null || !definition.IsBuildable)
        {
            reason = type + " cannot be built.";
            return false;
        }

        HexTile tile = _board.GetTile(coord);
        if (tile == null)
        {
            reason = "There is no tile at " + coord + ".";
            return false;
        }

        if (tile.IsVoided)
        {
            reason = "The tile has been dropped into the Ether.";
            return false;
        }

        if (tile.Owner != CurrentPlayerIndex)
        {
            reason = "The tile does not belong to the current player.";
            return false;
        }

        if (tile.Terrain == HexTerrainType.Rubble)
        {
            reason = "Rubble must be cleared before anything is built on it.";
            return false;
        }

        if (tile.HasBuilding)
        {
            reason = "The tile already carries a building.";
            return false;
        }

        if (type == HexBuildingType.Monument && !CanBuildMonument(CurrentPlayerIndex, out reason))
        {
            return false;
        }

        if (!CurrentPlayer.Resources.Covers(definition.Cost))
        {
            reason = "The player cannot pay " + definition.Cost + ".";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tells whether the current player may build the given building on the
    /// given tile right now.
    /// </summary>
    /// <param name="coord">Where to build.</param>
    /// <param name="type">What to build.</param>
    /// <returns>True when <see cref="Build"/> would succeed.</returns>
    public bool CanBuild(HexCoord coord, HexBuildingType type)
    {
        string reason;
        return CanBuild(coord, type, out reason);
    }

    /// <summary>
    /// Tells whether a player meets the prerequisites of the Monument, ignoring
    /// its price and the tile it would stand on.
    /// </summary>
    /// <param name="playerIndex">Which player to check.</param>
    /// <param name="reason">Receives which prerequisite is missing, or an empty string.</param>
    /// <returns>True when the player owns the required Church and Cottages and has no Monument yet.</returns>
    public bool CanBuildMonument(int playerIndex, out string reason)
    {
        reason = string.Empty;

        if (_board.CountBuildings(playerIndex, HexBuildingType.Monument) > 0)
        {
            reason = "The player already owns a Monument.";
            return false;
        }

        if (_board.CountBuildings(playerIndex, HexBuildingType.Church) < _config.MonumentRequiredChurches)
        {
            reason = "A Monument needs " + _config.MonumentRequiredChurches + " Church first.";
            return false;
        }

        if (_board.CountBuildings(playerIndex, HexBuildingType.Cottage) < _config.MonumentRequiredCottages)
        {
            reason = "A Monument needs " + _config.MonumentRequiredCottages + " Cottages first.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds a building, charging its price to the current player.
    /// </summary>
    /// <param name="coord">Where to build.</param>
    /// <param name="type">What to build.</param>
    /// <exception cref="InvalidOperationException">Building is not allowed here.</exception>
    public void Build(HexCoord coord, HexBuildingType type)
    {
        string reason;
        if (!CanBuild(coord, type, out reason))
        {
            throw new InvalidOperationException(reason);
        }

        HexworldBuildingDefinition definition = _config.GetBuilding(type);
        HexTile tile = _board.GetTile(coord);
        HexworldPlayerState player = CurrentPlayer;

        AddResources(player, HexworldResources.Zero - definition.Cost);
        tile.Building = new HexBuilding(type);

        if (type == HexBuildingType.Monument)
        {
            player.MonumentBuiltOnTurn = player.TurnsTaken;
        }

        Action<HexworldBuildingBuiltEvent> handler = BuildingBuilt;
        if (handler != null)
        {
            handler(new HexworldBuildingBuiltEvent(player.Index, coord, type));
        }
    }

    /// <summary>
    /// Tells whether the current player may clear the rubble on a tile.
    /// </summary>
    /// <param name="coord">Which tile to clear.</param>
    /// <param name="reason">Receives why clearing is refused, or an empty string.</param>
    /// <returns>True when <see cref="ClearRubble"/> would succeed.</returns>
    public bool CanClearRubble(HexCoord coord, out string reason)
    {
        reason = string.Empty;

        if (CurrentPhase != HexworldPhase.Build)
        {
            reason = "Rubble can only be cleared during the build phase.";
            return false;
        }

        HexTile tile = _board.GetTile(coord);
        if (tile == null)
        {
            reason = "There is no tile at " + coord + ".";
            return false;
        }

        if (tile.IsVoided)
        {
            reason = "The tile has been dropped into the Ether.";
            return false;
        }

        if (tile.Owner != CurrentPlayerIndex)
        {
            reason = "The tile does not belong to the current player.";
            return false;
        }

        if (tile.Terrain != HexTerrainType.Rubble)
        {
            reason = "The tile carries no rubble.";
            return false;
        }

        if (!CurrentPlayer.Resources.Covers(_config.RubbleClearCost))
        {
            reason = "The player cannot pay " + _config.RubbleClearCost + ".";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tells whether the current player may clear the rubble on a tile.
    /// </summary>
    /// <param name="coord">Which tile to clear.</param>
    /// <returns>True when <see cref="ClearRubble"/> would succeed.</returns>
    public bool CanClearRubble(HexCoord coord)
    {
        string reason;
        return CanClearRubble(coord, out reason);
    }

    /// <summary>
    /// Clears the rubble on a tile. The tile turns into grass and the owner
    /// receives the one-off payout.
    /// </summary>
    /// <param name="coord">Which tile to clear.</param>
    /// <exception cref="InvalidOperationException">Clearing is not allowed here.</exception>
    public void ClearRubble(HexCoord coord)
    {
        string reason;
        if (!CanClearRubble(coord, out reason))
        {
            throw new InvalidOperationException(reason);
        }

        HexTile tile = _board.GetTile(coord);
        HexTerrainType previous = tile.Terrain;

        AddResources(CurrentPlayer, HexworldResources.Zero - _config.RubbleClearCost);
        tile.Terrain = _config.RubbleClearedTerrain;
        AddResources(CurrentPlayer, _config.RubbleClearReward);

        Action<HexworldTerrainChangedEvent> handler = TerrainChanged;
        if (handler != null)
        {
            handler(new HexworldTerrainChangedEvent(coord, previous, tile.Terrain));
        }
    }

    /// <summary>
    /// Closes the build phase and opens the combat phase.
    /// </summary>
    /// <exception cref="InvalidOperationException">The build phase is not running.</exception>
    public void EndBuildPhase()
    {
        if (CurrentPhase != HexworldPhase.Build)
        {
            throw new InvalidOperationException("The build phase is not running.");
        }

        SetPhase(HexworldPhase.Combat);
    }

    // ------------------------------------------------------------------
    // Combat phase
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns how many soldiers a capture of the given tile needs at least.
    /// </summary>
    /// <param name="coord">The tile to capture.</param>
    /// <returns>The lowest soldier count that may be spent on this tile.</returns>
    public int GetMinimumSoldiers(HexCoord coord)
    {
        HexTile tile = _board.GetTile(coord);
        if (tile != null && tile.Owner == HexworldConfig.NeutralOwner)
        {
            return _config.NeutralCaptureSoldierCost;
        }

        return 1;
    }

    /// <summary>
    /// Computes the defence strength of a tile before the defender die is
    /// added. A UI shows this number so the player can judge the odds.
    /// </summary>
    /// <param name="coord">The tile under attack.</param>
    /// <returns>Base defence plus the building bonus plus the barracks bonus.</returns>
    public int GetDefenseStrength(HexCoord coord)
    {
        HexTile tile = _board.GetTile(coord);
        if (tile == null || tile.Owner == HexworldConfig.NeutralOwner)
        {
            return 0;
        }

        int defense = _config.DefenseBase;
        if (tile.HasBuilding)
        {
            defense += _config.DefenseBuildingBonus;
        }

        foreach (HexTile neighbor in _board.GetNeighbors(coord))
        {
            if (neighbor.IsOwnedBy(tile.Owner)
                && neighbor.HasBuilding
                && neighbor.Building.Type == HexBuildingType.Barracks)
            {
                defense += _config.DefensePerAdjacentBarracks;
            }
        }

        return defense;
    }

    /// <summary>
    /// Tells whether the current player may capture the given tile with the
    /// given number of soldiers.
    /// </summary>
    /// <param name="coord">The tile to capture.</param>
    /// <param name="soldiers">How many soldiers to spend. Ignored for a neutral tile.</param>
    /// <param name="reason">Receives why the capture is refused, or an empty string.</param>
    /// <returns>True when <see cref="Capture"/> would succeed.</returns>
    public bool CanCapture(HexCoord coord, int soldiers, out string reason)
    {
        reason = string.Empty;

        if (CurrentPhase != HexworldPhase.Combat)
        {
            reason = "Tiles can only be captured during the combat phase.";
            return false;
        }

        if (CurrentPlayer.HasCapturedThisTurn)
        {
            reason = "Only one tile may be captured per turn.";
            return false;
        }

        HexTile tile = _board.GetTile(coord);
        if (tile == null)
        {
            reason = "There is no tile at " + coord + ".";
            return false;
        }

        if (tile.IsVoided)
        {
            reason = "The tile has been dropped into the Ether.";
            return false;
        }

        if (tile.Owner == CurrentPlayerIndex)
        {
            reason = "The tile already belongs to the current player.";
            return false;
        }

        if (!_board.IsAdjacentToPlayer(coord, CurrentPlayerIndex))
        {
            reason = "The tile does not touch any tile of the current player.";
            return false;
        }

        int required = tile.Owner == HexworldConfig.NeutralOwner
            ? _config.NeutralCaptureSoldierCost
            : soldiers;

        if (required < 1)
        {
            reason = "At least one soldier must be spent.";
            return false;
        }

        if (CurrentPlayer.Resources.Soldiers < required)
        {
            reason = "The player has fewer than " + required + " soldiers.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tells whether the current player may capture the given tile with the
    /// given number of soldiers.
    /// </summary>
    /// <param name="coord">The tile to capture.</param>
    /// <param name="soldiers">How many soldiers to spend. Ignored for a neutral tile.</param>
    /// <returns>True when <see cref="Capture"/> would succeed.</returns>
    public bool CanCapture(HexCoord coord, int soldiers)
    {
        string reason;
        return CanCapture(coord, soldiers, out reason);
    }

    /// <summary>
    /// Captures a tile. A neutral tile changes hands for a fixed soldier price
    /// and without a fight. An enemy tile is fought over: soldiers plus a die
    /// against defence plus a die, and the attacker must roll strictly higher.
    /// Soldiers are spent whether the attack wins or loses.
    /// </summary>
    /// <param name="coord">The tile to capture.</param>
    /// <param name="soldiers">How many soldiers to spend. Ignored for a neutral tile.</param>
    /// <returns>The record of the attempt.</returns>
    /// <exception cref="InvalidOperationException">The capture is not allowed.</exception>
    public HexworldCombatResult Capture(HexCoord coord, int soldiers)
    {
        string reason;
        if (!CanCapture(coord, soldiers, out reason))
        {
            throw new InvalidOperationException(reason);
        }

        HexTile tile = _board.GetTile(coord);
        HexworldPlayerState player = CurrentPlayer;
        int defender = tile.Owner;
        HexworldCombatResult result;

        if (defender == HexworldConfig.NeutralOwner)
        {
            int cost = _config.NeutralCaptureSoldierCost;
            AddResources(player, HexworldResources.FromSoldiers(-cost));
            ChangeOwner(tile, player.Index);
            result = new HexworldCombatResult(
                player.Index, defender, coord, cost, 0, 0, cost, 0, true, HexBuildingType.None);
        }
        else
        {
            AddResources(player, HexworldResources.FromSoldiers(-soldiers));

            int attackRoll = _random.RollD6();
            int defenseRoll = _random.RollD6();
            int attackPower = soldiers + attackRoll;
            int defensePower = GetDefenseStrength(coord) + defenseRoll;
            bool captured = attackPower > defensePower;
            HexBuildingType destroyed = HexBuildingType.None;

            if (captured)
            {
                if (tile.HasBuilding)
                {
                    destroyed = tile.Building.Type;
                    DestroyBuilding(tile, HexworldBuildingLossReason.Combat);
                }

                ChangeOwner(tile, player.Index);
            }

            result = new HexworldCombatResult(
                player.Index,
                defender,
                coord,
                soldiers,
                attackRoll,
                defenseRoll,
                attackPower,
                defensePower,
                captured,
                destroyed);
        }

        player.HasCapturedThisTurn = true;

        Action<HexworldCombatResult> handler = CombatResolved;
        if (handler != null)
        {
            handler(result);
        }

        return result;
    }

    /// <summary>
    /// Closes the combat phase, checks both victory conditions and, when the
    /// game continues, starts the turn of the other player.
    /// </summary>
    /// <exception cref="InvalidOperationException">The combat phase is not running.</exception>
    public void EndTurn()
    {
        if (CurrentPhase != HexworldPhase.Combat)
        {
            throw new InvalidOperationException("The combat phase is not running.");
        }

        if (CheckVictory())
        {
            return;
        }

        BeginTurn(Opponent(CurrentPlayerIndex));
    }

    // ------------------------------------------------------------------
    // Turn flow and victory
    // ------------------------------------------------------------------

    /// <summary>
    /// Starts the turn of a player: the skull counter resets, the capture flag
    /// clears and the harvest dice are rolled.
    /// </summary>
    /// <param name="playerIndex">Whose turn starts.</param>
    private void BeginTurn(int playerIndex)
    {
        CurrentPlayerIndex = playerIndex;
        TurnNumber++;

        HexworldPlayerState player = _players[playerIndex];
        player.TurnsTaken++;
        player.Skulls = 0;
        player.HasCapturedThisTurn = false;

        CurrentPhase = HexworldPhase.Harvest;
        RaisePhaseChanged();
        RollHarvestDice();
    }

    /// <summary>
    /// Checks both victory conditions at the end of a turn.
    /// </summary>
    /// <returns>True when the game has ended.</returns>
    private bool CheckVictory()
    {
        HexworldPlayerState player = CurrentPlayer;

        HexTile monument = _board.FindBuilding(player.Index, HexBuildingType.Monument);
        if (monument != null && player.HasBuiltMonument && player.TurnsTaken > player.MonumentBuiltOnTurn)
        {
            EndGame(player.Index, HexworldWinReason.Monument);
            return true;
        }

        int opponent = Opponent(player.Index);
        if (IsAnnihilated(opponent))
        {
            EndGame(player.Index, HexworldWinReason.Annihilation);
            return true;
        }

        if (IsAnnihilated(player.Index))
        {
            EndGame(opponent, HexworldWinReason.Annihilation);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tells whether a player has been wiped out: no living tiles left, or the
    /// Castle destroyed.
    /// </summary>
    /// <param name="playerIndex">Which player to check.</param>
    /// <returns>True when the player has lost.</returns>
    public bool IsAnnihilated(int playerIndex)
    {
        if (_board.GetOwnedTiles(playerIndex).Count == 0)
        {
            return true;
        }

        return _board.FindBuilding(playerIndex, HexBuildingType.Castle) == null;
    }

    /// <summary>
    /// Ends the game and announces the winner.
    /// </summary>
    /// <param name="winner">Player index of the winner.</param>
    /// <param name="reason">How the game was won.</param>
    private void EndGame(int winner, HexworldWinReason reason)
    {
        Winner = winner;
        WinReason = reason;
        CurrentPhase = HexworldPhase.GameOver;
        _dice.Clear();

        Action<HexworldPhaseChangedEvent> phaseHandler = PhaseChanged;
        if (phaseHandler != null)
        {
            phaseHandler(new HexworldPhaseChangedEvent(CurrentPlayerIndex, CurrentPhase, TurnNumber));
        }

        Action<HexworldGameOverEvent> handler = GameOver;
        if (handler != null)
        {
            handler(new HexworldGameOverEvent(winner, reason, TurnNumber));
        }
    }

    // ------------------------------------------------------------------
    // Board mutations that raise events
    // ------------------------------------------------------------------

    /// <summary>
    /// Adds a delta to the stockpile of a player and announces the change. The
    /// stockpile never drops below zero.
    /// </summary>
    /// <param name="player">Whose stockpile to change.</param>
    /// <param name="delta">How much to add. A spend is negative.</param>
    private void AddResources(HexworldPlayerState player, HexworldResources delta)
    {
        if (delta.IsZero)
        {
            return;
        }

        HexworldResources before = player.Resources;
        HexworldResources after = (before + delta).ClampedToZero();
        if (after == before)
        {
            return;
        }

        player.Resources = after;

        Action<HexworldResourcesChangedEvent> handler = ResourcesChanged;
        if (handler != null)
        {
            handler(new HexworldResourcesChangedEvent(player.Index, after, after - before));
        }
    }

    /// <summary>
    /// Removes the building from a tile and announces the loss.
    /// </summary>
    /// <param name="tile">The tile to clear.</param>
    /// <param name="reason">Why the building is lost.</param>
    private void DestroyBuilding(HexTile tile, HexworldBuildingLossReason reason)
    {
        if (!tile.HasBuilding)
        {
            return;
        }

        HexBuildingType type = tile.Building.Type;
        int owner = tile.Owner;
        tile.Building = null;

        Action<HexworldBuildingDestroyedEvent> handler = BuildingDestroyed;
        if (handler != null)
        {
            handler(new HexworldBuildingDestroyedEvent(owner, tile.Coord, type, reason));
        }
    }

    /// <summary>
    /// Hands a tile to another player and announces the change.
    /// </summary>
    /// <param name="tile">The tile that changes hands.</param>
    /// <param name="newOwner">The new owner.</param>
    private void ChangeOwner(HexTile tile, int newOwner)
    {
        int previous = tile.Owner;
        if (previous == newOwner)
        {
            return;
        }

        tile.Owner = newOwner;

        Action<HexworldTileOwnerChangedEvent> handler = TileOwnerChanged;
        if (handler != null)
        {
            handler(new HexworldTileOwnerChangedEvent(tile.Coord, previous, newOwner));
        }
    }

    /// <summary>
    /// Drops a tile into the Ether. Any building on it dies with the tile.
    /// </summary>
    /// <param name="tile">The tile to drop.</param>
    private void VoidTile(HexTile tile)
    {
        if (tile.IsVoided)
        {
            return;
        }

        if (tile.HasBuilding)
        {
            DestroyBuilding(tile, HexworldBuildingLossReason.TileVoided);
        }

        int owner = tile.Owner;
        tile.IsVoided = true;

        Action<HexworldTileVoidedEvent> handler = TileVoided;
        if (handler != null)
        {
            handler(new HexworldTileVoidedEvent(tile.Coord, owner));
        }
    }

    // ------------------------------------------------------------------
    // Event helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Switches to a phase and announces it.
    /// </summary>
    /// <param name="phase">The phase that starts.</param>
    private void SetPhase(HexworldPhase phase)
    {
        CurrentPhase = phase;
        RaisePhaseChanged();
    }

    /// <summary>
    /// Announces the running phase.
    /// </summary>
    private void RaisePhaseChanged()
    {
        Action<HexworldPhaseChangedEvent> handler = PhaseChanged;
        if (handler != null)
        {
            handler(new HexworldPhaseChangedEvent(CurrentPlayerIndex, CurrentPhase, TurnNumber));
        }
    }

    /// <summary>
    /// Announces the current dice.
    /// </summary>
    /// <param name="isReroll">True when the announcement follows a reroll.</param>
    private void RaiseDiceRolled(bool isReroll)
    {
        Action<HexworldDiceRolledEvent> handler = DiceRolled;
        if (handler != null)
        {
            handler(new HexworldDiceRolledEvent(CurrentPlayerIndex, _dice, isReroll, RerollsRemaining));
        }
    }
}
