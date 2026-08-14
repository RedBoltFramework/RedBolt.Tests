namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PhysicsBox2DTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PhysicsBox2DTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(fixture.Context);
    }

    [Fact]
    public void Module_registration_exposes_rbfx_components_without_interface_facade()
    {
        Assert.NotNull(_fixture.Context.GetReflection<PhysicsWorld2D>());
        Assert.NotNull(_fixture.Context.GetReflection<RigidBody2D>());
        Assert.NotNull(_fixture.Context.GetReflection<CollisionPolygon2D>());
        Assert.NotNull(_fixture.Context.GetReflection<ConstraintWheel2D>());
        Assert.Null(typeof(PhysicsWorld2D).Assembly.GetType("RedBolt.PhysicsBox2D.IPhysicsWorld2D"));
    }

    [Fact]
    public void Dynamic_body_collides_and_queries_return_native_shapes()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld2D>();
        var beginContacts = 0;
        IReadOnlyList<PhysicsContact2D>? contacts = null;
        scene.SubscribeToEvent<PhysicsEvents2D.PhysicsBeginContact2DEvent>(world,
            (ref PhysicsEvents2D.PhysicsBeginContact2DEvent eventData) =>
            {
                ++beginContacts;
                contacts = eventData.Contacts;
            });

        var groundNode = scene.CreateChild("Ground");
        groundNode.CreateComponent<RigidBody2D>();
        groundNode.CreateComponent<CollisionEdge2D>().SetVertices(new Vector2f(-10.0f, 0.0f), new Vector2f(10.0f, 0.0f));

        var ballNode = scene.CreateChild("Ball");
        ballNode.SetPosition2D(0.0f, 4.0f);
        var ballBody = ballNode.CreateComponent<RigidBody2D>();
        ballBody.BodyType = BodyType2D.Dynamic;
        var ballShape = ballNode.CreateComponent<CollisionCircle2D>();
        ballShape.Radius = 0.5f;
        ballShape.Density = 1.0f;

        for (var i = 0; i < 240; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(beginContacts > 0);
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<PhysicsContact2D>>(contacts));
        Assert.InRange(contacts![0].Normal.Length, 0.9f, 1.1f);
        Assert.InRange(ballNode.GetPosition2D().Y, 0.35f, 0.75f);
        Assert.Same(ballBody, world.GetRigidBody(ballNode.GetPosition2D()));

        var hits = new List<PhysicsRaycastResult2D>();
        world.Raycast(hits, new Vector2f(0.0f, 5.0f), new Vector2f(0.0f, -1.0f));
        Assert.Contains(hits, hit => ReferenceEquals(hit.Body, ballBody));
        var closestHit = new PhysicsRaycastResult2D();
        world.RaycastSingle(ref closestHit, new Vector2f(0.0f, 5.0f), new Vector2f(0.0f, -1.0f));
        Assert.Same(ballBody, closestHit.Body);

        var bodies = new List<RigidBody2D>();
        world.GetRigidBodies(bodies, new Rect(new Vector2f(-1.0f, 0.0f), new Vector2f(1.0f, 1.0f)));
        Assert.Contains(ballBody, bodies);
    }

    [Fact]
    public void Zero_density_dynamic_body_keeps_fallback_mass_and_collision_response()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld2D>();

        var groundNode = scene.CreateChild("Ground");
        groundNode.CreateComponent<RigidBody2D>();
        groundNode.CreateComponent<CollisionEdge2D>().SetVertices(new Vector2f(-10.0f, 0.0f), new Vector2f(10.0f, 0.0f));

        var ballNode = scene.CreateChild("Ball");
        ballNode.SetPosition2D(0.0f, 4.0f);
        var ballBody = ballNode.CreateComponent<RigidBody2D>();
        ballBody.BodyType = BodyType2D.Dynamic;
        ballNode.CreateComponent<CollisionCircle2D>().Radius = 0.5f;
        ballBody.FixedRotation = true;

        Assert.Equal(1.0f, ballBody.Mass);
        for (var i = 0; i < 240; ++i)
            world.Update(1.0f / 60.0f);

        Assert.InRange(ballNode.GetPosition2D().Y, 0.35f, 0.75f);
    }

    [Fact]
    public void Box2d3_constraint_set_can_be_created_and_released()
    {
        using var scene = new Scene();
        scene.CreateComponent<PhysicsWorld2D>();
        var a = scene.CreateChild("A");
        var bodyA = a.CreateComponent<RigidBody2D>();
        a.CreateComponent<CollisionBox2D>().Size = Vector2f.One;
        var b = scene.CreateChild("B");
        b.SetPosition2D(1.0f, 0.0f);
        var bodyB = b.CreateComponent<RigidBody2D>();
        bodyB.BodyType = BodyType2D.Dynamic;
        b.CreateComponent<CollisionCircle2D>().Radius = 0.5f;

        var constraints = new Constraint2D[]
        {
            a.CreateComponent<ConstraintDistance2D>(), a.CreateComponent<ConstraintFriction2D>(),
            a.CreateComponent<ConstraintMotor2D>(), a.CreateComponent<ConstraintMouse2D>(),
            a.CreateComponent<ConstraintPrismatic2D>(), a.CreateComponent<ConstraintRevolute2D>(),
            a.CreateComponent<ConstraintRope2D>(), a.CreateComponent<ConstraintWeld2D>(),
            a.CreateComponent<ConstraintWheel2D>(),
        };
        foreach (var constraint in constraints)
        {
            constraint.OtherBody = bodyB;
            constraint.CreateJoint();
            constraint.ReleaseJoint();
        }
        Assert.Same(bodyA, constraints[0].OwnerBody);
    }

    [Fact]
    public void Update_contact_can_disable_solver_response()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld2D>();
        var updateContacts = 0;
        scene.SubscribeToEvent<PhysicsEvents2D.PhysicsUpdateContact2DEvent>(world,
            (ref PhysicsEvents2D.PhysicsUpdateContact2DEvent eventData) =>
            {
                ++updateContacts;
                eventData.Enabled = false;
            });

        var ground = scene.CreateChild("Ground");
        ground.CreateComponent<RigidBody2D>();
        ground.CreateComponent<CollisionEdge2D>().SetVertices(new Vector2f(-10.0f, 0.0f), new Vector2f(10.0f, 0.0f));

        var ball = scene.CreateChild("Ball");
        ball.SetPosition2D(0.0f, 2.0f);
        var body = ball.CreateComponent<RigidBody2D>();
        body.BodyType = BodyType2D.Dynamic;
        var shape = ball.CreateComponent<CollisionCircle2D>();
        shape.Radius = 0.5f;
        shape.Density = 1.0f;

        Assert.NotNull(world.World);
        Assert.NotNull(body.Body);
        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(updateContacts > 0,
            $"Expected pre-solve update contacts, but received {updateContacts}; ball Y is {ball.GetPosition2D().Y}.");
        Assert.True(ball.GetPosition2D().Y < -1.0f,
            $"Disabled contact still stopped the ball at Y={ball.GetPosition2D().Y} after {updateContacts} updates.");
    }
}
