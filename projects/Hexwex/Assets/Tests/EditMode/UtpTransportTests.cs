using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Hexwex.Net;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The network game over real UDP sockets on this machine, through Unity Transport.</summary>
    public sealed class UtpTransportTests
    {
        /// <summary>Far from the game's own port, so a running game does not get in the way.</summary>
        private const ushort Port = 47777;

        private static void PumpUntil(Func<bool> done, IEnumerable<NetGame> machines, int milliseconds = 5000)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (!done() && clock.ElapsedMilliseconds < milliseconds)
            {
                foreach (NetGame machine in machines)
                {
                    machine.Poll();
                }

                Thread.Sleep(2);
            }

            Assert.IsTrue(done(), "timed out after " + milliseconds + " ms");
        }

        [Test]
        public void AHostAndAClientPlayInStepOverSockets()
        {
            using (NetHost host = new NetHost(UtpTransport.Listen(Port), "Alice", "Mom010"))
            using (NetClient client = new NetClient(UtpTransport.Connect("127.0.0.1", Port), "Bob"))
            {
                NetGame[] machines = { host, client };

                PumpUntil(() => host.Names.Count == 2 && client.Seat == 1, machines);
                CollectionAssert.AreEqual(new[] { "Alice", "Bob" }, client.Names);

                host.StartGame();
                PumpUntil(() => client.Session != null, machines);

                foreach (NetGame machine in machines)
                {
                    Player own = machine.Session.PlayerOf(machine.LocalPlayerId);
                    machine.Submit(NetCommand.PlaceStronghold(own.Hexes.First(hex => hex.Building == null).Id));
                }

                PumpUntil(() => client.Sequence == 2, machines);
                host.Submit(NetCommand.StartGame());
                PumpUntil(() => client.Session.Stage == GameStage.Play, machines);

                // A burst bigger than the reliable window still arrives whole and in order.
                for (int index = 0; index < 150; index += 1)
                {
                    client.Submit(NetCommand.ChangeArmy(index % 2 == 0 ? 1 : -1));
                }

                PumpUntil(() => client.Sequence == host.Sequence && host.Sequence >= 153, machines, 15000);

                foreach (NetGame machine in machines)
                {
                    machine.Submit(NetCommand.EndPhase());
                }

                PumpUntil(() => client.Session.Phase == Phase.Tax && client.Sequence == host.Sequence, machines);
                Assert.AreEqual(host.Session.Checksum(), client.Session.Checksum());
            }
        }

        [Test]
        public void ALostHostClosesTheClient()
        {
            NetHost host = new NetHost(UtpTransport.Listen(Port + 1), "Alice", "Mom010");
            using (NetClient client = new NetClient(UtpTransport.Connect("127.0.0.1", (ushort)(Port + 1)), "Bob"))
            {
                string reason = null;
                client.Closed += text => reason = text;
                PumpUntil(() => client.Seat == 1, new NetGame[] { host, client });

                host.Dispose();
                PumpUntil(() => client.IsClosed, new NetGame[] { client }, 15000);
                Assert.IsNotNull(reason);
            }
        }
    }
}
