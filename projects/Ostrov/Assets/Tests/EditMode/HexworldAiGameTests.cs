using NUnit.Framework;

/// <summary>
/// Tests that drive the heuristic opponent. They check that a turn played by
/// the AI is legal and that a full AI against AI match with a fixed seed
/// finishes without an error.
/// </summary>
public class HexworldAiGameTests
{
    /// <summary>How many turns a match may take before the test gives up.</summary>
    private const int TurnLimit = 400;

    /// <summary>One AI turn runs all three phases and hands the turn on.</summary>
    [Test]
    public void AiTurn_RunsAllThreePhasesAndPassesTheTurn()
    {
        var game = new HexworldGame(777);
        var ai = new HexworldAiPlayer(game, 0);

        ai.PlayTurn();

        Assert.IsFalse(game.IsGameOver);
        Assert.AreEqual(1, game.CurrentPlayerIndex);
        Assert.AreEqual(HexworldPhase.Harvest, game.CurrentPhase);
        Assert.AreEqual(2, game.TurnNumber);
    }

    /// <summary>The AI never leaves a player with a negative stockpile.</summary>
    [Test]
    public void AiTurns_NeverDriveResourcesNegative()
    {
        var game = new HexworldGame(31337);
        var players = new[] { new HexworldAiPlayer(game, 0), new HexworldAiPlayer(game, 1) };

        for (int i = 0; i < 40 && !game.IsGameOver; i++)
        {
            players[game.CurrentPlayerIndex].PlayTurn();

            for (int p = 0; p < 2; p++)
            {
                HexworldResources resources = game.GetPlayer(p).Resources;
                Assert.GreaterOrEqual(resources.Food, 0);
                Assert.GreaterOrEqual(resources.Wood, 0);
                Assert.GreaterOrEqual(resources.Stone, 0);
                Assert.GreaterOrEqual(resources.Culture, 0);
                Assert.GreaterOrEqual(resources.Soldiers, 0);
            }
        }
    }

    /// <summary>A full match between two AI players reaches a winner.</summary>
    [Test]
    public void AiMatch_FinishesWithAWinner()
    {
        var game = new HexworldGame(2024);
        var players = new[] { new HexworldAiPlayer(game, 0), new HexworldAiPlayer(game, 1) };

        int turns = 0;
        while (!game.IsGameOver && turns < TurnLimit)
        {
            players[game.CurrentPlayerIndex].PlayTurn();
            turns++;
        }

        Assert.IsTrue(game.IsGameOver, "The match did not finish within " + TurnLimit + " turns.");
        Assert.IsTrue(game.Winner == 0 || game.Winner == 1);
        Assert.AreNotEqual(HexworldWinReason.None, game.WinReason);
        Assert.Less(turns, TurnLimit);
    }

    /// <summary>The same seed replays the same match, move for move.</summary>
    [Test]
    public void AiMatch_IsRepeatableForTheSameSeed()
    {
        int firstWinner = PlayMatch(5150);
        int secondWinner = PlayMatch(5150);

        Assert.AreEqual(firstWinner, secondWinner);
        Assert.IsTrue(firstWinner == 0 || firstWinner == 1, "The match did not reach a winner.");
    }

    /// <summary>Several seeds all reach a winner, so no start position deadlocks.</summary>
    [Test]
    public void AiMatch_FinishesForSeveralSeeds([Values(1, 7, 12345, 987654)] int seed)
    {
        var game = new HexworldGame(seed);
        var players = new[] { new HexworldAiPlayer(game, 0), new HexworldAiPlayer(game, 1) };

        int turns = 0;
        while (!game.IsGameOver && turns < TurnLimit)
        {
            players[game.CurrentPlayerIndex].PlayTurn();
            turns++;
        }

        Assert.IsTrue(game.IsGameOver, "Seed " + seed + " did not finish within " + TurnLimit + " turns.");
    }

    /// <summary>
    /// Runs a full AI match and returns the winner.
    /// </summary>
    /// <param name="seed">Seed of the randomness source.</param>
    /// <returns>Player index of the winner, or -1 when the match did not finish.</returns>
    private static int PlayMatch(int seed)
    {
        var game = new HexworldGame(seed);
        var players = new[] { new HexworldAiPlayer(game, 0), new HexworldAiPlayer(game, 1) };

        int turns = 0;
        while (!game.IsGameOver && turns < TurnLimit)
        {
            players[game.CurrentPlayerIndex].PlayTurn();
            turns++;
        }

        return game.Winner;
    }
}
