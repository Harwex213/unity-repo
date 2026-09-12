using System;
using NUnit.Framework;

/// <summary>
/// Tests of the combat phase: taking neutral ground, fighting for enemy tiles
/// and the limit of one capture per turn.
/// </summary>
public class HexworldCombatTests
{
    /// <summary>A tile nobody owns that touches the land of player 0.</summary>
    private static readonly HexCoord NeutralTile = new HexCoord(0, 0);

    /// <summary>A tile of player 1 that touches the centre of the island.</summary>
    private static readonly HexCoord EnemyTile = new HexCoord(1, 0);

    /// <summary>The Castle tile of player 1.</summary>
    private static readonly HexCoord EnemyCastle = new HexCoord(2, 0);

    /// <summary>Taking a neutral tile costs one soldier and needs no fight.</summary>
    [Test]
    public void Capture_OfNeutralTile_CostsOneSoldierAndSucceeds()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 2));

        HexworldTileOwnerChangedEvent reported = default(HexworldTileOwnerChangedEvent);
        game.TileOwnerChanged += e => reported = e;

        Assert.IsTrue(game.CanCapture(NeutralTile, 1));
        HexworldCombatResult result = game.Capture(NeutralTile, 1);

        Assert.IsTrue(result.Captured);
        Assert.IsTrue(result.WasNeutral);
        Assert.AreEqual(0, result.AttackRoll, "A neutral tile is taken without a roll.");
        Assert.AreEqual(0, game.Board.GetTile(NeutralTile).Owner);
        Assert.AreEqual(1, game.GetPlayer(0).Resources.Soldiers);
        Assert.AreEqual(NeutralTile, reported.Coord);
        Assert.AreEqual(HexworldConfig.NeutralOwner, reported.PreviousOwner);
        Assert.AreEqual(0, reported.NewOwner);
    }

    /// <summary>A player without soldiers takes nothing.</summary>
    [Test]
    public void Capture_IsRefused_WithoutSoldiers()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        HexworldTestFactory.SetResources(game, 0, HexworldResources.Zero);

        string reason;
        Assert.IsFalse(game.CanCapture(NeutralTile, 1, out reason));
        Assert.IsNotEmpty(reason);
        Assert.Throws<InvalidOperationException>(() => game.Capture(NeutralTile, 1));
    }

    /// <summary>Only tiles that touch your own land can be captured.</summary>
    [Test]
    public void Capture_IsRefused_WhenTheTileIsNotAdjacent()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 9));

        Assert.IsFalse(game.CanCapture(new HexCoord(3, 0), 3), "That tile is on the far side of the island.");
        Assert.IsTrue(game.CanCapture(NeutralTile, 1));
    }

    /// <summary>A tile dropped into the Ether is gone and cannot be captured.</summary>
    [Test]
    public void Capture_IsRefused_OnAVoidedTile()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 9));
        game.Board.GetTile(NeutralTile).IsVoided = true;

        Assert.IsFalse(game.CanCapture(NeutralTile, 1));
    }

    /// <summary>A player captures one tile per turn at most.</summary>
    [Test]
    public void Capture_IsAllowedOnlyOncePerTurn()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 9));

        game.Capture(NeutralTile, 1);

        Assert.IsFalse(game.CanCapture(new HexCoord(0, -1), 1));
        Assert.IsTrue(game.GetPlayer(0).HasCapturedThisTurn);
    }

    /// <summary>A stronger attack takes the enemy tile and razes what stood on it.</summary>
    [Test]
    public void Capture_OfEnemyTile_Succeeds_WhenTheAttackRollsHigher()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        game.Board.GetTile(NeutralTile).Owner = 0;
        HexworldTestFactory.PlaceBuilding(game, EnemyTile, HexBuildingType.Cottage, 1);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 6));

        HexworldBuildingDestroyedEvent destroyed = default(HexworldBuildingDestroyedEvent);
        game.BuildingDestroyed += e => destroyed = e;

        // Defence is 1 base plus 2 for the building, so 3 plus the defender die.
        Assert.AreEqual(3, game.GetDefenseStrength(EnemyTile));

        random.Enqueue(6, 1);
        HexworldCombatResult result = game.Capture(EnemyTile, 6);

        Assert.IsTrue(result.Captured);
        Assert.AreEqual(12, result.AttackPower);
        Assert.AreEqual(4, result.DefensePower);
        Assert.AreEqual(HexBuildingType.Cottage, result.DestroyedBuilding);
        Assert.AreEqual(0, game.Board.GetTile(EnemyTile).Owner);
        Assert.IsFalse(game.Board.GetTile(EnemyTile).HasBuilding);
        Assert.AreEqual(0, game.GetPlayer(0).Resources.Soldiers, "Every soldier was spent.");
        Assert.AreEqual(HexworldBuildingLossReason.Combat, destroyed.Reason);
    }

    /// <summary>A weaker attack loses the soldiers and changes nothing else.</summary>
    [Test]
    public void Capture_OfEnemyTile_Fails_WhenTheDefenseRollsHigher()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        game.Board.GetTile(NeutralTile).Owner = 0;
        HexworldTestFactory.PlaceBuilding(game, EnemyTile, HexBuildingType.Cottage, 1);
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 5));

        random.Enqueue(1, 6);
        HexworldCombatResult result = game.Capture(EnemyTile, 2);

        Assert.IsFalse(result.Captured);
        Assert.AreEqual(3, result.AttackPower);
        Assert.AreEqual(9, result.DefensePower);
        Assert.AreEqual(1, game.Board.GetTile(EnemyTile).Owner);
        Assert.IsTrue(game.Board.GetTile(EnemyTile).HasBuilding);
        Assert.AreEqual(3, game.GetPlayer(0).Resources.Soldiers, "The two soldiers were spent anyway.");
        Assert.IsTrue(game.GetPlayer(0).HasCapturedThisTurn, "A failed attack still uses the turn.");
    }

    /// <summary>An equal roll is not enough; the attack must be strictly stronger.</summary>
    [Test]
    public void Capture_OfEnemyTile_Fails_OnATie()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        game.Board.GetTile(NeutralTile).Owner = 0;
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 5));

        // Defence is 1 base, no building and no barracks.
        Assert.AreEqual(1, game.GetDefenseStrength(EnemyTile));

        random.Enqueue(2, 4);
        HexworldCombatResult result = game.Capture(EnemyTile, 3);

        Assert.AreEqual(5, result.AttackPower);
        Assert.AreEqual(5, result.DefensePower);
        Assert.IsFalse(result.Captured);
    }

    /// <summary>A neighbouring Barracks adds one to the defence of an enemy tile.</summary>
    [Test]
    public void DefenseStrength_CountsAdjacentBarracks()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        game.Board.GetTile(NeutralTile).Owner = 0;

        Assert.AreEqual(1, game.GetDefenseStrength(EnemyTile));

        HexworldTestFactory.PlaceBuilding(game, new HexCoord(1, -1), HexBuildingType.Barracks, 1);
        Assert.AreEqual(2, game.GetDefenseStrength(EnemyTile));

        HexworldTestFactory.PlaceBuilding(game, new HexCoord(2, -1), HexBuildingType.Barracks, 1);
        Assert.AreEqual(3, game.GetDefenseStrength(EnemyTile));
    }

    /// <summary>Taking the enemy Castle destroys it along with the tile.</summary>
    [Test]
    public void Capture_OfTheEnemyCastle_DestroysIt()
    {
        HexworldQueueRandom random;
        HexworldGame game = CreateGameInCombatPhase(out random);
        game.Board.GetTile(EnemyTile).Owner = 0;
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 10));

        random.Enqueue(6, 1);
        HexworldCombatResult result = game.Capture(EnemyCastle, 10);

        Assert.IsTrue(result.Captured);
        Assert.AreEqual(HexBuildingType.Castle, result.DestroyedBuilding);
        Assert.IsNull(game.Board.FindBuilding(1, HexBuildingType.Castle));
        Assert.AreEqual(0, game.Board.GetTile(EnemyCastle).Owner);
    }

    /// <summary>Tiles can only be captured during the combat phase.</summary>
    [Test]
    public void Capture_IsRefused_OutsideTheCombatPhase()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        game.Start();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(0, 0, 0, 0, 5));

        Assert.IsFalse(game.CanCapture(NeutralTile, 1));
    }

    /// <summary>
    /// Builds a game where the turn of player 0 already stands in the combat
    /// phase and no building rolls anything.
    /// </summary>
    /// <param name="random">Receives the scripted randomness source.</param>
    /// <returns>A started game in the combat phase of player 0.</returns>
    private static HexworldGame CreateGameInCombatPhase(out HexworldQueueRandom random)
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        random = new HexworldQueueRandom();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, random);
        game.Start();
        game.AdvancePhase();
        game.AdvancePhase();

        Assert.AreEqual(HexworldPhase.Combat, game.CurrentPhase);
        return game;
    }
}
