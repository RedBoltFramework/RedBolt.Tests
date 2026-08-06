namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Replica;
using RedBolt.Scene;

public sealed class SceneSynchronizationTests
{
    [Fact]
    public void Scene_is_synchronized_between_client_and_server()
    {
        using var fixture = new ReplicaTestContext();
        const float syncDelay = 0.25f;
        var prefab = CreateSimpleTestPrefab();

        var quality = new ConnectionQuality(0.08f, 0.12f, 0.20f, 0.02f, 0.02f);
        using var serverScene = new Scene();
        Scene[] clientScenes = [new Scene(), new Scene(), new Scene()];
        try
        {
            Matrix3x4 transformReplicatedNodeA;
            Matrix3x4 transformReplicatedNodeB;
            Matrix3x4 transformReplicatedNodeChild1;
            Matrix3x4 transformReplicatedNodeChild2;
            Matrix3x4 transformReplicatedNodeChild4;

            foreach (var clientScene in clientScenes)
                clientScene.CreateChild("Client Only Node");
            var serverOnlyNode = serverScene.CreateChild("Server Only Node");

            var replicatedNodeA = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Replicated Node A");
            replicatedNodeA.SetScale(2.0f);
            transformReplicatedNodeA = replicatedNodeA.GetWorldTransform();

            var replicatedNodeB = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Replicated Node B");
            replicatedNodeB.SetPosition(new Vector3f(-1.0f, 2.0f, 0.5f));
            transformReplicatedNodeB = replicatedNodeB.GetWorldTransform();

            var replicatedNodeChild1 = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                replicatedNodeA, prefab, "Replicated Node Child 1");
            replicatedNodeChild1.SetPosition(new Vector3f(-2.0f, 3.0f, 1.5f));
            transformReplicatedNodeChild1 = replicatedNodeChild1.GetWorldTransform();

            var replicatedNodeChild2 = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                replicatedNodeChild1, prefab, "Replicated Node Child 2");
            replicatedNodeChild2.SetRotation(new Quaternion(90.0f, Vector3f.Up));
            transformReplicatedNodeChild2 = replicatedNodeChild2.GetWorldTransform();

            var serverOnlyChild3 = replicatedNodeB.CreateChild("Server Only Child 3");
            serverOnlyChild3.SetPosition(new Vector3f(-1.0f, 0.0f, 0.0f));

