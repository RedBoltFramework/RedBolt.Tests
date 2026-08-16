// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class NavigationCoverageGapTests
{
    [Fact]
    public void Navigation_mesh_properties_and_empty_queries_cover_boundaries()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Navigation");
        var mesh = node.CreateComponent<NavigationMesh>();

        mesh.MeshName = "BoundaryMesh";
        mesh.MaxTiles = 0;
        mesh.TileSize = 0;
        mesh.CellSize = 0.0f;
        mesh.CellHeight = -1.0f;
        mesh.AgentHeight = 0.0f;
        mesh.AgentRadius = -1.0f;
        mesh.AgentMaxClimb = 0.0f;
        mesh.AgentMaxSlope = -1.0f;
        mesh.RegionMinSize = 0.0f;
        mesh.RegionMergeSize = -1.0f;
        mesh.EdgeMaxLength = 0.0f;
        mesh.EdgeMaxError = -1.0f;
        mesh.MaxPortalLinks = 0;
        mesh.DetailSampleDistance = 0.0f;
        mesh.DetailSampleMaxError = -1.0f;
        mesh.Padding = new Vector3f(2.0f, 3.0f, 4.0f);
        mesh.HeightRange = new Vector2f(5.0f, 1.0f);
        mesh.PartitionType = NavmeshPartitionType.Monotone;
        mesh.DrawOffMeshConnections = true;
        mesh.DrawNavAreas = true;

        Assert.Equal("BoundaryMesh", mesh.MeshName);
        Assert.Equal(1, mesh.MaxTiles);
        Assert.Equal(16, mesh.TileSize);
        Assert.True(mesh.CellSize > 0.0f);
        Assert.True(mesh.CellHeight > 0.0f);
        Assert.True(mesh.AgentHeight > 0.0f);
        Assert.True(mesh.AgentRadius > 0.0f);
        Assert.True(mesh.AgentMaxClimb > 0.0f);
        Assert.Equal(0.0f, mesh.AgentMaxSlope);
        Assert.True(mesh.RegionMinSize > 0.0f);
        Assert.True(mesh.RegionMergeSize > 0.0f);
        Assert.True(mesh.EdgeMaxLength > 0.0f);
        Assert.True(mesh.EdgeMaxError > 0.0f);
        Assert.Equal(1, mesh.MaxPortalLinks);
        Assert.True(mesh.DetailSampleDistance > 0.0f);
        Assert.True(mesh.DetailSampleMaxError > 0.0f);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), mesh.Padding);
        Assert.False(mesh.IsHeightRangeValid);
        Assert.Equal(NavmeshPartitionType.Monotone, mesh.PartitionType);
        Assert.True(mesh.DrawOffMeshConnections);
        Assert.True(mesh.DrawNavAreas);
        Assert.False(mesh.IsInitialized);

        mesh.CellSize = 0.3f;
        mesh.HeightRange = new Vector2f(-2.0f, 7.0f);
        mesh.SetAreaCost(3, 2.5f);
        Assert.True(mesh.IsHeightRangeValid);
        Assert.Equal(2.5f, mesh.GetAreaCost(3));

        var tileIndex = mesh.GetTileIndex(new Vector3f(-0.01f, 2.0f, 4.81f));
        Assert.Equal(new IntVector2(-1, 1), tileIndex);

        var bounded = mesh.GetTileBoundingBoxColumn(new IntVector2(-2, 3));
        Assert.True(bounded.Min.ApproximatelyEquivalent(new Vector3f(-9.6f, -2.0f, 14.4f)));
        Assert.True(bounded.Max.ApproximatelyEquivalent(new Vector3f(-4.8f, 7.0f, 19.2f)));

        mesh.HeightRange = new Vector2f(9.0f, 9.0f);
        var unbounded = mesh.GetTileBoundingBoxColumn(new IntVector2(0, 0));
        Assert.Equal(-MathDefs.LargeValue, unbounded.Min.Y);
        Assert.Equal(MathDefs.LargeValue, unbounded.Max.Y);

        var point = new Vector3f(1.0f, 2.0f, 3.0f);
        var end = new Vector3f(4.0f, 5.0f, 6.0f);
        Assert.Equal(point, mesh.FindNearestPoint(point, new Vector3f(2.0f, 2.0f, 2.0f), null, out var nearestRef));
        Assert.Equal(0L, nearestRef);
        Assert.Equal(end, mesh.MoveAlongSurface(point, end, new Vector3f(2.0f, 2.0f, 2.0f), -4));

        var vectorPath = new List<Vector3f> { point };
        mesh.FindPath(vectorPath, point, end);
        Assert.Empty(vectorPath);

        var richPath = new List<NavigationPathPoint>
        {
            new() { Position = point, Flag = NavigationPathPointFlag.Start, AreaID = 3 }
        };
        mesh.FindPath(richPath, point, end);
        Assert.Empty(richPath);

        Assert.Equal(Vector3f.Zero, mesh.GetRandomPoint(null, out var randomRef));
        Assert.Equal(0L, randomRef);
        Assert.Equal(point, mesh.GetRandomPointInCircle(point, 3.0f, new Vector3f(2.0f, 2.0f, 2.0f), null,
            out var circleRef));
        Assert.Equal(0L, circleRef);
        Assert.Equal(4.0f, mesh.GetDistanceToWall(point, 4.0f, new Vector3f(2.0f, 2.0f, 2.0f), null,
            out var hitPosition, out var hitNormal));
        Assert.Equal(Vector3f.Zero, hitPosition);
        Assert.Equal(Vector3f.Down, hitNormal);
        Assert.Equal(end, mesh.Raycast(point, end, new Vector3f(2.0f, 2.0f, 2.0f), null, out hitNormal));
        Assert.Equal(Vector3f.Down, hitNormal);

        Assert.False(mesh.BuildTiles(new IntVector2(0, 0), new IntVector2(0, 0)));
        mesh.BuildTilesAsync(new IntVector2(0, 0), new IntVector2(0, 0));
        Assert.Empty(mesh.GetTileData(new IntVector2(0, 0)));
        Assert.False(mesh.AddTile([0, 0, 0, 0]));
        mesh.RemoveTile(new IntVector2(0, 0));
        var emptySerializedTile = new byte[] { 0, 0, 0, 0 };
        mesh.OffsetTileData(emptySerializedTile, new IntVector3(36, 4, 36));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, emptySerializedTile);
        mesh.Clear();
        Assert.Empty(mesh.NavigationData);
    }

    [Fact]
    public void Navigation_mesh_empty_allocation_roundtrips_and_rejects_invalid_data()
    {
        using var scene = new Scene();
        var mesh = scene.CreateComponent<NavigationMesh>();
        mesh.MaxTiles = 4;

        Assert.True(mesh.Allocate());
        Assert.True(mesh.IsInitialized);
        Assert.Empty(mesh.GetAllTileIndices());
        Assert.False(mesh.HasTile(new IntVector2(0, 0)));
        Assert.Empty(mesh.GetTileData(new IntVector2(0, 0)));

        var data = mesh.NavigationData;
        Assert.NotEmpty(data);

        mesh.Clear();
        Assert.False(mesh.IsInitialized);
        mesh.NavigationData = data;
        Assert.True(mesh.IsInitialized);
        Assert.Empty(mesh.GetAllTileIndices());

        Assert.False(mesh.AddTile([0, 0, 0, 0]));
        var untouched = new byte[] { 0, 0, 0, 0 };
        mesh.OffsetTileData(untouched, new IntVector3(36, 1, 36));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, untouched);
        mesh.OffsetTileData(untouched, new IntVector3(1, 1, 36));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, untouched);

        mesh.RemoveTile(new IntVector2(99, 99));
        Assert.Empty(mesh.GetAllTileIndices());
        mesh.RemoveAllTiles();
        Assert.Empty(mesh.GetAllTileIndices());

        using var invalidHeader = new VectorBuffer();
        invalidHeader.WriteBoundingBox(new BoundingBox());
        invalidHeader.WriteInt(1);
        invalidHeader.WriteInt(0);
        invalidHeader.WriteInt(NavigationMesh.NavigationDataVersion);
        mesh.NavigationData = invalidHeader.GetBuffer();
        Assert.False(mesh.IsInitialized);

        using var invalidParameters = new VectorBuffer();
        invalidParameters.WriteBoundingBox(new BoundingBox());
        invalidParameters.WriteInt(0);
        invalidParameters.WriteInt(0);
        invalidParameters.WriteInt(NavigationMesh.NavigationDataVersion);
        invalidParameters.WriteFloat(1.0f);
        invalidParameters.WriteFloat(1.0f);
        invalidParameters.WriteInt(4);
        invalidParameters.WriteInt(32);
        mesh.NavigationData = invalidParameters.GetBuffer();
        Assert.False(mesh.IsInitialized);
    }

    [Fact]
    public void Navigation_serialization_helpers_cover_empty_and_owned_buffers()
    {
        using var output = new VectorBuffer();
        NavigationUtils.WriteDetourBuffer(output, new byte[] { 1, 2, 3 });
        NavigationUtils.WriteDetourBuffer(output, ReadOnlySpan<byte>.Empty);
        output.Seek(0);

        var first = NavigationUtils.ReadDetourBuffer(output);
        Assert.True(first.IsValid);
        Assert.Equal(new byte[] { 1, 2, 3 }, first.Data);
        Assert.Equal(3, first.DataSize);

        var second = NavigationUtils.ReadDetourBuffer(output);
        Assert.False(second.IsValid);
        Assert.Equal(0, second.DataSize);
        Assert.NotNull(second.Data);
        first.Release();
        second.Release();
        Assert.False(first.IsValid);
        Assert.False(second.IsValid);

        using var build = new NavBuildData();
        Assert.True(build.IsEmpty);
        build.Vertices.Add(Vector3f.Zero);
        Assert.True(build.IsEmpty);
        build.Indices.Add(0);
        Assert.False(build.IsEmpty);
        build.Dispose();

        using var simple = new SimpleNavBuildData();
        simple.Vertices.Add(Vector3f.Zero);
        simple.Indices.Add(0);
        simple.Dispose();
        Assert.False(simple.IsEmpty);

        using var dynamic = new DynamicNavBuildData();
        dynamic.Vertices.Add(Vector3f.Zero);
        dynamic.Indices.Add(0);
        dynamic.TileData.Add([4, 5]);
        dynamic.Dispose();
        Assert.Empty(dynamic.TileData);
        Assert.False(dynamic.IsEmpty);
    }

    [Fact]
    public void Navigation_components_cover_area_obstacle_and_off_mesh_boundaries()
    {
        using var scene = new Scene();
        var start = scene.CreateChild("Start");
        start.SetPosition(new Vector3f(2.0f, 3.0f, 4.0f));
        var end = scene.CreateChild("End");
        end.SetPosition(new Vector3f(7.0f, 3.0f, 9.0f));

        var area = start.CreateComponent<NavArea>();
        Assert.Equal(0, area.AreaID);
        area.AreaID = 255;
        Assert.Equal(255, area.AreaID);
        area.AreaID = 256;
        Assert.Equal(0, area.AreaID);
        area.BoundingBox = new BoundingBox(Vector3f.Zero, new Vector3f(2.0f, 3.0f, 4.0f));
        Assert.Equal(new BoundingBox(new Vector3f(2.0f, 3.0f, 4.0f), new Vector3f(4.0f, 6.0f, 8.0f)),
            area.WorldBoundingBox);
        area.DrawDebugGeometry(null, true);

        var navigable = start.CreateComponent<Navigable>();
        Assert.True(navigable.Recursive);
        Assert.True(navigable.Walkable);
        Assert.Equal((uint)NavigationDefs.DeduceAreaId, navigable.AreaId);
        Assert.Equal(NavigationDefs.DeduceAreaId, navigable.EffectiveAreaId);
        navigable.Walkable = false;
        Assert.Equal((byte)0, navigable.EffectiveAreaId);
        navigable.AreaId = 9;
        Assert.Equal((byte)9, navigable.EffectiveAreaId);
        navigable.Recursive = false;
        navigable.Walkable = true;
        Assert.False(navigable.Recursive);

        var obstacle = start.CreateComponent<Obstacle>();
        Assert.Equal(5.0f, obstacle.Radius);
        Assert.Equal(5.0f, obstacle.Height);
        obstacle.Radius = -2.0f;
        obstacle.Height = 0.0f;
        Assert.Equal(-2.0f, obstacle.Radius);
        Assert.Equal(0.0f, obstacle.Height);
        obstacle.DrawDebugGeometry(null, false);
        obstacle.DrawDebugGeometry(false);

        var connection = start.CreateComponent<OffMeshConnection>();
        Assert.Null(connection.EndPoint);
        Assert.Equal(1.0f, connection.Radius);
        Assert.True(connection.Bidirectional);
        Assert.Equal(1u, connection.Mask);
        Assert.Equal(1u, connection.AreaID);
        connection.DrawDebugGeometry(null, true);
        connection.EndPoint = end;
        connection.Radius = 2.0f;
        connection.Bidirectional = false;
        connection.Mask = 0x12;
        connection.AreaID = 7;
        Assert.Same(end, connection.EndPoint);
        connection.ApplyAttributes();
        connection.EndPoint = null;
        Assert.Null(connection.EndPoint);
    }

    [Fact]
    public void Dynamic_navigation_mesh_handles_empty_data_and_obstacle_tiles()
    {
        using var scene = new Scene();
        var dynamic = scene.CreateComponent<DynamicNavigationMesh>();
        dynamic.MaxTiles = 1;
        dynamic.MaximumLayers = 2;
        dynamic.TileSize = 16;
        dynamic.DrawObstacles = true;

        Assert.Equal(3, dynamic.MaximumLayers);
        dynamic.MaximumLayers = NavigationMesh.MaxLayers + 1;
        Assert.Equal(NavigationMesh.MaxLayers, dynamic.MaximumLayers);
        Assert.Equal(NavmeshPartitionType.Monotone, dynamic.PartitionType);
        Assert.False(dynamic.IsInitialized);
        Assert.Empty(dynamic.GetTileData(new IntVector2(0, 0)));
        Assert.False(dynamic.AddTile([0, 0, 0, 0]));

        var obstacleNode = scene.CreateChild("Obstacle");
        obstacleNode.SetPosition(new Vector3f(1.0f, 0.0f, 1.0f));
        var obstacle = obstacleNode.CreateComponent<Obstacle>();
        obstacle.Radius = 1.0f;
        Assert.True(dynamic.IsObstacleInTile(obstacle, new IntVector2(0, 0)));
        obstacleNode.SetPosition(new Vector3f(20.0f, 0.0f, 20.0f));
        Assert.False(dynamic.IsObstacleInTile(obstacle, new IntVector2(0, 0)));

        var emptyTileData = new byte[] { 0, 0, 0, 0 };
        dynamic.OffsetTileData(emptyTileData, new IntVector3(4, 0, 4));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, emptyTileData);

        Assert.True(dynamic.Allocate());
        Assert.True(dynamic.IsInitialized);
        var data = dynamic.NavigationData;
        Assert.NotEmpty(data);
        dynamic.Clear();
        Assert.False(dynamic.IsInitialized);
        dynamic.NavigationData = data;
        Assert.True(dynamic.IsInitialized);
        Assert.Empty(dynamic.GetTileData(new IntVector2(0, 0)));
        dynamic.RemoveTile(new IntVector2(0, 0));
        dynamic.RemoveAllTiles();
        Assert.NotEmpty(dynamic.GetNavigationDataAttr());
        dynamic.Clear();
        Assert.Empty(dynamic.GetNavigationDataAttr());
    }

    [Fact]
    public void Crowd_manager_filter_and_avoidance_attributes_roundtrip_without_tiles()
    {
        using var scene = new Scene();
        var mesh = scene.CreateComponent<NavigationMesh>();
        mesh.MaxTiles = 2;
        Assert.True(mesh.Allocate());

        var manager = scene.CreateComponent<CrowdManager>();
        manager.NavigationMesh = mesh;
        Assert.Same(mesh, manager.NavigationMesh);

        manager.SetIncludeFlags(0, 0x12);
        manager.SetExcludeFlags(0, 0x04);
        manager.SetAreaCost(0, 0, 1.25f);
        manager.SetAreaCost(0, 2, 3.5f);
        manager.SetIncludeFlags(1, 0x08);
        manager.SetExcludeFlags(1, 0x02);
        manager.SetAreaCost(0, uint.MaxValue, 4.0f);
        manager.SetAreaCost(uint.MaxValue, 0, 4.0f);

        Assert.Equal(2u, manager.NumQueryFilterTypes);
        Assert.Equal(3u, manager.GetNumAreas(0));
        Assert.Equal(0u, manager.GetNumAreas(uint.MaxValue));
        Assert.Equal((ushort)0x12, manager.GetIncludeFlags(0));
        Assert.Equal((ushort)0x04, manager.GetExcludeFlags(0));
        Assert.Equal(1.25f, manager.GetAreaCost(0, 0));
        Assert.Equal(3.5f, manager.GetAreaCost(0, 2));
        Assert.Equal(ushort.MaxValue, manager.GetIncludeFlags(uint.MaxValue));
        Assert.Equal(ushort.MinValue, manager.GetExcludeFlags(uint.MaxValue));
        Assert.Equal(1.0f, manager.GetAreaCost(uint.MaxValue, uint.MaxValue));

        using var filterData = manager.GetQueryFilterTypesAttr();
        Assert.Equal(10, filterData.Count);
        Assert.Equal(2u, filterData[0].GetUInt());
        manager.SetQueryFilterTypesAttr(filterData);
        Assert.Equal((ushort)0x12, manager.GetIncludeFlags(0));
        Assert.Equal(3.5f, manager.GetAreaCost(0, 2));

        using var truncatedFilterData = new VariantVector { new Variant(1u), new Variant(1u) };
        manager.SetQueryFilterTypesAttr(truncatedFilterData);
        using var emptyFilterData = new VariantVector();
        manager.SetQueryFilterTypesAttr(emptyFilterData);

        var avoidance = new CrowdObstacleAvoidanceParams
        {
            VelBias = 0.1f,
            WeightDesVel = 1.1f,
            WeightCurVel = 1.2f,
            WeightSide = 1.3f,
            WeightToi = 1.4f,
            HorizTime = 2.0f,
            GridSize = 4,
            AdaptiveDivs = 5,
            AdaptiveRings = 6,
            AdaptiveDepth = 7
        };
        manager.SetObstacleAvoidanceParams(0, avoidance);
        manager.SetObstacleAvoidanceParams(uint.MaxValue, avoidance);
        Assert.Equal(1u, manager.NumObstacleAvoidanceTypes);
        Assert.Equal(avoidance, manager.GetObstacleAvoidanceParams(0));
        Assert.Equal(default, manager.GetObstacleAvoidanceParams(uint.MaxValue));

        using var avoidanceData = manager.GetObstacleAvoidanceTypesAttr();
        Assert.Equal(11, avoidanceData.Count);
        Assert.Equal(1u, avoidanceData[0].GetUInt());
        manager.SetObstacleAvoidanceTypesAttr(avoidanceData);
        using var truncatedAvoidanceData = new VariantVector { new Variant(1u) };
        manager.SetObstacleAvoidanceTypesAttr(truncatedAvoidanceData);
        using var emptyAvoidanceData = new VariantVector();
        manager.SetObstacleAvoidanceTypesAttr(emptyAvoidanceData);

        manager.MaxAgents = 0;
        manager.MaxAgentRadius = 0.0f;
        manager.MaxAgents = 3;
        manager.MaxAgentRadius = 1.5f;
        Assert.Equal(3u, manager.MaxAgents);
        Assert.Equal(1.5f, manager.MaxAgentRadius);
        manager.SetCrowdTarget(Vector3f.One);
        manager.SetCrowdVelocity(Vector3f.One);
        manager.ResetCrowdTarget();
        manager.NavigationMesh = null;
        Assert.Null(manager.NavigationMesh);
    }

    [Fact]
    public void Crowd_agent_and_manager_empty_paths_cover_callbacks_and_state()
    {
        using var scene = new Scene();
        var manager = scene.CreateComponent<CrowdManager>();
        var node = scene.CreateChild("Agent");
        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var agent = node.CreateComponent<CrowdAgent>();

        Assert.Empty(manager.GetAgents());
        Assert.Contains(agent, manager.GetAgents(scene, false));
        Assert.False(agent.IsInCrowd);
        Assert.Equal(-1, agent.AgentCrowdId);
        Assert.Equal(node.Position, agent.Position);
        Assert.Equal(Vector3f.Zero, agent.DesiredVelocity);
        Assert.Equal(CrowdAgentState.Invalid, agent.AgentState);
        Assert.Equal(CrowdAgentTargetState.None, agent.TargetState);
        Assert.False(agent.HasArrived);

        var managerVelocityCalls = 0;
        manager.VelocityCallback = (CrowdAgent _, float _, ref Vector3f velocity, ref float speed) =>
        {
            ++managerVelocityCalls;
            velocity = Vector3f.One;
            speed = 2.0f;
        };
        var desiredVelocity = Vector3f.Zero;
        var desiredSpeed = 0.0f;
        manager.UpdateAgentVelocity(agent, 0.25f, ref desiredVelocity, ref desiredSpeed);
        Assert.Equal(1, managerVelocityCalls);
        Assert.Equal(Vector3f.One, desiredVelocity);
        Assert.Equal(2.0f, desiredSpeed);

        var agentPositionCalls = 0;
        agent.PositionCallback = (CrowdAgent _, float _, ref Vector3f position) =>
        {
            ++agentPositionCalls;
            position = new Vector3f(8.0f, 9.0f, 10.0f);
        };
        var position = Vector3f.Zero;
        manager.UpdateAgentPosition(agent, 0.25f, ref position);
        Assert.Equal(1, agentPositionCalls);
        Assert.Equal(new Vector3f(8.0f, 9.0f, 10.0f), position);

        agent.TargetPosition = new Vector3f(5.0f, 6.0f, 7.0f);
        Assert.True(agent.HasRequestedTarget);
        Assert.Equal(CrowdAgentRequestedTarget.Position, agent.RequestedTargetType);
        agent.UpdateWorldOrigin(new Vector3f(1.0f, 2.0f, 3.0f));
        Assert.Equal(new Vector3f(4.0f, 4.0f, 4.0f), agent.TargetPosition);
        agent.TargetVelocity = new Vector3f(2.0f, 0.0f, 1.0f);
        Assert.Equal(CrowdAgentRequestedTarget.Velocity, agent.RequestedTargetType);
        agent.ResetTarget();
        Assert.False(agent.HasRequestedTarget);

        agent.ActualVelocity = Vector3f.One;
        Assert.Equal(Vector3f.Zero, agent.ActualVelocity);
        agent.UpdateNodePosition = false;
        agent.MaxAccel = -1.0f;
        agent.MaxSpeed = -1.0f;
        agent.Radius = -1.0f;
        agent.Height = -1.0f;
        agent.MaxAccel = 2.0f;
        agent.MaxSpeed = 3.0f;
        agent.Radius = 0.75f;
        agent.Height = 1.8f;
        agent.QueryFilterType = uint.MaxValue;
        agent.ObstacleAvoidanceType = uint.MaxValue;
        agent.QueryFilterType = 1;
        agent.ObstacleAvoidanceType = 1;
        agent.NavigationQuality = NavigationQuality.Low;
        agent.NavigationQuality = NavigationQuality.Medium;
        agent.NavigationQuality = NavigationQuality.High;
        agent.CollisionQueryRange = 4.0f;
        agent.SeparationWeight = 1.0f;
        agent.CollisionLayer = 2;
        agent.CollisionMask = 3;
        agent.ApplyAttributes();

        Assert.False(agent.UpdateNodePosition);
        Assert.Equal(2.0f, agent.MaxAccel);
        Assert.Equal(3.0f, agent.MaxSpeed);
        Assert.Equal(0.75f, agent.Radius);
        Assert.Equal(1.8f, agent.Height);
        Assert.Equal(1u, agent.QueryFilterType);
        Assert.Equal(1u, agent.ObstacleAvoidanceType);
        Assert.Equal(NavigationQuality.High, agent.NavigationQuality);
        Assert.Equal(4.0f, agent.CollisionQueryRange);
        Assert.Equal(1.0f, agent.SeparationWeight);
        Assert.Equal(2u, agent.CollisionLayer);
        Assert.Equal(3u, agent.CollisionMask);

        agent.RequestedTargetType = CrowdAgentRequestedTarget.None;
        agent.ApplyAttributes();
        Assert.False(agent.HasRequestedTarget);
        agent.RequestedTargetType = CrowdAgentRequestedTarget.Position;
        agent.ApplyAttributes();
        Assert.True(agent.HasRequestedTarget);
        agent.SetEnabled(false);
        agent.SetEnabled(true);
        agent.DrawDebugGeometry(null, false);
        agent.DrawDebugGeometry(false);
    }

    [Fact]
    public void Navigation_component_archives_restore_off_mesh_endpoint_and_attributes()
    {
        using var source = new Scene();
        var start = source.CreateChild("Start");
        var end = source.CreateChild("End");
        var connection = start.CreateComponent<OffMeshConnection>();
        connection.EndPoint = end;
        connection.Radius = 3.0f;
        connection.Bidirectional = false;
        connection.Mask = 0x42;
        connection.AreaID = 11;

        var area = start.CreateComponent<NavArea>();
        area.AreaID = 12;
        area.BoundingBox = new BoundingBox(new Vector3f(-2.0f, -1.0f, -3.0f), new Vector3f(2.0f, 1.0f, 3.0f));
        var navigable = start.CreateComponent<Navigable>();
        navigable.Recursive = false;
        navigable.Walkable = false;
        navigable.AreaId = 8;

        using var json = new JSONValue();
        Assert.True(source.SaveJSON(json));

        using var loaded = new Scene();
        Assert.True(loaded.LoadJSON(json));
        var loadedStart = loaded.GetChild("Start");
        var loadedEnd = loaded.GetChild("End");
        Assert.NotNull(loadedStart);
        Assert.NotNull(loadedEnd);

        var loadedConnection = loadedStart!.GetComponent<OffMeshConnection>();
        Assert.NotNull(loadedConnection);
        Assert.Same(loadedEnd, loadedConnection!.EndPoint);
        Assert.Equal(3.0f, loadedConnection.Radius);
        Assert.False(loadedConnection.Bidirectional);
        Assert.Equal(0x42u, loadedConnection.Mask);
        Assert.Equal(11u, loadedConnection.AreaID);

        var loadedArea = loadedStart.GetComponent<NavArea>();
        Assert.NotNull(loadedArea);
        Assert.Equal(12, loadedArea!.AreaID);
        Assert.Equal(new Vector3f(-2.0f, -1.0f, -3.0f), loadedArea.BoundingBox.Min);
        Assert.Equal(new Vector3f(2.0f, 1.0f, 3.0f), loadedArea.BoundingBox.Max);

        var loadedNavigable = loadedStart.GetComponent<Navigable>();
        Assert.NotNull(loadedNavigable);
        Assert.False(loadedNavigable!.Recursive);
        Assert.False(loadedNavigable.Walkable);
        Assert.Equal((uint)8, loadedNavigable.AreaId);
        Assert.Equal((byte)8, loadedNavigable.EffectiveAreaId);
    }
}
