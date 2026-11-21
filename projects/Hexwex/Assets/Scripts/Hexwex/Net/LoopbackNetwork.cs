using System;
using System.Collections.Generic;

namespace Hexwex.Net
{
    /// <summary>
    /// A network in memory: one host and any number of clients in the same
    /// process. Messages wait until the receiving end polls, as they would on a
    /// real network, so the order of events is the same one a game meets over
    /// sockets. Used by the tests, and good for a host playing against clients
    /// in one editor.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private readonly Endpoint _host;
        private readonly Dictionary<int, Endpoint> _clients = new Dictionary<int, Endpoint>();
        private int _nextConnection = 1;

        public LoopbackNetwork()
        {
            _host = new Endpoint(this, -1);
        }

        /// <summary>The host's end. Its connections are the clients' numbers.</summary>
        public INetTransport Host
        {
            get { return _host; }
        }

        /// <summary>A new client that connects to the host. Its one connection is 0.</summary>
        public INetTransport Connect()
        {
            int connection = _nextConnection;
            _nextConnection += 1;
            Endpoint client = new Endpoint(this, connection);
            _clients[connection] = client;
            client.Enqueue(Event.Opened(0));
            _host.Enqueue(Event.Opened(connection));

            return client;
        }

        private void Deliver(Endpoint from, int connection, byte[] data)
        {
            // A copy, so a sender that reuses its buffer cannot change what was sent.
            byte[] copy = (byte[])data.Clone();
            if (from == _host)
            {
                if (_clients.TryGetValue(connection, out Endpoint client) && client.IsOpen)
                {
                    client.Enqueue(Event.Data(0, copy));
                }
            }
            else if (from.IsOpen)
            {
                _host.Enqueue(Event.Data(from.Number, copy));
            }
        }

        private void Close(Endpoint from, int connection)
        {
            if (from == _host)
            {
                if (_clients.TryGetValue(connection, out Endpoint client) && client.IsOpen)
                {
                    client.IsOpen = false;
                    client.Enqueue(Event.Closed(0));
                    _host.Enqueue(Event.Closed(connection));
                }

                return;
            }

            if (from.IsOpen)
            {
                from.IsOpen = false;
                from.Enqueue(Event.Closed(0));
                _host.Enqueue(Event.Closed(from.Number));
            }
        }

        private readonly struct Event
        {
            public readonly int Kind;
            public readonly int Connection;
            public readonly byte[] Payload;

            private Event(int kind, int connection, byte[] payload)
            {
                Kind = kind;
                Connection = connection;
                Payload = payload;
            }

            public static Event Opened(int connection) => new Event(0, connection, null);
            public static Event Data(int connection, byte[] payload) => new Event(1, connection, payload);
            public static Event Closed(int connection) => new Event(2, connection, null);
        }

        private sealed class Endpoint : INetTransport
        {
            private readonly LoopbackNetwork _network;
            private readonly Queue<Event> _inbox = new Queue<Event>();

            public Endpoint(LoopbackNetwork network, int number)
            {
                _network = network;
                Number = number;
            }

            public int Number { get; }
            public bool IsOpen { get; set; } = true;

            public event Action<int> Connected;
            public event Action<int> Disconnected;
            public event Action<int, byte[]> Received;

            public void Enqueue(Event item)
            {
                _inbox.Enqueue(item);
            }

            public void Poll()
            {
                // Only what had arrived when the poll began: a reply sent from a handler waits for the next poll.
                int count = _inbox.Count;
                for (int index = 0; index < count; index += 1)
                {
                    Event item = _inbox.Dequeue();
                    switch (item.Kind)
                    {
                        case 0:
                            Connected?.Invoke(item.Connection);
                            break;
                        case 1:
                            Received?.Invoke(item.Connection, item.Payload);
                            break;
                        default:
                            Disconnected?.Invoke(item.Connection);
                            break;
                    }
                }
            }

            public void Send(int connection, byte[] data)
            {
                _network.Deliver(this, connection, data);
            }

            public void Disconnect(int connection)
            {
                _network.Close(this, connection);
            }

            public void Dispose()
            {
                if (this == _network._host)
                {
                    foreach (int connection in new List<int>(_network._clients.Keys))
                    {
                        _network.Close(this, connection);
                    }
                }
                else
                {
                    _network.Close(this, 0);
                }
            }
        }
    }
}
