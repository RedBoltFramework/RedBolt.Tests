// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

public readonly record struct ConnectionQuality(
    float MinPing, float MaxPing, float SpikePing, float DropRate, float ShuffleRate);

internal sealed class ManualConnection : IDisposable
{
    public ManualConnection(ReplicationManager sink, uint seed)
    {
        Connection = new Transport(this);
        Peer = new ManualPeer(Connection, this);
        _sink = sink;
        _random = new RandomEngine(seed);
    }

    public static uint SystemTime { get; set; }

    public Transport Connection { get; }
    public ManualPeer Peer { get; }
    public ManualPeer? SinkConnection { get; set; }
    public ConnectionQuality Quality { get; set; }

    public void IncrementTime(uint delta)
    {
        _currentTime += delta;
        SendOrderedMessages(_messages[(false, true)]);
        SendOrderedMessages(_messages[(true, true)]);
        SendUnorderedMessages(_messages[(false, false)]);
        SendUnorderedMessages(_messages[(true, false)]);
    }

    public void Dispose()
    {
        Peer.Dispose();
        Connection.Dispose();
    }

    private bool SendData(ReadOnlySpan<byte> data, PacketType packetType)
    {
        if (data.Length == 0)
            return false;

        using var packet = new MemoryBuffer(data.ToArray());
        var messageId = (NetworkMessageId)packet.ReadVLE();
        var payloadSize = packet.Size - packet.Position;

        var currentDropRatio = _droppedMessages / System.Math.Max(1.0, _totalUnreliableMessages);
        var currentShuffleRatio = _shuffledMessages / System.Math.Max(1.0, _totalUnorderedMessages);
        var reliable = (packetType & PacketType.Reliable) != 0;
        var inOrder = (packetType & PacketType.Ordered) != 0;

        ++_totalMessages;
        if (!reliable)
            ++_totalUnreliableMessages;
        if (!inOrder)
            ++_totalUnorderedMessages;

        if (!reliable && currentDropRatio < Quality.DropRate)
        {
            ++_droppedMessages;
            return false;
        }

        var outgoingQueue = _messages[(reliable, inOrder)];
        var index = outgoingQueue.Count;
        if (!inOrder && currentShuffleRatio < Quality.ShuffleRate)
        {
            index = checked((int)_random.GetUInt(0, (uint)index));
            ++_shuffledMessages;
        }

        var payload = new byte[checked((int)payloadSize)];
        packet.Read(payload);
        outgoingQueue.Insert(index, new InternalMessage
        {
            ReceiveTime = _currentTime + GetDeliveryDelay(),
            MessageId = messageId,
            Data = payload
        });
        return true;
    }

    private uint GetDeliveryDelay()
    {
        var mean = (Quality.MinPing + Quality.MaxPing) / 2;
        var sigma = (Quality.MaxPing - Quality.MinPing) / 2;
        var source1 = _random.GetNormalFloat(mean, sigma / 1.5f);
        var source2 = _random.GetNormalFloat(mean, sigma / 1.5f);
        var value = System.Math.Clamp(source1 >= Quality.MinPing ? source1 : source2,
            Quality.MinPing, Quality.SpikePing);
        return (uint)(value * NetworkSimulator.MillisecondsInFrame * NetworkSimulator.FramesInSecond);
    }

    private void DeliverMessage(InternalMessage message)
    {
        using var memoryBuffer = new MemoryBuffer(message.Data);
        _sink.ProcessMessage(SinkConnection!, message.MessageId, memoryBuffer);
    }

    private void SendOrderedMessages(List<InternalMessage> messages)
    {
        var count = 0;
        while (count < messages.Count && messages[count].ReceiveTime < _currentTime)
        {
            DeliverMessage(messages[count]);
            ++count;
        }
        if (count != 0)
            messages.RemoveRange(0, count);
    }

    private void SendUnorderedMessages(List<InternalMessage> messages)
    {
        for (var i = messages.Count - 1; i >= 0; --i)
        {
            if (messages[i].ReceiveTime >= _currentTime)
                continue;
            DeliverMessage(messages[i]);
            messages.RemoveAt(i);
        }
    }

    private sealed class InternalMessage
    {
        public uint ReceiveTime;
        public NetworkMessageId MessageId;
        public byte[] Data = [];
    }

