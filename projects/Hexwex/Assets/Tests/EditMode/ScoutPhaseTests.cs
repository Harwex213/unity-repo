using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The scout phase: fog, the price of scouting, the one flight, the trail.</summary>
    public sealed class ScoutPhaseTests
    {
        /// <summary>A session with the stronghold placed, standing in the scout phase of turn 1.</summary>
        private static GameSession InScoutPhase(List<string> notices = null)
        {
            GameSession session = new GameSession("Mom010");
            if (notices != null)
            {
                session.Notice += notices.Add;
            }

            Assert.IsTrue(session.PlaceStronghold("0,0"));
            Assert.IsTrue(session.StartGame());
            session.EndPhase();
            session.EndPhase();
            Assert.AreEqual(Phase.Scout, session.Phase);

            return session;
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        public void ScoutingCostsHalfTheDistanceRoundedUp(int distance, int expected)
        {
            Assert.AreEqual(expected, WorldRules.ScoutCost(distance));
        }

        [Test]
        public void TheHomeRingIsKnownAndTheRestIsFog()
        {
            GameSession session = new GameSession("Mom010");
            World world = session.World;
            WorldCell home = world.Get(session.Human.CellId);

            Assert.AreEqual(home.Neighbors.Length + 1, world.Cells.Count(cell => cell.Revealed));
            Assert.IsTrue(home.Neighbors.All(index => world.Cells[index].Revealed));

            int[] distances = WorldGen.DistancesFrom(world.Cells, home.Index);
            foreach (WorldCell cell in world.Cells)
            {
                CellVisibility expected = distances[cell.Index] <= 1 ? CellVisibility.Revealed
                    : distances[cell.Index] == 2 ? CellVisibility.Frontier
                    : CellVisibility.Fogged;

                Assert.AreEqual(expected, WorldRules.Visibility(world, cell), cell.Id);
            }
        }

        [Test]
        public void ScoutingIsRefusedOutsideItsPhaseAndBeyondTheFrontier()
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            World world = session.World;
            int[] distances = WorldGen.DistancesFrom(world.Cells, world.Get(session.Human.CellId).Index);
            WorldCell frontier = world.Cells.First(cell => distances[cell.Index] == 2);
            WorldCell fogged = world.Cells.First(cell => distances[cell.Index] == 3);

            // The build phase: the map can be looked at, nothing more.
            Assert.IsFalse(session.Scout(frontier.Id));
            Assert.IsFalse(session.MoveIsland(world.Cells[world.Get(session.Human.CellId).Neighbors[0]].Id));

            session.EndPhase();
            session.EndPhase();
            session.Human.Resources[ResourceId.Scouting] = 0;
            Assert.IsFalse(session.Scout(frontier.Id));

            session.Human.Resources[ResourceId.Scouting] = 3;
            Assert.IsFalse(session.Scout(fogged.Id));
            Assert.IsTrue(session.Scout(frontier.Id));
            Assert.IsTrue(frontier.Revealed);
            Assert.AreEqual(2, session.Human.Resources[ResourceId.Scouting]);
            Assert.IsFalse(session.Scout(frontier.Id));

            // The scouted cell pushed the frontier one step out.
            Assert.IsTrue(frontier.Neighbors.Any(index => distances[index] == 3 && WorldRules.Visibility(world, world.Cells[index]) == CellVisibility.Frontier));
        }

        [Test]
        public void TheIslandFliesOnceATurnAndWakesTheIslandsItLandsOn()
        {
            GameSession session = InScoutPhase();
            World world = session.World;
            Player human = session.Human;
            WorldCell home = world.Get(human.CellId);
            WorldCell wild = home.Neighbors.Select(index => world.Cells[index]).FirstOrDefault(cell => cell.Kind == CellKind.Island && cell.IslandCount > 0);
            WorldCell target = wild ?? world.Cells[home.Neighbors[0]];
            WorldCell far = world.Cells.First(cell => System.Array.IndexOf(home.Neighbors, cell.Index) < 0 && cell != home);

            Assert.IsFalse(session.MoveIsland(home.Id));
            Assert.IsFalse(session.MoveIsland(far.Id));
            Assert.AreEqual(home.Neighbors.Length, WorldRules.ReachableCellIds(world, home.Id, false).Count);

            Assert.IsTrue(session.MoveIsland(target.Id));
            Assert.AreEqual(target.Id, human.CellId);
            Assert.IsNull(home.OwnerId);
            Assert.AreEqual(human.Id, target.OwnerId);
            Assert.IsTrue(session.MovedThisTurn);
            Assert.AreEqual(target.Kind == CellKind.Island, target.Activated);
            Assert.AreEqual(0, WorldRules.ReachableCellIds(world, target.Id, true).Count);
            Assert.IsFalse(session.MoveIsland(home.Id));

            // A flight leaves no trail, and the woken islands are the battle of the clear phase.
            int pending = WorldRules.PendingIslands(target, human);
            session.EndPhase();
            Assert.AreEqual(0, home.ToxicTrail);
            Assert.AreEqual(0, target.ToxicTrail);
            if (pending > 0)
            {
                Assert.AreEqual(Phase.Clear, session.Phase);
                Assert.AreEqual(pending + 1, session.Battle.Islands.Count);
            }
            else
            {
                Assert.AreEqual(2, session.Turn);
                Assert.AreEqual(Phase.Build, session.Phase);
            }
        }

        [Test]
        public void AnIslandThatStaysLeavesItsToxicityInTheCell()
        {
            GameSession session = InScoutPhase();
            Player human = session.Human;
            WorldCell home = session.World.Get(human.CellId);
            int load = Tax.TotalToxicity(human);

            session.EndPhase();

            Assert.AreEqual(load, home.ToxicTrail);
            Assert.AreEqual(human.CellId, home.Id);
        }

        [Test]
        public void TheTrailRollsTheSameEventForTheSameSeed()
        {
            Assert.AreEqual(0, TrailEvents.EventChance(0));
            Assert.AreEqual(0.25, TrailEvents.EventChance(200), 1e-12);
            Assert.AreEqual(0.6, TrailEvents.EventChance(100000), 1e-12);

            // No trail, no event: the first draw is never below zero.
            Assert.IsNull(TrailEvents.Roll(0, Rng.FromText("Mom010:trail:1")));

            TrailEvent first = TrailEvents.Roll(100000, new Rng(7));
            TrailEvent second = TrailEvents.Roll(100000, new Rng(7));
            Assert.AreSame(first, second);
        }

        [Test]
        public void ARivalFliesTowardTheLairAlongAShortestWay()
        {
            GameSession session = new GameSession("Mom010");
            World world = session.World;
            Player rival = session.Players.First(player => !player.IsHuman);
            WorldCell lair = world.BossCell;
            int[] distances = WorldGen.DistancesFrom(world.Cells, lair.Index);

            string next = RivalAi.StepToward(world, rival.CellId, lair.Id);

            Assert.IsNotNull(next);
            Assert.AreEqual(distances[world.Get(rival.CellId).Index] - 1, distances[world.Get(next).Index]);
            Assert.IsNull(RivalAi.StepToward(world, lair.Id, lair.Id));
        }

        [Test]
        public void TheRivalsSetOutOnTheHuntTurn()
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            Dictionary<string, string> start = session.Players.ToDictionary(player => player.Id, player => player.CellId);

            while (session.Turn < RivalAi.HuntStartTurn && session.Outcome == null)
            {
                session.EndPhase();
            }

            Assert.IsNull(session.Outcome);
            Assert.IsTrue(session.Players.All(player => player.CellId == start[player.Id]));

            // Build, tax, scout: the rivals fly when the scout phase closes.
            session.EndPhase();
            session.EndPhase();
            session.EndPhase();

            foreach (Player rival in session.Players.Where(player => !player.IsHuman && !player.Eliminated))
            {
                Assert.AreNotEqual(start[rival.Id], rival.CellId, rival.Id);
                Assert.AreEqual(rival.Id, session.World.Get(rival.CellId).OwnerId);
                Assert.IsNull(session.World.Get(start[rival.Id]).OwnerId);
            }
        }
    }
}
