using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Tests of both victory conditions: the Monument that survives a full round
/// and the annihilation of the opponent.
/// </summary>
public class HexworldVictoryTests
{
    /// <summary>An inner tile of player 0 where the Monument is safe from the Ether.</summary>
    private static readonly HexCoord MonumentTile = new HexCoord(-1, 0);

    /// <summary>The Monument wins the game one full round after it is finished.</summary>
    [Test]
    public void Monument_Wins_AfterItSurvivesAFullRound()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(40, 99, 99, 99, 0));
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-2, -1), HexBuildingType.Church, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 0), HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 1), HexBuildingType.Cottage, 0);

        HexworldGameOverEvent reported = default(HexworldGameOverEvent);
        game.GameOver += e => reported = e;

        game.Build(MonumentTile, HexBuildingType.Monument);
        game.AdvancePhase();
        game.AdvancePhase();

        Assert.IsFalse(game.IsGameOver, "The Monument has not stood for a full round yet.");
        Assert.AreEqual(1, game.CurrentPlayerIndex);

        HexworldTestFactory.PassTurn(game);
        Assert.IsFalse(game.IsGameOver);
        Assert.AreEqual(0, game.CurrentPlayerIndex);

        HexworldTestFactory.PassTurn(game);

        Assert.IsTrue(game.IsGameOver);
        Assert.AreEqual(0, game.Winner);
        Assert.AreEqual(HexworldWinReason.Monument, game.WinReason);
        Assert.AreEqual(HexworldWinReason.Monument, reported.Reason);
        Assert.AreEqual(HexworldPhase.GameOver, game.CurrentPhase);
    }

    /// <summary>A Monument lost before the round is over wins nothing.</summary>
    [Test]
    public void Monument_WinsNothing_WhenItIsLostFirst()
    {
        HexworldGame game = CreateGameInBuildPhase();
        HexworldTestFactory.SetResources(game, 0, new HexworldResources(40, 99, 99, 99, 0));
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-2, -1), HexBuildingType.Church, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 0), HexBuildingType.Cottage, 0);
        HexworldTestFactory.PlaceBuilding(game, new HexCoord(-3, 1), HexBuildingType.Cottage, 0);

        game.Build(MonumentTile, HexBuildingType.Monument);
        game.AdvancePhase();
        game.AdvancePhase();

        game.Board.GetTile(MonumentTile).Building = null;
        HexworldTestFactory.PassTurn(game);
        HexworldTestFactory.PassTurn(game);

        Assert.IsFalse(game.IsGameOver);
    }

    /// <summary>Destroying the enemy Castle ends the game at once.</summary>
    [Test]
    public void Annihilation_Wins_WhenTheEnemyCastleIsGone()
    {
        HexworldGame game = CreateGameInCombatPhase();

        game.Board.GetTile(new HexCoord(2, 0)).Building = null;

        HexworldGameOverEvent reported = default(HexworldGameOverEvent);
        game.GameOver += e => reported = e;

        game.EndTurn();

        Assert.IsTrue(game.IsGameOver);
        Assert.AreEqual(0, game.Winner);
        Assert.AreEqual(HexworldWinReason.Annihilation, game.WinReason);
        Assert.AreEqual(0, reported.Winner);
    }

    /// <summary>A player without a single living tile has lost.</summary>
    [Test]
    public void Annihilation_Wins_WhenTheEnemyHasNoTilesLeft()
    {
        HexworldGame game = CreateGameInCombatPhase();

        IReadOnlyList<HexTile> tiles = game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Owner == 1)
            {
                tiles[i].IsVoided = true;
            }
        }

        game.EndTurn();

        Assert.IsTrue(game.IsGameOver);
        Assert.AreEqual(0, game.Winner);
        Assert.AreEqual(HexworldWinReason.Annihilation, game.WinReason);
    }

    /// <summary>A player who wipes himself out hands the win to the opponent.</summary>
    [Test]
    public void Annihilation_Wins_ForTheOpponent_WhenTheCurrentPlayerIsWipedOut()
    {
        HexworldGame game = CreateGameInCombatPhase();

        game.Board.GetTile(new HexCoord(-2, 0)).Building = null;

        game.EndTurn();

        Assert.IsTrue(game.IsGameOver);
        Assert.AreEqual(1, game.Winner);
        Assert.AreEqual(HexworldWinReason.Annihilation, game.WinReason);
    }

    /// <summary>The game runs on while both Castles stand.</summary>
    [Test]
    public void Game_RunsOn_WhileBothCastlesStand()
    {
        HexworldGame game = CreateGameInCombatPhase();

        game.EndTurn();

        Assert.IsFalse(game.IsGameOver);
        Assert.AreEqual(1, game.CurrentPlayerIndex);
        Assert.AreEqual(HexworldPhase.Harvest, game.CurrentPhase);
        Assert.AreEqual(2, game.TurnNumber);
    }

    /// <summary>
    /// Builds a game in the build phase of player 0, with no building rolling
    /// anything so no skull can disturb the test.
    /// </summary>
    /// <returns>A started game in the build phase.</returns>
    private static HexworldGame CreateGameInBuildPhase()
    {
        HexworldConfig config = HexworldTestFactory.CreateSilentConfig();
        HexworldGame game = HexworldTestFactory.CreateUnstartedGame(config, new HexworldQueueRandom());
        game.Start();
        game.AdvancePhase();
        return game;
    }

    /// <summary>
    /// Builds a game in the combat phase of player 0.
    /// </summary>
    /// <returns>A started game in the combat phase.</returns>
    private static HexworldGame CreateGameInCombatPhase()
    {
        HexworldGame game = CreateGameInBuildPhase();
        game.AdvancePhase();
        return game;
    }
}
