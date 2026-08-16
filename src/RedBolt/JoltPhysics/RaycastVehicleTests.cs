// Copyright (c) 2026 RedBolt and contributors.
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
    public void Vehicle_suspension_contacts_floor_and_engine_moves_chassis_forward()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);

        var (vehicle, body, wheels) = CreateVehicle(scene);
        var initialPosition = body.Position;
        vehicle.UpdateInput(0.0f, 1.0f, 0.0f);

        for (var i = 0; i < 240; ++i)
            world.Update(1.0f / 60.0f);

        Assert.Contains(wheels, wheel => wheel.InContact);
        Assert.Contains(wheels, wheel => wheel.ContactNormal.Y > 0.5f);
        Assert.True(body.Position.Z > initialPosition.Z + 0.5f);
        Assert.Contains(wheels, wheel => wheel.SideSlipSpeed >= 0.0f);
        Assert.All(wheels, wheel => Assert.False(wheel.ContactPosition.IsNaN));
    }

    [Fact]
    public void Vehicle_reverse_input_moves_chassis_backward()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);
        var (vehicle, body, _) = CreateVehicle(scene);

        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        var initialPosition = body.Position;
        vehicle.UpdateInput(0.0f, -1.0f, 0.0f);
        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(body.Position.Z < initialPosition.Z - 0.5f);
    }

    [Fact]
    public void Vehicle_right_input_steers_right()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);
        var (vehicle, body, _) = CreateVehicle(scene);

        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        vehicle.UpdateInput(1.0f, 1.0f, 0.0f);
        for (var i = 0; i < 90; ++i)
            world.Update(1.0f / 60.0f);

        var bodyForward = body.Rotation * Vector3f.Forward;
        Assert.True(bodyForward.X > 0.1f);
    }

    [Fact]
    public void Vehicle_keeps_moving_under_throttle_while_steering()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);
        var (vehicle, body, wheels) = CreateVehicle(scene);
        body.LinearDamping = 0.2f;
        body.AngularDamping = 0.5f;
        var connectionPoints = new[]
        {
            new Vector3f(-0.75f, -0.4f, 1.5f),
            new Vector3f(0.75f, -0.4f, 1.5f),
            new Vector3f(-0.75f, -0.4f, -1.5f),
            new Vector3f(0.75f, -0.4f, -1.5f)
        };
        for (var i = 0; i < wheels.Length; ++i)
        {
            var wheel = wheels[i];
            wheel.ConnectionPoint = connectionPoints[i];
            wheel.SuspensionRestLength = 0.6f;
            wheel.MaxSuspensionTravel = 0.5f;
            wheel.SuspensionStiffness = 14.0f;
            wheel.DampingCompression = 4.0f;
            wheel.DampingRelaxation = 2.0f;
            wheel.FrictionSlip = 1000.0f;
            wheel.RollInfluence = 0.12f;
        }
        wheels[0].SteeringFactor = 1.0f;
        wheels[1].SteeringFactor = 1.0f;

        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        vehicle.UpdateInput(1.0f, 1.0f, 0.0f);
        for (var i = 0; i < 1800; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(body.LinearVelocity.Length > 2.0f,
            $"Vehicle slowed to {body.LinearVelocity.Length:F3} m/s under combined throttle and steering.");
    }

    [Fact]
    public void Vehicle_brake_slows_chassis()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);
        var (vehicle, body, _) = CreateVehicle(scene);

        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        vehicle.UpdateInput(0.0f, 1.0f, 0.0f);
        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);
        var speedBeforeBraking = body.LinearVelocity.Length;

        vehicle.UpdateInput(0.0f, 0.0f, 1.0f);
        for (var i = 0; i < 120; ++i)
            world.Update(1.0f / 60.0f);

        Assert.True(speedBeforeBraking > 1.0f);
        Assert.True(body.LinearVelocity.Length < speedBeforeBraking * 0.25f);
    }

    [Fact]
    public void Vehicle_suspension_settles_without_persistent_bounce()
    {
        using var scene = new Scene();
        var world = CreateWorldAndFloor(scene);
        var (_, body, wheels) = CreateVehicle(scene, 2.5f);

        var minimumHeight = float.MaxValue;
        var maximumHeight = float.MinValue;
        for (var i = 0; i < 600; ++i)
        {
            world.Update(1.0f / 60.0f);
            if (i < 480)
                continue;
            minimumHeight = MathF.Min(minimumHeight, body.Position.Y);
            maximumHeight = MathF.Max(maximumHeight, body.Position.Y);
        }

        Assert.All(wheels, wheel => Assert.True(wheel.InContact));
        Assert.True(maximumHeight - minimumHeight < 0.03f);
        Assert.True(MathF.Abs(body.LinearVelocity.Y) < 0.05f);
    }

    [Fact]
    public void Airborne_wheel_visuals_stay_locked_to_interpolated_chassis()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = true;
        var (vehicle, _, wheels) = CreateVehicle(scene, 25.0f);

        for (var step = 0; step < 120; ++step)
        {
            world.Update(1.0f / 60.0f);
            vehicle.PostUpdate(1.0f / 60.0f);

            foreach (var wheel in wheels)
            {
                var expectedPosition = wheel.ConnectionPoint
                    + wheel.Direction * wheel.SuspensionRestLength
                    + wheel.Offset;
                Assert.True((wheel.Node.Position - expectedPosition).Length < 0.0001f);
            }
        }
    }

    private static PhysicsWorld CreateWorldAndFloor(Scene scene)
    {
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floorNode = scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(100.0f, 1.0f, 100.0f));
        floorNode.CreateComponent<RigidBody>();
        return world;
    }

    private static (RaycastVehicle Vehicle, RigidBody Body, RaycastVehicleWheel[] Wheels) CreateVehicle(
        Scene scene, float height = 1.2f)
    {
        var vehicleNode = scene.CreateChild("Vehicle");
        vehicleNode.SetPosition(new Vector3f(0.0f, height, 0.0f));
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
        wheel.Axle = new Vector3f(-1.0f, 0.0f, 0.0f);
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
