namespace RedBolt.Tests;

using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class NavigationContractTests
{
    [Fact]
    public void Tile_offset_rejects_non_integral_and_non_aligned_rebases()
    {
        Assert.Null(NavigationUtils.CalculateTileOffset(new IntVector3(30, 1, 0), 10, 0.333f));
        Assert.Null(NavigationUtils.CalculateTileOffset(new IntVector3(31, 1, 0), 10, 1.0f));

        var result = NavigationUtils.CalculateTileOffset(new IntVector3(60, -4, -30), 10, 0.3f);
        Assert.True(result.HasValue);
        Assert.Equal(new IntVector2(20, -10), result.Value.TileOffset);
        Assert.Equal(-4, result.Value.OffsetY);
    }

    [Fact]
    public void Navigation_geometry_bounds_and_tile_count_cover_empty_and_overlap_cases()
    {
        Assert.Equal(0, NavigationUtils.CalculateMaxTiles(new BoundingBox(), 16, 1.0f));
        var geometry = new NavigationGeometryInfo
        {
            BoundingBox = new BoundingBox(new Vector3f(-1, 2, -3), new Vector3f(4, 8, 5))
        };

        var padded = NavigationUtils.CalculateBoundingBox([geometry], new Vector3f(1, 2, 3));
        Assert.Equal(new Vector3f(-2, 0, -6), padded.Min);
        Assert.Equal(new Vector3f(5, 10, 8), padded.Max);

        var tile = NavigationUtils.CalculateTileBoundingBox(
            [geometry], new BoundingBox(new Vector3f(-2, -100, -2), new Vector3f(5, 100, 6)));
        Assert.Equal(2, tile.Min.Y);
        Assert.Equal(8, tile.Max.Y);

        var emptyTile = NavigationUtils.CalculateTileBoundingBox(
            [], new BoundingBox(new Vector3f(-2, -100, -2), new Vector3f(5, 100, 6)));
        Assert.Equal(0, emptyTile.Min.Y);
        Assert.Equal(0, emptyTile.Max.Y);
    }

    [Fact]
    public void Area_deduction_preserves_explicit_values_and_classifies_flat_triangles()
    {
        float[] vertices = [0, 0, 0, 0, 0, 1, 1, 0, 0];
        int[] triangles = [0, 1, 2];
        byte[] areas = [NavigationDefs.DeduceAreaId, 7];

        NavigationUtils.DeduceAreaIds(45.0f, vertices, triangles, 1, areas);

        Assert.Equal(63, areas[0]);
        Assert.Equal(7, areas[1]);
    }

    [Fact]
    public void Build_data_and_detour_allocations_release_owned_buffers()
    {
        var allocation = new DetourAllocation([1, 2, 3]);
        Assert.True(allocation.IsValid);
        Assert.Equal(3, allocation.ToByteSpan().Length);
        allocation.Release();
        Assert.False(allocation.IsValid);
        Assert.Equal(0, allocation.DataSize);

        using var simple = new SimpleNavBuildData();
        simple.Vertices.Add(Vector3f.Zero);
        simple.Indices.Add(0);
        Assert.False(simple.IsEmpty);
        simple.Dispose();
        Assert.False(simple.IsEmpty);

        using var dynamic = new DynamicNavBuildData();
        dynamic.TileData.Add([1, 2]);
        dynamic.Dispose();
        Assert.Empty(dynamic.TileData);
    }

    [Fact]
    public void Navigation_components_apply_defaults_and_endpoint_resolution()
    {
        using var scene = new Scene();
        var start = scene.CreateChild("Start");
        var end = scene.CreateChild("End");
        var area = start.CreateComponent<NavArea>();
        var navigable = start.CreateComponent<Navigable>();
        var obstacle = start.CreateComponent<Obstacle>();
        var connection = start.CreateComponent<OffMeshConnection>();

        Assert.Equal(0, area.AreaID);
        Assert.Equal(new Vector3f(-10, -10, -10), area.BoundingBox.Min);
        Assert.Equal(new Vector3f(10, 10, 10), area.BoundingBox.Max);
        Assert.True(navigable.Recursive);
        Assert.True(navigable.Walkable);
        Assert.Equal(NavigationDefs.DeduceAreaId, navigable.AreaId);
        navigable.Walkable = false;
        Assert.Equal(0, navigable.EffectiveAreaId);
        Assert.Equal(5.0f, obstacle.Radius);
        Assert.Equal(5.0f, obstacle.Height);

        connection.EndPoint = end;
        connection.Radius = 2.0f;
        connection.Bidirectional = false;
        Assert.Same(end, connection.EndPoint);
        Assert.Equal(2.0f, connection.Radius);
        Assert.False(connection.Bidirectional);
    }

    [Fact]
    public void Navigation_queries_are_deterministic_before_mesh_allocation()
    {
        using var scene = new Scene();
        var navMesh = scene.CreateComponent<NavigationMesh>();
        var start = new Vector3f(1, 2, 3);
        var end = new Vector3f(4, 5, 6);

        Assert.False(navMesh.BuildTiles(new IntVector2(-1, -1), new IntVector2(1, 1)));
        Assert.Equal(start, navMesh.FindNearestPoint(start));
        Assert.Equal(end, navMesh.MoveAlongSurface(start, end, maxVisited: -7));

        var path = new List<Vector3f> { start };
        navMesh.FindPath(path, start, end);
        Assert.Empty(path);

        Assert.Equal(Vector3f.Zero, navMesh.GetRandomPoint());
        Assert.Equal(start, navMesh.GetRandomPointInCircle(start, 0));
        Assert.Equal(10.0f, navMesh.GetDistanceToWall(start, 10.0f));
        Assert.Equal(end, navMesh.Raycast(start, end));

        Assert.True(navMesh.Allocate());
        Assert.True(navMesh.IsInitialized);
        Assert.Empty(navMesh.GetAllTileIndices());
        Assert.False(navMesh.HasTile(new IntVector2(0, 0)));
        navMesh.RemoveAllTiles();
        navMesh.Clear();
        Assert.False(navMesh.IsInitialized);
    }

    [Fact]
    public void Crowd_queries_without_a_mesh_return_safe_sentinels_and_ignore_invalid_filters()
    {
        using var scene = new Scene();
        var manager = scene.CreateComponent<CrowdManager>();
        var point = new Vector3f(1, 2, 3);
        var end = new Vector3f(4, 5, 6);

        Assert.Empty(manager.GetAgents());
        Assert.Equal(point, manager.FindNearestPoint(point, int.MaxValue));
        Assert.Equal(end, manager.MoveAlongSurface(point, end, int.MaxValue, maxVisited: -1));

        var path = new List<Vector3f> { point };
        manager.FindPath(path, point, end, int.MaxValue);
        Assert.Equal([point], path);

        Assert.Equal(Vector3f.Zero, manager.GetRandomPoint(int.MaxValue));
        Assert.Equal(point, manager.GetRandomPointInCircle(point, 2, int.MaxValue));
        Assert.Equal(2.0f, manager.GetDistanceToWall(point, 2, int.MaxValue));
        Assert.Equal(end, manager.Raycast(point, end, int.MaxValue));
        Assert.Equal(ushort.MaxValue, manager.GetIncludeFlags(uint.MaxValue));
        Assert.Equal(ushort.MinValue, manager.GetExcludeFlags(uint.MaxValue));
        Assert.Equal(1.0f, manager.GetAreaCost(uint.MaxValue, uint.MaxValue));

        manager.SetCrowdTarget(end);
        manager.SetCrowdVelocity(Vector3f.One);
        manager.ResetCrowdTarget();
        Assert.Equal(0u, manager.GetNumAreas(uint.MaxValue));
        Assert.Equal(0u, manager.NumQueryFilterTypes);
    }
}
