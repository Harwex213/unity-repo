using System;
using NUnit.Framework;

/// <summary>
/// Tests of the build phase: prices, forbidden tiles, the Monument
/// prerequisites and clearing rubble.
/// </summary>
public class HexworldBuildTests
{
    /// <summary>An empty tile of player 0, one step from the Castle.</summary>
    private static readonly HexCoord FreeTile = new HexCoord(-1, 0);

    /// <summary>A second empty tile of player 0.</summary>
    private static readonly HexCoord SecondFreeTile = new HexCoord(-1, -1);

    /// <summary>A third empty tile of player 0.</summary>
    private static readonly HexCoord ThirdFreeTile = new HexCoord(-2, 1);

    /// <summary>A tile of player 1.</summary>
    private static readonly HexCoord EnemyTile = new HexCoord(1, 0);

    /// <summary>A tile nobody owns.</summary>
    private static readonly HexCoord NeutralTile = new HexCoord(0, 0);

    /// <summary>Building a cottage charges its price and puts it on the tile.</summary>
    [Test]
    public void Build_ChargesThePriceAndPlacesTheBuilding()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 5, 0, 0, 0));

        HexworldBuildingBuiltEvent reported = default(HexworldBuildingBuiltEvent);
        int events = 0;
        game.BuildingBuilt += e =>
        {
            reported = e;
            events++;
        };

        Assert.IsTrue(game.CanBuild(FreeTile, HexBuildingType.Cottage));
        game.Build(FreeTile, HexBuildingType.Cottage);

        Assert.AreEqual(2, game.GetPlayer(0).Resources.Wood);
        Assert.AreEqual(HexBuildingType.Cottage, game.Board.GetTile(FreeTile).Building.Type);
        Assert.AreEqual(1, events);
        Assert.AreEqual(FreeTile, reported.Coord);
        Assert.AreEqual(0, reported.PlayerIndex);
    }

    /// <summary>A player who cannot pay cannot build.</summary>
    [Test]
    public void Build_IsRefused_WhenThePriceIsTooHigh()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 2, 0, 0, 0));

        string reason;
        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Cottage, out reason));
        Assert.IsNotEmpty(reason);
        Assert.Throws<InvalidOperationException>(() => game.Build(FreeTile, HexBuildingType.Cottage));
    }

    /// <summary>Rubble must be cleared before anything is built on it.</summary>
    [Test]
    public void Build_IsRefused_OnRubble()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 20, 20, 0, 0));
        game.Board.GetTile(FreeTile).Terrain = HexTerrainType.Rubble;

        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Cottage));
        Assert.IsTrue(game.CanBuild(SecondFreeTile, HexBuildingType.Cottage));
    }

    /// <summary>A tile that is not yours cannot be built on.</summary>
    [Test]
    public void Build_IsRefused_OnForeignTiles()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 20, 20, 0, 0));

        Assert.IsFalse(game.CanBuild(EnemyTile, HexBuildingType.Cottage));
        Assert.IsFalse(game.CanBuild(NeutralTile, HexBuildingType.Cottage));
    }

    /// <summary>One tile carries one building.</summary>
    [Test]
    public void Build_IsRefused_OnAnOccupiedTile()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 20, 20, 0, 0));
        game.Build(FreeTile, HexBuildingType.Cottage);

        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Cottage));
        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Farm));
    }

    /// <summary>The Castle is a starting building and can never be built.</summary>
    [Test]
    public void Build_IsRefused_ForTheCastle()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(99, 99, 99, 99, 99));

        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Castle));
    }

    /// <summary>Buildings can only be raised during the build phase.</summary>
    [Test]
    public void Build_IsRefused_OutsideTheBuildPhase()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        game.Start();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 20, 20, 0, 0));

        Assert.AreEqual(HexworldPhase.Harvest, game.CurrentPhase);
        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Cottage));
    }

    /// <summary>A Monument needs a Church and two Cottages first.</summary>
    [Test]
    public void Monument_NeedsAChurchAndTwoCottages()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(99, 99, 99, 99, 99));

        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Monument), "No Church and no Cottages yet.");

        HexworldTestFactory.PlaceBuilding(game, SecondFreeTile, HexBuildingType.Church, 0);
        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Monument), "Cottages are still missing.");

        HexworldTestFactory.PlaceBuilding(game, ThirdFreeTile, HexBuildingType.Cottage, 0);
        Assert.IsFalse(game.CanBuild(FreeTile, HexBuildingType.Monument), "One Cottage is still missing.");

        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 0), HexBuildingType.Cottage, 0);
        Assert.IsTrue(game.CanBuild(FreeTile, HexBuildingType.Monument));
    }

    /// <summary>The Monument charges ten wood, fifteen stone and twenty culture.</summary>
    [Test]
    public void Monument_ChargesItsFullPrice()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 12, 16, 25, 0));
        HexworldTestFactory.PlaceBuilding(game, SecondFreeTile, HexBuildingType.Church, 0);
        HexworldTestFactory.PlaceBuilding(game, ThirdFreeTile, HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 0), HexBuildingType.Cottage, 0);

        game.Build(FreeTile, HexBuildingType.Monument);

        Assert.AreEqual(2, game.GetPlayer(0).Resources.Wood);
        Assert.AreEqual(1, game.GetPlayer(0).Resources.Stone);
        Assert.AreEqual(5, game.GetPlayer(0).Resources.Culture);
        Assert.AreEqual(1, game.GetPlayer(0).MonumentBuiltOnTurn);
    }

    /// <summary>A player builds at most one Monument.</summary>
    [Test]
    public void Monument_CannotBeBuiltTwice()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 99, 99, 99, 0));
        HexworldTestFactory.PlaceBuilding(game, SecondFreeTile, HexBuildingType.Church, 0);
        HexworldTestFactory.PlaceBuilding(game, ThirdFreeTile, HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 0), HexBuildingType.Cottage, 0);

        game.Build(FreeTile, HexBuildingType.Monument);

        Assert.IsFalse(game.CanBuild(new HexCoord(-3, 1), HexBuildingType.Monument));
    }

    /// <summary>Clearing rubble charges wood, turns the tile to grass and pays one stone.</summary>
    [Test]
    public void ClearRubble_TurnsTheTileToGrassAndPaysOneStone()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 5, 0, 0, 0));
        game.Board.GetTile(FreeTile).Terrain = HexTerrainType.Rubble;

        HexworldTerrainChangedEvent reported = default(HexworldTerrainChangedEvent);
        game.TerrainChanged += e => reported = e;

        Assert.IsTrue(game.CanClearRubble(FreeTile));
        game.ClearRubble(FreeTile);

        Assert.AreEqual(HexTerrainType.Grass, game.Board.GetTile(FreeTile).Terrain);
        Assert.AreEqual(3, game.GetPlayer(0).Resources.Wood);
        Assert.AreEqual(1, game.GetPlayer(0).Resources.Stone);
        Assert.AreEqual(FreeTile, reported.Coord);
        Assert.AreEqual(HexTerrainType.Rubble, reported.PreviousTerrain);
        Assert.IsTrue(game.CanBuild(FreeTile, HexBuildingType.Cottage));
    }

    /// <summary>A tile without rubble cannot be cleared, and neither can a foreign one.</summary>
    [Test]
    public void ClearRubble_IsRefused_WhenThereIsNoRubbleOrTheTileIsForeign()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 5, 0, 0, 0));

        Assert.IsFalse(game.CanClearRubble(FreeTile), "The tile is grass.");

        game.Board.GetTile(EnemyTile).Terrain = HexTerrainType.Rubble;
        Assert.IsFalse(game.CanClearRubble(EnemyTile), "The tile belongs to the other player.");

        Assert.Throws<InvalidOperationException>(() => game.ClearRubble(FreeTile));
    }

    /// <summary>Clearing rubble is refused when the player cannot pay for it.</summary>
    [Test]
    public void ClearRubble_IsRefused_WhenTheWoodIsShort()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 1, 0, 0, 0));
        game.Board.GetTile(FreeTile).Terrain = HexTerrainType.Rubble;

        Assert.IsFalse(game.CanClearRubble(FreeTile));
    }

    /// <summary>
    /// Builds a game where player 0 owns only the Castle and the turn already
    /// stands in the build phase.
    /// </summary>
    /// <returns>A started game in the build phase of player 0.</returns>
    private static HexworldGame CreateGameInBuildPhase()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        game.Start();
        game.AdvancePhase();

        Assert.AreEqual(HexworldPhase.Build, game.CurrentPhase);
        return game;
    }
}
