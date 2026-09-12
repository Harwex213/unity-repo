using System.Collections.Generic;

/// <summary>
/// Why a building left the board.
/// </summary>
public enum HexworldBuildingLossReason
{
    /// <summary>A skull disaster destroyed it.</summary>
    Disaster = 0,

    /// <summary>An enemy captured the tile under it.</summary>
    Combat = 1,

    /// <summary>The tile under it dropped into the Ether.</summary>
    TileVoided = 2,
}

/// <summary>
/// Says that the stockpile of one player changed.
/// </summary>
public readonly struct HexworldResourcesChangedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Whose stockpile changed.</param>
    /// <param name="resources">The stockpile after the change.</param>
    /// <param name="delta">How much was added; a spend is negative.</param>
    public HexworldResourcesChangedEvent(int playerIndex, HexworldResources resources, HexworldResources delta)
    {
        PlayerIndex = playerIndex;
        Resources = resources;
        Delta = delta;
    }

    /// <summary>Whose stockpile changed.</summary>
    public int PlayerIndex { get; }

    /// <summary>The stockpile after the change.</summary>
    public HexworldResources Resources { get; }

    /// <summary>How much was added. A spend shows up as negative amounts.</summary>
    public HexworldResources Delta { get; }
}

/// <summary>
/// Says that a building appeared on a tile.
/// </summary>
public readonly struct HexworldBuildingBuiltEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Who built it.</param>
    /// <param name="coord">Where it stands.</param>
    /// <param name="buildingType">What was built.</param>
    public HexworldBuildingBuiltEvent(int playerIndex, HexCoord coord, HexBuildingType buildingType)
    {
        PlayerIndex = playerIndex;
        Coord = coord;
        BuildingType = buildingType;
    }

    /// <summary>Who built it.</summary>
    public int PlayerIndex { get; }

    /// <summary>Where it stands.</summary>
    public HexCoord Coord { get; }

    /// <summary>What was built.</summary>
    public HexBuildingType BuildingType { get; }
}

/// <summary>
/// Says that a building left the board.
/// </summary>
public readonly struct HexworldBuildingDestroyedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Who owned it.</param>
    /// <param name="coord">Where it stood.</param>
    /// <param name="buildingType">What was destroyed.</param>
    /// <param name="reason">Why it was destroyed.</param>
    public HexworldBuildingDestroyedEvent(
        int playerIndex, HexCoord coord, HexBuildingType buildingType, HexworldBuildingLossReason reason)
    {
        PlayerIndex = playerIndex;
        Coord = coord;
        BuildingType = buildingType;
        Reason = reason;
    }

    /// <summary>Who owned it.</summary>
    public int PlayerIndex { get; }

    /// <summary>Where it stood.</summary>
    public HexCoord Coord { get; }

    /// <summary>What was destroyed.</summary>
    public HexBuildingType BuildingType { get; }

    /// <summary>Why it was destroyed.</summary>
    public HexworldBuildingLossReason Reason { get; }
}

/// <summary>
/// Says that a tile changed hands.
/// </summary>
public readonly struct HexworldTileOwnerChangedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="coord">Which tile changed hands.</param>
    /// <param name="previousOwner">Who owned it before, or -1 for nobody.</param>
    /// <param name="newOwner">Who owns it now, or -1 for nobody.</param>
    public HexworldTileOwnerChangedEvent(HexCoord coord, int previousOwner, int newOwner)
    {
        Coord = coord;
        PreviousOwner = previousOwner;
        NewOwner = newOwner;
    }

    /// <summary>Which tile changed hands.</summary>
    public HexCoord Coord { get; }

    /// <summary>Who owned it before, or -1 for nobody.</summary>
    public int PreviousOwner { get; }

    /// <summary>Who owns it now, or -1 for nobody.</summary>
    public int NewOwner { get; }
}

/// <summary>
/// Says that a tile dropped into the Ether and left the game for good.
/// </summary>
public readonly struct HexworldTileVoidedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="coord">Which tile was dropped.</param>
    /// <param name="ownerIndex">Who owned it, or -1 for nobody.</param>
    public HexworldTileVoidedEvent(HexCoord coord, int ownerIndex)
    {
        Coord = coord;
        OwnerIndex = ownerIndex;
    }

    /// <summary>Which tile was dropped.</summary>
    public HexCoord Coord { get; }

    /// <summary>Who owned it, or -1 for nobody.</summary>
    public int OwnerIndex { get; }
}

