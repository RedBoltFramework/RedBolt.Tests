namespace RedBolt.Tests;

using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltPhysicsTests
{
    public JoltPhysicsTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Dynamic_body_falls_collides_and_can_be_raycast()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floorNode = scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(20.0f, 1.0f, 20.0f));
        floorNode.CreateComponent<RigidBody>();

        var boxNode = scene.CreateChild("Box");
        boxNode.SetPosition(new Vector3f(0.0f, 4.0f, 0.0f));
        boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = boxNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;

        var collisionStarts = 0;
        var collisions = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent _) => ++collisionStarts);
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEvent>(
            (ref PhysicsEvents.PhysicsCollisionEvent _) => ++collisions);

        for (var i = 0; i < 180; ++i)
            world.Update(1.0f / 60.0f);

        Assert.InRange(boxNode.WorldPosition.Y, 0.45f, 0.65f);
        Assert.NotNull(body.JoltBodyId);
        Assert.True(collisionStarts > 0);
        Assert.True(collisions > 0);

        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 5.0f, 0.0f), Vector3f.Down), 10.0f, out var hit));
        Assert.Same(body, hit.Body);
        Assert.InRange(hit.Position.Y, 0.9f, 1.1f);
    }

    [Fact]
    public void Physics_components_publish_reflection_attributes_and_native_access()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        var node = scene.CreateChild("Body");
        node.CreateComponent<CollisionShape>().SetSphere(1.0f);
        var body = node.CreateComponent<RigidBody>();

        Assert.NotNull(world.World);
        Assert.NotNull(world.BodyInterface);
        Assert.NotNull(world.NarrowPhaseQuery);
        Assert.NotNull(body.JoltBodyId);

        var worldAttributes = Context.Instance.GetReflection(ObjectReflection<PhysicsWorld>.TypeId)!.Attributes!;
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Gravity");
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Physics FPS");
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Interpolation");

        var bodyAttributes = Context.Instance.GetReflection(ObjectReflection<RigidBody>.TypeId)!.Attributes!;
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Mass");
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Collision Layer");
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Collision Mask");
    }

    [Fact]
    public void Raycast_reports_unregistered_native_body_without_dereferencing_invalid_hits()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        using var nativeShape = new BoxShape(new Vector3f(0.5f, 0.5f, 0.5f));
        using var settings = new BodyCreationSettings(
            nativeShape, Vector3f.Zero, Quaternion.IDENTITY, EMotionType.Static,
            ObjectLayerPairFilterMask.SGetObjectLayer(2, uint.MaxValue & 0xffff));
        var bodyId = world.BodyInterface.CreateAndAddBody(settings, EActivation.DontActivate);

        try
        {
            Assert.True(world.TryRaycastSingle(
                new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down), 4.0f, out var hit, 2));
            Assert.Null(hit.Body);
            Assert.InRange(hit.Position.Y, 0.49f, 0.51f);
            Assert.True(hit.Normal.Y > 0.9f);
        }
        finally
        {
            world.BodyInterface.RemoveBody(in bodyId);
            world.BodyInterface.DestroyBody(in bodyId);
        }
    }

    [Fact]
    public void Parented_rigid_body_world_transforms_follow_parent_first()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var parentNode = scene.CreateChild("Parent");
        var childNode = parentNode.CreateChild("Child");
        childNode.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));

        childNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var childBody = childNode.CreateComponent<RigidBody>();
        childBody.Mass = 1.0f;
        childBody.UseGravity = false;

        parentNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var parentBody = parentNode.CreateComponent<RigidBody>();
        parentBody.Mass = 1.0f;
        parentBody.UseGravity = false;

        parentBody.LinearVelocity = Vector3f.Right;
        childBody.LinearVelocity = Vector3f.Right;
        world.Update(1.0f / 60.0f);

        Assert.InRange(parentNode.WorldPosition.X, 0.015f, 0.018f);
        Assert.InRange(childNode.WorldPosition.X, 0.015f, 0.018f);
        Assert.InRange(childNode.Position.X, -0.001f, 0.001f);
    }
}
