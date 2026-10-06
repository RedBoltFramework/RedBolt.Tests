// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class SceneLoaderTests
{
    [Fact]
    public void Server_client_scene_loader_sequence()
    {
        using var fixture = new ReplicaTestContext();
        using var sceneA = CreateSceneAndResource(fixture, "Scenes/TestSceneLoader_ReloadA.bin", "MarkerA");
        using var sceneB = CreateSceneAndResource(fixture, "Scenes/TestSceneLoader_ReloadB.bin", "MarkerB");
        using var sceneC = CreateSceneAndResource(fixture, "Scenes/TestSceneLoader_ReloadC.bin", "MarkerC");

        var random = new RandomEngine(0);
        var (server, client) = ManualConnection.CreatePair(random);
        using var serverConnection = server;
        using var clientConnection = client;

        using var serverLoader = new ServerSceneLoader(server.Connection, server.Peer);
        using var clientLoader = new ClientSceneLoader(client.Connection, client.Peer);

        using var clientScene = new Scene();
        clientLoader.SetScene(clientScene);

        StringVariantMap? pendingParams = null;
        Action? pendingCompletion = null;
        var requestCount = 0u;
        clientLoader.SetLoadRequestedCallback((args, onCompleted) =>
        {
            pendingParams?.Dispose();
            pendingParams = new StringVariantMap(args);
            pendingCompletion = onCompleted;
            ++requestCount;
        });

        try
        {
            // Load scene A on server
            SetScene(serverLoader, sceneA);
            server.IncrementTime(1);

            Assert.Equal(1u, requestCount);
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Null(client.Peer.ReplicationManager);

            // Send invalid confirmation
            using (var invalidResult = new VectorBuffer())
            {
                invalidResult.WriteUInt(0xDEADBEEF);
                client.Connection.SendMessage(NetworkMessageId.SceneLoadResult, invalidResult.Buffer);
            }
            client.IncrementTime(1);
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Null(client.Peer.ReplicationManager);

            // Load scene A load on client
            Assert.True(LoadSceneFromParams(fixture, clientScene, pendingParams!));
            pendingCompletion!();
            pendingCompletion = null;

            Assert.Null(server.Peer.ReplicationManager);
            Assert.Same(clientScene.GetComponent<ReplicationManager>(), client.Peer.ReplicationManager);
            Assert.NotNull(clientScene.FindChild("MarkerA", true));
            Assert.Null(clientScene.FindChild("MarkerB", true));
            Assert.Null(clientScene.FindChild("MarkerC", true));

            // Confirm scene A load on server
            client.IncrementTime(1);
            Assert.Same(sceneA.GetComponent<ReplicationManager>(), server.Peer.ReplicationManager);
            Assert.Same(clientScene.GetComponent<ReplicationManager>(), client.Peer.ReplicationManager);

            // Load scene B on server
            SetScene(serverLoader, sceneB);
            Assert.Equal(1u, requestCount);
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Same(clientScene.GetComponent<ReplicationManager>(), client.Peer.ReplicationManager);

            // Start loading scene B on client
            server.IncrementTime(1);
            Assert.Equal(2u, requestCount);
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Null(client.Peer.ReplicationManager);

            // Load scene C on server
            SetScene(serverLoader, sceneC);
            server.IncrementTime(1);
            Assert.Equal(3u, requestCount);
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Null(client.Peer.ReplicationManager);

            // Load scene C load on client
            Assert.True(LoadSceneFromParams(fixture, clientScene, pendingParams!));
            pendingCompletion!();
            pendingCompletion = null;
            Assert.Null(server.Peer.ReplicationManager);
            Assert.Same(clientScene.GetComponent<ReplicationManager>(), client.Peer.ReplicationManager);

            // Confirm scene C load on server
            client.IncrementTime(1);
            Assert.Same(sceneC.GetComponent<ReplicationManager>(), server.Peer.ReplicationManager);
            Assert.Same(clientScene.GetComponent<ReplicationManager>(), client.Peer.ReplicationManager);
            Assert.Null(clientScene.FindChild("MarkerA", true));
            Assert.Null(clientScene.FindChild("MarkerB", true));
            Assert.NotNull(clientScene.FindChild("MarkerC", true));
        }
        finally
        {
            pendingParams?.Dispose();
        }
    }

    [Fact]
    public void Set_scene_on_disconnected_server_connection_sends_request_on_connect()
    {
        using var fixture = new ReplicaTestContext();
        using var scene = CreateSceneAndResource(fixture, "Scenes/TestSceneLoader_Connect.bin", "Marker");
        using var pair = new LoaderPair();
        pair.Server.Connection.SetConnected(false);

        SetScene(pair.ServerLoader, scene);
        pair.Server.IncrementTime(1);
        Assert.Equal(0, pair.SentRequests);
        Assert.Equal(0u, pair.RequestCount);

        pair.Server.Connection.RaiseConnected();
        Assert.Equal(1, pair.SentRequests);
        pair.Server.IncrementTime(1);
        Assert.Equal(1u, pair.RequestCount);
        Assert.Null(pair.Server.Peer.ReplicationManager);
    }

    [Fact]
    public void Disconnect_clears_pending_request()
    {
        using var fixture = new ReplicaTestContext();
        using var scene = CreateSceneAndResource(fixture, "Scenes/TestSceneLoader_Disconnect.bin", "Marker");
        using var pair = new LoaderPair();

        SetScene(pair.ServerLoader, scene);
        pair.Server.IncrementTime(1);
        Assert.Equal(1u, pair.RequestCount);
        Assert.NotNull(pair.PendingCompletion);

        // The client answers the request after the server connection is lost.
        pair.Server.Connection.RaiseDisconnected();
        pair.PendingCompletion!();
        pair.Client.IncrementTime(1);

        Assert.Null(pair.Server.Peer.ReplicationManager);
    }

    [Fact]
    public void Set_scene_null_sends_nothing()
    {
        using var fixture = new ReplicaTestContext();
        using var pair = new LoaderPair();

        pair.ServerLoader.SetScene(null);
        pair.Server.IncrementTime(1);
        Assert.Equal(0, pair.SentRequests);
        Assert.Equal(0u, pair.RequestCount);

        // A connect event does not send a request without a scene.
        pair.Server.Connection.RaiseConnected();
        pair.Server.IncrementTime(1);
        Assert.Equal(0, pair.SentRequests);
        Assert.Equal(0u, pair.RequestCount);
    }

    private static void SetScene(ServerSceneLoader loader, Scene scene)
    {
        using var args = new StringVariantMap { ["Scene"] = new Variant(scene.GetFileName()) };
        loader.SetScene(scene, args);
    }

    private static Scene CreateSceneAndResource(ReplicaTestContext fixture, string sceneName, string markerName)
    {
        var scene = new Scene();
        scene.CreateComponent<ReplicationManager>();
        scene.CreateChild(markerName).SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));

        var resource = new BinaryFile();
        Assert.True(scene.Save(resource.AsSerializer()));

        scene.SetFileName(sceneName);
        resource.SetName(sceneName);
        fixture.Context.GetSubsystem<ResourceCache>()!.AddManualResource(resource);
        return scene;
    }

    private static bool LoadSceneFromParams(ReplicaTestContext fixture, Scene scene, StringVariantMap args)
    {
        var sceneName = args["Scene"].GetString();
        var resource = fixture.Context.GetSubsystem<ResourceCache>()!.GetExistingResource<BinaryFile>(sceneName);
        if (resource is null)
            return false;

        var source = resource.AsDeserializer();
        source.Seek(0);
        return scene.Load(source);
    }

    // A connected pair with both loaders. The client callback keeps the last completion action.
    private sealed class LoaderPair : IDisposable
    {
        public LoaderPair()
        {
            (Server, Client) = ManualConnection.CreatePair(new RandomEngine(0));
            Server.Connection.OnSendMessage += CountSentRequest;
            ServerLoader = new ServerSceneLoader(Server.Connection, Server.Peer);
            _clientLoader = new ClientSceneLoader(Client.Connection, Client.Peer);
            _clientScene = new Scene();
            _clientLoader.SetScene(_clientScene);
            _clientLoader.SetLoadRequestedCallback((_, onCompleted) =>
            {
                PendingCompletion = onCompleted;
                ++RequestCount;
            });
        }

        public ManualConnection Server { get; }
        public ManualConnection Client { get; }
        public ServerSceneLoader ServerLoader { get; }
        public Action? PendingCompletion { get; private set; }
        public uint RequestCount { get; private set; }
        public int SentRequests { get; private set; }

        public void Dispose()
        {
            ServerLoader.Dispose();
            _clientLoader.Dispose();
            _clientScene.Dispose();
            Server.Connection.OnSendMessage -= CountSentRequest;
            Server.Dispose();
            Client.Dispose();
        }

        private void CountSentRequest(NetworkConnection connection, NetworkMessageId messageId,
            ReadOnlySpan<byte> messagePayload, PacketType type, string debugInfo, ref bool handled)
        {
            if (messageId == NetworkMessageId.LoadScene)
                ++SentRequests;
        }

        private readonly ClientSceneLoader _clientLoader;
        private readonly Scene _clientScene;
    }
}
