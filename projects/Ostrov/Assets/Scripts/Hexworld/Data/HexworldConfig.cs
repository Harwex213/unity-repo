using System.Collections.Generic;

/// <summary>
/// Every balance number of the game in one place: island size, starting
/// resources, building prices, die faces, upkeep, disaster thresholds and
/// combat modifiers. The logic reads this object and never hardcodes a number,
/// so a designer changes balance here alone.
/// </summary>
/// <remarks>
/// Call <see cref="CreateDefault"/> for the shipped balance. Every property has
/// a setter, so a test may take the default and change a single number.
/// </remarks>
public sealed class HexworldConfig
{
    /// <summary>How many players take part. The game is built for two.</summary>
    public const int PlayerCount = 2;

    /// <summary>Owner index that marks a tile nobody owns.</summary>
    public const int NeutralOwner = -1;

    /// <summary>Building definitions by kind.</summary>
    private Dictionary<HexBuildingType, HexworldBuildingDefinition> _buildings
        = new Dictionary<HexBuildingType, HexworldBuildingDefinition>();

    /// <summary>Radius of the island in hexagon steps. Radius 3 means 37 tiles.</summary>
    public int IslandRadius { get; set; }

    /// <summary>What each player owns when the game starts.</summary>
    public HexworldResources StartingResources { get; set; }

    /// <summary>Where the Castle of each player stands at the start, indexed by player.</summary>
    public HexCoord[] CastleCoords { get; set; }

    /// <summary>How many times a player may reroll during one harvest.</summary>
    public int RerollsPerHarvest { get; set; }

    /// <summary>Food eaten by each upkeep-paying building every harvest.</summary>
    public int UpkeepFoodPerBuilding { get; set; }

    /// <summary>Skull count that destroys one random building of the player.</summary>
    public int SkullsToDestroyBuilding { get; set; }

    /// <summary>Skull count that additionally drops one random border tile into the Ether.</summary>
    public int SkullsToVoidTile { get; set; }

    /// <summary>Price of clearing one rubble tile.</summary>
    public HexworldResources RubbleClearCost { get; set; }

    /// <summary>One-off payout for clearing one rubble tile.</summary>
    public HexworldResources RubbleClearReward { get; set; }

    /// <summary>Terrain a cleared rubble tile turns into.</summary>
    public HexTerrainType RubbleClearedTerrain { get; set; }

    /// <summary>Per-turn bonus of a building standing on forest.</summary>
    public HexworldResources ForestBonus { get; set; }

    /// <summary>Per-turn bonus of a building standing on stone.</summary>
    public HexworldResources StoneBonus { get; set; }

    /// <summary>Soldiers spent to take a neutral tile. No roll is made.</summary>
    public int NeutralCaptureSoldierCost { get; set; }

    /// <summary>Defence every enemy tile has by itself.</summary>
    public int DefenseBase { get; set; }

    /// <summary>Extra defence of an enemy tile that carries a building.</summary>
    public int DefenseBuildingBonus { get; set; }

    /// <summary>Extra defence per adjacent enemy tile that carries a Barracks.</summary>
    public int DefensePerAdjacentBarracks { get; set; }

    /// <summary>How many Churches a player needs before a Monument may be built.</summary>
    public int MonumentRequiredChurches { get; set; }

    /// <summary>How many Cottages a player needs before a Monument may be built.</summary>
    public int MonumentRequiredCottages { get; set; }

    /// <summary>Terrain shares used when the island is generated, in percent.</summary>
    /// <remarks>Order is Grass, Forest, Stone, Rubble. The four values must sum to 100.</remarks>
    public int[] TerrainWeightsPercent { get; set; }

    /// <summary>All building definitions, keyed by building kind.</summary>
    public IReadOnlyDictionary<HexBuildingType, HexworldBuildingDefinition> Buildings
    {
        get { return _buildings; }
    }

    /// <summary>
    /// Looks a building definition up.
    /// </summary>
    /// <param name="type">Which building to look up.</param>
    /// <returns>The definition, or null when the kind has none.</returns>
    public HexworldBuildingDefinition GetBuilding(HexBuildingType type)
    {
        HexworldBuildingDefinition definition;
        return _buildings.TryGetValue(type, out definition) ? definition : null;
    }

    /// <summary>
    /// Replaces or adds one building definition.
    /// </summary>
    /// <param name="definition">The definition to store.</param>
    public void SetBuilding(HexworldBuildingDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        _buildings[definition.Type] = definition;
    }

