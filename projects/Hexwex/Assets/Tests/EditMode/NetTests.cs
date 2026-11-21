using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hexwex.Net;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>A network game over the loopback network: the table, the order of commands, and machines that stay in step.</summary>
    public sealed class NetTests
    {
        private LoopbackNetwork _network;
        private NetHost _host;
        private List<NetClient> _clients;
        private List<(int seat, int sequence)> _desyncs;

        [SetUp]
        public void SetUp()
        {
            _network = new LoopbackNetwork();
            _host = new NetHost(_network.Host, "Alice", "Mom010");
            _clients = new List<NetClient>();
            _desyncs = new List<(int, int)>();
            _host.Desync += (seat, sequence, ours, theirs) => _desyncs.Add((seat, sequence));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (NetClient client in _clients)
            {
                client.Dispose();
            }

            _host.Dispose();
        }

        private NetClient Join(string name)
        {
            NetClient client = new NetClient(_network.Connect(), name);
            _clients.Add(client);
            Pump();

            return client;
        }

        /// <summary>Polls every machine until nothing is left in flight.</summary>
        private void Pump()
        {
            for (int round = 0; round < 8; round += 1)
            {
                _host.Poll();
                foreach (NetClient client in _clients)
                {
                    client.Poll();
                }
            }
        }

        private IEnumerable<NetGame> Machines()
        {
            yield return _host;
            foreach (NetClient client in _clients.Where(client => !client.IsClosed))
            {
                yield return client;
            }
        }

        private void AssertInStep()
        {
            uint expected = _host.Session.Checksum();
            foreach (NetGame machine in Machines())
            {
                Assert.AreEqual(_host.Sequence, machine.Sequence, "seat " + machine.Seat + " is behind");
                Assert.AreEqual(expected, machine.Session.Checksum(), "seat " + machine.Seat + " drifted");
            }

            Assert.IsEmpty(_desyncs);
        }

        /// <summary>A table of three, each with a stronghold, started.</summary>
        private void StartThree()
        {
            Join("Bob");
            Join("Carol");
            _host.StartGame();
            Pump();

            foreach (NetGame machine in Machines())
            {
                Player own = machine.Session.PlayerOf(machine.LocalPlayerId);
                machine.Submit(NetCommand.PlaceStronghold(own.Hexes.First(hex => hex.Building == null).Id));
            }

            Pump();
            _host.Submit(NetCommand.StartGame());
            Pump();
        }

        /// <summary>Every machine ends the phase. One in a battle fights nothing: it sails out and sends the result.</summary>
        private void EveryoneEnds()
        {
            foreach (NetGame machine in Machines())
            {
                Seat seat = machine.Session.SeatOf(machine.LocalPlayerId);
                if (seat.Battle != null)
                {
                    seat.Battle.Retreat();
                    machine.Session.StepBattle(machine.LocalPlayerId, 1);
                    machine.Submit(NetCommand.EndBattle(seat.BattleResult));
                }
                else
                {
                    machine.Submit(NetCommand.EndPhase());
                }
            }

            Pump();
        }

        [Test]
        public void ACommandSurvivesTheWire()
        {
            NetCommand command = NetCommand.CleanseSoil("1,0", "0,-1");
            command.PlayerId = "human2";
            command.Result = new CleanupResult
            {
                Outcome = CleanupOutcome.Won,
                TotalIslands = 2,
                ClearedIslands = 2,
                ManaSpent = 3,
                Annexed = new List<AnnexedHex> { new AnnexedHex { Q = 4, R = -1, Biome = BiomeId.Swamp, Toxicity = 12 } },
                Survivors = Units.BuildRoster(2),
                Lost = Units.BuildRoster(1),
                Structures = new List<StructureResult> { new StructureResult { HexId = "0,0", Hp = 300, StartHp = 400, MaxHp = 600 } },
                DestroyedHexIds = new List<string> { "2,1" },
            };

            Message message = Wire.Decode(Wire.Command(7, command));

            Assert.AreEqual(MessageKind.Command, message.Kind);
            Assert.AreEqual(7, message.Sequence);
            Assert.AreEqual(command.ToString(), message.Command.ToString());
            Assert.AreEqual("0,-1", message.Command.Second);
            CleanupResult result = message.Command.Result;
            Assert.AreEqual(CleanupOutcome.Won, result.Outcome);
            Assert.AreEqual(BiomeId.Swamp, result.Annexed.Single().Biome);
            Assert.AreEqual(2, result.Survivors.Count);
            Assert.AreEqual(Units.ArmyUnit, result.Lost.Single().Key);
            Assert.AreEqual(300, result.Structures.Single().Hp);
            Assert.AreEqual("2,1", result.DestroyedHexIds.Single());
        }

        [Test]
        public void RubbishIsRefusedNotMisread()
        {
            Assert.Throws<InvalidDataException>(() => Wire.Decode(new byte[] { 99, 1, 2 }));
            Assert.Throws<InvalidDataException>(() => Wire.Decode(new byte[] { (byte)MessageKind.Hello, 1 }));

            // A list that claims more items than the message holds.
            byte[] lobby = Wire.Lobby("seed", new[] { "a" }, 1);
            lobby[1 + 4 + 4] = 0xFF;
            Assert.Throws<InvalidDataException>(() => Wire.Decode(lobby));
        }

        [Test]
        public void ClientsTakeTheNextSeatsAndSeeTheTable()
        {
            NetClient bob = Join("Bob");
            NetClient other = Join("Bob");

            CollectionAssert.AreEqual(new[] { "Alice", "Bob", "Bob 2" }, _host.Names);
            Assert.AreEqual(1, bob.Seat);
            Assert.AreEqual(2, other.Seat);
            CollectionAssert.AreEqual(_host.Names, other.Names);
            Assert.AreEqual("Mom010", bob.Seed);

            // One who leaves before the start frees the seat, and the seats behind move up.
            bob.Leave();
            Pump();
            CollectionAssert.AreEqual(new[] { "Alice", "Bob 2" }, _host.Names);
            Assert.AreEqual(1, other.Seat);
        }

        [Test]
        public void EveryMachineStartsTheSameGame()
        {
            Join("Bob");
            _host.SetSeed("Another");
            Pump();
            _host.StartGame();
            Pump();

            NetClient bob = _clients[0];
            Assert.IsNotNull(bob.Session);
            Assert.AreEqual("Another", bob.Session.Nickname);
            Assert.AreEqual("human2", bob.LocalPlayerId);
            Assert.AreEqual("Bob", bob.Session.PlayerOf("human2").Nickname);
            AssertInStep();
        }

        [Test]
        public void ThreeMachinesPlayTurnsInStep()
        {
            StartThree();
            Assert.IsTrue(Machines().All(machine => machine.Session.Stage == GameStage.Play));
            AssertInStep();

            // Bob builds on his own island; the build reaches everyone.
            NetClient bob = _clients[0];
            Player bobsIsland = bob.Session.PlayerOf(bob.LocalPlayerId);
            HexTile site = bobsIsland.Hexes.First(hex => hex.Id != bobsIsland.StrongholdHexId && Buildings.ForBiome(hex.Biome).Any(building => Buildings.CanAfford(bobsIsland.Resources, building, 0)));
            BuildingId choice = Buildings.ForBiome(site.Biome).First(building => Buildings.CanAfford(bobsIsland.Resources, building, 0)).Id;
            bob.Submit(NetCommand.Build(site.Id, choice));
            Pump();
            Assert.AreEqual(choice, _host.Session.PlayerOf(bob.LocalPlayerId).FindHex(site.Id).Building);
            AssertInStep();

            for (int turn = 1; turn <= 3; turn += 1)
            {
                for (int guard = 0; _host.Session.Turn == turn && guard < 8; guard += 1)
                {
                    EveryoneEnds();
                    AssertInStep();
                }

                Assert.AreEqual(turn + 1, _host.Session.Turn, "turn " + turn + " did not end");
            }
        }

        [Test]
        public void AClientActsOnlyForItsOwnSeat()
        {
            StartThree();
            NetClient bob = _clients[0];

            // Bob claims to be the host: the host writes Bob's seat into the command anyway.
            bob.Submit(NetCommand.ChangeArmy(1));
            int hostArmy = _host.Session.PlayerOf("human").Army;
            int bobArmy = _host.Session.PlayerOf("human2").Army;
            Pump();

            Assert.AreEqual(hostArmy, _host.Session.PlayerOf("human").Army);
            Assert.AreEqual(bobArmy + 1, _host.Session.PlayerOf("human2").Army);
            Assert.AreEqual("human2", _host.Log.Last().PlayerId);
            AssertInStep();
        }

        [Test]
        public void ARefusalReachesOnlyThePersonWhoTried()
        {
            StartThree();
            NetClient bob = _clients[0];
            List<string> hostNotices = new List<string>();
            List<string> bobNotices = new List<string>();
            _host.Notice += hostNotices.Add;
            bob.Notice += bobNotices.Add;

            bob.Submit(NetCommand.Research(TechId.Swords));
            Pump();

            Assert.IsNotEmpty(bobNotices);
            Assert.IsEmpty(hostNotices);
        }

        [Test]
        public void OneWhoLeavesAGameResignsAndTheOthersPlayOn()
        {
            StartThree();
            NetClient carol = _clients[1];
            carol.Leave();
            Pump();

            Assert.IsTrue(_host.Session.PlayerOf("human3").Eliminated);
            Assert.AreEqual(GameOutcomeReason.Left, _host.Session.PlayerOf("human3").EliminatedBy);

            // The phases no longer wait for her.
            EveryoneEnds();
            Assert.AreEqual(Phase.Tax, _host.Session.Phase);
            AssertInStep();
        }

        [Test]
        public void LateAndSurplusGuestsAreTurnedAway()
        {
            Join("Bob");
            Join("Carol");
            Join("Dave");
            NetClient fifth = Join("Eve");

            Assert.IsTrue(fifth.IsClosed);
            Assert.AreEqual(IslandGen.MaxPlayers, _host.Names.Count);

            _host.StartGame();
            Pump();
            NetClient late = Join("Frank");
            Assert.IsTrue(late.IsClosed);
            Assert.IsNull(late.Session);
        }

        [Test]
        public void AMachineThatDriftsIsCaught()
        {
            StartThree();
            NetClient bob = _clients[0];

            // Something that is not a command changes Bob's copy alone.
            bob.Session.PlayerOf("human").Resources[ResourceId.Stone] += 5;
            EveryoneEnds();

            Assert.IsTrue(_desyncs.Any(item => item.seat == 1));
        }
    }
}