            var replicatedNodeChild4 = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverOnlyChild3, prefab, "Replicated Node Child 4");
            transformReplicatedNodeChild4 = replicatedNodeChild4.GetWorldTransform();

            using var simulator = new NetworkSimulator(serverScene);
            simulator.SimulateTime(10.0f);

            foreach (var clientScene in clientScenes)
                simulator.AddClient(clientScene, quality);
            simulator.SimulateTime(10.0f);

            foreach (var clientScene in clientScenes)
            {
                var clientOnlyNode = clientScene.GetChild("Client Only Node", true)!;
                replicatedNodeA = clientScene.GetChild("Replicated Node A", true)!;
                replicatedNodeB = clientScene.GetChild("Replicated Node B", true)!;
                replicatedNodeChild1 = clientScene.GetChild("Replicated Node Child 1", true)!;
                replicatedNodeChild2 = clientScene.GetChild("Replicated Node Child 2", true)!;
                replicatedNodeChild4 = clientScene.GetChild("Replicated Node Child 4", true)!;

                Assert.Equal(3, clientScene.GetNumChildren());
                Assert.Same(clientScene, clientOnlyNode.Parent);
                Assert.Same(clientScene, replicatedNodeA.Parent);
                Assert.Same(clientScene, replicatedNodeB.Parent);
                Assert.Equal(0, clientOnlyNode.GetNumChildren());
                Assert.Equal(1, replicatedNodeA.GetNumChildren());
                Assert.Same(replicatedNodeA, replicatedNodeChild1.Parent);
                Assert.Equal(1, replicatedNodeChild1.GetNumChildren());
                Assert.Same(replicatedNodeChild1, replicatedNodeChild2.Parent);
                Assert.Equal(0, replicatedNodeChild2.GetNumChildren());
                Assert.Equal(1, replicatedNodeB.GetNumChildren());
                Assert.Same(replicatedNodeB, replicatedNodeChild4.Parent);
                Assert.Equal(0, replicatedNodeChild4.GetNumChildren());

                AssertMatrixEqual(transformReplicatedNodeA, replicatedNodeA.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeB, replicatedNodeB.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild1, replicatedNodeChild1.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild2, replicatedNodeChild2.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild4, replicatedNodeChild4.GetWorldTransform());
            }

            serverOnlyChild3.SetParent(serverScene.GetChild("Replicated Node A", true));
            serverScene.GetChild("Replicated Node Child 1", true)!.SetParent(serverScene);
            simulator.SimulateTime(syncDelay);

            foreach (var clientScene in clientScenes)
            {
                var clientOnlyNode = clientScene.GetChild("Client Only Node", true)!;
                replicatedNodeA = clientScene.GetChild("Replicated Node A", true)!;
                replicatedNodeB = clientScene.GetChild("Replicated Node B", true)!;
                replicatedNodeChild1 = clientScene.GetChild("Replicated Node Child 1", true)!;
                replicatedNodeChild2 = clientScene.GetChild("Replicated Node Child 2", true)!;
                replicatedNodeChild4 = clientScene.GetChild("Replicated Node Child 4", true)!;

                Assert.Equal(4, clientScene.GetNumChildren());
                Assert.Same(clientScene, clientOnlyNode.Parent);
                Assert.Same(clientScene, replicatedNodeA.Parent);
                Assert.Same(clientScene, replicatedNodeB.Parent);
                Assert.Same(clientScene, replicatedNodeChild1.Parent);
                Assert.Equal(0, clientOnlyNode.GetNumChildren());
                Assert.Equal(1, replicatedNodeA.GetNumChildren());
                Assert.Same(replicatedNodeA, replicatedNodeChild4.Parent);
                Assert.Equal(0, replicatedNodeChild4.GetNumChildren());
                Assert.Equal(0, replicatedNodeB.GetNumChildren());
                Assert.Equal(1, replicatedNodeChild1.GetNumChildren());
                Assert.Same(replicatedNodeChild1, replicatedNodeChild2.Parent);
                Assert.Equal(0, replicatedNodeChild2.GetNumChildren());

                AssertMatrixEqual(transformReplicatedNodeA, replicatedNodeA.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeB, replicatedNodeB.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild1, replicatedNodeChild1.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild2, replicatedNodeChild2.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild4, replicatedNodeChild4.GetWorldTransform());
            }

            serverScene.GetChild("Replicated Node A", true)!.Remove();
            NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Replicated Node C");
            simulator.SimulateTime(syncDelay);

            foreach (var clientScene in clientScenes)
            {
                var clientOnlyNode = clientScene.GetChild("Client Only Node", true)!;
                replicatedNodeB = clientScene.GetChild("Replicated Node B", true)!;
                var replicatedNodeC = clientScene.GetChild("Replicated Node C", true)!;
                replicatedNodeChild1 = clientScene.GetChild("Replicated Node Child 1", true)!;
                replicatedNodeChild2 = clientScene.GetChild("Replicated Node Child 2", true)!;

                Assert.Equal(4, clientScene.GetNumChildren());
                Assert.Same(clientScene, clientOnlyNode.Parent);
                Assert.Same(clientScene, replicatedNodeB.Parent);
                Assert.Same(clientScene, replicatedNodeC.Parent);
                Assert.Same(clientScene, replicatedNodeChild1.Parent);
                Assert.Equal(0, clientOnlyNode.GetNumChildren());
                Assert.Equal(0, replicatedNodeB.GetNumChildren());
                Assert.Equal(1, replicatedNodeChild1.GetNumChildren());
                Assert.Same(replicatedNodeChild1, replicatedNodeChild2.Parent);
                Assert.Equal(0, replicatedNodeChild2.GetNumChildren());

                AssertMatrixEqual(transformReplicatedNodeB, replicatedNodeB.GetWorldTransform());
                AssertMatrixEqual(Matrix3x4.Identity, replicatedNodeC.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild1, replicatedNodeChild1.GetWorldTransform());
                AssertMatrixEqual(transformReplicatedNodeChild2, replicatedNodeChild2.GetWorldTransform());
            }

            serverScene.GetChild("Replicated Node Child 2", true)!.SetParent(serverScene);
            serverScene.GetChild("Replicated Node Child 1", true)!.Remove();
            serverScene.GetChild("Replicated Node B", true)!.Remove();
            serverScene.GetChild("Replicated Node C", true)!.Remove();
            simulator.SimulateTime(syncDelay);

            foreach (var clientScene in clientScenes)
            {
                var clientOnlyNode = clientScene.GetChild("Client Only Node", true)!;
                replicatedNodeChild2 = clientScene.GetChild("Replicated Node Child 2", true)!;
                Assert.Equal(2, clientScene.GetNumChildren());
                Assert.Same(clientScene, clientOnlyNode.Parent);
                Assert.Same(clientScene, replicatedNodeChild2.Parent);
                AssertMatrixEqual(transformReplicatedNodeChild2, replicatedNodeChild2.GetWorldTransform());
            }
        }
        finally
        {
            foreach (var clientScene in clientScenes)
                clientScene.Dispose();
        }
    }

    [Fact]
    public void Ownership_is_consistent_on_server_and_on_clients()
    {
        using var fixture = new ReplicaTestContext();
        var prefab = CreateSimpleTestPrefab();
        var quality = new ConnectionQuality(0.08f, 0.12f, 0.20f, 0.02f, 0.02f);
        using var serverScene = new Scene();
        Scene[] clientScenes = [new Scene(), new Scene(), new Scene()];
        try
        {
            using var simulator = new NetworkSimulator(serverScene);
            foreach (var clientScene in clientScenes)
                simulator.AddClient(clientScene, quality);

            var node = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Unowned Node");
            var networkObject = node.FindComponent<NetworkObject>(
                ComponentSearchFlag.Self | ComponentSearchFlag.Derived)!;
            Assert.Equal(NetworkObjectMode.Standalone, networkObject.NetworkMode);

            for (var i = 0; i < clientScenes.Length; ++i)
            {
                node = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                    serverScene, prefab, $"Owned Node {i}");
                networkObject = node.GetDerivedComponent<NetworkObject>()!;
                networkObject.SetOwner(simulator.GetServerToClientConnection(clientScenes[i]));
                Assert.Equal(NetworkObjectMode.Standalone, networkObject.NetworkMode);
            }
            simulator.SimulateTime(10.0f);

            Assert.Equal(NetworkObjectMode.Server, GetObject(serverScene, "Unowned Node").NetworkMode);
            Assert.Equal(NetworkObjectMode.Server, GetObject(serverScene, "Owned Node 0").NetworkMode);
            Assert.Equal(NetworkObjectMode.Server, GetObject(serverScene, "Owned Node 1").NetworkMode);
            Assert.Equal(NetworkObjectMode.Server, GetObject(serverScene, "Owned Node 2").NetworkMode);

            for (var clientIndex = 0; clientIndex < clientScenes.Length; ++clientIndex)
            {
                Assert.Equal(NetworkObjectMode.ClientReplicated,
                    GetObject(clientScenes[clientIndex], "Unowned Node").NetworkMode);
                for (var objectIndex = 0; objectIndex < clientScenes.Length; ++objectIndex)
                {
                    var expectedMode = clientIndex == objectIndex
                        ? NetworkObjectMode.ClientOwned
                        : NetworkObjectMode.ClientReplicated;
                    Assert.Equal(expectedMode,
                        GetObject(clientScenes[clientIndex], $"Owned Node {objectIndex}").NetworkMode);
                }

                var clientReplica = clientScenes[clientIndex]
                    .GetComponent<ReplicationManager>()!.ClientReplica!;
                Assert.Same(GetObject(clientScenes[clientIndex], $"Owned Node {clientIndex}"),
                    clientReplica.OwnedNetworkObject);

                var connection = simulator.GetServerToClientConnection(clientScenes[clientIndex])!;
                var serverReplicator = serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!;
                Assert.Same(GetObject(serverScene, $"Owned Node {clientIndex}"),
                    serverReplicator.GetNetworkObjectOwnedByConnection(connection));
            }

            serverScene.GetChild("Owned Node 0", true)!.Remove();
            serverScene.GetChild("Owned Node 1", true)!.Remove();
            serverScene.GetChild("Owned Node 2", true)!.Remove();
            simulator.SimulateTime(10.0f);

            foreach (var clientScene in clientScenes)
            {
                Assert.Null(clientScene.GetComponent<ReplicationManager>()!.ClientReplica!.OwnedNetworkObject);
                var connection = simulator.GetServerToClientConnection(clientScene)!;
                Assert.Null(serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!
                    .GetNetworkObjectOwnedByConnection(connection));
            }
        }
        finally
        {
            foreach (var clientScene in clientScenes)
                clientScene.Dispose();
        }
    }

    [Fact]
    public void Position_and_rotation_are_synchronized_between_client_and_server()
    {
        using var fixture = new ReplicaTestContext();
        var prefab = CreateSimpleTestPrefab();
        var interpolationQuality = new ConnectionQuality(0.08f, 0.12f, 0.20f, 0, 0);
        var extrapolationQuality = new ConnectionQuality(0.25f, 0.35f, 0.40f, 0, 0);
        const float positionError = ReplicatedTransform.DefaultMovementThreshold;
        const float moveSpeedNodeA = 1.0f;
        const float rotationSpeedNodeA = 10.0f;
        const float moveSpeedNodeB = 0.1f;

        using var serverScene = new Scene();
        using var interpolatingClientScene = new Scene();
        interpolatingClientScene.SetName("Interpolating Scene");
        using var extrapolatingClientScene = new Scene();
        extrapolatingClientScene.SetName("Extrapolation Scene");

        var serverNodeA = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverScene, prefab, "Node");
        var serverTransformA = serverNodeA.GetComponent<ReplicatedTransform>()!;
        serverTransformA.ExtrapolateRotation = true;

        var serverNodeB = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverNodeA, prefab, "Node Child", new Vector3f(0.0f, 0.0f, 1.0f));
        var serverTransformB = serverNodeB.GetComponent<ReplicatedTransform>()!;
        serverTransformB.ExtrapolateRotation = true;

        using var animator = new TransformAnimator(serverScene, serverNodeA, serverNodeB,
            moveSpeedNodeA, rotationSpeedNodeA, moveSpeedNodeB);
        using var simulator = new NetworkSimulator(serverScene);
        var serverReplicator = serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!;
        simulator.SimulateTime(9.0f);

        simulator.AddClient(interpolatingClientScene, interpolationQuality);
        simulator.AddClient(extrapolatingClientScene, extrapolationQuality);
        simulator.SimulateTime(9.0f);

        {
            var clientReplica = interpolatingClientScene.GetComponent<ReplicationManager>()!.ClientReplica!;
            var replicaTime = clientReplica.ReplicaTime;
            var delay = serverReplicator.ServerTime - replicaTime;
            var clientNodeA = interpolatingClientScene.GetChild("Node", true)!;
            var clientNodeB = interpolatingClientScene.GetChild("Node Child", true)!;

            Assert.InRange(delay / NetworkSimulator.FramesInSecond, 0.17, 0.23);
            Assert.True(ToVector3f(serverTransformA.SampleTemporalPosition(replicaTime).Value)
                .ApproximatelyEquivalent(clientNodeA.GetWorldPosition(), positionError));
            Assert.True(serverTransformA.SampleTemporalRotation(replicaTime).Value
                .Equivalent(clientNodeA.GetWorldRotation(), MathDefs.Epsilon));
            Assert.True(ToVector3f(serverTransformB.SampleTemporalPosition(replicaTime).Value)
                .ApproximatelyEquivalent(clientNodeB.GetWorldPosition(), positionError));
            Assert.True(serverTransformB.SampleTemporalRotation(replicaTime).Value
                .Equivalent(clientNodeB.GetWorldRotation(), MathDefs.Epsilon));
        }

        {
            var clientReplica = extrapolatingClientScene.GetComponent<ReplicationManager>()!.ClientReplica!;
            var replicaTime = clientReplica.ReplicaTime;
            var delay = serverReplicator.ServerTime - replicaTime;
            var clientNodeA = extrapolatingClientScene.GetChild("Node", true)!;
            var clientNodeB = extrapolatingClientScene.GetChild("Node Child", true)!;

            Assert.InRange(delay / NetworkSimulator.FramesInSecond, 0.22, 0.28);
            Assert.True(ToVector3f(serverTransformA.SampleTemporalPosition(replicaTime).Value)
                .ApproximatelyEquivalent(clientNodeA.GetWorldPosition(), positionError));
            Assert.True(serverTransformA.SampleTemporalRotation(replicaTime).Value
                .Equivalent(clientNodeA.GetWorldRotation(), MathDefs.Epsilon));
            Assert.True(ToVector3f(serverTransformB.SampleTemporalPosition(replicaTime).Value)
                .ApproximatelyEquivalent(clientNodeB.GetWorldPosition(), 0.002f));
            Assert.True(serverTransformB.SampleTemporalRotation(replicaTime).Value
                .Equivalent(clientNodeB.GetWorldRotation(), MathDefs.Epsilon));
        }
    }

    [Fact]
    public void Prefabs_are_replicated_on_clients()
    {
        using var fixture = new ReplicaTestContext();
        var prefab = CreateComplexTestPrefab();
        var quality = new ConnectionQuality(0.08f, 0.12f, 0.20f, 0.02f, 0.02f);
        using var serverScene = new Scene();
        Scene[] clientScenes = [new Scene(), new Scene(), new Scene()];
        try
        {
            using var simulator = new NetworkSimulator(serverScene);
            foreach (var clientScene in clientScenes)
                simulator.AddClient(clientScene, quality);

            NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Node 1", new Vector3f(1.0f, 0.0f, 0.0f));
            NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
                serverScene, prefab, "Node 2", new Vector3f(2.0f, 0.0f, 0.0f));
            simulator.SimulateTime(10.0f);

            foreach (var clientScene in clientScenes)
            {
                var node1 = clientScene.GetChild("Node 1", true)!;
                var node2 = clientScene.GetChild("Node 2", true)!;
                var node1Child = node1.GetChild("Child")!;
                var node2Child = node2.GetChild("Child")!;
                Assert.NotNull(node1);
                Assert.NotNull(node2);
                Assert.NotNull(node1Child);
                Assert.NotNull(node2Child);

                Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f), node1.GetWorldPosition());
                Assert.Equal(new Vector3f(1.0f, 1.0f, 0.0f), node1Child.GetWorldPosition());
                Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), node2.GetWorldPosition());
                Assert.Equal(new Vector3f(2.0f, 1.0f, 0.0f), node2Child.GetWorldPosition());

                var staticModel1 = node1.GetComponent<StaticModel>()!;
                var staticModel2 = node2.GetComponent<StaticModel>()!;
                var light1 = node1Child.GetComponent<Light>()!;
                var light2 = node2Child.GetComponent<Light>()!;
                Assert.True(staticModel1.GetCastShadows());
                Assert.True(staticModel2.GetCastShadows());
                Assert.True(light1.GetCastShadows());
                Assert.True(light2.GetCastShadows());
                Assert.Equal(Color.Red, light1.GetColor());
                Assert.Equal(Color.Red, light2.GetColor());
            }
        }
        finally
        {
            foreach (var clientScene in clientScenes)
                clientScene.Dispose();
        }
    }

    private static PrefabResource CreateSimpleTestPrefab()
    {
        using var node = new Node();
        node.CreateComponent<ReplicatedTransform>();
        node.CreateComponent<ReplicatedParent>();
        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@/SceneSynchronization/SimpleTest.prefab");
    }

    private static PrefabResource CreateComplexTestPrefab()
    {
        using var node = new Node();
        node.CreateComponent<ReplicatedTransform>();
        node.CreateComponent<ReplicatedParent>();
        node.CreateComponent<StaticModel>().SetCastShadows(true);

        var childNode = node.CreateChild("Child");
        childNode.SetPosition(new Vector3f(0.0f, 1.0f, 0.0f));
        var light = childNode.CreateComponent<Light>();
        light.SetCastShadows(true);
        light.SetColor(Color.Red);

        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@/SceneSynchronization/ComplexTest.prefab");
    }

    private static Vector3f ToVector3f(Vector3d value) =>
        new((float)value.X, (float)value.Y, (float)value.Z);

    private static NetworkObject GetObject(Scene scene, string name) => scene.GetChild(name, true)!
        .FindComponent<NetworkObject>(ComponentSearchFlag.Self | ComponentSearchFlag.Derived)!;

    private static void AssertMatrixEqual(Matrix3x4 expected, Matrix3x4 actual,
        float epsilon = 0.001f)
    {
        Assert.InRange(System.Math.Abs(expected.M00 - actual.M00), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M01 - actual.M01), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M02 - actual.M02), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M03 - actual.M03), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M10 - actual.M10), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M11 - actual.M11), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M12 - actual.M12), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M13 - actual.M13), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M20 - actual.M20), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M21 - actual.M21), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M22 - actual.M22), 0, epsilon);
        Assert.InRange(System.Math.Abs(expected.M23 - actual.M23), 0, epsilon);
    }

    private sealed class TransformAnimator : RedObject
    {
        public TransformAnimator(Scene scene, Node nodeA, Node nodeB,
            float moveSpeedNodeA, float rotationSpeedNodeA, float moveSpeedNodeB)
        {
            _nodeA = nodeA;
            _nodeB = nodeB;
            _moveSpeedNodeA = moveSpeedNodeA;
            _rotationSpeedNodeA = rotationSpeedNodeA;
            _moveSpeedNodeB = moveSpeedNodeB;
            SubscribeToEvent<SceneEvents.SceneUpdateEvent>(scene, HandleSceneUpdate);
        }

        private void HandleSceneUpdate(ref SceneEvents.SceneUpdateEvent eventData)
        {
            _nodeA.Translate(eventData.TimeStep * _moveSpeedNodeA * Vector3f.Left,
                TransformSpace.Parent);
            _nodeA.Rotate(new Quaternion(eventData.TimeStep * _rotationSpeedNodeA, Vector3f.Up),
                TransformSpace.Parent);
            _nodeB.Translate(eventData.TimeStep * _moveSpeedNodeB * Vector3f.Forward,
                TransformSpace.Parent);
        }

        private readonly Node _nodeA;
        private readonly Node _nodeB;
        private readonly float _moveSpeedNodeA;
        private readonly float _rotationSpeedNodeA;
        private readonly float _moveSpeedNodeB;
    }
}
