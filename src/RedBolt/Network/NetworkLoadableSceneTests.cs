namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class NetworkLoadableSceneTests
{
    [Fact]
    public void Sends_pending_load_request_once_connection_is_established()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection);

        var clientLoadedSceneName = string.Empty;
        var clientLoadedSuccessfully = false;
        clientLoadableScene.LoadSceneFinished +=
            (NetworkLoadableScene _, string sceneFileName, ref bool success, VectorBuffer _) =>
            {
                clientLoadedSceneName = sceneFileName;
                clientLoadedSuccessfully = success;
            };

        var serverFinishedSceneName = string.Empty;
        var serverLoadedSuccessfully = false;
        var serverFinishedCount = 0u;
        serverLoadableScene.RemoteLoadSceneFinished +=
            (_, sceneFileName, success, _) =>
            {
                serverFinishedSceneName = sceneFileName;
                serverLoadedSuccessfully = success;
                ++serverFinishedCount;
            };

        const string sceneName = "Scenes/TestNetworkLoadableScene_A.bin";
        using var serverScene = CreateBinarySceneResource(sceneName, "MarkerA");
        serverLoadableScene.SetScene(serverScene);
        Assert.Equal(0, serverConnection.NumPendingPackets);
        Assert.False(serverLoadableScene.IsSwitchInProgress);

        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        Assert.Equal(1, serverConnection.NumPendingPackets);
        Assert.True(serverLoadableScene.IsSwitchInProgress);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.True(clientLoadedSuccessfully);
        Assert.Equal(sceneName, clientLoadedSceneName);
        Assert.Equal(1, clientConnection.NumPendingPackets);
        Assert.NotNull(clientLoadableScene.Scene);
        Assert.NotNull(clientLoadableScene.Scene!.FindChild("MarkerA", true));

        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.Equal(1u, serverFinishedCount);
        Assert.True(serverLoadedSuccessfully);
        Assert.Equal(sceneName, serverFinishedSceneName);
        Assert.False(serverLoadableScene.IsSwitchInProgress);
    }

    [Fact]
    public void Switches_between_in_memory_binary_scenes()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverPeer = new ReplicatedPeer(serverConnection);
        using var clientPeer = new ReplicatedPeer(clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection, serverPeer);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection, clientPeer);

        var clientFinishedCount = 0u;
        clientLoadableScene.LoadSceneFinished +=
            (NetworkLoadableScene _, string _, ref bool success, VectorBuffer _) =>
            {
                Assert.True(success);
                ++clientFinishedCount;
            };
        var serverFinishedCount = 0u;
        serverLoadableScene.RemoteLoadSceneFinished += (_, _, success, _) =>
        {
            Assert.True(success);
            ++serverFinishedCount;
        };

        using var firstScene = CreateBinarySceneResource(
            "Scenes/TestNetworkLoadableScene_B1.bin", "MarkerB1");
        using var secondScene = CreateBinarySceneResource(
            "Scenes/TestNetworkLoadableScene_B2.bin", "MarkerB2");
        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        serverLoadableScene.SetScene(firstScene);
        Assert.True(serverLoadableScene.IsSwitchInProgress);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.Equal(1u, serverFinishedCount);
        Assert.Equal(1u, clientFinishedCount);
        Assert.NotNull(clientLoadableScene.Scene!.FindChild("MarkerB1", true));
        Assert.Null(clientLoadableScene.Scene.FindChild("MarkerB2", true));

        serverLoadableScene.SetScene(secondScene);
        Assert.True(serverLoadableScene.IsSwitchInProgress);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.NotNull(clientLoadableScene.Scene.FindChild("MarkerB2", true));
        Assert.Null(clientLoadableScene.Scene.FindChild("MarkerB1", true));
        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.Equal(2u, serverFinishedCount);
        Assert.Equal(2u, clientFinishedCount);
        Assert.False(serverLoadableScene.IsSwitchInProgress);
    }

    [Fact]
    public void May_reject_client_load_request()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection);

        clientLoadableScene.LoadSceneRequestReceived +=
            (NetworkLoadableScene _, string _, MemoryBuffer _, ref bool accept) => accept = false;
        var clientFinished = false;
        var clientSuccess = true;
        clientLoadableScene.LoadSceneFinished +=
            (NetworkLoadableScene _, string _, ref bool success, VectorBuffer _) =>
            {
                clientFinished = true;
                clientSuccess = success;
            };
        var serverFinished = false;
        var serverSuccess = true;
        serverLoadableScene.RemoteLoadSceneFinished += (_, _, success, _) =>
        {
            serverFinished = true;
            serverSuccess = success;
        };

        using var serverScene = CreateBinarySceneResource(
            "Scenes/TestNetworkLoadableScene_Reject.bin", "MarkerReject");
        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        serverLoadableScene.SetScene(serverScene);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.True(clientFinished);
        Assert.False(clientSuccess);
        Assert.Null(clientLoadableScene.Scene);
        Assert.Equal(1, clientConnection.NumPendingPackets);

        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.True(serverFinished);
        Assert.False(serverSuccess);
        Assert.False(serverLoadableScene.IsSwitchInProgress);
    }

    [Fact]
    public void Reports_failure_when_scene_resource_is_missing()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection);

        var clientFinished = false;
        var clientSuccess = true;
        clientLoadableScene.LoadSceneFinished +=
            (NetworkLoadableScene _, string _, ref bool success, VectorBuffer _) =>
            {
                clientFinished = true;
                clientSuccess = success;
            };
        var serverFinished = false;
        var serverSuccess = true;
        serverLoadableScene.RemoteLoadSceneFinished += (_, _, success, _) =>
        {
            serverFinished = true;
            serverSuccess = success;
        };

        using var missingScene = new Scene();
        missingScene.SetFileName("Scenes/TestNetworkLoadableScene_Missing.bin");
        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        serverLoadableScene.SetScene(missingScene);
        Assert.True(serverLoadableScene.IsSwitchInProgress);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.True(clientFinished);
        Assert.False(clientSuccess);
        Assert.NotNull(clientLoadableScene.Scene);
        Assert.Equal(1, clientConnection.NumPendingPackets);

        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.True(serverFinished);
        Assert.False(serverSuccess);
        Assert.False(serverLoadableScene.IsSwitchInProgress);
    }

    [Fact]
    public void Resets_pending_server_switch_on_disconnect()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection);
        using var serverScene = CreateBinarySceneResource(
            "Scenes/TestNetworkLoadableScene_Disconnect.bin", "MarkerDisconnect");
        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        serverLoadableScene.SetScene(serverScene);
        Assert.True(serverLoadableScene.IsSwitchInProgress);
        Assert.Equal(1, serverConnection.FlushOutgoing());
        Assert.NotNull(clientLoadableScene.Scene);
        Assert.Equal(1, clientConnection.NumPendingPackets);

        serverConnection.Disconnect();
        Assert.False(serverLoadableScene.IsSwitchInProgress);
    }

    [Fact]
    public void Wires_replication_managers_automatically()
    {
        using var fixture = new ReplicaTestContext();
        using var server = new TestNetworkServer();
        using var serverConnection = new InMemoryConnection();
        using var clientConnection = new InMemoryConnection();
        Connect(server, serverConnection, clientConnection);
        using var serverPeer = new ReplicatedPeer(serverConnection);
        using var clientPeer = new ReplicatedPeer(clientConnection);
        using var serverLoadableScene = new NetworkLoadableScene(serverConnection, serverPeer);
        using var clientLoadableScene = new NetworkLoadableScene(clientConnection, clientPeer);
        using var serverScene = CreateBinarySceneResource(
            "Scenes/TestNetworkLoadableScene_Replication.bin", "MarkerReplication", true);
        var serverReplicationManager = serverScene.GetComponent<ReplicationManager>()!;
        serverReplicationManager.StartServer();
        clientConnection.DispatchConnected();
        serverConnection.DispatchConnected();

        serverLoadableScene.SetScene(serverScene);
        Assert.Equal(1, serverConnection.FlushOutgoing());

        var clientScene = clientLoadableScene.Scene;
        Assert.NotNull(clientScene);
        var clientReplicationManager = clientScene!.GetComponent<ReplicationManager>();
        Assert.NotNull(clientReplicationManager);
        Assert.Same(clientReplicationManager, clientPeer.ReplicationManager);

        Assert.Equal(1, clientConnection.FlushOutgoing());
        Assert.Same(serverReplicationManager, serverPeer.ReplicationManager);
    }

    private static Scene CreateBinarySceneResource(
        string sceneName, string markerName, bool withReplicationManager = false)
    {
        var scene = new Scene();
        if (withReplicationManager)
            scene.CreateComponent<ReplicationManager>();
        scene.CreateChild(markerName).SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));

        var resource = new BinaryFile();
        Assert.True(scene.Save(resource.AsSerializer()));
        scene.SetFileName(sceneName);
        resource.SetName(sceneName);
        Assert.True(Context.Instance.GetSubsystem<ResourceCache>()!.AddManualResource(resource));
        return scene;
    }

    private static void Connect(TestNetworkServer server,
        InMemoryConnection serverConnection, InMemoryConnection clientConnection)
    {
        serverConnection.Peer = clientConnection;
        clientConnection.Peer = serverConnection;
        serverConnection.SetServerSide(server);
    }

    private sealed class TestNetworkServer : NetworkServer
    {
        public override bool Listen(URL url) => false;
        public override void Stop() { }
        public override bool IsListening => false;
    }

    private sealed class InMemoryConnection : NetworkConnection
    {
        public InMemoryConnection? Peer { get; set; }
        public int NumPendingPackets => _packets.Count;

        public override bool Connect(URL url) => false;

        public override void Disconnect()
        {
            _state = NetworkConnectionState.Disconnected;
            OnDisconnected();
        }

        public override bool SendData(MemoryBuffer data,
            PacketType type = PacketType.ReliableOrdered)
        {
            var oldPosition = data.Position;
            data.Seek(0);
            var packet = new byte[checked((int)data.Size)];
            data.Read(packet);
            data.Seek(oldPosition);
            _packets.Add(packet);
            return true;
        }

        public void SetServerSide(NetworkServer server) => SetServer(server);

        public void DispatchConnected()
        {
            _state = NetworkConnectionState.Connected;
            OnConnected();
        }

        public int FlushOutgoing()
        {
            var packets = _packets.ToArray();
            _packets.Clear();
            foreach (var packet in packets)
            {
                using var message = new MemoryBuffer(packet);
                Peer!.OnData(message);
            }
            return packets.Length;
        }

        private readonly List<byte[]> _packets = [];
    }
}