/// <summary>
/// Says that the terrain of a tile changed. Clearing rubble is the only cause.
/// </summary>
public readonly struct HexworldTerrainChangedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="coord">Which tile changed.</param>
    /// <param name="previousTerrain">What it was.</param>
    /// <param name="newTerrain">What it is now.</param>
    public HexworldTerrainChangedEvent(HexCoord coord, HexTerrainType previousTerrain, HexTerrainType newTerrain)
    {
        Coord = coord;
        PreviousTerrain = previousTerrain;
        NewTerrain = newTerrain;
    }

    /// <summary>Which tile changed.</summary>
    public HexCoord Coord { get; }

    /// <summary>What it was.</summary>
    public HexTerrainType PreviousTerrain { get; }

    /// <summary>What it is now.</summary>
    public HexTerrainType NewTerrain { get; }
}

/// <summary>
/// Says that the harvest dice were rolled or rerolled.
/// </summary>
public readonly struct HexworldDiceRolledEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Who rolled.</param>
    /// <param name="dice">The dice of the running harvest with their current faces.</param>
    /// <param name="isReroll">True when this was a reroll rather than the opening roll.</param>
    /// <param name="rerollsRemaining">How many rerolls are still available.</param>
    public HexworldDiceRolledEvent(
        int playerIndex, IReadOnlyList<HexworldDie> dice, bool isReroll, int rerollsRemaining)
    {
        PlayerIndex = playerIndex;
        Dice = dice;
        IsReroll = isReroll;
        RerollsRemaining = rerollsRemaining;
    }

    /// <summary>Who rolled.</summary>
    public int PlayerIndex { get; }

    /// <summary>The dice of the running harvest with their current faces.</summary>
    public IReadOnlyList<HexworldDie> Dice { get; }

    /// <summary>True when this was a reroll rather than the opening roll.</summary>
    public bool IsReroll { get; }

    /// <summary>How many rerolls are still available.</summary>
    public int RerollsRemaining { get; }
}

/// <summary>
/// Says that the turn moved to another phase, or to another player.
/// </summary>
public readonly struct HexworldPhaseChangedEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Whose turn it is now.</param>
    /// <param name="phase">Which phase started.</param>
    /// <param name="turnNumber">How many turns have started in total.</param>
    public HexworldPhaseChangedEvent(int playerIndex, HexworldPhase phase, int turnNumber)
    {
        PlayerIndex = playerIndex;
        Phase = phase;
        TurnNumber = turnNumber;
    }

    /// <summary>Whose turn it is now.</summary>
    public int PlayerIndex { get; }

    /// <summary>Which phase started.</summary>
    public HexworldPhase Phase { get; }

    /// <summary>How many turns have started in total, counting both players.</summary>
    public int TurnNumber { get; }
}

/// <summary>
/// Says that a skull disaster struck.
/// </summary>
public readonly struct HexworldDisasterEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="playerIndex">Who suffered the disaster.</param>
    /// <param name="skulls">How many skulls were rolled.</param>
    /// <param name="destroyedBuilding">True when a building was destroyed.</param>
    /// <param name="voidedTile">True when a border tile dropped into the Ether.</param>
    public HexworldDisasterEvent(int playerIndex, int skulls, bool destroyedBuilding, bool voidedTile)
    {
        PlayerIndex = playerIndex;
        Skulls = skulls;
        DestroyedBuilding = destroyedBuilding;
        VoidedTile = voidedTile;
    }

    /// <summary>Who suffered the disaster.</summary>
    public int PlayerIndex { get; }

    /// <summary>How many skulls were rolled during the harvest.</summary>
    public int Skulls { get; }

    /// <summary>True when a building was destroyed.</summary>
    public bool DestroyedBuilding { get; }

    /// <summary>True when a border tile dropped into the Ether.</summary>
    public bool VoidedTile { get; }
}

/// <summary>
/// Says that the game ended.
/// </summary>
public readonly struct HexworldGameOverEvent
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="winner">Player index of the winner.</param>
    /// <param name="reason">How the game was won.</param>
    /// <param name="turnNumber">On which turn the game ended.</param>
    public HexworldGameOverEvent(int winner, HexworldWinReason reason, int turnNumber)
    {
        Winner = winner;
        Reason = reason;
        TurnNumber = turnNumber;
    }

    /// <summary>Player index of the winner.</summary>
    public int Winner { get; }

    /// <summary>How the game was won.</summary>
    public HexworldWinReason Reason { get; }

    /// <summary>On which turn the game ended.</summary>
    public int TurnNumber { get; }
}
