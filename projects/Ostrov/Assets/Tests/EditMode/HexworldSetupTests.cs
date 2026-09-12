using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Tests of the starting position: island size, Castle placement, tile
/// ownership, starting stockpiles and the determinism of the generator.
/// </summary>
public class HexworldSetupTests
{
    /// <summary>The island is a hexagonal block of 37 tiles.</summary>
    [Test]
    public void Board_Holds37Tiles()
    {
        var game = new HexworldGame(12345);

        Assert.AreEqual(37, game.Board.TileCount);
        Assert.AreEqual(3, game.Board.Radius);
    }

    /// <summary>Both Castles stand on their configured tiles and belong to their players.</summary>
    [Test]
    public void Castles_StandOnTheirConfiguredTiles()
    {
        var game = new HexworldGame(12345);

        HexTile first = game.Board.GetTile(new HexCoord(-2, 0));
        HexTile second = game.Board.GetTile(new HexCoord(2, 0));

        Assert.AreEqual(0, first.Owner);
        Assert.AreEqual(HexBuildingType.Castle, first.Building.Type);
        Assert.AreEqual(1, second.Owner);
        Assert.AreEqual(HexBuildingType.Castle, second.Building.Type);
    }

    /// <summary>Each player starts with the Castle tile and its six neighbours.</summary>
    [Test]
    public void EachPlayer_StartsWithSevenTiles()
    {
        var game = new HexworldGame(12345);

        Assert.AreEqual(7, game.Board.GetOwnedTiles(0).Count);
        Assert.AreEqual(7, game.Board.GetOwnedTiles(1).Count);

        foreach (HexCoord neighbor in new HexCoord(-2, 0).GetNeighbors())
        {
            HexTile tile = game.Board.GetTile(neighbor);
            Assert.AreEqual(0, tile.Owner, neighbor + " should belong to player 0.");
            Assert.IsFalse(tile.HasBuilding, neighbor + " should be empty.");
            Assert.AreEqual(HexTerrainType.Grass, tile.Terrain, neighbor + " should be grass.");
        }
    }

    /// <summary>The remaining tiles belong to nobody.</summary>
    [Test]
    public void RemainingTiles_AreNeutral()
    {
        var game = new HexworldGame(12345);

        int neutral = 0;
        IReadOnlyList<HexTile> tiles = game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Owner == HexworldConfig.NeutralOwner)
            {
                neutral++;
            }
        }

        Assert.AreEqual(37 - 14, neutral);
    }

    /// <summary>Both players open with the configured stockpile.</summary>
    [Test]
    public void Players_StartWithTheConfiguredResources()
    {
        var game = new HexworldGame(12345);
        var expected = new HexworldResources(5, 5, 3, 0, 2);

        Assert.AreEqual(expected, game.GetPlayer(0).Resources);
        Assert.AreEqual(expected, game.GetPlayer(1).Resources);
    }

    /// <summary>The first turn belongs to player 0 and opens in the harvest phase.</summary>
    [Test]
    public void FirstTurn_BelongsToPlayerZeroAndStartsWithHarvest()
    {
        var game = new HexworldGame(12345);

        Assert.AreEqual(0, game.CurrentPlayerIndex);
        Assert.AreEqual(HexworldPhase.Harvest, game.CurrentPhase);
        Assert.AreEqual(1, game.TurnNumber);
        Assert.IsFalse(game.IsGameOver);
    }

    /// <summary>The same seed always produces the same island.</summary>
    [Test]
    public void SameSeed_ProducesTheSameIsland()
    {
        var first = new HexworldGame(4242);
        var second = new HexworldGame(4242);

        for (int i = 0; i < first.Board.TileCount; i++)
        {
            Assert.AreEqual(
                first.Board.Tiles[i].Terrain,
                second.Board.Tiles[i].Terrain,
                "Terrain differs at " + first.Board.Tiles[i].Coord + ".");
        }
    }

    /// <summary>Different seeds produce different islands.</summary>
    [Test]
    public void DifferentSeeds_ProduceDifferentIslands()
    {
        var first = new HexworldGame(1);
        var second = new HexworldGame(99999);

        bool anyDifference = false;
        for (int i = 0; i < first.Board.TileCount; i++)
        {
            if (first.Board.Tiles[i].Terrain != second.Board.Tiles[i].Terrain)
            {
                anyDifference = true;
                break;
            }
        }

        Assert.IsTrue(anyDifference, "Two different seeds produced the same island.");
    }

    /// <summary>A view may subscribe before the opening roll when the game starts on demand.</summary>
    [Test]
    public void DeferredStart_RaisesTheOpeningEventsAfterSubscription()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        var random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);

        int phaseEvents = 0;
        int diceEvents = 0;
        game.PhaseChanged += _ => phaseEvents++;
        game.DiceRolled += _ => diceEvents++;

        Assert.IsFalse(game.IsStarted);
        game.Start();

        Assert.IsTrue(game.IsStarted);
        Assert.AreEqual(1, phaseEvents);
        Assert.AreEqual(1, diceEvents);
    }
}
