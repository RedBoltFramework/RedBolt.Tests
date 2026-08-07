namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.JoltPhysics;
using RedBolt.Math;
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
}
