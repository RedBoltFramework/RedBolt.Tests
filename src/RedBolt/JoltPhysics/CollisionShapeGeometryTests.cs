namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CollisionShapeGeometryTests
{
    private readonly HeadlessEngineFixture _fixture;

    public CollisionShapeGeometryTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Model_geometry_is_cached_released_and_invalidated_on_reload()
    {
        var model = _fixture.Resources.Get<Model>("Models/Box.mdl");
        using var cache = new CollisionGeometryDataCache(ShapeType.TriangleMesh);

        var first = cache.GetOrCreateGeometry(model, 0);
        var second = cache.GetOrCreateGeometry(model, 0);
        Assert.Same(first, second);

        cache.ReleaseCachedGeometry(model);
        var afterRelease = cache.GetOrCreateGeometry(model, 0);
        Assert.NotSame(first, afterRelease);

        _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
        model.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Null(cache.GetCachedGeometry(model, 0));
    }

    [Fact]
    public async Task Model_reload_subscription_created_on_worker_is_deferred_to_main_thread()
    {
        var model = _fixture.Resources.Get<Model>("Models/Box.mdl");
        using var cache = new CollisionGeometryDataCache(ShapeType.TriangleMesh);

        var geometry = await Task.Run(() => cache.GetOrCreateGeometry(model, 0));

        model.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Same(geometry, cache.GetCachedGeometry(model, 0));

        _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
        model.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Null(cache.GetCachedGeometry(model, 0));
    }

    [Fact]
    public void Model_triangle_mesh_and_custom_triangle_mesh_are_raycastable()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;

        var modelNode = scene.CreateChild("ModelMesh");
        var modelShape = modelNode.CreateComponent<CollisionShape>();
        modelShape.SetTriangleMesh(_fixture.Resources.Get<Model>("Models/Box.mdl"));
        var modelBody = modelNode.CreateComponent<RigidBody>();

        Assert.Equal(ShapeType.TriangleMesh, modelShape.ShapeType);
        Assert.NotNull(modelShape.GeometryData);
        Assert.NotNull(modelShape.JoltShape);
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down), 4.0f, out var modelHit));
        Assert.Same(modelBody, modelHit.Body);
        Assert.InRange(modelHit.Position.Y, 0.45f, 0.55f);

        modelShape.Enabled = false;
        var customNode = scene.CreateChild("CustomMesh");
        customNode.SetPosition(new Vector3f(0.0f, -1.0f, 0.0f));
        var custom = customNode.CreateComponent<CustomGeometry>();
        custom.BeginGeometry(0, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(-2.0f, 0.0f, -2.0f));
        custom.DefineVertex(new Vector3f(-2.0f, 0.0f, 2.0f));
        custom.DefineVertex(new Vector3f(2.0f, 0.0f, 2.0f));
        custom.DefineVertex(new Vector3f(-2.0f, 0.0f, -2.0f));
        custom.DefineVertex(new Vector3f(2.0f, 0.0f, 2.0f));
        custom.DefineVertex(new Vector3f(2.0f, 0.0f, -2.0f));
        custom.Commit();
        var customShape = customNode.CreateComponent<CollisionShape>();
        customShape.SetCustomTriangleMesh(custom);
        var customBody = customNode.CreateComponent<RigidBody>();

        Assert.Equal(custom.Id, customShape.CustomGeometryId);
        Assert.NotNull(customShape.GeometryData);
        Assert.NotNull(customShape.JoltShape);
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Down), 4.0f, out var customHit));
        Assert.Same(customBody, customHit.Body);
        Assert.InRange(customHit.Position.Y, -1.05f, -0.95f);
    }

    [Fact]
    public void Convex_hull_cone_and_static_plane_participate_in_simulation()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var planeNode = scene.CreateChild("Plane");
        var planeShape = planeNode.CreateComponent<CollisionShape>();
        planeShape.SetStaticPlane();
        planeNode.CreateComponent<RigidBody>();

        var convexNode = scene.CreateChild("Convex");
        convexNode.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));
        var convexShape = convexNode.CreateComponent<CollisionShape>();
        convexShape.SetConvexHull(_fixture.Resources.Get<Model>("Models/Box.mdl"));
        var convexBody = convexNode.CreateComponent<RigidBody>();
        convexBody.Mass = 1.0f;

        var coneNode = scene.CreateChild("Cone");
        coneNode.SetPosition(new Vector3f(3.0f, 1.0f, 0.0f));
        var coneShape = coneNode.CreateComponent<CollisionShape>();
        coneShape.SetCone(1.0f, 2.0f);
        coneNode.CreateComponent<RigidBody>();

        for (var i = 0; i < 180; ++i)
            world.Update(1.0f / 60.0f);

        Assert.Equal(ShapeType.StaticPlane, planeShape.ShapeType);
        Assert.NotNull(planeShape.JoltShape);
        Assert.Equal(ShapeType.ConvexHull, convexShape.ShapeType);
        Assert.NotNull(convexShape.GeometryData);
        Assert.NotNull(convexShape.JoltShape);
        Assert.InRange(convexNode.WorldPosition.Y, 0.45f, 0.65f);
        Assert.Equal(ShapeType.Cone, coneShape.ShapeType);
        Assert.NotNull(coneShape.JoltShape);
        Assert.True(coneShape.GetWorldBoundingBox().Size.Y > 1.9f);
    }

    [Fact]
    public void Gimpact_mesh_creates_dynamic_concave_shape()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floor = scene.CreateChild("Floor");
        floor.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floor.CreateComponent<CollisionShape>().SetBox(new Vector3f(20.0f, 1.0f, 20.0f));
        floor.CreateComponent<RigidBody>();

        var meshNode = scene.CreateChild("GImpact");
        meshNode.SetPosition(new Vector3f(0.0f, 3.0f, 0.0f));
        var meshShape = meshNode.CreateComponent<CollisionShape>();
        meshShape.SetGImpactMesh(_fixture.Resources.Get<Model>("Models/Box.mdl"));
        var meshBody = meshNode.CreateComponent<RigidBody>();
        meshBody.Mass = 1.0f;

        Assert.Equal(ShapeType.GImpactMesh, meshShape.ShapeType);
        Assert.NotNull(meshShape.GeometryData);
        Assert.NotNull(meshShape.JoltShape);
        Assert.NotNull(meshBody.JoltBodyId);

        for (var i = 0; i < 180; ++i)
            world.Update(1.0f / 60.0f);
        Assert.InRange(meshNode.WorldPosition.Y, 0.35f, 0.75f);
    }
}
