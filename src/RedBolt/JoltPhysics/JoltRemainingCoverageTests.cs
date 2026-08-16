// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt.Bindings.Jolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltRemainingCoverageTests
{
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    public JoltRemainingCoverageTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Physics_world_prunes_weak_registrations_and_rebases_live_bodies()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        AddDeadWeakReference<RigidBody>(world, "_rigidBodies");
        AddDeadWeakReference<CollisionShape>(world, "_collisionShapes");

        var setup = CreateBox(scene, "RebasedBody", new Vector3f(10.0f, 20.0f, 30.0f), 0.0f);
        var originalPosition = setup.Body.Position;

        scene.UpdateWorldOrigin(new Vector3i(2, 3, 4));

        Assert.Equal(originalPosition - new Vector3f(2.0f, 3.0f, 4.0f), setup.Body.Position);

        using (var debug = new DebugRenderer())
            world.DrawDebugGeometry(debug, depthTest: false);

        setup.Body.Dispose();
        AddDeadWeakReference<CollisionShape>(world, "_collisionShapes");
        setup.Shape.Dispose();

        using var worldWithDeadBody = new PhysicsWorld();
        AddDeadWeakReference<RigidBody>(worldWithDeadBody, "_rigidBodies");
        worldWithDeadBody.Dispose();
    }

    [Fact]
    public void Physics_world_contacts_raycast_filters_and_signal_modes_are_deterministic()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var floor = CreateBox(scene, "Floor", new Vector3f(0.0f, -0.5f, 0.0f), 0.0f);
        var sensor = CreateBox(scene, "Sensor", new Vector3f(0.0f, 0.25f, 0.0f), 1.0f);
        sensor.Body.UseGravity = false;

        var starts = 0;
        var ongoing = 0;
        var ends = 0;
        var nodeStarts = 0;
        var nodeOngoing = 0;
        var nodeEnds = 0;
        var maximumContactCount = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent eventData) =>
            {
                ++starts;
                maximumContactCount = System.Math.Max(maximumContactCount, eventData.Contacts.Count);
            });
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEvent>(
            (ref PhysicsEvents.PhysicsCollisionEvent _) => ++ongoing);
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEndEvent>(
            (ref PhysicsEvents.PhysicsCollisionEndEvent _) => ++ends);
        sensor.Node.SubscribeToEvent<PhysicsEvents.NodeCollisionStartEvent>(
            (ref PhysicsEvents.NodeCollisionStartEvent _) => ++nodeStarts);
        sensor.Node.SubscribeToEvent<PhysicsEvents.NodeCollisionEvent>(
            (ref PhysicsEvents.NodeCollisionEvent _) => ++nodeOngoing);
        sensor.Node.SubscribeToEvent<PhysicsEvents.NodeCollisionEndEvent>(
            (ref PhysicsEvents.NodeCollisionEndEvent _) => ++nodeEnds);

        world.Update(1.0f / 60.0f);

        Assert.True(starts > 0);
        Assert.True(ongoing > 0);
        Assert.True(nodeStarts > 0);
        Assert.True(nodeOngoing > 0);
        Assert.True(maximumContactCount > 1);

        var colliding = new List<RigidBody>();
        world.GetCollidingBodies(colliding, floor.Body);
        Assert.Contains(sensor.Body, colliding);
        colliding.Clear();
        world.GetCollidingBodies(colliding, sensor.Body);
        Assert.Contains(floor.Body, colliding);

        sensor.Body.CollisionEventMode = CollisionEventMode.Never;
        colliding.Clear();
        world.GetCollidingBodies(colliding, floor.Body);
        Assert.Empty(colliding);
        sensor.Body.CollisionEventMode = CollisionEventMode.Always;
        colliding.Clear();
        world.GetCollidingBodies(colliding, floor.Body);
        Assert.Contains(sensor.Body, colliding);

        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 5.0f, 0.0f), Vector3f.Down), 10.0f, out _));

        sensor.Node.SetPosition(new Vector3f(20.0f, 0.25f, 0.0f));
        world.Update(1.0f / 60.0f);
        Assert.True(ends > 0);
        Assert.True(nodeEnds > 0);

        using var staticA = new RigidBody();
        using var staticB = new RigidBody();
        Assert.False(InvokeShouldSignal(staticA, staticB));

        staticA.Mass = 1.0f;
        staticB.Mass = 1.0f;
        staticA.CollisionEventMode = CollisionEventMode.Never;
        Assert.False(InvokeShouldSignal(staticA, staticB));

        staticA.CollisionEventMode = CollisionEventMode.Active;
        staticB.CollisionEventMode = CollisionEventMode.Active;
        Assert.False(InvokeShouldSignal(staticA, staticB));

        staticA.CollisionEventMode = CollisionEventMode.Always;
        Assert.True(InvokeShouldSignal(staticA, staticB));
    }

    [Fact]
    public void Physics_world_synthetic_contact_guards_handle_missing_and_detached_bodies()
    {
        using var world = new PhysicsWorld();
        using var first = new RigidBody { Mass = 1.0f, CollisionEventMode = CollisionEventMode.Always };
        using var second = new RigidBody { Mass = 1.0f, CollisionEventMode = CollisionEventMode.Always };
        using var third = new RigidBody { Mass = 1.0f, CollisionEventMode = CollisionEventMode.Always };
        using var fourth = new RigidBody { Mass = 1.0f, CollisionEventMode = CollisionEventMode.Always };

        MapBody(world, 1, first);
        MapBody(world, 2, second);
        MapBody(world, 7, third);
        MapBody(world, 8, fourth);
        AddDeadBodyMapping(world, 5);

        AddSyntheticContact(world, "_currentContacts", 1, 2);
        AddSyntheticContact(world, "_currentContacts", 3, 4);
        AddSyntheticContact(world, "_currentContacts", 5, 6);
        AddSyntheticContact(world, "_previousContacts", 1, 2);
        AddSyntheticContact(world, "_previousContacts", 7, 8);

        InvokeInstance(world, "SendCollisionEvents");

        Assert.Equal(3, GetDictionary(world, "_previousContacts").Count);
        Assert.Equal(3, GetDictionary(world, "_currentContacts").Count);
    }

    [Fact]
    public void Physics_world_collision_callbacks_tolerate_component_removal()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        _ = CreateBox(scene, "Floor", new Vector3f(0.0f, -0.5f, 0.0f), 0.0f);
        _ = CreateBox(scene, "Moving", new Vector3f(0.0f, 0.25f, 0.0f), 1.0f);
        var removed = false;
        RigidBody? removedBody = null;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent eventData) =>
            {
                if (!removed)
                {
                    removed = true;
                    removedBody = eventData.BodyA;
                    eventData.BodyA!.Remove();
                }
            });

        world.Update(1.0f / 60.0f);
        world.Update(1.0f / 60.0f);

        Assert.True(removed);
        Assert.NotNull(removedBody);
        Assert.Null(removedBody!.TryGetNode());
    }

    [Fact]
    public void Rigid_body_fallbacks_rendering_and_shape_selection_are_deterministic()
    {
        using var detached = new RigidBody();
        detached.SetTransform(new Vector3f(3.0f, 4.0f, 5.0f), Quaternion.IDENTITY);
        Assert.Equal(new Vector3f(3.0f, 4.0f, 5.0f), detached.Position);
        Assert.Equal(Quaternion.IDENTITY, detached.Rotation);
        Assert.Equal(Vector3f.Zero, detached.GetVelocityAtPoint(Vector3f.One));
        detached.ApplyForce(Vector3f.One, Vector3f.Zero);
        detached.ApplyImpulse(Vector3f.One, Vector3f.Zero);
        InvokeInstance(detached, "OffsetWorldPosition", new Vector3f(1.0f, 2.0f, 3.0f));

        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        var node = scene.CreateChild("Body");
        var body = node.CreateComponent<RigidBody>();
        body.ApplyAttributes();
        body.Mass = 1.0f;

        var disabledShape = node.CreateComponent<CollisionShape>();
        disabledShape.SetBox(Vector3f.One);
        disabledShape.Enabled = false;
        var activeShape = node.CreateComponent<CollisionShape>();
        activeShape.SetSphere(1.0f);
        body.ReAddBodyToWorld();
        Assert.NotNull(body.JoltBodyId);

        body.Friction = 0.25f;
        body.Restitution = 0.75f;
        Assert.Equal(0.25f, body.Friction);
        Assert.Equal(0.75f, body.Restitution);
        Assert.Equal(Vector3f.Zero, body.GetVelocityAtPoint(new Vector3f(0.25f, 0.0f, 0.0f)));

        body.ApplyForce(Vector3f.Zero, Vector3f.One);
        body.ApplyImpulse(Vector3f.Zero, Vector3f.One);
        using (var debug = new DebugRenderer())
        {
            body.DrawDebugGeometry(debug, depthTest: false);
            body.Enabled = false;
            body.DrawDebugGeometry(debug, depthTest: true);
            body.DrawDebugGeometry(null, depthTest: false);
            body.Enabled = true;
        }

        body.ReleaseBody();
        Assert.Equal(node.WorldPosition, body.Position);
        Assert.Equal(node.WorldRotation, body.Rotation);

        node.RemoveComponent(body);
        body.Dispose();
        activeShape.Dispose();
        disabledShape.Dispose();
    }

    [Fact]
    public void Rigid_body_preparation_covers_gravity_kinematics_and_threaded_dirty_updates()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var setup = CreateBox(scene, "PreparedBody", Vector3f.Zero, 1.0f);
        setup.Body.UseGravity = true;
        setup.Body.GravityOverride = new Vector3f(0.0f, -20.0f, 0.0f);
        world.Update(1.0f / 60.0f);

        setup.Body.GravityOverride = Vector3f.Zero;
        setup.Body.IsKinematic = true;
        world.Update(1.0f / 60.0f);
        setup.Body.SetTransform(new Vector3f(2.0f, 1.0f, 0.0f), Quaternion.IDENTITY);
        world.Update(1.0f / 60.0f);

        SetPrivateField(scene, "_threadedUpdate", true);
        setup.Node.SetPosition(new Vector3f(4.0f, 2.0f, 0.0f));
        Assert.True(scene.IsThreadedUpdate());
        scene.EndThreadedUpdate();
        Assert.False(scene.IsThreadedUpdate());
    }

    [Fact]
    public void Rigid_body_dead_world_fallback_and_missing_shape_paths_are_safe()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        var node = scene.CreateChild("BodyWithoutShape");
        var body = node.CreateComponent<RigidBody>();
        body.ApplyAttributes();
        Assert.Null(body.JoltBodyId);
        Assert.Null(InvokeInstance(body, "GetActiveCollisionShape"));

        var shape = node.CreateComponent<CollisionShape>();
        shape.SetBox(Vector3f.One);
        body.ReAddBodyToWorld();
        body.Mass = 1.0f;
        Assert.NotNull(body.JoltBodyId);

        var physicsWorldField = typeof(RigidBody).GetField("_physicsWorld", InstanceNonPublic)!;
        var savedPhysicsWorld = physicsWorldField.GetValue(body);
        physicsWorldField.SetValue(body, new WeakReference<PhysicsWorld>(null!));
        InvokeInstance(body, "ApplyWorldTransform", 0.5f);
        physicsWorldField.SetValue(body, savedPhysicsWorld);

        body.Dispose();
        shape.Dispose();
    }

    private static (Node Node, CollisionShape Shape, RigidBody Body) CreateBox(
        Scene scene, string name, Vector3f position, float mass, bool trigger = false)
    {
        var node = scene.CreateChild(name);
        node.SetPosition(position);
        var shape = node.CreateComponent<CollisionShape>();
        shape.SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();
        body.Mass = mass;
        body.UseGravity = false;
        body.IsTrigger = trigger;
        return (node, shape, body);
    }

    private static void AddDeadWeakReference<T>(object owner, string fieldName)
        where T : class
    {
        var field = owner.GetType().GetField(fieldName, InstanceNonPublic)!;
        var entries = (IList)field.GetValue(owner)!;
        entries.Add(new WeakReference<T>(null!));
    }

    private static void MapBody(PhysicsWorld world, uint bodyId, RigidBody body)
    {
        GetDictionary(world, "_bodiesById")[bodyId] = new WeakReference<RigidBody>(body);
    }

    private static void AddDeadBodyMapping(PhysicsWorld world, uint bodyId)
    {
        GetDictionary(world, "_bodiesById")[bodyId] = new WeakReference<RigidBody>(null!);
    }

    private static void AddSyntheticContact(PhysicsWorld world, string fieldName, uint body1, uint body2)
    {
        var pairType = typeof(PhysicsWorld).GetNestedType("BodyPair", BindingFlags.NonPublic)!;
        var pairConstructor = pairType.GetConstructor(
            InstanceNonPublic | BindingFlags.Public, null, new[] { typeof(uint), typeof(uint) }, null)!;
        var pair = pairConstructor.Invoke(new object?[] { body1, body2 });

        var contactType = typeof(PhysicsWorld).GetNestedType("ContactData", BindingFlags.NonPublic)!;
        var contactConstructor = contactType.GetConstructors(InstanceNonPublic | BindingFlags.Public)
            .Single(constructor => constructor.GetParameters().Length == 4);
        var contact = contactConstructor.Invoke(new object?[] { pair, false, false, new[] { new PhysicsContact() } });
        GetDictionary(world, fieldName)[pair] = contact;
    }

    private static IDictionary GetDictionary(object owner, string fieldName)
    {
        var field = owner.GetType().GetField(fieldName, InstanceNonPublic)!;
        return (IDictionary)field.GetValue(owner)!;
    }

    private static bool InvokeShouldSignal(RigidBody first, RigidBody second) =>
        (bool)typeof(PhysicsWorld).GetMethod("ShouldSignal", StaticNonPublic)!
            .Invoke(null, new object?[] { first, second })!;

    private static object? InvokeInstance(object target, string methodName, params object?[] arguments) =>
        target.GetType().GetMethod(methodName, InstanceNonPublic)!.Invoke(target, arguments);

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        target.GetType().GetField(fieldName, InstanceNonPublic)!.SetValue(target, value);
    }
}
