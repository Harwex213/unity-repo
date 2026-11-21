using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Hexwex.Net
{
    /// <summary>
    /// <see cref="INetTransport"/> over Unity Transport: UDP on one machine or a
    /// LAN, with no Steam and no account. Every message goes through one
    /// pipeline that fragments what does not fit a packet and resends what is
    /// lost, in order, so it arrives whole, once and in sequence.
    ///
    /// A message the driver cannot take at once (its reliable window is full)
    /// waits in a queue for its connection and goes out on a later poll, still
    /// in order.
    /// </summary>
    public sealed class UtpTransport : INetTransport
    {
        public const ushort DefaultPort = 7777;
        /// <summary>The largest message: enough for a battle result with every structure of a big island.</summary>
        private const int MaxMessage = 32 * 1024;
        /// <summary>Packets in flight per connection before the reliable stage asks to wait. 64 is its most.</summary>
        private const int ReliableWindow = 64;

        private readonly bool _isHost;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private readonly Dictionary<int, NetworkConnection> _connections = new Dictionary<int, NetworkConnection>();
        private readonly Dictionary<NetworkConnection, int> _ids = new Dictionary<NetworkConnection, int>();
        private readonly Dictionary<int, Queue<byte[]>> _waiting = new Dictionary<int, Queue<byte[]>>();
        private int _nextId = 1;

        private UtpTransport(bool isHost)
        {
            _isHost = isHost;
            NetworkSettings settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(payloadCapacity: MaxMessage);
            settings.WithReliableStageParameters(windowSize: ReliableWindow);
            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        }

        public event Action<int> Connected;
        public event Action<int> Disconnected;
        public event Action<int, byte[]> Received;

        /// <summary>A host that listens on every local address at the port.</summary>
        public static UtpTransport Listen(ushort port = DefaultPort)
        {
            UtpTransport transport = new UtpTransport(true);
            if (transport._driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0)
            {
                transport.Dispose();
                throw new InvalidOperationException("Порт " + port + " занят");
            }

            transport._driver.Listen();

            return transport;
        }

        /// <summary>A client of the host at the address. <see cref="Connected"/> fires once the host answers; its connection is 0.</summary>
        public static UtpTransport Connect(string address, ushort port = DefaultPort)
        {
            if (!NetworkEndpoint.TryParse(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port, out NetworkEndpoint endpoint))
            {
                throw new ArgumentException("Не адрес: " + address);
            }

            UtpTransport transport = new UtpTransport(false);
            NetworkConnection connection = transport._driver.Connect(endpoint);
            transport._connections[0] = connection;
            transport._ids[connection] = 0;

            return transport;
        }

        public void Poll()
        {
            if (!_driver.IsCreated)
            {
                return;
            }

            _driver.ScheduleUpdate().Complete();

            if (_isHost)
            {
                NetworkConnection accepted;
                while ((accepted = _driver.Accept()) != default)
                {
                    int id = _nextId;
                    _nextId += 1;
                    _connections[id] = accepted;
                    _ids[accepted] = id;
                    Connected?.Invoke(id);
                }
            }

            NetworkEvent.Type type;
            while ((type = _driver.PopEvent(out NetworkConnection connection, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                if (!_ids.TryGetValue(connection, out int id))
                {
                    continue;
                }

                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        Connected?.Invoke(id);
                        break;
                    case NetworkEvent.Type.Data:
                        Received?.Invoke(id, ReadAll(ref reader));
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Forget(id);
                        Disconnected?.Invoke(id);
                        break;
                }
            }

            Flush();
        }

        public void Send(int connection, byte[] data)
        {
            if (!_connections.ContainsKey(connection))
            {
                return;
            }

            if (data.Length > MaxMessage)
            {
                throw new ArgumentException("Сообщение больше " + MaxMessage + " байт");
            }

            if (!_waiting.TryGetValue(connection, out Queue<byte[]> queue))
            {
                queue = new Queue<byte[]>();
                _waiting[connection] = queue;
            }

            queue.Enqueue(data);
            Flush(connection);
        }

        public void Disconnect(int connection)
        {
            if (!_connections.TryGetValue(connection, out NetworkConnection target))
            {
                return;
            }

            // What is still waiting goes out first, as far as the driver will take it.
            Flush(connection);
            _driver.Disconnect(target);
            Forget(connection);
            // The driver tells only the other end; this end hears it from here.
            Disconnected?.Invoke(connection);
        }

        public void Dispose()
        {
            if (!_driver.IsCreated)
            {
                return;
            }

            foreach (NetworkConnection connection in _connections.Values)
            {
                _driver.Disconnect(connection);
            }

            // One last update sends the goodbyes.
            _driver.ScheduleUpdate().Complete();
            _connections.Clear();
            _ids.Clear();
            _waiting.Clear();
            _driver.Dispose();
        }

        private void Flush()
        {
            foreach (int connection in new List<int>(_waiting.Keys))
            {
                Flush(connection);
            }
        }

        /// <summary>Sends what waits for the connection, in order, until the driver asks to wait.</summary>
        private void Flush(int connection)
        {
            if (!_waiting.TryGetValue(connection, out Queue<byte[]> queue) || !_connections.TryGetValue(connection, out NetworkConnection target))
            {
                return;
            }

            while (queue.Count > 0)
            {
                int status = _driver.BeginSend(_pipeline, target, out DataStreamWriter writer);
                if (status != 0)
                {
                    // Not connected yet, or the window is full: try again on the next poll.
                    return;
                }

                byte[] data = queue.Peek();
                using (NativeArray<byte> bytes = new NativeArray<byte>(data, Allocator.Temp))
                {
                    writer.WriteBytes(bytes);
                }

                if (_driver.EndSend(writer) < 0)
                {
                    return;
                }

                queue.Dequeue();
            }
        }

        private void Forget(int connection)
        {
            if (_connections.TryGetValue(connection, out NetworkConnection target))
            {
                _ids.Remove(target);
            }

            _connections.Remove(connection);
            _waiting.Remove(connection);
        }

        private static byte[] ReadAll(ref DataStreamReader reader)
        {
            byte[] data = new byte[reader.Length];
            using (NativeArray<byte> bytes = new NativeArray<byte>(reader.Length, Allocator.Temp))
            {
                reader.ReadBytes(bytes);
                bytes.CopyTo(data);
            }

            return data;
        }
    }
}
