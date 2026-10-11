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
        public void TheLevelStandsItsIslandsApartInTheClearing()
        {
            Player player = Defender(out _);
            LevelSpec level = Level(player, 3, Knights(2));
            CleanupFog fog = CleanupSim.Create(level, 1).Fog;

            Assert.AreEqual(4, level.Islands.Count);
            Assert.AreEqual(CleanupSide.Player, level.Islands[0].Side);
            Assert.AreEqual(player.Hexes.Count, level.Islands[0].Hexes.Count);
            Assert.AreEqual(1, level.Islands[0].Hexes.Count(hex => hex.Stronghold));

            for (int a = 0; a < level.Islands.Count; a += 1)
            {
                IslandExtent first = CleanupLevel.Extent(level.Islands[a].Hexes);
                double ax = level.Islands[a].X + first.Cx;
                double ay = level.Islands[a].Y + first.Cy;

                // Every island starts with open sky between it and the fog.
                Assert.LessOrEqual(fog.DepthAt(ax, ay) + first.Radius, -CleanupFog.Margin + 1);

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
        /// <summary>Sails the player's island straight out of the clearing, away from its middle.</summary>
        private static void SailOut(CleanupSim sim)
        {
            SimIsland player = sim.PlayerIsland;
            double x = player.CenterX - sim.Fog.CenterX;
            double y = player.CenterY - sim.Fog.CenterY;
            if (CleanupMath.Hypot(x, y) < 1)
            {
                x = 1;
                y = 0;
            }

            sim.SetInput(x, y);
        }

        [Test]
        public void NobodySeesFarIntoTheFog()
        {
            double far = CleanupFog.Sight + 1;

            // In the clearing everyone sees everyone, at any distance.
            Assert.IsTrue(CleanupFog.Sees(false, 0, 0, false, 5000, 0));
            // The fog hides whoever is in it, and blinds whoever looks out of it.
            Assert.IsFalse(CleanupFog.Sees(false, 0, 0, true, far, 0));
            Assert.IsFalse(CleanupFog.Sees(true, 0, 0, false, far, 0));
            Assert.IsFalse(CleanupFog.Sees(true, 0, 0, true, 0, far));
            // Close by, the fog hides nothing.
            Assert.IsTrue(CleanupFog.Sees(false, 0, 0, true, CleanupFog.Sight, 0));
            Assert.IsTrue(CleanupFog.Sees(true, 0, 0, true, 0, CleanupFog.Sight));
        }

        [Test]
        public void TheFogHidesTheIslandAndIsTheWayOut()
        {
            CleanupSim sim = CleanupSim.Create(Level(Defender(out _), 1, Knights(4)), 9);
            Assert.IsFalse(sim.Hidden);
            Assert.AreEqual(0, sim.ExitProgress);

            List<SimEventType> seen = new List<SimEventType>();
            for (int tick = 0; tick < 3000 && !sim.Hidden; tick += 1)
            {
                SailOut(sim);
                sim.Step();
                seen.AddRange(sim.DrainEvents().Select(simEvent => simEvent.Type));
            }

            // In the fog the island is hidden, and the battle goes on.
            Assert.IsTrue(sim.Hidden);
            Assert.AreEqual(SimStatus.Running, sim.Status);
            Assert.Contains(SimEventType.Hid, seen);
            Assert.IsTrue(sim.Fog.Covers(sim.PlayerIsland.CenterX, sim.PlayerIsland.CenterY));

            // It may turn back: out of the fog it is in sight again.
            for (int tick = 0; tick < 3000 && sim.Hidden; tick += 1)
            {
                sim.SetInput(sim.Fog.CenterX - sim.PlayerIsland.CenterX, sim.Fog.CenterY - sim.PlayerIsland.CenterY);
                sim.Step();
                seen.AddRange(sim.DrainEvents().Select(simEvent => simEvent.Type));
            }

            Assert.IsFalse(sim.Hidden);
            Assert.AreEqual(SimStatus.Running, sim.Status);
            Assert.Contains(SimEventType.Showed, seen);

            // Sailing on through the whole depth of the fog leaves the battle.
            double progress = 0;
            for (int tick = 0; tick < 6000 && sim.Status == SimStatus.Running; tick += 1)
            {
                SailOut(sim);
                sim.Step();
                Assert.GreaterOrEqual(sim.ExitProgress, progress - 0.05);
                progress = sim.ExitProgress;
            }

            Assert.AreEqual(SimStatus.Retreated, sim.Status);
            Assert.AreEqual(CleanupOutcome.Retreated, sim.Result().Outcome);
        }

        [Test]
        public void EnemyIslandsDoNotFollowIntoTheFog()
        {
            CleanupSim sim = CleanupSim.Create(Level(Defender(out _), 3, Knights(4)), 21);

            // The island sails in a third of the way and waits there.
            for (int tick = 0; tick < 3000 && sim.ExitProgress < 0.33; tick += 1)
            {
                SailOut(sim);
                sim.Step();
            }

            Assert.IsTrue(sim.Hidden);
            sim.SetInput(0, 0);

            for (int tick = 0; tick < 900 && sim.Status == SimStatus.Running; tick += 1)
            {
                // Held against the drift it still has, so that it stays where it hid.
                if (sim.ExitProgress > 0.5)
                {
                    sim.SetInput(sim.Fog.CenterX - sim.PlayerIsland.CenterX, sim.Fog.CenterY - sim.PlayerIsland.CenterY);
                }
                else
                {
                    sim.SetInput(0, 0);
                }

                sim.Step();

                foreach (SimIsland island in sim.Islands.Where(island => island.Side == CleanupSide.Enemy && !island.IsGone))
                {
                    Assert.LessOrEqual(sim.Fog.DepthAt(island.CenterX, island.CenterY) + island.Body.Radius, 1e-6, island.Label + " is in the fog at tick " + sim.Tick);
                }
            }

            Assert.AreEqual(SimStatus.Running, sim.Status);
            Assert.IsTrue(sim.Hidden);
        }
    }
}
