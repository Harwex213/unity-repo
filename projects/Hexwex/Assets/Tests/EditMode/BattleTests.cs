using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The clear phase: the levy, the level, the sim and what the battle hands back.</summary>
    public sealed class BattleTests
    {
        private static readonly string[] MilitiaOnly = { "militia" };

        /// <summary>A player with a stronghold on the centre hex and one building beside it.</summary>
        private static Player Defender(out HexTile built)
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            Player human = session.Human;
            built = human.Hexes.First(hex => hex.Id != "0,0" && Buildings.ForBiome(hex.Biome).Count > 0);
            built.Building = Buildings.BestForBiome(built.Biome).Id;

            return human;
        }

        private static LevelSpec Level(Player player, int islands, List<Unit> roster, bool boss = false)
        {
            return CleanupLevel.Create(
                new LevelSetup { Player = player, Turn = 3, IslandCount = islands, CellBiome = BiomeId.Swamp, ToxicTrail = 0, Roster = roster, Boss = boss },
                Rng.FromText("level"));
        }

        private static List<Unit> Knights(int count)
        {
            return Enumerable.Repeat(Units.Get("knight"), count).ToList();
        }

        /// <summary>Sails the player's island at the nearest island that still stands, tick by tick.</summary>
        private static void SailAndFight(CleanupSim sim, int maxTicks)
        {
            for (int tick = 0; tick < maxTicks && sim.Status == SimStatus.Running; tick += 1)
            {
                SimIsland player = sim.PlayerIsland;
                SimIsland goal = sim.Islands
                    .Where(island => island.Side == CleanupSide.Enemy && !island.IsGone)
                    .OrderBy(island => CleanupMath.Hypot(island.CenterX - player.CenterX, island.CenterY - player.CenterY))
                    .FirstOrDefault();
                if (goal != null)
                {
                    sim.SetInput(goal.CenterX - player.CenterX, goal.CenterY - player.CenterY);
                }

                sim.Step();
            }
        }

        [Test]
        public void TheLevyTakesPeopleUntilNoneAreLeft()
        {
            List<Unit> roster = Units.BuildRoster(6, MilitiaOnly, new Rng(1));

            Assert.AreEqual(6, roster.Count);
            Assert.IsTrue(roster.All(unit => unit.Key == "militia"));
            Assert.AreEqual(0, Units.BuildRoster(0, MilitiaOnly, new Rng(1)).Count);

            // A mixed levy never spends more people than there are.
            string[] unlocked = { "militia", "swordsman", "knight" };
            List<Unit> mixed = Units.BuildRoster(11, unlocked, new Rng(5));
            Assert.AreEqual(11, mixed.Sum(unit => unit.Upkeep));
        }

        [TestCase(1, 0, 1)]
        [TestCase(3, 0, 2)]
        [TestCase(1, 200, 2)]
        [TestCase(40, 5000, 6)]
        public void TheRaidTierGrowsWithTurnsAndTrail(int turn, int trail, int expected)
        {
            Assert.AreEqual(expected, CleanupLevel.RaidTier(turn, trail));
        }

        [Test]
        public void TheLevelStandsItsIslandsApartInsideTheBorder()
        {
            Player player = Defender(out _);
            LevelSpec level = Level(player, 3, Knights(2));

            Assert.AreEqual(4, level.Islands.Count);
            Assert.AreEqual(CleanupSide.Player, level.Islands[0].Side);
            Assert.AreEqual(player.Hexes.Count, level.Islands[0].Hexes.Count);
            Assert.AreEqual(1, level.Islands[0].Hexes.Count(hex => hex.Stronghold));

            for (int a = 0; a < level.Islands.Count; a += 1)
            {
                IslandExtent first = CleanupLevel.Extent(level.Islands[a].Hexes);
                double ax = level.Islands[a].X + first.Cx;
                double ay = level.Islands[a].Y + first.Cy;

                Assert.Less(System.Math.Abs(ax) + first.Radius, level.Bounds.HalfWidth - CleanupBorder.BorderDepth);
                Assert.Less(System.Math.Abs(ay) + first.Radius, level.Bounds.HalfHeight - CleanupBorder.BorderDepth);

                for (int b = a + 1; b < level.Islands.Count; b += 1)
                {
                    IslandExtent second = CleanupLevel.Extent(level.Islands[b].Hexes);
                    double distance = CleanupMath.Hypot(level.Islands[b].X + second.Cx - ax, level.Islands[b].Y + second.Cy - ay);

                    Assert.Greater(distance, first.Radius + second.Radius);
                }
            }

            foreach (IslandSpec island in level.Islands.Skip(1))
            {
                Assert.Greater(island.Garrison.Count, 0);
                Assert.IsTrue(HexMath.IsConnected(island.Hexes.Select(hex => new Axial(hex.Q, hex.R)).ToList()));
            }
        }

        [Test]
        public void TheLairIsOneBigIslandWithTheBossFirst()
        {
            LevelSpec level = Level(Defender(out _), 1, Knights(1), true);

            Assert.AreEqual(2, level.Islands.Count);
            Assert.AreEqual(19, level.Islands[1].Hexes.Count);
            Assert.AreEqual("plague_lord", level.Islands[1].Garrison[0].Key);
            Assert.AreEqual(9, level.Islands[1].Garrison.Count);
            Assert.AreEqual(IslandBehavior.Drift, level.Islands[1].Behavior);
        }

        [Test]
        public void TheSameSeedPlaysTheSameBattle()
        {
            string Play()
            {
                CleanupSim sim = CleanupSim.Create(Level(Defender(out _), 2, Knights(6)), 77);
                SailAndFight(sim, 900);

                return sim.Tick + ":" + sim.Kills + ":" + sim.Units.Count + ":"
                    + string.Join(";", sim.Units.Select(unit => unit.Id + "@" + unit.X.ToString("R") + "," + unit.Y.ToString("R") + "," + unit.Hp.ToString("R")));
            }

            Assert.AreEqual(Play(), Play());
        }

        [Test]
        public void TwoHexesTouchExactlyAtOneHexStep()
        {
            double step = CleanupCollision.HexStep;

            Assert.AreEqual(0, CleanupCollision.HexGap(step, 0, out _), 1e-9);
            Assert.Less(CleanupCollision.HexGap(step * 0.5, 0, out _), 0);
            Assert.Greater(CleanupCollision.HexGap(step * 2, 0, out _), 0);
            // A diagonal neighbour is one step away too.
            HexMath.HexToPixel(0, 1, HexMath.HexSize, out double x, out double y);
            Assert.AreEqual(0, CleanupCollision.HexGap(x, y, out _), 1e-9);
        }

        [Test]
        public void AStrongArmyClearsTheLevelAndDocksItsIslands()
        {
            Player player = Defender(out _);
            CleanupSim sim = CleanupSim.Create(Level(player, 1, Knights(14)), 4242);
            int hexesBefore = sim.PlayerIsland.Hexes.Count;

            // Ten minutes of battle at most.
            SailAndFight(sim, 18000);
            CleanupResult result = sim.Result();

            Assert.AreEqual(SimStatus.Won, sim.Status);
            Assert.AreEqual(CleanupOutcome.Won, result.Outcome);
            Assert.AreEqual(1, result.TotalIslands);
            Assert.AreEqual(1, result.ClearedIslands);
            Assert.Greater(result.Kills, 0);
            Assert.AreEqual(14, result.Survivors.Count + result.Lost.Count);

            // The husk joined: its hexes are hexes of the player's island now, each at a free spot.
            Assert.AreEqual(1, result.AttachedIslands);
            Assert.Greater(result.Annexed.Count, 0);
            Assert.AreEqual(hexesBefore + result.Annexed.Count, sim.PlayerIsland.Hexes.Count);
            Assert.AreEqual(sim.PlayerIsland.Hexes.Count, sim.PlayerIsland.Hexes.Select(hex => hex.Id).Distinct().Count());
            Assert.IsTrue(HexMath.IsConnected(sim.PlayerIsland.Hexes.Select(hex => new Axial(hex.Q, hex.R)).ToList()));
        }

        [Test]
        public void AnUndefendedIslandFallsToTheBoss()
        {
            CleanupSim sim = CleanupSim.Create(Level(Defender(out _), 1, new List<Unit>(), true), 9);

            SailAndFight(sim, 27000);

            Assert.AreEqual(SimStatus.Lost, sim.Status);
            CleanupResult result = sim.Result();
            Assert.AreEqual(CleanupOutcome.Lost, result.Outcome);
            Assert.AreEqual(2, result.Razed);
            Assert.IsTrue(result.Structures.All(entry => entry.Hp == 0));
            Assert.AreEqual(0, result.Annexed.Count);
        }

        [Test]
        public void ShatterDestroysAHexAndWhatHangsOnIt()
        {
            Player player = Defender(out HexTile built);
            CleanupSim sim = CleanupSim.Create(Level(player, 1, Knights(2)), 11);
            SimIsland island = sim.PlayerIsland;
            int stronghold = island.Hexes.FindIndex(hex => hex.Stronghold);
            int target = island.Hexes.FindIndex(hex => hex.Id == built.Id);
            int before = island.Hexes.Count;

            Assert.IsNotNull(sim.CastShatter(0, stronghold));
            Assert.AreEqual(before, island.Hexes.Count);

            List<int> plan = sim.ShatterPlan(0, target, out string refusal);
            Assert.IsNull(refusal);
            Assert.Contains(target, plan);

            Assert.IsNull(sim.CastShatter(0, target));
            Assert.AreEqual(before - plan.Count, island.Hexes.Count);
            Assert.AreEqual(player.Resources[ResourceId.Mana] - ShatterSkill.ManaCost, sim.Mana);
            Assert.IsFalse(island.Hexes.Any(hex => hex.Id == built.Id));
            // The building went with its hex, and the skill is cooling down.
            Assert.AreEqual(1, sim.Razed);
            Assert.IsNotNull(sim.SkillRefusal());
            Assert.Greater(sim.SkillCooldown, 5);

            CleanupResult result = sim.Result();
            Assert.Contains(built.Id, result.DestroyedHexIds);
            Assert.AreEqual(ShatterSkill.ManaCost, result.ManaSpent);
        }

        [Test]
        public void ARetreatLeavesTheIslandsInTheCell()
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            session.EndPhase();
            session.EndPhase();

            Player human = session.Human;
            WorldCell home = session.World.Get(human.CellId);
            WorldCell wild = home.Neighbors.Select(index => session.World.Cells[index]).First(cell => cell.Kind == CellKind.Island && cell.IslandCount > 0);
            int waiting = wild.IslandCount;
            Assert.IsTrue(session.MoveIsland(wild.Id));

            session.EndPhase();
            Assert.AreEqual(Phase.Clear, session.Phase);
            Assert.AreEqual(waiting + 1, session.Battle.Islands.Count);
            Assert.AreEqual(human.Resources[ResourceId.Population], session.Battle.Roster.Sum(unit => unit.Upkeep));

            // The turn does not move on while the level runs.
            session.StepBattle(30);
            session.EndPhase();
            Assert.AreEqual(Phase.Clear, session.Phase);
            Assert.AreEqual(1, session.Turn);

            session.Battle.Retreat();
            session.StepBattle(1);
            Assert.AreEqual(CleanupOutcome.Retreated, session.BattleResult.Outcome);

            int hexes = human.Hexes.Count;
            session.EndPhase();

            Assert.AreEqual(2, session.Turn);
            Assert.AreEqual(Phase.Build, session.Phase);
            Assert.IsNull(session.Battle);
            Assert.AreEqual(waiting, wild.IslandCount);
            Assert.IsFalse(wild.Cleared);
            Assert.AreEqual(hexes, human.Hexes.Count);
            Assert.AreEqual(session.LastBattleResult.Survivors.Count, human.Army);
        }
    }
}
