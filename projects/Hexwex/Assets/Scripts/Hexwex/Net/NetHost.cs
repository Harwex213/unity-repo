using System.Collections.Generic;
using System.IO;
using Hexwex.Core;

namespace Hexwex.Net
{
    /// <summary>
    /// The host of a network game. It plays seat 0 itself and keeps the table:
    /// clients join until it starts the game, up to <see cref="IslandGen.MaxPlayers"/>
    /// in all, and bots take the seats left over.
    ///
    /// Once the game runs, every action from every seat comes here. The host gives
    /// it the next number, sends it to every client and plays it on its own copy.
    /// Each client plays the same numbers in the same order. A client may act only
    /// for its own seat: the host writes the seat into the action from the
    /// connection it came on. After a phase moves on each client sends its
    /// checksum, and the host compares it with its own.
    ///
    /// A client that leaves before the start frees its seat. One that leaves a
    /// running game resigns it, so the others are not kept waiting.
    /// </summary>
    public sealed class NetHost : NetGame
    {
        /// <summary>How many recent checksums the host keeps to compare against.</summary>
        private const int ChecksumMemory = 256;

        /// <summary>The connection of each seat after seat 0, in seat order.</summary>
        private readonly List<int> _seats = new List<int>();
        /// <summary>Connections that are open but have not said hello yet.</summary>
        private readonly HashSet<int> _pending = new HashSet<int>();
        private readonly Dictionary<int, uint> _checksums = new Dictionary<int, uint>();
        private readonly Queue<int> _checksumOrder = new Queue<int>();

        public NetHost(INetTransport transport, string hostName, string seed)
            : base(transport)
        {
            Seed = string.IsNullOrWhiteSpace(seed) ? GameSession.DefaultNickname : seed.Trim();
            Names = new List<string> { string.IsNullOrWhiteSpace(hostName) ? "Хост" : hostName.Trim() };
            Seat = 0;
        }

        public override bool IsHost
        {
            get { return true; }
        }

        /// <summary>Every command played so far, in order. A new copy of the game played from it is the same game.</summary>
        public List<NetCommand> Log { get; } = new List<NetCommand>();

        public bool IsStarted
        {
            get { return Session != null; }
        }

        /// <summary>Changes the world seed while the table is still open.</summary>
        public void SetSeed(string seed)
        {
            if (IsStarted || string.IsNullOrWhiteSpace(seed))
            {
                return;
            }

            Seed = seed.Trim();
            SendLobby();
            RaiseLobbyChanged();
        }

        /// <summary>Closes the table and starts the game for everyone at it.</summary>
        public void StartGame()
        {
            if (IsStarted)
            {
                return;
            }

            for (int index = 0; index < _seats.Count; index += 1)
            {
                Transport.Send(_seats[index], Wire.Start(Seed, Names, index + 1));
            }

            // Strangers who have not said hello by now are too late.
            foreach (int connection in _pending)
            {
                Transport.Send(connection, Wire.Refused("Игра уже началась"));
                Transport.Disconnect(connection);
            }

            _pending.Clear();
            Begin(Seed, new List<string>(Names), 0);
        }

        public override void Submit(NetCommand command)
        {
            if (!IsStarted || IsClosed)
            {
                return;
            }

            command.PlayerId = LocalPlayerId;
            Order(command);
        }

        protected override void OnConnected(int connection)
        {
            _pending.Add(connection);
        }

        protected override void OnDisconnected(int connection)
        {
            _pending.Remove(connection);
            int index = _seats.IndexOf(connection);
            if (index < 0)
            {
                return;
            }

            if (!IsStarted)
            {
                // Before the start the seat is simply freed, and the seats behind it move up.
                _seats.RemoveAt(index);
                Names.RemoveAt(index + 1);
                SendLobby();
                RaiseLobbyChanged();

                return;
            }

            // In a running game the seat resigns, and the connection is forgotten.
            _seats[index] = -1;
            Order(new NetCommand { Kind = CommandKind.Resign, PlayerId = IslandGen.HumanSeatId(index + 1) });
        }

        protected override void OnReceived(int connection, byte[] data)
        {
            Message message;
            try
            {
                message = Wire.Decode(data);
            }
            catch (InvalidDataException)
            {
                // A client that sends rubbish is shown the door.
                Transport.Disconnect(connection);

                return;
            }

            switch (message.Kind)
            {
                case MessageKind.Hello:
                    Welcome(connection, message);
                    break;
                case MessageKind.Submit:
                    FromClient(connection, message.Command);
                    break;
                case MessageKind.Checksum:
                    Compare(connection, message.Sequence, message.Hash);
                    break;
            }
        }

        private void Welcome(int connection, Message hello)
        {
            if (!_pending.Remove(connection))
            {
                return;
            }

            string refusal = hello.Protocol != Wire.Protocol ? "Другая версия игры"
                : IsStarted ? "Игра уже началась"
                : Names.Count >= IslandGen.MaxPlayers ? "Все места заняты"
                : null;
            if (refusal != null)
            {
                Transport.Send(connection, Wire.Refused(refusal));
                Transport.Disconnect(connection);

                return;
            }

            _seats.Add(connection);
            Names.Add(UniqueName(hello.Name, _seats.Count + 1));
            SendLobby();
            RaiseLobbyChanged();
        }

        /// <summary>A name nobody else at the table has: "Bob" twice becomes "Bob" and "Bob 2".</summary>
        private string UniqueName(string wanted, int seatNumber)
        {
            string name = string.IsNullOrWhiteSpace(wanted) ? "Игрок " + seatNumber : wanted.Trim();
            if (name.Length > 24)
            {
                name = name.Substring(0, 24);
            }

            string unique = name;
            for (int copy = 2; Names.Contains(unique); copy += 1)
            {
                unique = name + " " + copy;
            }

            return unique;
        }

        private void SendLobby()
        {
            for (int index = 0; index < _seats.Count; index += 1)
            {
                Transport.Send(_seats[index], Wire.Lobby(Seed, Names, index + 1));
            }
        }

        private void FromClient(int connection, NetCommand command)
        {
            int index = _seats.IndexOf(connection);
            if (!IsStarted || index < 0 || command == null || command.Kind == CommandKind.StartGame)
            {
                return;
            }

            // A client acts for its own seat, whatever the command says.
            command.PlayerId = IslandGen.HumanSeatId(index + 1);
            Order(command);
        }

        /// <summary>Gives the command the next number, sends it to every client and plays it here.</summary>
        private void Order(NetCommand command)
        {
            int sequence = Sequence + 1;
            Log.Add(command);
            byte[] data = Wire.Command(sequence, command);
            foreach (int connection in _seats)
            {
                if (connection >= 0)
                {
                    Transport.Send(connection, data);
                }
            }

            Phase phase = Session.Phase;
            int turn = Session.Turn;
            Play(sequence, command);
            if (IsChecksumPoint(phase, turn, Session))
            {
                Remember(sequence, Session.Checksum());
            }
        }

        private void Remember(int sequence, uint hash)
        {
            _checksums[sequence] = hash;
            _checksumOrder.Enqueue(sequence);
            while (_checksumOrder.Count > ChecksumMemory)
            {
                _checksums.Remove(_checksumOrder.Dequeue());
            }
        }

        private void Compare(int connection, int sequence, uint theirs)
        {
            int index = _seats.IndexOf(connection);
            if (index >= 0 && _checksums.TryGetValue(sequence, out uint ours) && ours != theirs)
            {
                RaiseDesync(index + 1, sequence, ours, theirs);
            }
        }
    }
}
