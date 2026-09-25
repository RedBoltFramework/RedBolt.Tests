// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class KinematicCharacterControllerTests
{
    public KinematicCharacterControllerTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Jolt_character_walks_and_jumps_on_rigid_bodies()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floor = scene.CreateChild("Floor");
        floor.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floor.CreateComponent<CollisionShape>().SetBox(new Vector3f(20.0f, 1.0f, 20.0f));
        floor.CreateComponent<RigidBody>();

        var node = scene.CreateChild("Character");
        node.SetPosition(new Vector3f(0.0f, 0.05f, 0.0f));
        var controller = node.CreateComponent<KinematicCharacterController>();
        controller.Height = 2.0f;
        controller.Offset = Vector3f.Up;

        const float timeStep = 1.0f / 60.0f;
        for (var i = 0; i < 120; ++i)
            world.Update(timeStep);

        Assert.True(controller.OnGround);
        Assert.InRange(node.WorldPosition.Y, -0.05f, 0.1f);

        controller.SetWalkIncrement(Vector3f.Forward * (2.0f * timeStep));
        for (var i = 0; i < 120; ++i)
            world.Update(timeStep);
        Assert.InRange(node.WorldPosition.Z, 3.9f, 4.1f);

        controller.SetWalkIncrement(Vector3f.Zero);
        controller.Jump();
        for (var i = 0; i < 15; ++i)
            world.Update(timeStep);
        Assert.True(node.WorldPosition.Y > 1.0f);

        for (var i = 0; i < 180; ++i)
            world.Update(timeStep);
        Assert.True(controller.OnGround);
        Assert.InRange(node.WorldPosition.Y, -0.05f, 0.1f);
    }

    [Fact]
    public void Jolt_character_is_reflected_with_legacy_attributes()
    {
        var attributes = Context.Instance
            .GetReflection(ObjectReflection<KinematicCharacterController>.TypeId)!.Attributes!;
        Assert.Contains(attributes, attribute => attribute.Name == "Height");
        Assert.Contains(attributes, attribute => attribute.Name == "Diameter");
        Assert.Contains(attributes, attribute => attribute.Name == "Collision Layer");
        Assert.Contains(attributes, attribute => attribute.Name == "Max Slope");
    }

    [Fact]
    public void Jolt_character_reports_sensor_trigger_lifecycle()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var sensorNode = scene.CreateChild("Sensor");
        sensorNode.SetPosition(new Vector3f(0.0f, 0.9f, 0.0f));
        sensorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(4.0f, 2.0f, 4.0f));
        var sensorBody = sensorNode.CreateComponent<RigidBody>();
        sensorBody.IsTrigger = true;

        var characterNode = scene.CreateChild("Character");
        var controller = characterNode.CreateComponent<KinematicCharacterController>();
        controller.Gravity = Vector3f.Zero;

        var starts = 0;
        var ongoing = 0;
        var ends = 0;
        PhysicsEvents.PhysicsCollisionStartEvent startEvent = default;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent eventData) =>
            {
                if (eventData.BodyB == sensorBody)
                {
                    ++starts;
                    startEvent = eventData;
                }
            });
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEvent>(
            (ref PhysicsEvents.PhysicsCollisionEvent eventData) =>
            {
                if (eventData.BodyB == sensorBody)
                    ++ongoing;
            });
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEndEvent>(
            (ref PhysicsEvents.PhysicsCollisionEndEvent eventData) =>
            {
                if (eventData.BodyB == sensorBody)
                    ++ends;
            });

        const float timeStep = 1.0f / 60.0f;
        for (var i = 0; i < 3; ++i)
            world.Update(timeStep);

        Assert.Equal(1, starts);
        Assert.True(ongoing > 0);
        Assert.Null(startEvent.BodyA);
        Assert.Same(sensorBody, startEvent.BodyB);
        Assert.True(startEvent.Trigger);
        Assert.Empty(startEvent.Contacts);

        characterNode.SetWorldPosition(new Vector3f(20.0f, 0.0f, 0.0f));
        for (var i = 0; i < 3; ++i)
            world.Update(timeStep);

        Assert.Equal(1, ends);
    }

    [Fact]
    public void Enabling_character_triggers_mid_overlap_starts_a_new_lifecycle()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var sensorNode = scene.CreateChild("Sensor");
        sensorNode.SetPosition(new Vector3f(0.0f, 0.9f, 0.0f));
        sensorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(4.0f, 2.0f, 4.0f));
        sensorNode.CreateComponent<RigidBody>().IsTrigger = true;

        var characterNode = scene.CreateChild("Character");
        var controller = characterNode.CreateComponent<KinematicCharacterController>();
        controller.Gravity = Vector3f.Zero;
        controller.ActivateTriggers = false;

        var starts = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent _) => ++starts);

        const float timeStep = 1.0f / 60.0f;
        for (var i = 0; i < 2; ++i)
            world.Update(timeStep);
        Assert.Equal(0, starts);

        controller.ActivateTriggers = true;
        world.Update(timeStep);

        Assert.Equal(1, starts);
    }

    [Fact]
    public void Jolt_character_uses_physics_overtime_for_interpolation()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = true;
        world.Gravity = Vector3f.Zero;

        var node = scene.CreateChild("Character");
        var controller = node.CreateComponent<KinematicCharacterController>();
        controller.Gravity = Vector3f.Zero;
        controller.SetWalkIncrement(Vector3f.Right);

        const float fixedTimeStep = 1.0f / 60.0f;
        world.CustomUpdate(1, fixedTimeStep, fixedTimeStep * 0.5f);

        Assert.InRange(controller.RawPosition.X, 0.9f, 1.1f);
        Assert.InRange(node.WorldPosition.X, 0.4f, 0.6f);
    }

    [Fact]
    public void Trigger_character_does_not_push_rigid_bodies()
    {
        var triggerBodyPosition = SimulateCharacterPush(isTrigger: true);
        var solidBodyPosition = SimulateCharacterPush(isTrigger: false);

        Assert.InRange(triggerBodyPosition, 1.95f, 2.05f);
        Assert.True(solidBodyPosition > triggerBodyPosition + 0.25f);
    }

    private static float SimulateCharacterPush(bool isTrigger)
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var bodyNode = scene.CreateChild("Body");
        bodyNode.SetPosition(new Vector3f(0.0f, 0.9f, 2.0f));
        bodyNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = bodyNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;

        var characterNode = scene.CreateChild("Character");
        var controller = characterNode.CreateComponent<KinematicCharacterController>();
        controller.Gravity = Vector3f.Zero;
        controller.IsTrigger = isTrigger;

        const float timeStep = 1.0f / 60.0f;
        controller.SetWalkIncrement(Vector3f.Forward * (2.0f * timeStep));
        for (var i = 0; i < 120; ++i)
            world.Update(timeStep);

        return bodyNode.WorldPosition.Z;
    }
}
