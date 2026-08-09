namespace RedBolt.Tests;

using RedBolt.Bindings.Jolt;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltCoverageGapTests
{
    public JoltCoverageGapTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Collision_shape_detached_defaults_and_unsupported_paths_are_safe()
    {
        using var shape = new CollisionShape();

        Assert.Equal(ShapeType.Box, shape.ShapeType);
        Assert.Equal(Vector3f.One, shape.Size);
        Assert.Equal(Vector3f.Zero, shape.Position);
        Assert.Equal(Quaternion.IDENTITY, shape.Rotation);
        Assert.Equal(0.04f, shape.Margin);
        Assert.True(shape.IsSupported);
        Assert.Null(shape.JoltShape);

        shape.SetBox(Vector3f.Zero);
        shape.SetSphere(0.0f);
        shape.SetCylinder(0.0f, 0.0f);
        shape.SetCapsule(0.0f, 0.0f);
        Assert.Null(shape.JoltShape);

        shape.Size = new Vector3f(-2.0f, 3.0f, -4.0f);
        shape.Position = new Vector3f(1.0f, 2.0f, 3.0f);
        shape.Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f).Normalized;
        shape.SetTransform(shape.Position, shape.Rotation);
        shape.Margin = -1.0f;

        Assert.Equal(0.0f, shape.Margin);
        Assert.Equal(new Vector3f(-2.0f, 3.0f, -4.0f), shape.Size);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), shape.Position);

        shape.ShapeType = ShapeType.TriangleMesh;
        Assert.False(shape.IsSupported);
        Assert.Null(shape.JoltShape);
        shape.ShapeType = ShapeType.Box;
        Assert.True(shape.IsSupported);
        Assert.Null(shape.JoltShape);

        shape.NotifyRigidBody();
        shape.ReleaseShape();
    }

    [Fact]
    public void Collision_shape_builds_supported_shapes_and_decorated_variants()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Shape");
        var shape = node.CreateComponent<CollisionShape>();

        Assert.NotNull(shape.JoltShape);

        shape.SetBox(
            Vector3f.Zero,
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Quaternion(0.1f, 0.2f, 0.3f, 0.9f).Normalized);
        Assert.NotNull(shape.JoltShape);

        shape.SetSphere(2.0f);
        Assert.NotNull(shape.JoltShape);
        shape.ShapeType = ShapeType.Sphere;
        shape.Size = new Vector3f(1.0f, 2.0f, 3.0f);
        Assert.NotNull(shape.JoltShape);

        shape.SetCylinder(0.0f, 0.0f);
        Assert.NotNull(shape.JoltShape);
        shape.SetCapsule(0.0f, 0.0f);
        Assert.NotNull(shape.JoltShape);

        shape.SetTransform(Vector3f.Zero, Quaternion.IDENTITY);
        shape.SetTransform(Vector3f.Zero, Quaternion.IDENTITY);
        shape.ShapeType = ShapeType.Terrain;
        Assert.False(shape.IsSupported);
        Assert.Null(shape.JoltShape);
        shape.ShapeType = ShapeType.Box;
        Assert.NotNull(shape.JoltShape);

        node.SetScale(new Vector3f(2.0f, 1.0f, 0.5f));
        node.SetScale(new Vector3f(2.0f, 1.0f, 0.5f));
        Assert.NotNull(shape.JoltShape);

        shape.ReleaseShape();
        Assert.Null(shape.JoltShape);
        shape.SetBox(Vector3f.One);
        Assert.NotNull(shape.JoltShape);
    }

    [Fact]
    public void Detached_rigid_body_properties_clamp_and_native_shortcuts_are_safe()
    {
        using var body = new RigidBody();

        Assert.Null(body.JoltBodyId);
        Assert.Null(body.PhysicsWorld);
        Assert.False(body.IsActive);
        Assert.Equal(Vector3f.Zero, body.Position);
        Assert.Equal(Quaternion.IDENTITY, body.Rotation);
        Assert.Equal(Vector3f.One, body.LinearFactor);
        Assert.Equal(Vector3f.One, body.AngularFactor);

        body.Mass = -1.0f;
        body.Friction = -0.25f;
        body.Restitution = 0.75f;
        body.LinearVelocity = new Vector3f(1.0f, 2.0f, 3.0f);
        body.AngularVelocity = new Vector3f(4.0f, 5.0f, 6.0f);
        body.LinearFactor = Vector3f.Zero;
        body.AngularFactor = new Vector3f(0.0f, 1.0f, 0.0f);
        body.LinearRestThreshold = -1.0f;
        body.AngularRestThreshold = -1.0f;
        body.LinearDamping = -1.0f;
        body.AngularDamping = -1.0f;
        body.CcdRadius = -1.0f;
        body.CcdMotionThreshold = -1.0f;
        body.UseGravity = false;
        body.GravityOverride = new Vector3f(0.0f, -5.0f, 0.0f);
        body.IsKinematic = true;
        body.IsTrigger = true;
        body.CollisionLayer = 3;
        body.CollisionMask = 7;
        body.CollisionEventMode = CollisionEventMode.Always;
        body.SetTransform(new Vector3f(7.0f, 8.0f, 9.0f), Quaternion.IDENTITY);

        Assert.Equal(0.0f, body.Mass);
        Assert.Equal(-0.25f, body.Friction);
        Assert.Equal(0.75f, body.Restitution);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), body.LinearVelocity);
        Assert.Equal(new Vector3f(4.0f, 5.0f, 6.0f), body.AngularVelocity);
        Assert.Equal(Vector3f.Zero, body.LinearFactor);
        Assert.Equal(new Vector3f(0.0f, 1.0f, 0.0f), body.AngularFactor);
        Assert.Equal(0.0f, body.LinearRestThreshold);
        Assert.Equal(0.0f, body.AngularRestThreshold);
        Assert.Equal(0.0f, body.LinearDamping);
        Assert.Equal(0.0f, body.AngularDamping);
        Assert.Equal(0.0f, body.CcdRadius);
        Assert.Equal(0.0f, body.CcdMotionThreshold);
        Assert.False(body.UseGravity);
        Assert.Equal(new Vector3f(0.0f, -5.0f, 0.0f), body.GravityOverride);
        Assert.True(body.IsKinematic);
        Assert.True(body.IsTrigger);
        Assert.Equal(3u, body.CollisionLayer);
        Assert.Equal(7u, body.CollisionMask);
        Assert.Equal(CollisionEventMode.Always, body.CollisionEventMode);
        Assert.Equal(new Vector3f(7.0f, 8.0f, 9.0f), body.Position);

        body.Position = new Vector3f(10.0f, 11.0f, 12.0f);
        body.Rotation = new Quaternion(0.0f, 0.25f, 0.0f, 1.0f).Normalized;
        Assert.Equal(new Vector3f(10.0f, 11.0f, 12.0f), body.Position);
        Assert.Equal(body.Rotation, body.Rotation);

        body.SetCollisionLayerAndMask(3, 7);
        body.SetCollisionLayerAndMask(9, 11);
        Assert.Equal(9u, body.CollisionLayer);
        Assert.Equal(11u, body.CollisionMask);

        var colliding = new List<RigidBody> { new() };
        body.GetCollidingBodies(colliding);
        Assert.Empty(colliding);
        Assert.Throws<ArgumentNullException>(() => body.GetCollidingBodies(null!));

        body.ApplyForce(Vector3f.Zero);
        body.ApplyForce(Vector3f.One);
        body.ApplyForce(Vector3f.One, Vector3f.Zero);
        body.ApplyTorque(Vector3f.Zero);
        body.ApplyTorque(Vector3f.One);
        body.ApplyImpulse(Vector3f.Zero);
        body.ApplyImpulse(Vector3f.One);
        body.ApplyImpulse(Vector3f.One, Vector3f.Zero);
        body.ApplyTorqueImpulse(Vector3f.Zero);
        body.ApplyTorqueImpulse(Vector3f.One);
        body.Activate();
        body.ReAddBodyToWorld();
        body.ReleaseBody();
    }

    [Fact]
    public void Attached_rigid_body_rebuilds_motion_layers_and_property_paths()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var node = scene.CreateChild("Body");
        var shape = node.CreateComponent<CollisionShape>();
        shape.SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();

        Assert.Same(world, body.PhysicsWorld);
        Assert.NotNull(body.JoltBodyId);
        Assert.False(body.IsActive);

        body.Mass = 2.0f;
        body.LinearFactor = new Vector3f(1.0f, 0.0f, 1.0f);
        body.AngularFactor = new Vector3f(0.0f, 1.0f, 1.0f);
        body.LinearDamping = 0.1f;
        body.AngularDamping = 0.2f;
        body.CcdMotionThreshold = 0.5f;
        body.Friction = 0.4f;
        body.Restitution = 0.2f;
        body.LinearVelocity = new Vector3f(1.0f, 0.0f, 0.0f);
        body.AngularVelocity = new Vector3f(0.0f, 1.0f, 0.0f);
        body.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.IDENTITY);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), body.Position);
        Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f), body.LinearVelocity);
        Assert.Equal(new Vector3f(0.0f, 1.0f, 0.0f), body.AngularVelocity);
        Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f), body.GetVelocityAtPoint(Vector3f.Zero));

        body.UseGravity = false;
        body.GravityOverride = new Vector3f(0.0f, -20.0f, 0.0f);
        body.IsTrigger = true;
        body.CollisionLayer = 2;
        body.CollisionMask = 0x10;
        body.SetCollisionLayerAndMask(4, 0x20);
        body.ApplyForce(Vector3f.One);
        body.ApplyForce(Vector3f.One, Vector3f.Zero);
        body.ApplyTorque(Vector3f.One);
        body.ApplyImpulse(Vector3f.One);
        body.ApplyImpulse(Vector3f.One, Vector3f.Zero);
        body.ApplyTorqueImpulse(Vector3f.One);
        body.Activate();

        body.IsKinematic = true;
        Assert.NotNull(body.JoltBodyId);
        body.IsKinematic = false;
        body.Mass = 0.0f;
        Assert.NotNull(body.JoltBodyId);

        body.Enabled = false;
        Assert.Null(body.JoltBodyId);
        body.Enabled = true;
        Assert.NotNull(body.JoltBodyId);

        var colliding = new List<RigidBody> { body };
        body.GetCollidingBodies(colliding);
        Assert.Empty(colliding);

        body.ReleaseBody();
        Assert.Null(body.JoltBodyId);
        body.ReleaseBody();
        body.ReAddBodyToWorld();
        Assert.NotNull(body.JoltBodyId);
    }

    [Fact]
    public void Physics_world_boundaries_update_without_bodies_and_raycast_safely()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        Assert.Equal(new Vector3f(0.0f, -9.81f, 0.0f), world.Gravity);
        Assert.Equal(60, world.Fps);
        Assert.Equal(0, world.MaxSubSteps);
        Assert.Equal(10, world.NumIterations);
        Assert.True(world.Interpolation);
        Assert.False(world.IsSimulating);
        Assert.NotNull(world.World);
        Assert.NotNull(world.BodyInterface);
        Assert.NotNull(world.NarrowPhaseQuery);
        Assert.NotNull(world.TempAllocator);
        Assert.NotNull(world.JobSystem);

        world.Gravity = Vector3f.Zero;
        world.Fps = 0;
        Assert.Equal(1, world.Fps);
        world.Fps = 2000;
        Assert.Equal(1000, world.Fps);
        world.MaxSubSteps = -1;
        world.NumIterations = 0;
        Assert.Equal(1, world.NumIterations);
        world.NumIterations = 1000;
        Assert.Equal(256, world.NumIterations);
        world.Interpolation = false;

        var body = new RigidBody();
        var ray = new Ray(Vector3f.Zero, Vector3f.Down);
        Assert.False(world.TryRaycastSingle(ray, 0.0f, out var invalidHit));
        Assert.Equal(default, invalidHit);
        var noHit = new PhysicsRaycastResult
        {
            Body = body,
            Distance = 1.0f,
            HitFraction = 0.5f
        };
        world.RaycastSingle(ref noHit, ray, 0.0f);
        Assert.Equal(default, noHit);

        var result = new List<RigidBody> { body };
        world.GetCollidingBodies(result, body);
        Assert.Empty(result);
        Assert.Throws<ArgumentNullException>(() => world.GetCollidingBodies(null!, body));
        Assert.Throws<ArgumentNullException>(() => world.GetCollidingBodies(result, null!));
        world.DrawDebugGeometry(null, false);
        body.Dispose();
    }

}
