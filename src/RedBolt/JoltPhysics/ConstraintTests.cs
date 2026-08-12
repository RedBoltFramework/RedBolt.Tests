namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ConstraintTests
{
    public ConstraintTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Constraint_exposes_reflected_surface_and_recreates_native_types()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var otherNode = scene.CreateChild("Other");
        otherNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var otherBody = otherNode.CreateComponent<RigidBody>();

        var ownNode = scene.CreateChild("Own");
        ownNode.SetPosition(new Vector3f(0.0f, 2.0f, 0.0f));
        ownNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var ownBody = ownNode.CreateComponent<RigidBody>();
        ownBody.Mass = 1.0f;
        var constraint = ownNode.CreateComponent<Constraint>();
        constraint.OtherBody = otherBody;
        constraint.WorldPosition = ownNode.WorldPosition;
        constraint.SetAxis(Vector3f.Up);
        constraint.SetOtherAxis(Vector3f.Up);
        constraint.LowLimit = new Vector2f(-1.0f, -10.0f);
        constraint.HighLimit = new Vector2f(1.0f, 10.0f);
        constraint.ERP = -1.0f;
        constraint.CFM = -1.0f;

        Assert.Equal(0.0f, constraint.ERP);
        Assert.Equal(0.0f, constraint.CFM);
        Assert.Same(world, constraint.PhysicsWorld);
        Assert.Same(ownBody, constraint.OwnBody);
        Assert.Same(otherBody, constraint.OtherBody);

        foreach (var constraintType in Enum.GetValues<ConstraintType>())
        {
            constraint.ConstraintType = constraintType;
            Assert.Equal(constraintType, constraint.ConstraintType);
            Assert.NotNull(constraint.JoltConstraint);
        }

        var dependencies = new List<Node>();
        constraint.GetDependencyNodes(dependencies);
        Assert.Contains(otherNode, dependencies);

        ownBody.Mass = 2.0f;
        Assert.NotNull(ownBody.JoltBodyId);
        Assert.NotNull(constraint.JoltConstraint);

        constraint.Enabled = false;
        Assert.NotNull(constraint.JoltConstraint);
        constraint.Enabled = true;

        var attributes = Context.Instance.GetReflection(ObjectReflection<Constraint>.TypeId)!.Attributes!;
        Assert.Contains(attributes, attribute => attribute.Name == "Constraint Type");
        Assert.Contains(attributes, attribute => attribute.Name == "Other Body NodeID");
        Assert.Contains(attributes, attribute => attribute.Name == "High Limit");
        Assert.Contains(attributes, attribute => attribute.Name == "Low Limit");
        Assert.Contains(attributes, attribute => attribute.Name == "ERP Parameter");
        Assert.Contains(attributes, attribute => attribute.Name == "CFM Parameter");
        Assert.Contains(attributes, attribute => attribute.Name == "Disable Collision");

        constraint.ReleaseConstraint();
        Assert.Null(constraint.JoltConstraint);
        constraint.ConstraintType = constraint.ConstraintType;
        Assert.NotNull(constraint.JoltConstraint);
    }

    [Fact]
    public void Point_constraint_can_pin_a_dynamic_body_to_the_static_world()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var node = scene.CreateChild("Pinned");
        node.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));
        node.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        var constraint = node.CreateComponent<Constraint>();
        constraint.WorldPosition = node.WorldPosition;

        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        Assert.NotNull(constraint.JoltConstraint);
        Assert.Null(constraint.OtherBody);
        Assert.InRange(node.WorldPosition.Y, 2.95f, 3.05f);
    }

    [Fact]
    public void Disable_collision_rejects_contacts_between_connected_bodies()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;
        world.Gravity = Vector3f.Zero;

        var firstNode = scene.CreateChild("First");
        firstNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var firstBody = firstNode.CreateComponent<RigidBody>();
        firstBody.Mass = 1.0f;
        firstBody.UseGravity = false;
        firstBody.CollisionEventMode = CollisionEventMode.Always;

        var secondNode = scene.CreateChild("Second");
        secondNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var secondBody = secondNode.CreateComponent<RigidBody>();
        secondBody.Mass = 1.0f;
        secondBody.UseGravity = false;
        secondBody.CollisionEventMode = CollisionEventMode.Always;

        var constraint = secondNode.CreateComponent<Constraint>();
        constraint.OtherBody = firstBody;
        constraint.WorldPosition = Vector3f.Zero;
        constraint.DisableCollision = true;

        var collisionStarts = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent _) => ++collisionStarts);

        for (var i = 0; i < 3; ++i)
            world.Update(1.0f / 60.0f);

        Assert.Equal(0, collisionStarts);

        constraint.DisableCollision = false;
        constraint.Enabled = false;
        secondBody.Position = new Vector3f(2.0f, 0.0f, 0.0f);
        world.Update(1.0f / 60.0f);
        secondBody.Position = new Vector3f(0.25f, 0.0f, 0.0f);
        for (var i = 0; i < 3; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(collisionStarts > 0);
    }
}
