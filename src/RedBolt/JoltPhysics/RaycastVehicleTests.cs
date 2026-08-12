namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RaycastVehicleTests
{
    public RaycastVehicleTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Vehicle_exposes_reflected_surface_and_tracks_wheel_lifecycle()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var (vehicle, body, wheels) = CreateVehicle(scene);

        Assert.Equal(4, vehicle.NumWheels);
        Assert.Equal(Enumerable.Range(0, 4), wheels.Select(wheel => wheel.WheelIndex));
        Assert.Equal(RaycastVehicle.RIGHT_UP_FORWARD, vehicle.CoordinateSystem);
        Assert.Equal(RaycastVehicle.DefaultEngineForce, vehicle.EngineForce);
        Assert.Equal(RaycastVehicle.DefaultBrakingForce, vehicle.BrakingForce);

        var vehicleAttributes = Context.Instance.GetReflection(ObjectReflection<RaycastVehicle>.TypeId)!.Attributes!;
        Assert.Equal(
            [
                "Engine Force", "Braking Force", "Maximum side slip threshold",
                "RPM for wheel motors in air (0=calculate)", "Coordinate system"
            ],
            vehicleAttributes.Select(attribute => attribute.Name));

        var wheelAttributes = Context.Instance.GetReflection(ObjectReflection<RaycastVehicleWheel>.TypeId)!.Attributes!;
        Assert.Equal(
            [
                "Connection Point", "Offset", "Rotation", "Set Connection", "Direction", "Axle", "Roll Influence", "Radius",
                "Steering Factor", "Engine Factor", "Brake Factor", "Suspension Rest Length",
                "Max Suspension Travel", "Suspension Stiffness", "Damping Compression", "Damping Relaxation",
                "Friction Slip", "Max Suspension Force", "Sliding Factor", "Skid Info", "Steering", "Brake",
                "Engine Force", "Side Slip Speed", "Is In Contact", "Contact Position", "Contact Normal"
            ],
            wheelAttributes.Select(attribute => attribute.Name));

        body.Mass = 801.0f;
        Assert.NotNull(body.JoltBodyId);
        Assert.Equal(4, vehicle.NumWheels);

        wheels[1].Remove();
        Assert.Equal(-1, wheels[1].WheelIndex);
        Assert.Equal(3, vehicle.NumWheels);
        Assert.Equal(0, wheels[0].WheelIndex);
        Assert.Equal(1, wheels[2].WheelIndex);
        Assert.Equal(2, wheels[3].WheelIndex);
    }

    [Fact]
    public void Vehicle_suspension_contacts_floor_and_engine_moves_chassis()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floorNode = scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(100.0f, 1.0f, 100.0f));
        floorNode.CreateComponent<RigidBody>();

        var (vehicle, body, wheels) = CreateVehicle(scene);
        var initialPosition = body.Position;
        vehicle.UpdateInput(0.0f, 1.0f, 0.0f);

        for (var i = 0; i < 240; ++i)
            world.Update(1.0f / 60.0f);

        Assert.Contains(wheels, wheel => wheel.InContact);
        Assert.Contains(wheels, wheel => wheel.ContactNormal.Y > 0.5f);
        Assert.True(MathF.Abs(body.Position.Z - initialPosition.Z) > 0.5f);
        Assert.Contains(wheels, wheel => wheel.SideSlipSpeed >= 0.0f);
        Assert.All(wheels, wheel => Assert.False(wheel.ContactPosition.IsNaN));
    }

    private static (RaycastVehicle Vehicle, RigidBody Body, RaycastVehicleWheel[] Wheels) CreateVehicle(Scene scene)
    {
        var vehicleNode = scene.CreateChild("Vehicle");
        vehicleNode.SetPosition(new Vector3f(0.0f, 1.2f, 0.0f));
        vehicleNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(2.0f, 1.0f, 4.0f));
        var body = vehicleNode.CreateComponent<RigidBody>();
        body.Mass = 800.0f;
        body.Friction = 0.5f;

        var vehicle = vehicleNode.CreateComponent<RaycastVehicle>();
        vehicle.Init();

        var wheels = new[]
        {
            CreateWheel(vehicleNode, "Front Left", new Vector3f(-0.9f, 0.0f, 1.4f), steeringFactor: 0.5f),
            CreateWheel(vehicleNode, "Front Right", new Vector3f(0.9f, 0.0f, 1.4f), steeringFactor: 0.5f),
            CreateWheel(vehicleNode, "Rear Left", new Vector3f(-0.9f, 0.0f, -1.4f), engineFactor: 1.0f),
            CreateWheel(vehicleNode, "Rear Right", new Vector3f(0.9f, 0.0f, -1.4f), engineFactor: 1.0f)
        };
        vehicle.ResetWheels();
        return (vehicle, body, wheels);
    }

    private static RaycastVehicleWheel CreateWheel(
        Node vehicleNode, string name, Vector3f connectionPoint, float steeringFactor = 0.0f,
        float engineFactor = 0.0f)
    {
        var wheelNode = vehicleNode.CreateChild(name);
        var wheel = wheelNode.CreateComponent<RaycastVehicleWheel>();
        wheel.ConnectionPoint = connectionPoint;
        wheel.Radius = 0.5f;
        wheel.SuspensionRestLength = 0.7f;
        wheel.MaxSuspensionTravel = 0.3f;
        wheel.SuspensionStiffness = 6.0f;
        wheel.DampingCompression = 0.9f;
        wheel.DampingRelaxation = 0.9f;
        wheel.SteeringFactor = steeringFactor;
        wheel.EngineFactor = engineFactor;
        return wheel;
    }
}
