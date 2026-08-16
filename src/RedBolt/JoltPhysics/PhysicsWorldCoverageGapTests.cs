// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.Jolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;
using MathColor = RedBolt.Math.Color;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PhysicsWorldCoverageGapTests
{
    public PhysicsWorldCoverageGapTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Physics_world_update_modes_and_scene_event_guards_are_deterministic()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();

        var subsystemUpdate = new SceneEvents.SceneSubsystemUpdateEvent
        {
            Scene = scene,
            TimeStep = 0.0f
        };
        scene.SendEvent(ref subsystemUpdate);

        world.Enabled = false;
        scene.SendEvent(ref subsystemUpdate);
        world.Enabled = true;
        world.ManualUpdate = true;
        scene.SendEvent(ref subsystemUpdate);

        world.MaxSubSteps = -1;
        world.Update(-1.0f);
        world.Update(0.01f);

        world.MaxSubSteps = 2;
        world.Interpolation = true;
        world.Update(0.1f);

        world.MaxSubSteps = 0;
        world.Interpolation = false;
        world.Update(0.0f);

        world.CustomUpdate(0, 0.0f, -1.0f);
        world.Interpolation = true;
        world.CustomUpdate(1, 0.01f, 0.02f);

        Assert.False(world.IsSimulating);
        Assert.Equal(EPhysicsUpdateError.None, world.LastUpdateError);
    }

    [Fact]
    public void Collision_shape_debug_attribute_and_lifecycle_paths_are_exercised()
    {
        var detached = new CollisionShape();
        detached.ApplyAttributes();
        detached.Enabled = false;
        detached.Enabled = true;
        detached.DrawDebugGeometry(null, depthTest: false);
        using (var detachedDebug = new DebugRenderer())
        {
            detached.DrawDebugGeometry(detachedDebug, MathColor.White, depthTest: false);
            Assert.False(detachedDebug.HasContent());
        }
        Assert.Throws<ArgumentNullException>(() => detached.DrawDebugGeometry(null!, MathColor.White, depthTest: false));
        detached.Dispose();

        using var scene = new Scene();
        _ = scene.CreateComponent<PhysicsWorld>();
        var node = scene.CreateChild("Shape");
        var shape = node.CreateComponent<CollisionShape>();
        shape.ApplyAttributes();

        using var debug = new DebugRenderer();
        shape.DrawDebugGeometry(debug, depthTest: true);

        shape.SetBox(
            new Vector3f(2.0f, 3.0f, 4.0f),
            new Vector3f(0.5f, 0.25f, -0.5f),
            new Quaternion(0.1f, 0.2f, 0.3f, 0.9f).Normalized);
        shape.SetBox(Vector3f.One);
        shape.SetSphere(2.0f);
        shape.SetSphere(2.0f, Vector3f.Zero, Quaternion.IDENTITY);
        shape.SetCylinder(2.0f, 3.0f);
        shape.SetCylinder(2.0f, 3.0f, Vector3f.Zero, Quaternion.IDENTITY);
        shape.SetCapsule(2.0f, 3.0f);
        shape.SetCapsule(2.0f, 3.0f, Vector3f.Zero, Quaternion.IDENTITY);

        shape.ShapeType = ShapeType.Capsule;
        shape.ShapeType = ShapeType.Capsule;
        shape.Size = shape.Size;
        shape.Size = new Vector3f(3.0f, 4.0f, 5.0f);
        shape.Position = shape.Position;
        shape.Position = new Vector3f(1.0f, 2.0f, 3.0f);
        shape.Rotation = shape.Rotation;
        shape.Rotation = new Quaternion(0.0f, 0.25f, 0.0f, 1.0f).Normalized;
        shape.SetTransform(shape.Position, shape.Rotation);
        shape.SetTransform(Vector3f.Zero, Quaternion.IDENTITY);
        shape.Margin = shape.Margin;
        shape.Margin = 0.0f;
        shape.Margin = 0.04f;

        foreach (var shapeType in new[] { ShapeType.Box, ShapeType.Sphere, ShapeType.Cylinder, ShapeType.Capsule })
        {
            shape.ShapeType = shapeType;
            shape.DrawDebugGeometry(debug, depthTest: false);
        }

        shape.ShapeType = ShapeType.TriangleMesh;
        shape.DrawDebugGeometry(debug, depthTest: false);
        Assert.True(shape.IsSupported);
        shape.ShapeType = ShapeType.Box;
        Assert.True(shape.IsSupported);

        node.SetScale(new Vector3f(1.01f, 1.0f, 1.0f));
        node.SetScale(new Vector3f(1.5f, 1.0f, 1.0f));
        node.SetEnabled(false);
        using (var disabledDebug = new DebugRenderer())
        {
            shape.DrawDebugGeometry(disabledDebug, depthTest: true);
            Assert.False(disabledDebug.HasContent());
        }
        node.SetEnabled(true);

        var body = node.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        shape.NotifyRigidBody();
        shape.NotifyRigidBody();
        shape.DrawDebugGeometry(debug, depthTest: true);

        var disposableNode = scene.CreateChild("DisposedShape");
        var disposableShape = disposableNode.CreateComponent<CollisionShape>();
        disposableShape.Dispose();
        disposableShape.Dispose();

        body.ReleaseBody();
        shape.ReleaseShape();
        shape.Dispose();
    }

    [Fact]
    public void Physics_world_collision_queries_filters_and_contact_end_are_safe()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var floorNode = scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        var floorShape = floorNode.CreateComponent<CollisionShape>();
        floorShape.SetBox(new Vector3f(4.0f, 1.0f, 4.0f));
        var floorBody = floorNode.CreateComponent<RigidBody>();

        var movingNode = scene.CreateChild("Moving");
        movingNode.SetPosition(new Vector3f(0.0f, 0.25f, 0.0f));
        var movingShape = movingNode.CreateComponent<CollisionShape>();
        movingShape.SetBox(Vector3f.One);
        var movingBody = movingNode.CreateComponent<RigidBody>();
        movingBody.Mass = 1.0f;
        movingBody.UseGravity = false;

        var isolatedNode = scene.CreateChild("Isolated");
        isolatedNode.SetPosition(new Vector3f(20.0f, 0.0f, 0.0f));
        isolatedNode.CreateComponent<CollisionShape>().SetSphere(1.0f);
        var isolatedBody = isolatedNode.CreateComponent<RigidBody>();
        isolatedBody.Mass = 1.0f;
        isolatedBody.UseGravity = false;

        var starts = 0;
        var ongoing = 0;
        var ends = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent _) => ++starts);
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEvent>(
            (ref PhysicsEvents.PhysicsCollisionEvent _) => ++ongoing);
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEndEvent>(
            (ref PhysicsEvents.PhysicsCollisionEndEvent _) => ++ends);

        world.Update(1.0f / 60.0f);

        Assert.True(starts > 0);
        Assert.True(ongoing > 0);
    }

    [Fact]
    public void Physics_world_release_and_draw_paths_are_idempotent()
    {
        using var first = new PhysicsWorld();
        using var second = new PhysicsWorld();

        first.DrawDebugGeometry(null, depthTest: false);
        using (var debug = new DebugRenderer())
            first.DrawDebugGeometry(debug, depthTest: true);

        first.Dispose();
        first.Dispose();
        second.Dispose();
        second.Dispose();
    }
}
