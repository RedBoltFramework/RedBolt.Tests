// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.PhysicsJolt;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class PredictedKinematicControllerTests
{
    [Fact]
    public void Predicted_Jolt_controller_works_standalone()
    {
        using var fixture = new ReplicaTestContext();
        Physics.RegisterPhysicsLibrary(fixture.Context);
        var prefab = CreateTestPrefab();
        using var scene = CreateTestScene();
        scene.CreateComponent<ReplicationManager>();

        var node = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            scene, prefab, "Player", new Vector3f(0.0f, 0.05f, 0.0f));
        var controller = node.GetComponent<PredictedKinematicController>()!;

        SimulateStandalone(0.5f);
        controller.SetWalkVelocity(Vector3f.Forward * 2.0f);
        SimulateStandalone(2.0f);

        Assert.InRange(controller.Velocity.Z, 1.95f, 2.05f);
        Assert.InRange(node.WorldPosition.Z, 3.9f, 4.1f);
        Assert.InRange(node.WorldPosition.Y, -0.05f, 0.1f);
    }

    [Fact]
    public void Client_prediction_is_reconciled_with_Jolt_server()
    {
        using var fixture = new ReplicaTestContext();
        Physics.RegisterPhysicsLibrary(fixture.Context);
        var prefab = CreateTestPrefab();
        using var serverScene = CreateTestScene();
        using var clientScene = CreateTestScene();
        using var simulator = new NetworkSimulator(serverScene);
        simulator.AddClient(clientScene,
            new ConnectionQuality(0.08f, 0.12f, 0.20f, 0.02f, 0.02f));

        var serverNode = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverScene, prefab, "Player", new Vector3f(0.0f, 0.05f, 0.0f));
        serverNode.GetComponent<BehaviorNetworkObject>()!
            .SetOwner(simulator.GetServerToClientConnection(clientScene));

        simulator.SimulateTime(5.0f);
        var clientNode = clientScene.GetChild("Player", true);
        Assert.NotNull(clientNode);
        var clientController = clientNode.GetComponent<PredictedKinematicController>();
        Assert.True(clientController is not null,
            $"Client components: {string.Join(", ", clientNode.GetComponents().Select(component => component.GetTypeName()))}");
        var serverController = serverNode.GetComponent<PredictedKinematicController>()!;

        clientController.SetWalkVelocity(Vector3f.Forward * 2.0f);
        simulator.SimulateTime(2.0f);

        Assert.InRange(clientController.Velocity.Z, 1.95f, 2.05f);
        Assert.InRange(serverController.Velocity.Z, 1.95f, 2.05f);
        Assert.InRange(clientNode.WorldPosition.Z, 3.8f, 4.1f);
        Assert.True(serverNode.WorldPosition.Z < clientNode.WorldPosition.Z);

        clientController.SetWalkVelocity(Vector3f.Zero);
        simulator.SimulateTime(1.0f);
        Assert.True(serverNode.WorldPosition.ApproximatelyEquivalent(clientNode.WorldPosition, 0.05f));
    }

    [Fact]
    public void Jolt_steps_receive_synchronized_network_frames()
    {
        using var fixture = new ReplicaTestContext();
        Physics.RegisterPhysicsLibrary(fixture.Context);
        using var serverScene = CreateTestScene();
        using var clientScene = CreateTestScene();
        var serverWorld = serverScene.GetComponent<PhysicsWorld>()!;
        var clientWorld = clientScene.GetComponent<PhysicsWorld>()!;
        var serverFrames = 0;
        var clientFrames = 0;
        serverWorld.SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(
            (ref PhysicsEvents.PhysicsPreStepEvent eventData) =>
            {
                if (eventData.NetworkFrame is not null)
                    ++serverFrames;
            });
        clientWorld.SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(
            (ref PhysicsEvents.PhysicsPreStepEvent eventData) =>
            {
                if (eventData.NetworkFrame is not null)
                    ++clientFrames;
            });

        using var simulator = new NetworkSimulator(serverScene);
        simulator.AddClient(clientScene, new ConnectionQuality(0.01f, 0.01f, 0.01f, 0.0f, 0.0f));
        simulator.SimulateTime(2.0f);

        Assert.True(serverFrames > 20);
        Assert.True(clientFrames > 20);
    }

    private static Scene CreateTestScene()
    {
        var scene = new Scene();
        var physicsWorld = scene.CreateComponent<PhysicsWorld>();
        physicsWorld.Fps = checked((int)NetworkSimulator.FramesInSecond * 2);

        var floor = scene.CreateChild("Floor");
        floor.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floor.CreateComponent<CollisionShape>().SetBox(new Vector3f(100.0f, 1.0f, 100.0f));
        floor.CreateComponent<RigidBody>();
        return scene;
    }

    private static PrefabResource CreateTestPrefab()
    {
        using var node = new Node();
        var kinematicController = node.CreateComponent<KinematicCharacterController>();
        kinematicController.Height = 2.0f;
        kinematicController.Offset = Vector3f.Up;
        node.CreateComponent<ReplicatedTransform>();
        node.CreateComponent<PredictedKinematicController>();
        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@/PredictedKinematicController/Test.prefab");
    }

    private static void SimulateStandalone(float duration)
    {
        var network = Context.Instance.GetSubsystem<Network>()!;
        const float timeStep = 0.01f;
        var numSteps = checked((int)MathF.Round(duration / timeStep));
        for (var i = 0; i < numSteps; ++i)
        {
            network.Update(timeStep);
            var inputReady = new CoreEvents.InputReadyEvent { TimeStep = timeStep };
            network.SendEvent(ref inputReady);
            var update = new CoreEvents.UpdateEvent { TimeStep = timeStep };
            network.SendEvent(ref update);
            var postUpdate = new CoreEvents.PostUpdateEvent { TimeStep = timeStep };
            network.SendEvent(ref postUpdate);
            var renderUpdate = new CoreEvents.RenderUpdateEvent { TimeStep = timeStep };
            network.SendEvent(ref renderUpdate);
        }
    }
}
