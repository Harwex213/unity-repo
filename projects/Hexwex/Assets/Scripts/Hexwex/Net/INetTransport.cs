using System;

namespace Hexwex.Net
{
    /// <summary>
    /// How bytes get between machines. A host's transport has a connection per
    /// client; a client's has one, to the host. Every message arrives whole, once
    /// and in the order it was sent on its connection. Nothing is raised outside
    /// <see cref="Poll"/>, so the game reads the network at one point of its frame.
    ///
    /// Implementations: <see cref="LoopbackNetwork"/> in memory, for tests and a host
    /// and its own client in one process; Unity Transport over UDP for a LAN or
    /// one machine; Steam networking later.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        /// <summary>Takes in what arrived since the last call and raises the events for it.</summary>
        void Poll();

        void Send(int connection, byte[] data);

        void Disconnect(int connection);

        /// <summary>A connection is open: a client reached the host, or the host answered this client.</summary>
        event Action<int> Connected;
        /// <summary>A connection closed, from either end, or was lost.</summary>
        event Action<int> Disconnected;
        event Action<int, byte[]> Received;
    }
}
