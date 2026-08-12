namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CollisionShapeTerrainTests
{
    public CollisionShapeTerrainTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Terrain_shape_raycast_tracks_heightmap_recreation()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        using var heightMap = CreateFlatHeightMap(9, 10);
        var terrainNode = scene.CreateChild("Terrain");
        var terrain = terrainNode.CreateComponent<Terrain>();
        terrain.SetPatchSize(4);
        terrain.SetSpacing(new Vector3f(1.0f, 0.1f, 1.0f));
        Assert.True(terrain.SetHeightMap(heightMap));

        var shape = terrainNode.CreateComponent<CollisionShape>();
        shape.SetTerrain();
        var body = terrainNode.CreateComponent<RigidBody>();

        Assert.Equal(ShapeType.Terrain, shape.ShapeType);
        Assert.True(shape.IsSupported);
        Assert.NotNull(shape.JoltShape);
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 5.0f, 0.0f), Vector3f.Down), 10.0f, out var initialHit));
        Assert.Same(body, initialHit.Body);
        Assert.InRange(initialHit.Position.Y, 0.95f, 1.05f);

        heightMap.SetData(Enumerable.Repeat((byte)20, 9 * 9).ToArray());
        terrain.ApplyHeightMap();

        Assert.NotNull(shape.JoltShape);
        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 5.0f, 0.0f), Vector3f.Down), 10.0f, out var updatedHit));
        Assert.Same(body, updatedHit.Body);
        Assert.InRange(updatedHit.Position.Y, 1.95f, 2.05f);
    }

    private static Image CreateFlatHeightMap(int size, byte height)
    {
        var image = new Image();
        Assert.True(image.SetSize(size, size, 1));
        image.SetData(Enumerable.Repeat(height, size * size).ToArray());
        return image;
    }
}
