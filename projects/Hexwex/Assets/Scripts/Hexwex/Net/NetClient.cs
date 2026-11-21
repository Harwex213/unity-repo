using System.IO;
using Hexwex.Core;

namespace Hexwex.Net
{
    /// <summary>
    /// A client of a network game. It says hello with its name, waits at the table
    /// until the host starts, and then plays the host's numbered commands on its
    /// own copy of the game. Its own actions go to the host and come back in their
    /// place in the order, like everyone else's. After a phase moves on it sends
    /// its checksum, so the host can tell when the copies drift apart.
    /// </summary>
    public sealed class NetClient : NetGame
    {
        private const int Host = 0;
        private readonly string _name;

        public NetClient(INetTransport transport, string name)
            : base(transport)
        {
            _name = name;
        }

        public override bool IsHost
        {
            get { return false; }
        }

        public override void Submit(NetCommand command)
        {
            if (Session == null || IsClosed)
            {
                return;
            }

            command.PlayerId = LocalPlayerId;
            Transport.Send(Host, Wire.Submit(command));
        }

        /// <summary>Leaves the game: in a running game the host resigns the seat.</summary>
        public void Leave()
        {
            Transport.Disconnect(Host);
        }

        protected override void OnConnected(int connection)
        {
            Transport.Send(Host, Wire.Hello(_name));
        }

        protected override void OnDisconnected(int connection)
        {
            Close("Связь с хостом потеряна");
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
                Close("Хост прислал непонятное сообщение");
                Transport.Disconnect(Host);

                return;
            }

            switch (message.Kind)
            {
                case MessageKind.Lobby:
                    Seed = message.Seed;
                    Names = message.Names;
                    Seat = message.Seat;
                    RaiseLobbyChanged();
                    break;
                case MessageKind.Start:
                    Begin(message.Seed, message.Names, message.Seat);
                    break;
                case MessageKind.Command:
                    PlayFromHost(message.Sequence, message.Command);
                    break;
                case MessageKind.Refused:
                    Close(message.Reason);
                    break;
            }
        }

        private void PlayFromHost(int sequence, NetCommand command)
        {
            if (Session == null)
            {
                return;
            }

            // The transport keeps the order; a gap means a copy that can no longer be trusted.
            if (sequence != Sequence + 1)
            {
                Close("Пропущен ход сети: " + (Sequence + 1) + " вместо " + sequence);
                Transport.Disconnect(Host);

                return;
            }

            Phase phase = Session.Phase;
            int turn = Session.Turn;
            Play(sequence, command);
            if (IsChecksumPoint(phase, turn, Session))
            {
                Transport.Send(Host, Wire.Checksum(sequence, Session.Checksum()));
            }
        }
    }
}
