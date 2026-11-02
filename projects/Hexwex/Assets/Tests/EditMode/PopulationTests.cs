using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The population split into idle, army and workers, after the prototype's <c>core/population.ts</c>.</summary>
    public sealed class PopulationTests
    {
        private static Player Island(int population, int army, params BuildingId?[] buildings)
        {
            Player player = new Player { Id = "p", Army = army };
            player.Resources[ResourceId.Population] = population;
            for (int index = 0; index < buildings.Length; index += 1)
            {
                player.Hexes.Add(new HexTile
                {
                    Id = HexMath.HexId(index, 0),
                    Q = index,
                    Biome = BiomeId.Plains,
                    Building = buildings[index],
                    Workers = buildings[index].HasValue ? 1 : 0,
                });
            }

            return player;
        }

        [Test]
        public void TheIdleAreEveryoneNotInTheArmyOrABuilding()
        {
            Player player = Island(10, 3, BuildingId.Farm, null, BuildingId.Mine);
            PopulationSplit split = Population.Of(player);

            Assert.AreEqual(10, split.Total);
            Assert.AreEqual(3, split.Army);
            Assert.AreEqual(2, split.Workers);
            Assert.AreEqual(5, split.Idle);
        }

        [Test]
        public void DeathsTakeTheIdleThenTheWorkersFromTheLastHexThenTheArmy()
        {
            Player player = Island(6, 3, BuildingId.Farm, BuildingId.Mine);

            Population.LosePeople(player, 1);
            Assert.AreEqual(1, player.Hexes[0].Workers);
            Assert.AreEqual(1, player.Hexes[1].Workers);
            Assert.AreEqual(3, player.Army);

            Population.LosePeople(player, 1);
            Assert.AreEqual(1, player.Hexes[0].Workers);
            Assert.AreEqual(0, player.Hexes[1].Workers);
            Assert.AreEqual(3, player.Army);

            Population.LosePeople(player, 2);
            Assert.AreEqual(0, player.Hexes[0].Workers);
            Assert.AreEqual(2, player.Army);
            Assert.AreEqual(2, player.Resources[ResourceId.Population]);
        }

        [Test]
        public void FallenSoldiersLeaveTheArmyAndThePopulation()
        {
            Player player = Island(8, 4, BuildingId.Farm);

            Population.LoseSoldiers(player, 3);

            Assert.AreEqual(1, player.Army);
            Assert.AreEqual(5, player.Resources[ResourceId.Population]);
            Assert.AreEqual(1, player.Hexes[0].Workers);
        }

        [Test]
        public void AnUnstaffedBuildingRollsNoDie()
        {
            Player player = Island(5, 0, BuildingId.Farm, BuildingId.Farm);
            player.Hexes[1].Workers = 0;

            TaxPlan plan = TaxPlan.Roll(player, 1, "Mom010");

            Assert.AreEqual(1, plan.Rolls.Count);
            Assert.AreEqual(player.Hexes[0].Id, plan.Rolls[0].HexId);
        }

        [Test]
        public void ARemovedBuildingLeavesNoStaleWorkers()
        {
            Player player = Island(4, 0, BuildingId.Farm);
            player.Hexes[0].Building = null;

            Population.Settle(player);

            Assert.AreEqual(0, player.Hexes[0].Workers);
            Assert.AreEqual(4, Population.Of(player).Idle);
        }

        [Test]
        public void TheArmyTakesOnlyIdlePeople()
        {
            Player player = Island(5, 1, BuildingId.Farm);

            Population.SetArmySize(player, 10);
            Assert.AreEqual(4, player.Army);

            Population.SetArmySize(player, -2);
            Assert.AreEqual(0, player.Army);
        }

        [Test]
        public void TheBotsStaffTheirBuildingsInHexOrder()
        {
            Player player = Island(1, 0, BuildingId.Farm, BuildingId.Mine);
            player.Hexes[0].Workers = 0;
            player.Hexes[1].Workers = 0;

            Population.AutoStaff(player);

            Assert.AreEqual(1, player.Hexes[0].Workers);
            Assert.AreEqual(0, player.Hexes[1].Workers);
        }

        [Test]
        public void ANewBuildingTakesAnIdleWorkerAndPeopleMoveOnlyInTheBuildPhase()
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            Player human = session.Human;
            List<string> notices = new List<string>();
            session.Notice += notices.Add;

            // The player starts with three soldiers; the rest of the people are idle.
            Assert.AreEqual(IslandGen.HumanStartingArmy, human.Army);
            int idle = Population.Of(human).Idle;
            Assert.Greater(idle, 0);

            HexTile site = human.Hexes.First(hex => hex.Id != human.StrongholdHexId && Buildings.ForBiome(hex.Biome).Count > 0);
            foreach (ResourceId id in new[] { ResourceId.Stone, ResourceId.Wood, ResourceId.Hammers })
            {
                human.Resources[id] = 99;
            }

            Assert.IsTrue(session.Build(site.Id, Buildings.ForBiome(site.Biome)[0].Id));
            Assert.AreEqual(1, site.Workers);
            Assert.AreEqual(idle - 1, Population.Of(human).Idle);

            Assert.IsTrue(session.SetHexWorkers(site.Id, 0));
            Assert.AreEqual(idle, Population.Of(human).Idle);
            Assert.IsTrue(session.ChangeArmy(1));
            Assert.AreEqual(IslandGen.HumanStartingArmy + 1, human.Army);

            session.EndPhase();
            Assert.IsFalse(session.ChangeArmy(1));
            Assert.AreEqual("Жителей можно перераспределять только в фазе строительства", notices.Last());
        }

        [Test]
        public void TheStrongholdPaysPowerWithoutARollAndItsFacesLeaveToxicity()
        {
            Assert.IsTrue(Stronghold.Faces.All(face => face.Toxicity == face.Amount));
            Assert.AreEqual(ResourceId.Power, Stronghold.AlwaysYields.Single().Resource);
        }
    }
}