    internal sealed class Transport : NetworkConnection
    {
        public Transport(ManualConnection owner)
        {
            _owner = owner;
            _state = NetworkConnectionState.Connected; // Always connected
        }

        public override bool Connect(URL url) => true;
        public override void Disconnect() { }
        public override bool SendData(ReadOnlySpan<byte> data,
            PacketType type = PacketType.ReliableOrdered) => _owner.SendData(data, type);

        public void SetConnected(bool connected) =>
            _state = connected ? NetworkConnectionState.Connected : NetworkConnectionState.Disconnected;

        private readonly ManualConnection _owner;
    }

    internal sealed class ManualPeer : ReplicatedPeer
    {
        public ManualPeer(NetworkConnection connection, ManualConnection owner)
            : base(connection)
        {
            _owner = owner;
        }

        public override string ToString() => "Manual Connection";
        public override bool IsClockSynchronized => true;
        public override uint RemoteToLocalTime(uint time) => time;
        public override uint LocalToRemoteTime(uint time) => time;
        public override uint LocalTime => SystemTime;
        public override uint LocalTimeOfLatestRoundtrip => SystemTime;
        public override uint Ping => (uint)System.MathF.Round(
            1000 * (_owner.Quality.MinPing + _owner.Quality.MaxPing) / 2,
            MidpointRounding.AwayFromZero);

        private readonly ManualConnection _owner;
    }

    private readonly ReplicationManager _sink;
    private readonly RandomEngine _random;
    private readonly Dictionary<(bool Reliable, bool Ordered), List<InternalMessage>> _messages = new()
    {
        [(false, false)] = [],
        [(false, true)] = [],
        [(true, false)] = [],
        [(true, true)] = []
    };
    private uint _currentTime;
    private double _totalMessages;
    private double _totalUnorderedMessages;
    private double _totalUnreliableMessages;
    private double _droppedMessages;
    private double _shuffledMessages;
}

internal sealed class NetworkSimulator : IDisposable
{
    public const uint FramesInSecond = 25;
    public const uint MillisecondsInFrame = 1000 / FramesInSecond;
    public const uint MillisecondsInQuant = 10;

    public NetworkSimulator(Scene serverScene, uint seed = 0)
    {
        _network = Context.Instance.GetSubsystem<Network>()!;
        _random = new RandomEngine(seed);
        _serverScene = serverScene;
        _serverReplicationManager = serverScene.GetOrCreateComponent<ReplicationManager>();
        _serverReplicationManager.StartServer();
    }

    public void AddClient(Scene clientScene, ConnectionQuality quality)
    {
        var clientReplicationManager = clientScene.GetOrCreateComponent<ReplicationManager>();
        var clientToServer = new ManualConnection(_serverReplicationManager, _random.GetUInt());
        var serverToClient = new ManualConnection(clientReplicationManager, _random.GetUInt());

        clientToServer.SinkConnection = serverToClient.Peer;
        clientToServer.Quality = quality;
        serverToClient.SinkConnection = clientToServer.Peer;
        serverToClient.Quality = quality;

        clientReplicationManager.StartClient(clientToServer.Peer);
        _serverReplicationManager.ServerReplicator!.AddConnection(serverToClient.Peer);
        _clients.Add(new PerClient(clientScene, clientReplicationManager,
            clientToServer, serverToClient));
    }

    public void RemoveClient(Scene clientScene)
    {
        var index = _clients.FindIndex(data => ReferenceEquals(data.ClientScene, clientScene));
        if (index == -1)
            return;

        var data = _clients[index];
        data.ClientReplicationManager.StartStandalone();
        _serverReplicationManager.ServerReplicator!.RemoveConnection(data.ServerToClient.Peer);
        data.ClientToServer.Dispose();
        data.ServerToClient.Dispose();
        _clients.RemoveAt(index);
    }

