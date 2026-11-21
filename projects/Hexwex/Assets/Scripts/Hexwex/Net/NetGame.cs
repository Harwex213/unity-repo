using System;
using System.Collections.Generic;
using Hexwex.Core;

namespace Hexwex.Net
{
    /// <summary>
    /// One machine's side of a network game, host or client. Before the start it
    /// holds the lobby: the world seed and the names at the table. After the start
    /// it holds this machine's copy of the <see cref="GameSession"/> and the seat
    /// played here. The game screen reads <see cref="Session"/> as it reads a
    /// single-player session, and sends its actions through <see cref="Submit"/>.
    /// </summary>
    public abstract class NetGame : IDisposable
    {
        protected readonly INetTransport Transport;

        protected NetGame(INetTransport transport)
        {
            Transport = transport;
            Transport.Received += OnReceived;
            Transport.Connected += OnConnected;
            Transport.Disconnected += OnDisconnected;
        }

        /// <summary>The world seed of the game.</summary>
        public string Seed { get; protected set; }
        /// <summary>The names at the table, in seat order. Seat 0 is the host.</summary>
        public List<string> Names { get; protected set; } = new List<string>();
        /// <summary>The seat played on this machine, or -1 before the host has given one.</summary>
        public int Seat { get; protected set; } = -1;
        /// <summary>This machine's copy of the game, from the start on.</summary>
        public GameSession Session { get; protected set; }
        /// <summary>The number of the last command played here.</summary>
        public int Sequence { get; protected set; }
        public bool IsClosed { get; protected set; }

        /// <summary>The player played on this machine.</summary>
        public string LocalPlayerId
        {
            get { return Seat >= 0 ? IslandGen.HumanSeatId(Seat) : null; }
        }

        public abstract bool IsHost { get; }

        /// <summary>The table changed before the start: someone came or went.</summary>
        public event Action LobbyChanged;
        /// <summary>The game began: <see cref="Session"/> is set.</summary>
        public event Action Started;
        /// <summary>A command was played on this machine's copy of the game.</summary>
        public event Action<NetCommand> Played;
        /// <summary>A message for the person at this machine: a refusal of their action, or news for everyone.</summary>
        public event Action<string> Notice;
        /// <summary>The game ended for this machine: the host closed it, the connection was lost, or the host turned it away.</summary>
        public event Action<string> Closed;
        /// <summary>Two machines no longer agree on the game: the seat, the command number, and the two checksums.</summary>
        public event Action<int, int, uint, uint> Desync;

        /// <summary>Reads the network. Call it once a frame.</summary>
        public void Poll()
        {
            if (!IsClosed)
            {
                Transport.Poll();
            }
        }

        /// <summary>Takes an action for the seat played here. It is played once the host has put it in order.</summary>
        public abstract void Submit(NetCommand command);

        public virtual void Dispose()
        {
            IsClosed = true;
            Transport.Received -= OnReceived;
            Transport.Connected -= OnConnected;
            Transport.Disconnected -= OnDisconnected;
            Transport.Dispose();
        }

        protected abstract void OnReceived(int connection, byte[] data);

        protected abstract void OnConnected(int connection);

        protected abstract void OnDisconnected(int connection);

        /// <summary>Makes this machine's copy of the game from the seed and the seats.</summary>
        protected void Begin(string seed, List<string> names, int seat)
        {
            Seed = seed;
            Names = names;
            Seat = seat;
            Sequence = 0;
            Session = new GameSession(seed, names);
            Session.PlayerNotice += (playerId, message) =>
            {
                if (playerId == null || playerId == LocalPlayerId)
                {
                    Notice?.Invoke(message);
                }
            };
            Started?.Invoke();
        }

        /// <summary>Plays a numbered command on this machine's copy of the game.</summary>
        protected void Play(int sequence, NetCommand command)
        {
            Sequence = sequence;
            command.Apply(Session);
            Played?.Invoke(command);
        }

        protected void RaiseLobbyChanged()
        {
            LobbyChanged?.Invoke();
        }

        protected void RaiseNotice(string message)
        {
            Notice?.Invoke(message);
        }

        protected void RaiseDesync(int seat, int sequence, uint ours, uint theirs)
        {
            Desync?.Invoke(seat, sequence, ours, theirs);
        }

        protected void Close(string reason)
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            Closed?.Invoke(reason);
        }

        /// <summary>
        /// Whether the checksum is due after this command: when a phase or a turn has
        /// just moved on, which is where two drifting copies first differ.
        /// </summary>
        protected static bool IsChecksumPoint(Phase phaseBefore, int turnBefore, GameSession session)
        {
            return session.Phase != phaseBefore || session.Turn != turnBefore;
        }
    }
}
