// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;

using System.Net;
using System.Net.Sockets;

public sealed class NetworkTransportTests
{
    [Fact]
    public void SendMessage_frames_the_whole_span()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var packets = CapturePackets(connection);
        string? debugInfo = null;
        connection.OnSendMessage += (NetworkConnection sender, NetworkMessageId id, ReadOnlySpan<byte> data,
            PacketType type, string info, ref bool handled) => debugInfo = info;
        var source = new byte[] { 10, 20, 30 };

        Assert.True(connection.SendMessage(NetworkMessageId.User, source,
            PacketType.ReliableOrdered, "message"));

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
        connection.OnSendMessage += (NetworkConnection sender, NetworkMessageId id, ReadOnlySpan<byte> data,
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
            using var message = new MemoryBuffer(ReadPayload(packet));
            reader.OnMessage(ReadMessageId(packet), message, data => restored = ReadRemaining(data));
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, restored);
        Assert.Equal(0u, readBuffer.Size);
    }

    [Fact]
    public void LargeMessage_round_trip_keeps_a_size_that_is_an_exact_multiple_of_the_payload_size()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();
        var expected = Enumerable.Range(1, (int)connection.MaxPayloadSize * 3).Select(i => (byte)i).ToArray();

        using (var writer = new LargeMessageWriter(connection, writeBuffer, new System.Text.StringBuilder(),
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
            writer.Buffer.Write(expected);
        }

        Assert.Equal(3, packets.Count);
        Assert.All(packets, packet => Assert.Equal((int)connection.MaxPayloadSize, ReadPayload(packet).Length));
        using var readBuffer = new VectorBuffer();
        var reader = new LargeMessageReader(readBuffer,
            NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
        byte[]? restored = null;
        foreach (var packet in packets)
        {
            using var message = new MemoryBuffer(ReadPayload(packet));
            reader.OnMessage(ReadMessageId(packet), message, data => restored = ReadRemaining(data));
        }

        Assert.Equal(expected, restored);
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
    public void LargeMessageWriter_sends_one_empty_message_for_an_empty_buffer()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();

        using (new LargeMessageWriter(connection, writeBuffer, new System.Text.StringBuilder(),
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
        }

        var packet = Assert.Single(packets);
        Assert.Equal(NetworkMessageId.AddObjects, ReadMessageId(packet));
        Assert.Empty(ReadPayload(packet));
    }

    [Fact]
    public void LargeMessageWriter_sends_nothing_when_discarded()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();

        using (var writer = new LargeMessageWriter(connection, writeBuffer, new System.Text.StringBuilder(),
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
            writer.Discard();
        }

        Assert.Empty(packets);
    }

    [Fact]
    public void LargeMessage_round_trip_keeps_an_empty_message()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection { MaxMessageSizeOverride = 9 };
        var packets = CapturePackets(connection);
        using var writeBuffer = new VectorBuffer();
        using (new LargeMessageWriter(connection, writeBuffer, new System.Text.StringBuilder(),
                   NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects))
        {
        }

        using var readBuffer = new VectorBuffer();
        var reader = new LargeMessageReader(readBuffer,
            NetworkMessageId.AddObjectsIncomplete, NetworkMessageId.AddObjects);
        var calls = 0;
        byte[]? restored = null;
        foreach (var packet in packets)
        {
            using var message = new MemoryBuffer(ReadPayload(packet));
            reader.OnMessage(ReadMessageId(packet), message, data =>
            {
                ++calls;
                restored = ReadRemaining(data);
            });
        }

        Assert.Equal(1, calls);
        Assert.Empty(restored!);
    }

    [Fact]
    public void LargeMessageReader_appends_the_whole_source_and_moves_it_to_the_end()
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

        Assert.Equal(first.Size, first.Position);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, restored);
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
    public void OnMessageReceived_passes_the_payload_after_the_message_id()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        NetworkMessageId? receivedId = null;
        byte[]? receivedPayload = null;
        connection.OnMessageReceived += (NetworkConnection sender, NetworkMessageId id,
            ReadOnlySpan<byte> payload, ref bool handled) =>
        {
            receivedId = id;
            receivedPayload = payload.ToArray();
        };
        using var incomingData = new VectorBuffer();
        incomingData.WriteVLE((uint)NetworkMessageId.User);
        incomingData.Write(new byte[] { 7, 8, 9 });

        connection.Receive(incomingData.Buffer);

        Assert.Equal(NetworkMessageId.User, receivedId);
        Assert.Equal(new byte[] { 7, 8, 9 }, receivedPayload);
    }

    [Fact]
    public void StatisticsCounter_tracks_unhandled_outgoing_and_all_incoming_packets()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var counter = new NetworkStatisticsCounter(connection, windowSize: 1, intervalMs: 10);
        connection.OnSendData += HandleOutgoing;
        var outgoing = new byte[] { 1, 2, 3 };
        var incoming = new byte[] { 0, 4, 5, 6 };

        Assert.True(connection.SendData(outgoing));
        connection.Receive(incoming);
        global::System.Threading.Thread.Sleep(20);

        var snapshot = counter.Snapshot;
        Assert.Equal(3ul, snapshot.BytesOutPerSec);
        Assert.Equal(4ul, snapshot.BytesInPerSec);
        Assert.Equal(1, snapshot.PacketsOutPerSec);
        Assert.Equal(1, snapshot.PacketsInPerSec);

        static void HandleOutgoing(NetworkConnection connection, ReadOnlySpan<byte> data,
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
        var payload = new byte[] { 3, 1, 4, 1, 5 };

        Assert.True(client.SendMessage(NetworkMessageId.User, payload));
        Assert.True(environment.WaitUntil(() => received is not null));
        Assert.Equal(new byte[] { 3, 1, 4, 1, 5 }, received);

        client.Disconnect();
        server.Stop();
        environment.WaitUntil(() => client.IsDisconnected, milliseconds: 2000);

        void OnMessage(NetworkConnection connection, NetworkMessageId messageId,
            ReadOnlySpan<byte> data, ref bool handled)
        {
            Assert.Equal(NetworkMessageId.User, messageId);
            received = data.ToArray();
        }
    }

    [Fact]
    public void Server_keeps_the_connection_during_OnDisconnected_and_removes_it_afterwards()
    {
        using var environment = new NetworkEnvironment();
        using var server = new TestServer();
        server.SetConnectionFactory(() => new TestConnection());
        var connection = server.CreateConnection();
        Assert.Contains(connection, server.Connections);

        var listedDuringEvent = false;
        var eventCount = 0;
        server.OnDisconnected += (_, disconnected) =>
        {
            ++eventCount;
            listedDuringEvent = server.Connections.Contains(disconnected);
        };

        server.RaiseDisconnected(connection);

        Assert.Equal(1, eventCount);
        Assert.True(listedDuringEvent);
        Assert.DoesNotContain(connection, server.Connections);
    }

    [Fact]
    public void Server_defers_dispatch_from_a_network_thread_to_the_main_thread()
    {
        using var environment = new NetworkEnvironment();
        using var server = new TestServer();
        server.SetConnectionFactory(() => new TestConnection());
        var connection = server.CreateConnection();
        var eventCount = 0;
        server.OnDisconnected += (_, _) => ++eventCount;

        var thread = new global::System.Threading.Thread(() => server.RaiseDisconnected(connection));
        thread.Start();
        thread.Join();

        Assert.Equal(0, eventCount);
        Assert.Contains(connection, server.Connections);
        Assert.True(environment.WaitUntil(() => eventCount == 1));
        Assert.DoesNotContain(connection, server.Connections);
    }

    [Fact]
    public void Server_can_be_constructed_without_a_work_queue()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            Assert.Null(context.GetSubsystem<WorkQueue>());
            using var server = new TestServer();
            Assert.Empty(server.Connections);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void NetworkMessageLogger_observes_sent_and_received_messages()
    {
        using var environment = new NetworkEnvironment();
        using var connection = new TestConnection();
        var logger = new CapturingMessageLogger(connection);
        var outgoing = new byte[] { 1, 2, 3 };
        using var incomingData = new VectorBuffer();
        incomingData.WriteVLE((uint)NetworkMessageId.User);
        incomingData.Write(new byte[] { 4, 5 });

        Assert.True(connection.SendMessage(NetworkMessageId.User, outgoing,
            PacketType.ReliableOrdered, "payload"));
        connection.Receive(incomingData.Buffer);

        Assert.Equal((NetworkMessageId.User, 3, PacketType.ReliableOrdered, "payload"),
            logger.Sent);
        // The received size is the payload size, which excludes the message id.
        Assert.Equal((NetworkMessageId.User, 2, string.Empty), logger.Received);
        logger.IsEnabled = false;
        Assert.Equal(RedBolt.Logging.LogLevel.None,
            logger.GetMessageLogLevel(NetworkMessageId.User));
    }

    [Fact]
    public void CreateConnection_and_CreateServer_default_to_the_DataChannel_types()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();

        using var connection = network.CreateConnection();
        using var server = network.CreateServer();

        Assert.IsType<DataChannelConnection>(connection);
        Assert.IsType<DataChannelServer>(server);
    }

    [Fact]
    public void CreateConnection_and_CreateServer_accept_an_explicit_DataChannel_type()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();

        using var connection = network.CreateConnection(typeof(DataChannelConnection).GetTypeId());
        using var server = network.CreateServer(typeof(DataChannelServer).GetTypeId());

        Assert.IsType<DataChannelConnection>(connection);
        Assert.IsType<DataChannelServer>(server);
    }

    [Fact]
    public void CreateConnection_and_CreateServer_return_null_for_unknown_types()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();
        var unknown = new StringHash("NoSuchNetworkType");

        Assert.Null(network.CreateConnection(unknown));
        Assert.Null(network.CreateServer(unknown));
    }

    [Fact]
    public void CreateConnection_and_CreateServer_return_null_for_types_of_the_wrong_base()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();

        Assert.Null(network.CreateConnection(typeof(DataChannelServer).GetTypeId()));
        Assert.Null(network.CreateServer(typeof(DataChannelConnection).GetTypeId()));
    }

    [Fact]
    public void CreateConnection_and_CreateServer_return_null_for_a_factory_type_of_the_wrong_base()
    {
        using var environment = new NetworkEnvironment();
        Context.Instance.RegisterAssembly(typeof(RedBolt.Scene.ShakeComponent).Assembly);
        using var network = new RedBolt.Network.Network();
        var componentType = typeof(RedBolt.Scene.ShakeComponent).GetTypeId();
        Assert.True(Context.Instance.GetReflection(componentType)!.HasObjectFactory());

        Assert.Null(network.CreateConnection(componentType));
        Assert.Null(network.CreateServer(componentType));
    }

    [Fact]
    public void DataChannel_types_have_no_object_factory()
    {
        using var environment = new NetworkEnvironment();
        using var network = new RedBolt.Network.Network();

        var connection = Context.Instance.GetReflection(typeof(DataChannelConnection).GetTypeId());
        var server = Context.Instance.GetReflection(typeof(DataChannelServer).GetTypeId());

        Assert.NotNull(connection);
        Assert.NotNull(server);
        Assert.False(connection.HasObjectFactory());
        Assert.False(server.HasObjectFactory());
    }

    private static List<CapturedPacket> CapturePackets(TestConnection connection)
    {
        var packets = new List<CapturedPacket>();
        connection.OnSendData += Capture;
        return packets;

        void Capture(NetworkConnection sender, ReadOnlySpan<byte> data, PacketType type, ref bool handled)
        {
            packets.Add(new CapturedPacket(data.ToArray(), type));
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

    private sealed class TestServer : NetworkServer
    {
        public override bool Listen(URL url) => false;
        public override void Stop() { }
        public override bool IsListening => false;

        public void RaiseDisconnected(NetworkConnection connection) => DispatchDisconnected(connection);
    }

    private sealed class TestConnection : NetworkConnection
    {
        public uint? MaxMessageSizeOverride { get; set; }

        public override uint MaxMessageSize => MaxMessageSizeOverride ?? base.MaxMessageSize;

        public override bool Connect(URL url) => true;

        public void Receive(ReadOnlySpan<byte> data) => DispatchDataReceived(data);
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
        }

        public int InputProcessed { get; private set; }
        public int ServerUpdates { get; private set; }
        public int ClientUpdates { get; private set; }

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
    }

    private sealed class CapturingMessageLogger : NetworkMessageLogger
    {
        public CapturingMessageLogger(NetworkConnection connection)
            : base(connection)
        {
        }

        public (NetworkMessageId Id, int Size, PacketType Type, string DebugInfo) Sent { get; private set; }
        public (NetworkMessageId Id, int Size, string DebugInfo) Received { get; private set; }

        public override void LogSentMessage(NetworkMessageId messageId, ReadOnlySpan<byte> data,
            PacketType packetType, string debugInfo = "")
        {
            Sent = (messageId, data.Length, packetType, debugInfo);
        }

        public override void LogReceivedMessage(
            NetworkMessageId messageId, ReadOnlySpan<byte> data, string debugInfo = "")
        {
            Received = (messageId, data.Length, debugInfo);
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