    public void SimulateEngineFrame(float timeStep)
    {
        var elapsedNetworkMilliseconds =
            (uint)(timeStep * MillisecondsInFrame * FramesInSecond);

        foreach (var data in _clients)
            data.ClientToServer.IncrementTime(elapsedNetworkMilliseconds);
        foreach (var data in _clients)
            data.ServerToClient.IncrementTime(elapsedNetworkMilliseconds);

        _network.Update(timeStep);

        var inputReady = new CoreEvents.InputReadyEvent { TimeStep = timeStep };
        _network.SendEvent(ref inputReady);
        var update = new CoreEvents.UpdateEvent { TimeStep = timeStep };
        _network.SendEvent(ref update);
        var postUpdate = new CoreEvents.PostUpdateEvent { TimeStep = timeStep };
        _network.SendEvent(ref postUpdate);
        var renderUpdate = new CoreEvents.RenderUpdateEvent { TimeStep = timeStep };
        _network.SendEvent(ref renderUpdate);
    }

    public void SimulateTime(float time, uint millisecondsInQuant = MillisecondsInQuant)
    {
        Assert.Equal(0u, MillisecondsInFrame % millisecondsInQuant);
        var timeStep = millisecondsInQuant / 1000.0f;
        var numStepsRaw = time / timeStep;
        var numSteps = (uint)System.MathF.Round(numStepsRaw, MidpointRounding.AwayFromZero);
        Assert.InRange(System.Math.Abs(numSteps - numStepsRaw), 0, 0.001f);

        for (var i = 0u; i < numSteps; ++i)
        {
            ManualConnection.SystemTime += millisecondsInQuant;
            SimulateEngineFrame(timeStep);
        }
    }

    public ReplicatedPeer? GetServerToClientConnection(Scene clientScene) =>
        _clients.FirstOrDefault(data => ReferenceEquals(data.ClientScene, clientScene))?.ServerToClient.Peer;

    /// <summary>Changes the connection state of the server-to-client transport.</summary>
    public void SetServerToClientConnected(Scene clientScene, bool connected) =>
        FindClient(clientScene).ServerToClient.Connection.SetConnected(connected);

    private PerClient FindClient(Scene clientScene) =>
        _clients.First(data => ReferenceEquals(data.ClientScene, clientScene));

    public void Dispose()
    {
        foreach (var data in _clients)
        {
            data.ClientReplicationManager.StartStandalone();
            _serverReplicationManager.ServerReplicator?.RemoveConnection(data.ServerToClient.Peer);
            data.ClientToServer.Dispose();
            data.ServerToClient.Dispose();
        }
        _clients.Clear();
        _serverReplicationManager.StartStandalone();
    }

    public static Node SpawnOnServer<T>(Node parent, PrefabResource prefab, string name,
        Vector3f? position = null, Quaternion? rotation = null)
        where T : StaticNetworkObject
    {
        var node = parent.InstantiatePrefab(prefab.GetNodePrefab(), position, rotation)!;
        node.SetName(name);
        var networkObject = (StaticNetworkObject)node.CreateComponent(typeof(T))!;
        networkObject.SetClientPrefab(prefab);
        return node;
    }

    public static PrefabResource ConvertNodeToPrefab(Node node, string name)
    {
        var prefab = new PrefabResource();
        prefab.SetName(name);
        node.GeneratePrefab(prefab.GetMutableNodePrefab());
        prefab.NormalizeIds();
        Context.Instance.GetSubsystem<ResourceCache>()!.AddManualResource(prefab);
        return prefab;
    }

    private sealed record PerClient(Scene ClientScene,
        ReplicationManager ClientReplicationManager, ManualConnection ClientToServer,
        ManualConnection ServerToClient);

    private readonly Network _network;
    private readonly RandomEngine _random;
    private readonly Scene _serverScene;
    private readonly ReplicationManager _serverReplicationManager;
    private readonly List<PerClient> _clients = [];
}

internal sealed class ReplicaTestContext : IDisposable
{
    public ReplicaTestContext()
    {
        _previousContext = Context.Instance;
        Context = new Context();
        RedBolt.Core.Context.SetInstance(Context);
        Context.RegisterSubsystem(new WorkQueue());
        Context.RegisterSubsystem(new Network());
        Context.RegisterSubsystem(new ResourceCache());
        Context.GetSubsystem<Network>()!.SetUpdateFps(NetworkSimulator.FramesInSecond);
        ManualConnection.SystemTime = 0;
    }

    public Context Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        RedBolt.Core.Context.SetInstance(_previousContext);
    }

    private readonly Context _previousContext;
}