    /// <summary>
    /// Returns the per-turn bonus a building gets from the terrain under it.
    /// </summary>
    /// <param name="terrain">Terrain of the tile.</param>
    /// <returns>The bonus, which is empty for grass and rubble.</returns>
    public HexworldResources GetTerrainBonus(HexTerrainType terrain)
    {
        switch (terrain)
        {
            case HexTerrainType.Forest: return ForestBonus;
            case HexTerrainType.Stone: return StoneBonus;
            default: return HexworldResources.Zero;
        }
    }

    /// <summary>
    /// Builds the shipped balance: a radius 3 island, two Castles facing each
    /// other and the seven building kinds with their dice.
    /// </summary>
    /// <returns>A fresh config that the caller may still edit.</returns>
    public static HexworldConfig CreateDefault()
    {
        var config = new HexworldConfig
        {
            IslandRadius = 3,
            StartingResources = new HexworldResources(5, 5, 3, 0, 2),
            CastleCoords = new[] { new HexCoord(-2, 0), new HexCoord(2, 0) },
            RerollsPerHarvest = 2,
            UpkeepFoodPerBuilding = 1,
            SkullsToDestroyBuilding = 3,
            SkullsToVoidTile = 5,
            RubbleClearCost = HexworldResources.FromWood(2),
            RubbleClearReward = HexworldResources.FromStone(1),
            RubbleClearedTerrain = HexTerrainType.Grass,
            ForestBonus = HexworldResources.FromWood(1),
            StoneBonus = HexworldResources.FromStone(1),
            NeutralCaptureSoldierCost = 1,
            DefenseBase = 1,
            DefenseBuildingBonus = 2,
            DefensePerAdjacentBarracks = 1,
            MonumentRequiredChurches = 1,
            MonumentRequiredCottages = 2,
            TerrainWeightsPercent = new[] { 40, 20, 20, 20 },
        };

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Castle,
            HexworldResources.Zero,
            HexworldResources.Zero,
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromFood(1)),
                HexworldDiceFace.Pays(HexworldResources.FromWood(1)),
                HexworldDiceFace.Pays(HexworldResources.FromStone(1)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(1)),
                HexworldDiceFace.Pays(HexworldResources.FromSoldiers(1)),
                HexworldDiceFace.Pays(new HexworldResources(1, 1, 0, 0, 0)),
            },
            false,
            false,
            false));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Cottage,
            HexworldResources.FromWood(3),
            HexworldResources.Zero,
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromFood(1)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(2)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(2)),
                HexworldDiceFace.Pays(new HexworldResources(1, 1, 0, 0, 0)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(1)),
                HexworldDiceFace.Skull(),
            },
            true,
            true,
            true));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Farm,
            new HexworldResources(0, 2, 1, 0, 0),
            HexworldResources.FromFood(2),
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromFood(1)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(2)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(2)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(3)),
                HexworldDiceFace.Pays(new HexworldResources(1, 1, 0, 0, 0)),
                HexworldDiceFace.Pays(HexworldResources.FromFood(2)),
            },
            true,
            true,
            true));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Quarry,
            new HexworldResources(0, 3, 2, 0, 0),
            HexworldResources.Zero,
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromStone(1)),
                HexworldDiceFace.Pays(HexworldResources.FromStone(2)),
                HexworldDiceFace.Pays(HexworldResources.FromStone(2)),
                HexworldDiceFace.Pays(HexworldResources.FromStone(3)),
                HexworldDiceFace.Pays(new HexworldResources(0, 1, 1, 0, 0)),
                HexworldDiceFace.Skull(),
            },
            true,
            true,
            true));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Church,
            new HexworldResources(0, 4, 3, 0, 0),
            HexworldResources.Zero,
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromCulture(1)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(1)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(2)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(2)),
                HexworldDiceFace.Pays(HexworldResources.FromCulture(3)),
                HexworldDiceFace.Pays(new HexworldResources(1, 0, 0, 1, 0)),
            },
            true,
            true,
            true));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Barracks,
            new HexworldResources(0, 3, 3, 0, 0),
            HexworldResources.Zero,
            new[]
            {
                HexworldDiceFace.Pays(HexworldResources.FromSoldiers(1)),
                HexworldDiceFace.Pays(HexworldResources.FromSoldiers(1)),
                HexworldDiceFace.Pays(HexworldResources.FromSoldiers(2)),
                HexworldDiceFace.Pays(HexworldResources.FromSoldiers(2)),
                HexworldDiceFace.Pays(new HexworldResources(1, 0, 0, 0, 1)),
                HexworldDiceFace.Skull(),
            },
            true,
            true,
            true));

        config.SetBuilding(new HexworldBuildingDefinition(
            HexBuildingType.Monument,
            new HexworldResources(0, 10, 15, 20, 0),
            HexworldResources.Zero,
            null,
            true,
            false,
            true));

        return config;
    }
}
