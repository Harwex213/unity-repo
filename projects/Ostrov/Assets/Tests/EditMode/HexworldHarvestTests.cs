using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Tests of the harvest phase: which buildings roll, how rerolls behave, how
/// upkeep starves buildings and when skull disasters strike.
/// </summary>
public class HexworldHarvestTests
{
    /// <summary>Tiles of player 0 that start empty and can hold a test building.</summary>
    private static readonly HexCoord[] FreeTiles =
    {
        new HexCoord(-1, 0),
        new HexCoord(-1, -1),
        new HexCoord(-2, -1),
        new HexCoord(-3, 0),
        new HexCoord(-3, 1),
        new HexCoord(-2, 1),
    };

    /// <summary>Only active buildings that own a die take part in the roll.</summary>
    [Test]
    public void Roll_UsesOneDiePerActiveBuilding()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        var random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);

        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[1], HexBuildingType.Farm, 0);
        game.Start();

        Assert.AreEqual(3, game.Dice.Count, "Castle, Cottage and Farm should each roll one die.");
    }

    /// <summary>A building with no die never appears in the roll.</summary>
    [Test]
    public void Roll_SkipsBuildingsWithoutADie()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        var random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);

        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Monument, 0);
        game.Start();

        Assert.AreEqual(1, game.Dice.Count, "Only the Castle should roll.");
    }

    /// <summary>A starving building is skipped by the next roll.</summary>
    [Test]
    public void Roll_SkipsInactiveBuildings()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        var random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);

        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        game.Board.GetTile(FreeTiles[0]).Building.IsActive = false;
        game.Start();

        Assert.AreEqual(1, game.Dice.Count);
    }

    /// <summary>A harvest opens with exactly two rerolls.</summary>
    [Test]
    public void Reroll_StartsWithExactlyTwo()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        game.Start();

        Assert.AreEqual(2, game.RerollsRemaining);

        game.Reroll(0);
        Assert.AreEqual(1, game.RerollsRemaining);

        game.Reroll(0);
        Assert.AreEqual(0, game.RerollsRemaining);

        Assert.IsFalse(game.CanReroll(new[] { 0 }));
        Assert.Throws<InvalidOperationException>(() => game.Reroll(0));
    }

    /// <summary>A die showing a skull is locked and cannot be rerolled.</summary>
    [Test]
    public void Reroll_LeavesSkullDiceLocked()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldTestFactory.SetUniformDie(config, HexBuildingType.Cottage, HexworldDiceFace.Skull());

        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        game.Start();

        int skullIndex = FindDie(game, HexBuildingType.Cottage);
        int castleIndex = FindDie(game, HexBuildingType.Castle);

        Assert.IsTrue(game.Dice[skullIndex].IsSkull);
        Assert.IsFalse(game.Dice[skullIndex].CanReroll);
        Assert.IsTrue(game.Dice[castleIndex].CanReroll);

        Assert.IsFalse(game.CanReroll(new[] { skullIndex }));
        Assert.Throws<InvalidOperationException>(() => game.Reroll(skullIndex));
        Assert.AreEqual(2, game.RerollsRemaining, "A refused reroll must not be charged.");

        Assert.IsTrue(game.CanReroll(new[] { castleIndex }));
    }

    /// <summary>A reroll changes only the dice the player selected.</summary>
    [Test]
    public void Reroll_ChangesOnlyTheSelectedDice()
    {
        HexworldConfig config = HexworldTestFactory.CreateConfig();
        var random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Quarry, 0);
        game.Start();

        int castleIndex = FindDie(game, HexBuildingType.Castle);
        int quarryIndex = FindDie(game, HexBuildingType.Quarry);
        int castleFaceBefore = game.Dice[castleIndex].FaceIndex;

        random.Enqueue(3);
        game.Reroll(quarryIndex);

        Assert.AreEqual(castleFaceBefore, game.Dice[castleIndex].FaceIndex);
        Assert.AreEqual(3, game.Dice[quarryIndex].FaceIndex);
    }

    /// <summary>Dice pay out, flat income is added and the terrain bonus is applied.</summary>
    [Test]
    public void Harvest_AddsDiceYieldFlatIncomeAndTerrainBonus()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldTestFactory.SetUniformDie(
            config, HexBuildingType.Farm, HexworldDiceFace.Pays(HexworldResources.FromFood(1)));
        HexworldTestFactory.SetFlatIncome(config, HexBuildingType.Farm, HexworldResources.FromFood(2));

        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Farm, 0);
        game.Board.GetTile(FreeTiles[0]).Terrain = HexTerrainType.Forest;
        game.Start();

        HexworldTestFactory.SetResources(game, 0, new HexworldResources(10, 0, 0, 0, 0));
        game.ConfirmHarvest();

        // 10 food, plus 1 from the die, plus 2 flat income, minus 1 upkeep, and 1 wood from the forest.
        Assert.AreEqual(12, game.GetPlayer(0).Resources.Food);
        Assert.AreEqual(1, game.GetPlayer(0).Resources.Wood);
    }

    /// <summary>Every building except the Castle eats one food each harvest.</summary>
    [Test]
    public void Upkeep_ChargesOneFoodPerBuilding()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[1], HexBuildingType.Cottage, 0);
        game.Start();

        HexworldTestFactory.SetResources(game, 0, new HexworldResources(5, 0, 0, 0, 0));
        game.ConfirmHarvest();

        Assert.AreEqual(3, game.GetPlayer(0).Resources.Food, "The Castle pays no upkeep.");
        Assert.IsTrue(game.Board.GetTile(FreeTiles[0]).Building.IsActive);
        Assert.IsTrue(game.Board.GetTile(FreeTiles[1]).Building.IsActive);
    }

    /// <summary>When the food runs short, the buildings left hungry go inactive.</summary>
    [Test]
    public void Upkeep_StarvesTheBuildingsItCannotFeed()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[1], HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[2], HexBuildingType.Cottage, 0);
        game.Start();

        HexworldTestFactory.SetResources(game, 0, new HexworldResources(1, 0, 0, 0, 0));
        game.ConfirmHarvest();

        Assert.AreEqual(0, game.GetPlayer(0).Resources.Food, "Food must never go negative.");

        int active = 0;
        for (int i = 0; i < FreeTiles.Length; i++)
        {
            HexTile tile = game.Board.GetTile(FreeTiles[i]);
            if (tile.HasBuilding && tile.Building.IsActive)
            {
                active++;
            }
        }

        Assert.AreEqual(1, active, "Only one of the three cottages could be fed.");
    }

    /// <summary>A building that is fed again becomes active again.</summary>
    [Test]
    public void Upkeep_ReactivatesAFedBuilding()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        HexworldTestFactory.PlaceBuilding(game, FreeTiles[0], HexBuildingType.Cottage, 0);
        game.Board.GetTile(FreeTiles[0]).Building.IsActive = false;
        game.Start();

        HexworldTestFactory.SetResources(game, 0, new HexworldResources(5, 0, 0, 0, 0));
        game.ConfirmHarvest();

        Assert.IsTrue(game.Board.GetTile(FreeTiles[0]).Building.IsActive);
        Assert.AreEqual(4, game.GetPlayer(0).Resources.Food);
    }

    /// <summary>Fewer than three skulls cause no disaster.</summary>
    [Test]
    public void Disaster_DoesNotStrikeBelowThreeSkulls()
    {
        HexworldGame game = CreateSkullGame(2);

        game.ConfirmHarvest();

        Assert.AreEqual(2, game.GetPlayer(0).Skulls);
        Assert.AreEqual(2, game.Board.CountBuildings(0, HexBuildingType.Cottage));
    }

    /// <summary>Three skulls destroy one building of the player.</summary>
    [Test]
    public void Disaster_AtThreeSkulls_DestroysOneBuilding()
    {
        HexworldGame game = CreateSkullGame(3);

        HexworldDisasterEvent reported = default(HexworldDisasterEvent);
        game.DisasterStruck += e => reported = e;

        game.ConfirmHarvest();

        Assert.AreEqual(3, game.GetPlayer(0).Skulls);
        Assert.AreEqual(2, game.Board.CountBuildings(0, HexBuildingType.Cottage));
        Assert.IsNotNull(game.Board.FindBuilding(0, HexBuildingType.Castle), "The Castle is never destroyed.");
        Assert.IsTrue(reported.DestroyedBuilding);
        Assert.IsFalse(reported.VoidedTile);
        Assert.AreEqual(0, CountVoidedTiles(game));
    }

    /// <summary>Five skulls also drop one border tile of the player into the Ether.</summary>
    [Test]
    public void Disaster_AtFiveSkulls_AlsoVoidsABorderTile()
    {
        HexworldGame game = CreateSkullGame(5);

        HexworldDisasterEvent reported = default(HexworldDisasterEvent);
        game.DisasterStruck += e => reported = e;

        game.ConfirmHarvest();

        Assert.AreEqual(5, game.GetPlayer(0).Skulls);
        Assert.AreEqual(4, game.Board.CountBuildings(0, HexBuildingType.Cottage));
        Assert.IsTrue(reported.DestroyedBuilding);
        Assert.IsTrue(reported.VoidedTile);
        Assert.AreEqual(1, CountVoidedTiles(game));

        IReadOnlyList<HexTile> tiles = game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].IsVoided)
            {
                Assert.IsTrue(HexGrid.IsBorder(tiles[i].Coord, game.Board.Radius));
                Assert.IsFalse(tiles[i].HasBuilding, "A voided tile carries nothing.");
            }
        }
    }

    /// <summary>The skull counter clears when the next turn of that player begins.</summary>
    [Test]
    public void Skulls_ResetWhenTheTurnBegins()
    {
        HexworldGame game = CreateSkullGame(3);
        game.ConfirmHarvest();
        Assert.AreEqual(3, game.GetPlayer(0).Skulls);

        game.AdvancePhase();
        game.AdvancePhase();
        HexworldTestFactory.PassTurn(game);

        Assert.AreEqual(0, game.CurrentPlayerIndex);
        Assert.AreEqual(0, game.GetPlayer(0).Skulls);
    }

    /// <summary>
    /// Builds a game where player 0 owns the given number of all-skull cottages
    /// and enough food to feed them, so the harvest produces exactly that many
    /// skulls.
    /// </summary>
    /// <param name="skulls">How many skull dice the player should roll.</param>
    /// <returns>A started game waiting in the harvest phase.</returns>
    private static HexworldGame CreateSkullGame(int skulls)
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldTestFactory.SetUniformDie(config, HexBuildingType.Cottage, HexworldDiceFace.Skull());

        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        for (int i = 0; i < skulls; i++)
        {
            HexworldTestFactory.PlaceBuilding(game, FreeTiles[i], HexBuildingType.Cottage, 0);
        }

        game.Start();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(20, 0, 0, 0, 0));
        return game;
    }

    /// <summary>
    /// Counts the tiles that have been dropped into the Ether.
    /// </summary>
    /// <param name="game">The game to read.</param>
    /// <returns>How many tiles are voided.</returns>
    private static int CountVoidedTiles(HexworldGame game)
    {
        int count = 0;
        IReadOnlyList<HexTile> tiles = game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].IsVoided)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Finds the die a given building rolled.
    /// </summary>
    /// <param name="game">The game to read.</param>
    /// <param name="type">Which building to look for.</param>
    /// <returns>The index of its die.</returns>
    private static int FindDie(HexworldGame game, HexBuildingType type)
    {
        for (int i = 0; i < game.Dice.Count; i++)
        {
            if (game.Dice[i].BuildingType == type)
            {
                return i;
            }
        }

        Assert.Fail("No die was rolled for " + type + ".");
        return -1;
    }
}
