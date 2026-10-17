using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    public sealed class GameRulesTests
    {
        private static HexTile Tile(BiomeId biome, int toxicity = 0, BuildingId? building = null, int q = 0, int r = 0)
        {
            return new HexTile { Id = HexMath.HexId(q, r), Q = q, R = r, Biome = biome, Toxicity = toxicity, Building = building };
        }

        private static string Describe(Player player)
        {
            return string.Join(" ", player.Hexes.Select(hex => hex.Id + ":" + hex.Biome + ":" + hex.Building + ":" + hex.Toxicity));
        }

        [Test]
        public void TheSameNicknameGrowsTheSameIslands()
        {
            List<Player> first = IslandGen.CreatePlayers("Mom010");
            List<Player> second = IslandGen.CreatePlayers("Mom010");
            List<Player> other = IslandGen.CreatePlayers("Mom011");

            Assert.AreEqual(4, first.Count);
            for (int index = 0; index < first.Count; index += 1)
            {
                Assert.AreEqual(Describe(first[index]), Describe(second[index]));
            }

            Assert.AreNotEqual(Describe(first[0]), Describe(other[0]));
        }

        [TestCase("Mom010")]
        [TestCase("Остров")]
        [TestCase("a")]
        public void AnIslandIsOnePlayablePiece(string nickname)
        {
            foreach (Player player in IslandGen.CreatePlayers(nickname))
            {
                // The centre and the two inner rings always stay: 1 + 6 + 12.
                Assert.GreaterOrEqual(player.Hexes.Count, 19);
                Assert.LessOrEqual(player.Hexes.Count, 37);
                Assert.IsTrue(HexMath.IsConnected(player.Hexes.Select(hex => hex.Axial).ToList()));
                Assert.AreEqual(player.Hexes.Count, player.Hexes.Select(hex => hex.Id).Distinct().Count());
                Assert.IsTrue(player.Hexes.Any(hex => Buildings.CanBuildOn(Buildings.Get(BuildingId.Mine), hex.Biome)));
                Assert.IsTrue(player.Hexes.Any(hex => Buildings.CanBuildOn(Buildings.Get(BuildingId.Sawmill), hex.Biome)));
                Assert.IsTrue(player.Hexes.Any(hex => Buildings.CanBuildOn(Buildings.Get(BuildingId.Farm), hex.Biome)));
            }
        }

        [Test]
        public void ToxicityCutsTheYield()
        {
            Face food = new Face(ResourceId.Food, 5, 0);
            Face stone = new Face(ResourceId.Stone, 3, 2);

            Assert.AreEqual(5, Tax.EffectiveYield(food, Tile(BiomeId.Hills)));
            // 5 * 0.9 = 4.5, and JavaScript rounds a half up.
            Assert.AreEqual(5, Tax.EffectiveYield(food, Tile(BiomeId.Hills, 10)));
            Assert.AreEqual(0, Tax.EffectiveYield(food, Tile(BiomeId.Hills, 50)));
            Assert.AreEqual(2, Tax.EffectiveYield(stone, Tile(BiomeId.Hills, 50)));
            Assert.AreEqual(0, Tax.EffectiveYield(stone, Tile(BiomeId.Hills, 100)));
            Assert.AreEqual(8, Tax.ToxicityGain(stone, Tile(BiomeId.Hills)));
            Assert.AreEqual(3, Tax.ToxicityGain(stone, Tile(BiomeId.Hills, 97)));
        }

        [Test]
        public void FiltersAndIrrigationChangeThePayout()
        {
            TechEffects effects = Techs.Effects(new[] { TechId.Filters, TechId.Irrigation });

            Tax.FacePayout(new Face(ResourceId.Food, 4, 1), Tile(BiomeId.Grassland), effects, out int amount, out int toxicity);

            Assert.AreEqual(5, amount);
            Assert.AreEqual(3, toxicity);
        }

        [Test]
        public void ABuildingsDieIsItsBaseFacesPlusTheBiomeFace()
        {
            Building mine = Buildings.Get(BuildingId.Mine);

            Assert.AreEqual(5, Buildings.FacesOn(mine, BiomeId.Volcano).Length);
            Assert.AreEqual(10, Buildings.FacesOn(mine, BiomeId.Volcano)[4].Amount);
            Assert.IsFalse(Buildings.CanBuildOn(mine, BiomeId.Forrest));
            Assert.AreEqual(BuildingId.Sawmill, Buildings.BestForBiome(BiomeId.Forrest).Id);
            Assert.IsFalse(Buildings.ForBiome(BiomeId.Grassland).Any(building => building.Trophy));
        }

        [Test]
        public void SwappingAFaceCostsPowerAndTheRolledFaceIsFree()
        {
            Player player = new Player { Id = "p", Resources = ResourceTable.CreateStartingPool() };
            player.Hexes.Add(Tile(BiomeId.Volcano, 0, BuildingId.Mine));
            TaxPlan plan = TaxPlan.Roll(player, 1, "seed");
            TaxRoll roll = plan.Rolls.Single();
            int other = (roll.RolledIndex + 1) % 4;

            Assert.AreEqual(4, roll.BiomeFaceIndex);
            Assert.AreEqual(0, plan.PowerSpent());
            Assert.IsNull(plan.PickRefusal(player, roll.HexId, other));

            plan.Pick(roll.HexId, other);
            Assert.AreEqual(1, plan.PowerSpent());
            Assert.AreEqual(1, plan.PowerLeft(player));

            if (roll.RolledIndex != 4)
            {
                plan.Pick(roll.HexId, 4);
                Assert.AreEqual(2, plan.PowerSpent());
            }

            player.Resources[ResourceId.Power] = 0;
            plan.Pick(roll.HexId, roll.RolledIndex);
            Assert.IsNotNull(plan.PickRefusal(player, roll.HexId, other));
        }

        [Test]
        public void CollectingPaysTheYieldDirtiesTheHexAndFillsTheMeter()
        {
            Player player = new Player { Id = "p" };
            HexTile hex = Tile(BiomeId.Volcano, 0, BuildingId.Mine);
            player.Hexes.Add(hex);
            TaxPlan plan = TaxPlan.Roll(player, 1, "seed");
            plan.Rolls[0].ChosenIndex = 4;
            plan.Rolls[0].RolledIndex = 4;

            plan.Collect(player, TechEffects.None);

            Assert.AreEqual(10, player.Resources[ResourceId.Stone]);
            Assert.AreEqual(20, hex.Toxicity);
            Assert.AreEqual(50, player.ToxicMeter);
        }

        [Test]
        public void TheSlotStaysOffBelowTheFirstZoneAndReplaysExactly()
        {
            Assert.AreEqual(0, ToxicSlot.MeterLevel(249));
            Assert.AreEqual(1, ToxicSlot.MeterLevel(250));
            Assert.AreEqual(3, ToxicSlot.MeterLevel(1000));

            Player calm = new Player { Id = "p", ToxicMeter = 100 };
            Assert.IsNull(ToxicSlot.Roll(calm, 3, "seed"));

            SlotSpin first = ToxicSlot.Roll(new Player { Id = "p", ToxicMeter = 1000, Resources = ResourceTable.CreateStartingPool() }, 3, "seed");
            SlotSpin second = ToxicSlot.Roll(new Player { Id = "p", ToxicMeter = 1000, Resources = ResourceTable.CreateStartingPool() }, 3, "seed");

            Assert.AreEqual(first.Reels, second.Reels);
            Assert.AreEqual(first.Event.Id, second.Event.Id);
            Assert.AreEqual(3, first.Level);
            // A full meter forces one skull, so the spin cannot be pure luck.
            Assert.GreaterOrEqual(first.BadCount, 1);
            Assert.AreEqual(SlotSymbol.Skull, first.Reels[first.ForcedReel]);
        }

        [Test]
        public void ARuinedStrongholdIsADefeatAndRepairsAQuarterPerTurn()
        {
            Player human = new Player { Id = IslandGen.HumanPlayerId, IsHuman = true, StrongholdHexId = "0,0" };
            human.Hexes.Add(Tile(BiomeId.Hills));
            Player rival = new Player { Id = "r" };
            rival.Hexes.Add(Tile(BiomeId.Hills, 0, BuildingId.Farm));
            List<Player> players = new List<Player> { human, rival };

            Assert.IsNull(GameOver.Check(players, human.Id, 1));
            Assert.IsTrue(rival.HasHadBuildings);

            StructureHp.Set(human.Hexes[0], StructureKind.Stronghold, 0);
            Assert.IsNull(HexDie.On(human, human.Hexes[0]));
            Assert.AreEqual(GameOutcomeReason.Ruined, GameOver.Check(players, human.Id, 1).Reason);

            StructureHp.RepairIsland(human, null);
            Assert.AreEqual(150, human.Hexes[0].DamagedHp);
            Assert.IsNull(GameOver.Check(players, human.Id, 1));

            rival.Hexes[0].Building = null;
            Assert.AreEqual(GameOutcomeReason.Elimination, GameOver.Check(players, human.Id, 2).Reason);
        }

        [Test]
        public void ASessionPlaysATurnOfBuildAndTax()
        {
            GameSession session = new GameSession("Mom010");
            Player human = session.Human;
            List<string> notices = new List<string>();
            session.Notice += notices.Add;

            Assert.IsTrue(session.Players.Where(player => !player.IsHuman).All(player => player.StrongholdHexId != null));
            Assert.IsFalse(session.StartGame());
            Assert.AreEqual(1, notices.Count);

            Assert.IsTrue(session.PlaceStronghold("0,0"));
            Assert.IsTrue(session.StartGame());

            HexTile site = human.Hexes.First(hex => hex.Id != "0,0" && Buildings.ForBiome(hex.Biome).Count > 0);
            Building building = Buildings.BestForBiome(site.Biome);
            int stoneBefore = human.Resources[ResourceId.Stone];

            Assert.IsTrue(session.Build(site.Id, building.Id));
            Assert.AreEqual(stoneBefore - building.Cost.Stone, human.Resources[ResourceId.Stone]);
            Assert.IsFalse(session.Build(site.Id, building.Id));
            Assert.IsFalse(session.Build("0,0", building.Id));

            session.EndPhase();
            Assert.AreEqual(Phase.Tax, session.Phase);
            Assert.AreEqual(2, session.HumanTaxPlan.Rolls.Count);

            int powerBefore = human.Resources[ResourceId.Power];
            session.EndPhase();

            Assert.AreEqual(1, session.Turn);
            Assert.AreEqual(Phase.Scout, session.Phase);
            Assert.IsNull(session.TaxPlans);
            // The stronghold pays one power on top of its roll.
            Assert.AreEqual(powerBefore + 1, human.Resources[ResourceId.Power]);
            Assert.AreEqual(3, session.LastPayouts.Count);
            Assert.IsNull(session.Outcome);

            session.EndPhase();

            Assert.AreEqual(2, session.Turn);
            Assert.AreEqual(Phase.Build, session.Phase);
        }

        [Test]
        public void SoilCleansingTakesOneHexAndCleansAnother()
        {
            GameSession session = new GameSession("Mom010");
            session.PlaceStronghold("0,0");
            session.StartGame();
            Player human = session.Human;

            Assert.IsNotNull(session.SoilCleanseBlock());

            HexTile dirty = human.FindHex("1,0");
            dirty.Toxicity = 60;
            HexTile sacrifice = human.Hexes.First(hex => hex.Id != "0,0" && hex.Id != "1,0" && BuildRules.SacrificeRefusal(human, hex) == null);
            int count = human.Hexes.Count;

            Assert.IsNull(session.SoilCleanseBlock());
            Assert.IsFalse(session.CleanseSoil("0,0", dirty.Id));
            Assert.IsTrue(session.CleanseSoil(sacrifice.Id, dirty.Id));
            Assert.AreEqual(0, dirty.Toxicity);
            Assert.AreEqual(count - 1, human.Hexes.Count);
            Assert.IsNotNull(session.SoilCleanseBlock());
        }
    }
}
