using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>A session with more than one person at it: seats, readiness, and who acts on what.</summary>
    public sealed class SeatTests
    {
        private const string First = IslandGen.HumanPlayerId;
        private static readonly string Second = IslandGen.HumanSeatId(1);

        private static string Describe(Player player)
        {
            return player.Id + "|" + player.Nickname + "|" + player.Army + "|" + string.Join(" ", player.Hexes.Select(hex => hex.Id + ":" + hex.Biome + ":" + hex.Building + ":" + hex.Toxicity));
        }

        /// <summary>A game of two people, each with a stronghold, started.</summary>
        private static GameSession Started()
        {
            GameSession session = new GameSession("Mom010", new[] { "Alice", "Bob" });
            foreach (string id in session.HumanIds)
            {
                Player player = session.PlayerOf(id);
                Assert.IsTrue(session.PlaceStronghold(id, player.Hexes.First(hex => hex.Building == null).Id));
            }

            Assert.IsTrue(session.StartGame());

            return session;
        }

        /// <summary>Every person ends the phase; one still fighting retreats first.</summary>
        private static void EveryoneEnds(GameSession session)
        {
            foreach (string id in session.HumanIds.ToList())
            {
                Seat seat = session.SeatOf(id);
                if (seat.Battle != null && seat.Battle.Status == SimStatus.Running)
                {
                    seat.Battle.Retreat();
                    session.StepBattle(id, 1);
                }

                session.EndPhase(id);
            }
        }

        [Test]
        public void ASecondPersonTakesTheFirstRivalsSlot()
        {
            List<Player> single = IslandGen.CreatePlayers("Mom010");
            List<Player> pair = IslandGen.CreatePlayers("Mom010", new[] { "Mom010", "Bob" });

            Assert.AreEqual(IslandGen.MaxPlayers, pair.Count);
            Assert.AreEqual(Describe(single[0]), Describe(pair[0]));

            Player second = pair[1];
            Assert.AreEqual(Second, second.Id);
            Assert.AreEqual("Bob", second.Nickname);
            Assert.IsTrue(second.IsHuman);
            Assert.AreEqual(single[1].Color, second.Color);
            Assert.AreEqual(IslandGen.HumanStartingArmy, second.Army);
            Assert.IsFalse(second.Hexes.Any(hex => hex.Building.HasValue));

            // The rivals left over are the very same as in a single-player game.
            Assert.AreEqual(Describe(single[2]), Describe(pair[2]));
            Assert.AreEqual(Describe(single[3]), Describe(pair[3]));
        }

        [Test]
        public void ASinglePlayerSessionIsUnchangedBySeats()
        {
            GameSession plain = new GameSession("Mom010");
            GameSession seated = new GameSession("Mom010", new[] { "Mom010" });

            CollectionAssert.AreEqual(plain.Players.Select(Describe), seated.Players.Select(Describe));
            CollectionAssert.AreEqual(new[] { First }, plain.HumanIds);
        }

        [Test]
        public void TheGameStartsOnlyWhenEveryStrongholdStands()
        {
            GameSession session = new GameSession("Mom010", new[] { "Alice", "Bob" });
            Player alice = session.PlayerOf(First);
            Assert.IsTrue(session.PlaceStronghold(First, alice.Hexes[0].Id));

            Assert.IsFalse(session.StartGame());
            Assert.AreEqual(GameStage.Setup, session.Stage);

            Player bob = session.PlayerOf(Second);
            Assert.IsTrue(session.PlaceStronghold(Second, bob.Hexes[0].Id));
            Assert.IsTrue(session.StartGame());
            Assert.AreEqual(GameStage.Play, session.Stage);
        }

        [Test]
        public void ThePhaseWaitsForEveryone()
        {
            GameSession session = Started();

            Assert.IsTrue(session.EndPhase(First));
            Assert.AreEqual(Phase.Build, session.Phase);
            Assert.IsTrue(session.IsReady(First));
            CollectionAssert.AreEqual(new[] { Second }, session.WaitingFor());

            Assert.IsTrue(session.EndPhase(Second));
            Assert.AreEqual(Phase.Tax, session.Phase);
            Assert.IsFalse(session.IsReady(First));
            CollectionAssert.AreEquivalent(session.HumanIds, session.WaitingFor());
        }

        [Test]
        public void EachPersonActsOnTheirOwnIslandOnly()
        {
            GameSession session = Started();
            Player alice = session.PlayerOf(First);
            Player bob = session.PlayerOf(Second);
            bob.Resources[ResourceId.Stone] = 99;
            bob.Resources[ResourceId.Wood] = 99;
            bob.Resources[ResourceId.Hammers] = 99;
            int aliceStone = alice.Resources[ResourceId.Stone];

            HexTile site = bob.Hexes.First(hex => hex.Id != bob.StrongholdHexId && Buildings.ForBiome(hex.Biome).Count > 0);
            Assert.IsTrue(session.Build(Second, site.Id, Buildings.ForBiome(site.Biome)[0].Id));

            Assert.IsTrue(site.Building.HasValue);
            Assert.AreEqual(1, site.Workers);
            Assert.AreEqual(aliceStone, alice.Resources[ResourceId.Stone]);
            HexTile same = alice.FindHex(site.Id);
            Assert.IsTrue(same == null || !same.Building.HasValue);

            // A bot's seat takes no orders, and nor does an id that is not in the game.
            Player bot = session.Players.First(player => !player.IsHuman);
            Assert.IsFalse(session.Build(bot.Id, bot.Hexes[0].Id, BuildingId.Farm));
            Assert.IsFalse(session.EndPhase("nobody"));
        }

        [Test]
        public void ARefusalGoesOnlyToThePersonWhoTried()
        {
            GameSession session = Started();
            List<string> toFirst = new List<string>();
            List<(string, string)> routed = new List<(string, string)>();
            session.Notice += toFirst.Add;
            session.PlayerNotice += (id, message) => routed.Add((id, message));

            session.EndPhase(First);
            session.EndPhase(Second);
            Assert.IsFalse(session.ChangeArmy(Second, 1));

            Assert.IsTrue(routed.Any(item => item.Item1 == Second));
            Assert.IsEmpty(toFirst);
        }

        [Test]
        public void EverySeatRollsAndPicksItsOwnDice()
        {
            GameSession session = Started();
            Player bob = session.PlayerOf(Second);
            EveryoneEnds(session);
            Assert.AreEqual(Phase.Tax, session.Phase);

            TaxPlan plan = session.TaxPlanOf(Second);
            Assert.IsNotNull(plan);
            Assert.AreEqual(Second, plan.PlayerId);
            TaxRoll roll = plan.Rolls.Single(candidate => candidate.HexId == bob.StrongholdHexId);

            int other = (roll.RolledIndex + 1) % roll.Faces.Length;
            bob.Resources[ResourceId.Power] = 10;
            Assert.IsTrue(session.PickTaxFace(Second, roll.HexId, other));
            Assert.AreEqual(other, roll.ChosenIndex);
            Assert.AreEqual(session.TaxPlanOf(First).FindRoll(session.PlayerOf(First).StrongholdHexId).RolledIndex,
                session.TaxPlanOf(First).FindRoll(session.PlayerOf(First).StrongholdHexId).ChosenIndex);

            EveryoneEnds(session);
            Assert.AreEqual(Phase.Scout, session.Phase);
            Assert.IsNotNull(session.SeatOf(Second).LastPayouts);
            Assert.IsNotNull(session.SeatOf(First).LastPayouts);
        }

        [Test]
        public void TwoPeoplePlayWholeTurnsTogether()
        {
            GameSession session = Started();

            for (int turn = 1; turn <= 3 && session.Outcome == null; turn += 1)
            {
                int guard = 0;
                while (session.Turn == turn && session.Outcome == null && guard < 10)
                {
                    EveryoneEnds(session);
                    guard += 1;
                }

                Assert.Less(guard, 10, "the turn did not move on");
            }

            Assert.GreaterOrEqual(session.Turn, 3);
            Assert.AreEqual(Phase.Build, session.Phase);
        }

        [Test]
        public void AFallenPersonLeavesAndTheOthersPlayOn()
        {
            GameSession session = Started();
            Player bob = session.PlayerOf(Second);
            StructureHp.Set(bob.FindHex(bob.StrongholdHexId), StructureKind.Stronghold, 0);

            EveryoneEnds(session);

            Assert.IsNull(session.Outcome);
            Assert.IsTrue(bob.Eliminated);
            Assert.AreEqual(GameOutcomeReason.Ruined, bob.EliminatedBy);
            Assert.AreEqual(GameOutcomeKind.Defeat, session.OutcomeFor(Second).Kind);
            Assert.IsNull(session.OutcomeFor(First));

            // The phase no longer waits for him, and he can no longer act.
            Assert.AreEqual(Phase.Tax, session.Phase);
            CollectionAssert.AreEqual(new[] { First }, session.WaitingFor());
            Assert.IsFalse(session.EndPhase(Second));
            Assert.IsTrue(session.EndPhase(First));
            Assert.AreEqual(Phase.Scout, session.Phase);
        }

        [Test]
        public void TheLastPersonStandingWinsByElimination()
        {
            Player alice = new Player { Id = First, IsHuman = true };
            alice.Hexes.Add(new HexTile { Id = "0,0", Biome = BiomeId.Plains, Building = BuildingId.Farm, Workers = 1 });
            Player bob = new Player { Id = Second, IsHuman = true, StrongholdHexId = "0,0" };
            bob.Hexes.Add(new HexTile { Id = "0,0", Biome = BiomeId.Plains });
            List<Player> players = new List<Player> { alice, bob };
            string[] people = { First, Second };

            Assert.IsNull(GameOver.Check(players, people, 1));

            StructureHp.Set(bob.Hexes[0], StructureKind.Stronghold, 0);
            GameOutcome outcome = GameOver.Check(players, people, 2);

            Assert.IsNotNull(outcome);
            Assert.AreEqual(GameOutcomeKind.Victory, outcome.KindFor(First));
            Assert.AreEqual(GameOutcomeReason.Elimination, outcome.ReasonFor(First));
            Assert.AreEqual(GameOutcomeKind.Defeat, outcome.KindFor(Second));
            Assert.AreEqual(GameOutcomeReason.Ruined, outcome.ReasonFor(Second));
        }

        [Test]
        public void AConverterEndsTheGameForEveryone()
        {
            Player alice = new Player { Id = First, IsHuman = true };
            alice.Hexes.Add(new HexTile { Id = "0,0", Biome = BiomeId.Plains, Building = BuildingId.Converter });
            Player bob = new Player { Id = Second, IsHuman = true };
            bob.Hexes.Add(new HexTile { Id = "0,0", Biome = BiomeId.Plains, Building = BuildingId.Farm });

            GameOutcome outcome = GameOver.Check(new List<Player> { alice, bob }, new[] { First, Second }, 4);

            Assert.AreEqual(GameOutcomeKind.Victory, outcome.Kind);
            Assert.AreEqual(GameOutcomeKind.Defeat, outcome.KindFor(Second));
            Assert.AreEqual(GameOutcomeReason.RivalConverter, outcome.ReasonFor(Second));
        }
    }
}
