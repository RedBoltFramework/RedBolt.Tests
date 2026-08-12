namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PhysicsQueryParityTests
{
    public PhysicsQueryParityTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Raycasts_return_ordered_filtered_and_segmented_results()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var near = CreateBox(scene, "Near", new Vector3f(0.0f, 3.0f, 0.0f));
        var far = CreateBox(scene, "Far", Vector3f.Zero);
        world.UpdateCollisions();

        var hits = new List<PhysicsRaycastResult>();
        var ray = new Ray(new Vector3f(0.0f, 6.0f, 0.0f), Vector3f.Down);
        world.Raycast(hits, ray, 10.0f);

        Assert.Equal(2, hits.Count);
        Assert.True(ReferenceEquals(near.Body, hits[0].Body), "Closest all-hit ray body was not mapped");
        Assert.True(ReferenceEquals(far.Body, hits[1].Body), "Farthest all-hit ray body was not mapped");
        Assert.True(hits[0].Distance < hits[1].Distance);

        var segmented = default(PhysicsRaycastResult);
        world.RaycastSingleSegmented(ref segmented, ray, 10.0f, 2.0f);
        Assert.True(ReferenceEquals(near.Body, segmented.Body),
            $"Segmented ray body was not mapped: position={segmented.Position}, distance={segmented.Distance}, body={segmented.Body}");
        Assert.InRange(segmented.Distance, 2.45f, 2.55f);

        far.Body.CollisionLayer = 0x2;
        hits.Clear();
        world.Raycast(hits, ray, 10.0f, 0x1);
        Assert.Single(hits);
        Assert.True(ReferenceEquals(near.Body, hits[0].Body), "Filtered all-hit ray body was not mapped");

        var miss = default(PhysicsRaycastResult);
        world.RaycastSingle(ref miss, new Ray(new Vector3f(100.0f, 0.0f, 0.0f), Vector3f.Up), 2.0f);
        Assert.Null(miss.Body);
        Assert.True(float.IsPositiveInfinity(miss.Distance));
        Assert.Equal(0.0f, miss.HitFraction);
    }

    [Fact]
    public void Sweeps_and_overlap_queries_use_narrow_phase_and_ignore_cast_source()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var target = CreateBox(scene, "Target", Vector3f.Zero);
        var overlap = CreateBox(scene, "Overlap", new Vector3f(0.75f, 0.0f, 0.0f));
        var source = CreateBox(scene, "Source", new Vector3f(-4.0f, 0.0f, 0.0f));
        var distant = CreateBox(scene, "Distant", new Vector3f(10.0f, 0.0f, 0.0f));
        world.UpdateCollisions();

        var sphereHit = default(PhysicsRaycastResult);
        world.SphereCast(ref sphereHit,
            new Ray(new Vector3f(-2.0f, 0.0f, 0.0f), Vector3f.Right), 0.25f, 4.0f);
        Assert.NotNull(sphereHit.Body);
        Assert.Contains(sphereHit.Body, new[] { target.Body, overlap.Body });

        var convexHit = default(PhysicsRaycastResult);
        world.ConvexCast(ref convexHit, source.Shape, source.Node.WorldPosition, Quaternion.IDENTITY,
            new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.IDENTITY);
        Assert.NotNull(convexHit.Body);
        Assert.NotSame(source.Body, convexHit.Body);
        Assert.Contains(convexHit.Body, new[] { target.Body, overlap.Body });

        var bodies = new List<RigidBody>();
        world.GetRigidBodies(bodies, new Sphere(Vector3f.Zero, 1.25f));
        Assert.Contains(target.Body, bodies);
        Assert.Contains(overlap.Body, bodies);
        Assert.DoesNotContain(source.Body, bodies);
        Assert.DoesNotContain(distant.Body, bodies);

        world.GetRigidBodies(bodies, new BoundingBox(
            new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));
        Assert.Contains(target.Body, bodies);
        Assert.Contains(overlap.Body, bodies);
        Assert.DoesNotContain(distant.Body, bodies);

        world.GetRigidBodies(bodies, target.Body);
        Assert.Contains(overlap.Body, bodies);
        Assert.DoesNotContain(target.Body, bodies);
    }

    [Fact]
    public void Rigid_body_parity_state_force_reset_and_shape_batching_are_functional()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Gravity = Vector3f.Zero;

        var node = scene.CreateChild("Body");
        var shape = node.CreateComponent<CollisionShape>();
        shape.SetBox(Vector3f.One, new Vector3f(0.25f, 0.0f, 0.0f));
        var body = node.CreateComponent<RigidBody>();
        body.Mass = 1.0f;

        body.AnisotropicFriction = new Vector3f(1.0f, 0.5f, 0.25f);
        body.RollingFriction = 0.2f;
        body.ContactProcessingThreshold = 42.0f;
        Assert.Equal(new Vector3f(1.0f, 0.5f, 0.25f), body.AnisotropicFriction);
        Assert.Equal(0.2f, body.RollingFriction);
        Assert.Equal(42.0f, body.ContactProcessingThreshold);
        Assert.True(body.CenterOfMass.ApproximatelyEquivalent(new Vector3f(0.25f, 0.0f, 0.0f)));

        body.ApplyForce(new Vector3f(100.0f, 0.0f, 0.0f));
        body.ApplyTorque(new Vector3f(0.0f, 100.0f, 0.0f));
        body.ResetForces();
        world.Update(1.0f / 60.0f);
        Assert.True(body.LinearVelocity.ApproximatelyEquivalent(Vector3f.Zero));
        Assert.True(body.AngularVelocity.ApproximatelyEquivalent(Vector3f.Zero));

        var originalBodyId = body.JoltBodyId;
        body.DisableMassUpdate();
        shape.SetBox(new Vector3f(2.0f, 1.0f, 1.0f));
        Assert.Equal(originalBodyId, body.JoltBodyId);
        body.EnableMassUpdate();
        Assert.NotEqual(originalBodyId, body.JoltBodyId);
        body.PrepareToWorldOriginUpdate();

        world.MaxNetworkAngularVelocity = 40000.0f;
        world.InternalEdge = false;
        world.SplitImpulse = true;
        Assert.Equal(32767.0f, world.MaxNetworkAngularVelocity);
        Assert.False(world.InternalEdge);
        Assert.True(world.SplitImpulse);

        var overtime = -1.0f;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsPostUpdateEvent>(
            (ref PhysicsEvents.PhysicsPostUpdateEvent eventData) => overtime = eventData.Overtime);
        world.Update(0.001f);
        Assert.InRange(overtime, 0.0009f, 0.0011f);
    }

    [Fact]
    public void Rigid_body_compounds_all_enabled_collision_shapes()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var node = scene.CreateChild("Compound");
        var leftShape = node.CreateComponent<CollisionShape>();
        leftShape.SetBox(Vector3f.One, new Vector3f(-2.0f, 0.0f, 0.0f));
        var rightShape = node.CreateComponent<CollisionShape>();
        rightShape.SetBox(Vector3f.One, new Vector3f(2.0f, 0.0f, 0.0f));
        var body = node.CreateComponent<RigidBody>();
        world.UpdateCollisions();

        Assert.NotNull(body.JoltShape);
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(2.0f, 2.0f, 0.0f), Vector3f.Down), 4.0f, out var rightHit));
        Assert.Same(body, rightHit.Body);

        rightShape.Enabled = false;
        world.UpdateCollisions();
        Assert.False(world.TryRaycastSingle(
            new Ray(new Vector3f(2.0f, 2.0f, 0.0f), Vector3f.Down), 4.0f, out _));
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(-2.0f, 2.0f, 0.0f), Vector3f.Down), 4.0f, out var leftHit));
        Assert.Same(body, leftHit.Body);
    }

    private static (Node Node, CollisionShape Shape, RigidBody Body) CreateBox(
        Scene scene, string name, Vector3f position)
    {
        var node = scene.CreateChild(name);
        node.SetPosition(position);
        var shape = node.CreateComponent<CollisionShape>();
        shape.SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();
        return (node, shape, body);
    }
}
