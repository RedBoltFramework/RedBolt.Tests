// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;

using System.Net;
using System.Net.Sockets;

public sealed class NetworkTransportTests
{
    [Fact]
    public void SendMessage_copies_the_entire_source_buffer()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var packets = CapturePackets(connection);
        string? debugInfo = null;
        connection.OnSendMessage += (NetworkConnection sender, NetworkMessageId id, MemoryBuffer data,
            PacketType type, string info, ref bool handled) => debugInfo = info;
        using var source = new MemoryBuffer(new byte[] { 10, 20, 30 });
        source.Seek(2);

        Assert.True(connection.SendMessage(NetworkMessageId.User, source,
            PacketType.ReliableOrdered, "message"));

        Assert.Equal(2u, source.Position);
        var packet = Assert.Single(packets);
        using var reader = new MemoryBuffer(packet.Data);
        Assert.Equal((uint)NetworkMessageId.User, reader.ReadVLE());
        Assert.Equal(new byte[] { 10, 20, 30 }, ReadRemaining(reader));
        Assert.Equal(PacketType.ReliableOrdered, packet.Type);
        Assert.Equal("message", debugInfo);
    }

    [Fact]
    public void WriteSerializedMessage_frames_and_sends_once_with_debug_info()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var packets = CapturePackets(connection);
        string? debugInfo = null;
        connection.OnSendMessage += (NetworkConnection sender, NetworkMessageId id, MemoryBuffer data,
            PacketType type, string info, ref bool handled) => debugInfo = info;

        NetworkMessageUtils.WriteSerializedMessage(connection, NetworkMessageId.SceneClock,
            new ClockMessage { Value = 0x12345678 }, PacketType.UnreliableOrdered);
        NetworkMessageUtils.WriteSerializedMessage(connection, NetworkMessageId.SceneClock,
            new ClockMessage { Value = 0x0BADF00D }, PacketType.UnreliableOrdered);

        Assert.Equal(2, packets.Count);
        using var reader = new MemoryBuffer(packets[0].Data);
        Assert.Equal((uint)NetworkMessageId.SceneClock, reader.ReadVLE());
        Assert.Equal(0x12345678u, reader.ReadUInt());
        Assert.Equal(PacketType.UnreliableOrdered, packets[0].Type);
        Assert.Equal(sizeof(uint), ReadPayload(packets[1]).Length);
        Assert.Equal("Clock 195948557", debugInfo);
    }

    [Fact]
    public void GetMaxPayloadSize_subtracts_the_message_header_from_the_raw_size()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();

        Assert.Equal((uint)NetworkProtocol.MaxNetworkPacketSize, connection.MaxMessageSize);
        Assert.Equal(
            (uint)(NetworkProtocol.MaxNetworkPacketSize - NetworkProtocol.NetworkMessageHeaderSize),
            connection.MaxPayloadSize);

        connection.MaxMessageSizeOverride = 9;
        Assert.Equal(9u, connection.MaxMessageSize);
        Assert.Equal(9u - NetworkProtocol.NetworkMessageHeaderSize, connection.MaxPayloadSize);

        connection.MaxMessageSizeOverride = 2;
        Assert.Equal(0u, connection.MaxPayloadSize);
    }

    [Fact]
    public void LargeMessageWriter_splits_and_reader_reassembles_payload()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();
        var writeDebugInfo = new System.Text.StringBuilder();

        using (var writer = new LargeMessageWriter(connection, writeBuffer, writeDebugInfo,
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
            writer.Buffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
            writer.DebugInfo.Append("objects");
        }

        Assert.Equal(3, packets.Count);
        Assert.Equal(new[]
        {
            NetworkMessageId.AddObjectsIncomplete,
            NetworkMessageId.AddObjectsIncomplete,
            NetworkMessageId.AddObjects
        }, packets.Select(ReadMessageId));

        using var readBuffer = new VectorBuffer();
        var reader = new LargeMessageReader(readBuffer,
            NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
        byte[]? restored = null;
        foreach (var packet in packets)
        {
            using var message = new MemoryBuffer(packet.Data);
            var messageId = (NetworkMessageId)message.ReadVLE();
            reader.OnMessage(messageId, message, data => restored = ReadRemaining(data));
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, restored);
        Assert.Equal(0u, readBuffer.Size);
    }

    [Fact]
    public void LargeMessageWriter_marks_only_the_last_chunk_when_size_is_an_exact_multiple()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();

        using (var writer = new LargeMessageWriter(connection, writeBuffer, new System.Text.StringBuilder(),
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
            writer.Buffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        }

        Assert.Equal(new[] { NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects },
            packets.Select(ReadMessageId));
        Assert.Equal(new byte[] { 6, 7, 8, 9, 10 }, ReadPayload(packets[1]));
    }

    [Fact]
    public void LargeMessageReader_preserves_source_position()
    {
        using var buffer = new VectorBuffer();
        var reader = new LargeMessageReader(buffer,
            NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
        using var first = new MemoryBuffer(new byte[] { 1, 2, 3 });
        using var last = new MemoryBuffer(new byte[] { 4, 5 });
        first.Seek(1);
        byte[]? restored = null;

        reader.OnMessage(NetworkMessageId.AddObjectsIncomplete, first,
            _ => Assert.Fail("Incomplete message must not invoke the callback"));
        reader.OnMessage(NetworkMessageId.AddObjects, last, data => restored = ReadRemaining(data));

        Assert.Equal(1u, first.Position);
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, restored);
    }

    [Fact]
    public void MultiMessageWriter_repeats_header_when_packet_limit_is_reached()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 10 };
        var packets = CapturePackets(connection);
        using var buffer = new VectorBuffer();
        var debugInfo = new System.Text.StringBuilder();

        using (var writer = new MultiMessageWriter(connection, buffer, debugInfo,
                   NetworkMessageId.UpdateObjectsReliable, PacketType.ReliableOrdered))
        {
            writer.Buffer.Write(new byte[] { 9, 9 });
            writer.CompleteHeader();
            writer.Buffer.Write(new byte[] { 1, 2 });
            writer.CompletePayload();
            writer.Buffer.Write(new byte[] { 3, 4, 5 });
            writer.CompletePayload();
        }

        Assert.Equal(2, packets.Count);
        Assert.Equal(new byte[] { 9, 9, 1, 2 }, ReadPayload(packets[0]));
        Assert.Equal(new byte[] { 9, 9, 3, 4, 5 }, ReadPayload(packets[1]));
    }

    [Fact]
    public void StatisticsCounter_tracks_unhandled_outgoing_and_all_incoming_packets()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var counter = new NetworkStatisticsCounter(connection, windowSize: 1, intervalMs: 10);
        connection.OnSendData += HandleOutgoing;
        using var outgoing = new MemoryBuffer(new byte[] { 1, 2, 3 });
        using var incoming = new MemoryBuffer(new byte[] { 0, 4, 5, 6 });

        Assert.True(connection.SendData(outgoing));
        connection.Receive(incoming);
        global::System.Threading.Thread.Sleep(20);

        var snapshot = counter.Snapshot;
        Assert.Equal(3ul, snapshot.BytesOutPerSec);
        Assert.Equal(4ul, snapshot.BytesInPerSec);
        Assert.Equal(1, snapshot.PacketsOutPerSec);
        Assert.Equal(1, snapshot.PacketsInPerSec);

        static void HandleOutgoing(NetworkConnection connection, MemoryBuffer data,
            PacketType type, ref bool handled)
        {
        }
    }

    [Fact]
    public void Network_updates_server_periodically_and_client_every_frame()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();
        using var probe = new NetworkEventProbe();
        network.SetUpdateFps(10);

        network.Update(0.05f);
        network.PostUpdate(0.05f);
        network.Update(0.05f);
        network.PostUpdate(0.05f);

        Assert.Equal(2, probe.InputProcessed);
        Assert.Equal(1, probe.ServerUpdates);
        Assert.Equal(2, probe.ClientUpdates);
        Assert.Equal(1, probe.ServerUpdatesSent);
        Assert.Equal(2, probe.ClientUpdatesSent);
        Assert.True(network.IsUpdateNow);
        Assert.InRange(network.UpdateOvertime, 0.0f, 0.000001f);
    }

    [Fact]
    public void DataChannel_server_and_client_exchange_a_framed_message()
    {
        using var environment = new NetworkEnvironment();
        using var server = new DataChannelServer();
        using var client = new DataChannelConnection();
        var port = GetAvailableTcpPort();

        Assert.True(server.Listen(new URL((ushort)port)));
        Assert.True(client.Connect(new URL($"ws://127.0.0.1:{port}")));
        Assert.True(environment.WaitUntil(() =>
            client.IsConnected
            && server.Connections.Count == 1
            && server.Connections[0].IsConnected));

        byte[]? received = null;
        var serverConnection = server.Connections[0];
        serverConnection.OnMessageReceived += OnMessage;
        using var payload = new MemoryBuffer(new byte[] { 3, 1, 4, 1, 5 });

        Assert.True(client.SendMessage(NetworkMessageId.User, payload));
        Assert.True(environment.WaitUntil(() => received is not null));
        Assert.Equal(new byte[] { 3, 1, 4, 1, 5 }, received);

        client.Disconnect();
        server.Stop();
        environment.WaitUntil(() => client.IsDisconnected, milliseconds: 2000);

        void OnMessage(NetworkConnection connection, NetworkMessageId messageId,
            MemoryBuffer data, ref bool handled)
        {
            Assert.Equal(NetworkMessageId.User, messageId);
            received = ReadRemaining(data);
        }
    }

    [Fact]
    public void NetworkMessageLogger_observes_sent_and_received_messages()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var logger = new CapturingMessageLogger(connection);
        using var outgoing = new MemoryBuffer(new byte[] { 1, 2, 3 });
        using var incomingData = new VectorBuffer();
        incomingData.WriteVLE((uint)NetworkMessageId.User);
        incomingData.Write(new byte[] { 4, 5 });
        using var incoming = new MemoryBuffer(incomingData);

        Assert.True(connection.SendMessage(NetworkMessageId.User, outgoing,
            PacketType.ReliableOrdered, "payload"));
        connection.Receive(incoming);

        Assert.Equal((NetworkMessageId.User, 3u, PacketType.ReliableOrdered, "payload"),
            logger.Sent);
        Assert.Equal((NetworkMessageId.User, 3u, string.Empty), logger.Received);
        logger.IsEnabled = false;
        Assert.Equal(RedBolt.Logging.LogLevel.None,
            logger.GetMessageLogLevel(NetworkMessageId.User));
    }

    private static List<CapturedPacket> CapturePackets(TestConnection connection)
    {
        var packets = new List<CapturedPacket>();
        connection.OnSendData += Capture;
        return packets;

        void Capture(NetworkConnection sender, MemoryBuffer data, PacketType type, ref bool handled)
        {
            var position = data.Position;
            data.Seek(0);
            var bytes = new byte[checked((int)data.Size)];
            data.Read(bytes);
            data.Seek(position);
            packets.Add(new CapturedPacket(bytes, type));
        }
    }

    private static NetworkMessageId ReadMessageId(CapturedPacket packet)
    {
        using var reader = new MemoryBuffer(packet.Data);
        return (NetworkMessageId)reader.ReadVLE();
    }

    private static byte[] ReadPayload(CapturedPacket packet)
    {
        using var reader = new MemoryBuffer(packet.Data);
        reader.ReadVLE();
        return ReadRemaining(reader);
    }

    private static byte[] ReadRemaining(MemoryBuffer source)
    {
        var result = new byte[checked((int)(source.Size - source.Position))];
        source.Read(result);
        return result;
    }

    private static int GetAvailableTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record CapturedPacket(byte[] Data, PacketType Type);

    private sealed class TestConnection : NetworkConnection
    {
        public uint? MaxMessageSizeOverride { get; set; }

        public override uint MaxMessageSize => MaxMessageSizeOverride ?? base.MaxMessageSize;

        public override bool Connect(URL url) => true;

        public void Receive(MemoryBuffer data) => DispatchDataReceived(data);
    }

    private struct ClockMessage : INetworkSerializableMessage
    {
        public uint Value;

        public void Load(MemoryBuffer source) => Value = source.ReadUInt();

        public void Save(VectorBuffer destination) => destination.WriteUInt(Value);

        public override readonly string ToString() => $"Clock {Value}";
    }

    private sealed class NetworkEventProbe : RedObject
    {
        public NetworkEventProbe()
        {
            SubscribeToEvent<NetworkEvents.NetworkInputProcessedEvent>(OnInputProcessed);
            SubscribeToEvent<NetworkEvents.NetworkUpdateEvent>(OnUpdate);
            SubscribeToEvent<NetworkEvents.NetworkUpdateSentEvent>(OnUpdateSent);
        }

        public int InputProcessed { get; private set; }
        public int ServerUpdates { get; private set; }
        public int ClientUpdates { get; private set; }
        public int ServerUpdatesSent { get; private set; }
        public int ClientUpdatesSent { get; private set; }

        private void OnInputProcessed(ref NetworkEvents.NetworkInputProcessedEvent eventData)
        {
            ++InputProcessed;
        }

        private void OnUpdate(ref NetworkEvents.NetworkUpdateEvent eventData)
        {
            if (eventData.IsServer)
                ++ServerUpdates;
            else
                ++ClientUpdates;
        }

        private void OnUpdateSent(ref NetworkEvents.NetworkUpdateSentEvent eventData)
        {
            if (eventData.IsServer)
                ++ServerUpdatesSent;
            else
                ++ClientUpdatesSent;
        }
    }

    private sealed class CapturingMessageLogger : NetworkMessageLogger
    {
        public CapturingMessageLogger(NetworkConnection connection)
            : base(connection)
        {
        }

        public (NetworkMessageId Id, uint Size, PacketType Type, string DebugInfo) Sent { get; private set; }
        public (NetworkMessageId Id, uint Size, string DebugInfo) Received { get; private set; }

        public override void LogSentMessage(NetworkMessageId messageId, MemoryBuffer data,
            PacketType packetType, string debugInfo = "")
        {
            Sent = (messageId, data.Size, packetType, debugInfo);
        }

        public override void LogReceivedMessage(
            NetworkMessageId messageId, MemoryBuffer data, string debugInfo = "")
        {
            Received = (messageId, data.Size, debugInfo);
        }
    }

    private sealed class NetworkEnvironment : IDisposable
    {
        public NetworkEnvironment()
        {
            _previousContext = Context.Instance;
            _context = new Context();
            Context.SetInstance(_context);
            _context.RegisterSubsystem(new WorkQueue());
        }

        public void Dispose()
        {
            _context.Dispose();
            Context.SetInstance(_previousContext);
        }

        public bool WaitUntil(Func<bool> condition, int milliseconds = 10000)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < milliseconds)
            {
                WorkQueue.Update();
                if (condition())
                    return true;
                global::System.Threading.Thread.Sleep(10);
            }
            WorkQueue.Update();
            return condition();
        }

        private WorkQueue WorkQueue => _context.GetSubsystem<WorkQueue>()!;

        private readonly Context _previousContext;
        private readonly Context _context;
    }
}
