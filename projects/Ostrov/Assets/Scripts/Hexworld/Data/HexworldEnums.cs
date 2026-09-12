/// <summary>
/// Terrain of a single island tile. Terrain never changes on its own; the only
/// runtime transition is <see cref="Rubble"/> becoming <see cref="Grass"/> after
/// the owner clears it during the build phase.
/// </summary>
public enum HexTerrainType
{
    /// <summary>Plain buildable ground. Gives no per-turn bonus.</summary>
    Grass = 0,

    /// <summary>Wooded ground. A building standing here yields +1 Wood every turn.</summary>
    Forest = 1,

    /// <summary>Rocky ground. A building standing here yields +1 Stone every turn.</summary>
    Stone = 2,

    /// <summary>Ruined ground. Nothing can be built here until the owner clears it.</summary>
    Rubble = 3,
}

/// <summary>
/// Every building kind in the game. <see cref="None"/> marks an empty tile and is
/// never stored as an actual building instance.
/// </summary>
public enum HexBuildingType
{
    /// <summary>No building. Used as the "nothing here" value.</summary>
    None = 0,

    /// <summary>Starting building of a player. It cannot be built and cannot be destroyed by skulls.</summary>
    Castle = 1,

    /// <summary>Cheap food producer.</summary>
    Cottage = 2,

    /// <summary>Food producer with a guaranteed flat income on top of its die.</summary>
    Farm = 3,

    /// <summary>Stone producer.</summary>
    Quarry = 4,

    /// <summary>Culture producer. Required before a Monument may be built.</summary>
    Church = 5,

    /// <summary>Soldier producer. Also strengthens the defence of adjacent friendly tiles.</summary>
    Barracks = 6,

    /// <summary>Victory building. It rolls no die and wins the game once it survives a full round.</summary>
    Monument = 7,
}

/// <summary>
/// Phases of a single player turn. A turn always runs Harvest, then Build, then
/// Combat, after which the turn passes to the other player.
/// </summary>
public enum HexworldPhase
{
    /// <summary>Dice of all active buildings are rolled, rerolled, then applied.</summary>
    Harvest = 0,

    /// <summary>The player builds buildings and clears rubble.</summary>
    Build = 1,

    /// <summary>The player may capture one tile.</summary>
    Combat = 2,

    /// <summary>The game has ended; no further actions are accepted.</summary>
    GameOver = 3,
}

/// <summary>
/// Why the game ended.
/// </summary>
public enum HexworldWinReason
{
    /// <summary>The game is still running.</summary>
    None = 0,

    /// <summary>The winner built a Monument and kept it for a full round.</summary>
    Monument = 1,

    /// <summary>The loser lost every tile, or lost the Castle.</summary>
    Annihilation = 2,
}

/// <summary>
/// The five resource kinds tracked per player.
/// </summary>
public enum HexworldResourceType
{
    /// <summary>Pays the per-turn upkeep of every building except the Castle.</summary>
    Food = 0,

    /// <summary>Main building material. Also pays for clearing rubble.</summary>
    Wood = 1,

    /// <summary>Secondary building material.</summary>
    Stone = 2,

    /// <summary>Pays for the Monument.</summary>
    Culture = 3,

    /// <summary>Spent to capture tiles.</summary>
    Soldiers = 4,
}
