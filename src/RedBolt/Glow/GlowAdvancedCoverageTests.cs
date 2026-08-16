// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;

using RedBolt.Core;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GlowAdvancedCoverageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GlowAdvancedCoverageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Raytracer_scene_creates_terrain_geometry_and_disposes_idempotently()
    {
        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        using var heightMap = CreateHeightMap();
        using var material = new Material();
        material.SetShaderParameter("MatDiffColor", new Vector4f(0.5f, 0.6f, 0.7f, 1.0f));
        material.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        material.SetShaderParameter("UOffset", Vector4f.Zero);
        material.SetShaderParameter("VOffset", Vector4f.Zero);
        material.SetPixelShaderDefines("ALPHAMASK");
        var diffuseImage = new Image { Name = "GlowTests/RaytracerTerrainDiffuse" };
        Assert.True(diffuseImage.SetSize(1, 1, 4));
        diffuseImage.SetPixel(0, 0, new Color(0.75f, 0.5f, 0.25f, 0.5f));
        Assert.True(_fixture.Resources.AddManualResource(diffuseImage));
        using var diffuseTexture = new Texture2D { Name = diffuseImage.Name };
        material.SetTexture(ShaderResources.Albedo, diffuseTexture);

        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetPatchSize(4);
        terrain.SetSpacing(new Vector3f(2.0f, 3.0f, 2.0f));
        terrain.SetBakeLightmap(true);
        terrain.SetMaterial(material);
        Assert.True(terrain.SetHeightMap(heightMap));

        var raytracer = RaytracerScene.Create(_fixture.Context, [terrain], 1, []);
        Assert.Single(raytracer.Geometries);
        Assert.Equal(0, raytracer.Geometries[0].ObjectIndex);
        Assert.Equal(0, raytracer.Geometries[0].GeometryIndex);
        Assert.Equal(0, raytracer.Geometries[0].LodIndex);
        Assert.False(raytracer.Geometries[0].Material.Opaque);
        Assert.True(raytracer.Geometries[0].Material.StoreUv);
        Assert.NotNull(raytracer.Geometries[0].Material.DiffuseImage);
        Assert.True(raytracer.MaxDistance > 0.0f);

        var hit = new RedBolt.Bindings.Embree.RTCHit { PrimID = 0, U = 0.0f, V = 0.0f };
        var directFilter = GetPrivateMethod("IsTransparentForDirect");
        var incoming = new object[] { raytracer.Geometries[0], hit, Vector3f.One };
        Assert.True((bool)directFilter.Invoke(null, incoming)!);
        Assert.NotEqual(Vector3f.One, (Vector3f)incoming[2]!);
        var indirectFilter = GetPrivateMethod("IsTransparentForIndirect");
        _ = indirectFilter.Invoke(null, [raytracer.Geometries[0], hit]);

        var geometryBuffer = new LightmapChartGeometryBuffer(0, 1);
        geometryBuffer.GeometryIds[0] = 1;
        geometryBuffer.LightMasks[0] = uint.MaxValue;
        geometryBuffer.Positions[0] = Vector3f.Zero;
        geometryBuffer.SmoothNormals[0] = Vector3f.Up;
        var directSettings = new DirectLightTracingSettings(1) { NumTasks = 1 };
        var geometryMapping = new[] { -1, 0 };
        foreach (var lightType in new[] { LightType.Point, LightType.Spot })
        {
            var light = new BakedLight
            {
                LightType = lightType,
                LightMode = LightMode.Baked,
                LightMask = uint.MaxValue,
                Color = Color.White,
                Position = new Vector3f(0.0f, 10.0f, 0.0f),
                Direction = Vector3f.Down,
                Distance = 20.0f,
                Cutoff = 0.25f
            };
            LightTracer.BakeDirectLightForCharts(new LightmapChartBakedDirect(1), geometryBuffer,
                raytracer, geometryMapping, light, directSettings);
        }

        using var staticMaterial = new Material();
        staticMaterial.SetShaderParameter("MatDiffColor", Vector4f.One);
        staticMaterial.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        staticMaterial.SetShaderParameter("UOffset", new Vector4f(0.0f, 0.0f, 0.0f, 2.0f));
        staticMaterial.SetShaderParameter("VOffset", new Vector4f(0.0f, 0.0f, 0.0f, 2.0f));
        staticMaterial.SetPixelShaderDefines("ALPHAMASK");
        staticMaterial.SetTexture(ShaderResources.Albedo, diffuseTexture);
        var staticModel = scene.CreateChild("StaticModel").CreateComponent<StaticModel>();
        staticModel.SetModel(_fixture.Resources.Get<Model>("Models/Sphere.mdl"));
        staticModel.SetMaterial(staticMaterial);
        staticModel.SetBakeLightmap(true);
        staticModel.SetLightmapScaleOffset(new Vector4f(1.0f, 1.0f, 0.0f, 0.0f));
        using var lightmapMaterial = new Material();
        lightmapMaterial.SetShaderParameter("MatDiffColor", Vector4f.One);
        lightmapMaterial.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        lightmapMaterial.SetShaderParameter("UOffset", Vector4f.Zero);
        lightmapMaterial.SetShaderParameter("VOffset", Vector4f.Zero);
        var lightmapModel = scene.CreateChild("ClampedStaticModel").CreateComponent<StaticModel>();
        lightmapModel.SetModel(_fixture.Resources.Get<Model>("Models/Sphere.mdl"));
        lightmapModel.SetMaterial(lightmapMaterial);
        lightmapModel.SetBakeLightmap(true);
        lightmapModel.SetLightmapScaleOffset(new Vector4f(2.0f, 2.0f, -1.0f, -1.0f));
        using var staticRaytracer = RaytracerScene.Create(_fixture.Context, [staticModel, lightmapModel], 1, []);
        Assert.NotEmpty(staticRaytracer.Geometries);
        Assert.True(staticRaytracer.Geometries[0].Material.StoreUv);

        raytracer.Dispose();
        raytracer.Dispose();
    }

    [NativeBindingFact]
    public void Indirect_tracing_processes_terrain_hits_for_charts_and_probes()
    {
        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        using var heightMap = CreateHeightMap();
        using var material = new Material();
        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        material.SetShaderParameter("UOffset", Vector4f.Zero);
        material.SetShaderParameter("VOffset", Vector4f.Zero);
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetPatchSize(4);
        terrain.SetSpacing(new Vector3f(10.0f, 1.0f, 10.0f));
        terrain.SetBakeLightmap(true);
        terrain.SetMaterial(material);
        Assert.True(terrain.SetHeightMap(heightMap));

        var backgrounds = new List<BakedSceneBackground>
        {
            new() { Intensity = 1.0f, Color = new Color(0.1f, 0.2f, 0.3f) }
        };
        using var raytracer = RaytracerScene.Create(_fixture.Context, [terrain], 1, backgrounds);
        var ground = terrain.HeightMapToWorld(new Vector2i(2, 2));
        var position = ground + Vector3f.Up * 0.5f;

        var geometryBuffer = new LightmapChartGeometryBuffer(0, 1);
        geometryBuffer.GeometryIds[0] = 1;
        geometryBuffer.Positions[0] = position;
        geometryBuffer.FaceNormals[0] = Vector3f.Down;
        geometryBuffer.SmoothNormals[0] = Vector3f.Down;
        geometryBuffer.BackgroundIds[0] = 0;
        var direct = new LightmapChartBakedDirect(1);
        direct.SurfaceLight[0] = Vector3f.One;
        direct.Albedo[0] = Vector3f.One;
        var chartIndirect = new LightmapChartBakedIndirect(1);
        var tracing = new IndirectLightTracingSettings(128, 2) { NumTasks = 1 };
        var mesh = new TetrahedralMesh();
        mesh.Define([]);
        var probeData = new LightProbeCollectionBakedData();

        LightTracer.BakeIndirectLightForCharts(chartIndirect, [direct], geometryBuffer, mesh, probeData,
            raytracer, [-1, 0], tracing);

        var collection = new LightProbeCollectionForBaking();
        collection.WorldPositions.Add(position);
        collection.WorldPositions.Add(position - Vector3f.Up);
        collection.BackgroundIds.Add(0);
        collection.BackgroundIds.Add(0);
        collection.LightMasks.Add(uint.MaxValue);
        collection.LightMasks.Add(uint.MaxValue);
        var bakedProbes = new LightProbeCollectionBakedData();
        bakedProbes.Resize(2);
        var probeTracing = new IndirectLightTracingSettings(128, 1) { NumTasks = 1 };
        LightTracer.BakeIndirectLightForLightProbes(bakedProbes, collection, [direct], raytracer, probeTracing);

        var directProbeData = new LightProbeCollectionBakedData();
        directProbeData.Resize(1);
        var directProbeSettings = new DirectLightTracingSettings(32) { NumTasks = 1 };
        LightTracer.BakeDirectLightForLightProbes(directProbeData, collection, raytracer,
            new BakedLight
            {
                LightType = LightType.Directional,
                LightMode = LightMode.Baked,
                LightMask = uint.MaxValue,
                Color = Color.White,
                Direction = Vector3f.Down
            }, directProbeSettings);

        var tooShortLight = new BakedLight
        {
            LightType = LightType.Point,
            LightMode = LightMode.Baked,
            LightMask = uint.MaxValue,
            Color = Color.White,
            Position = position + Vector3f.Up,
            Distance = 0.01f
        };
        LightTracer.BakeDirectLightForCharts(new LightmapChartBakedDirect(1), geometryBuffer,
            raytracer, [-1, 0], tooShortLight, new DirectLightTracingSettings(1) { NumTasks = 1 });

        Assert.Single(chartIndirect.Light);
        Assert.Equal(2, bakedProbes.SphericalHarmonics.Count);
    }

    [NativeBindingFact]
    public void Indirect_chart_tracing_uses_light_probe_sh_when_geometry_has_multiple_lods()
    {
        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        using var model = CreateTwoLodModel();
        using var material = new Material();
        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        material.SetShaderParameter("UOffset", Vector4f.Zero);
        material.SetShaderParameter("VOffset", Vector4f.Zero);
        var staticModel = scene.CreateChild("TwoLods").CreateComponent<StaticModel>();
        staticModel.SetModel(model);
        staticModel.SetMaterial(material);
        staticModel.SetBakeLightmap(true);

        using var raytracer = RaytracerScene.Create(_fixture.Context, [staticModel], 1, []);
        Assert.Equal(2, raytracer.Geometries.Count);
        Assert.All(raytracer.Geometries, geometry => Assert.Equal(2, geometry.NumLods));

        var geometryBuffer = new LightmapChartGeometryBuffer(0, 1);
        geometryBuffer.GeometryIds[0] = 1;
        geometryBuffer.Positions[0] = Vector3f.Zero;
        geometryBuffer.SmoothNormals[0] = Vector3f.Up;
        var indirect = new LightmapChartBakedIndirect(1);
        var mesh = new TetrahedralMesh();
        mesh.Define([]);
        var settings = new IndirectLightTracingSettings(1, 1) { NumTasks = 1 };

        LightTracer.BakeIndirectLightForCharts(indirect, [new LightmapChartBakedDirect(1)],
            geometryBuffer, mesh, new LightProbeCollectionBakedData(), raytracer, [-1, 0], settings);

        Assert.Single(indirect.Light);
    }

    [NativeBindingFact]
    public void Indirect_light_probe_bake_handles_a_miss_against_the_background()
    {
        var backgrounds = new List<BakedSceneBackground>
        {
            new()
            {
                Intensity = 1.0f,
                Color = new Color(0.25f, 0.5f, 0.75f)
            }
        };
        using var raytracer = RaytracerScene.Create(_fixture.Context, [], 0, backgrounds);

        var collection = new LightProbeCollectionForBaking();
        collection.WorldPositions.Add(Vector3f.Zero);
        collection.LightMasks.Add(uint.MaxValue);
        collection.BackgroundIds.Add(0);

        var bakedData = new LightProbeCollectionBakedData();
        bakedData.Resize(1);
        var settings = new IndirectLightTracingSettings(1, 1)
        {
            NumTasks = 1
        };

        LightTracer.BakeIndirectLightForLightProbes(
            bakedData, collection, Array.Empty<LightmapChartBakedDirect?>(), raytracer, settings);

        Assert.Single(bakedData.SphericalHarmonics);
    }

    [Fact]
    public void Light_tracer_private_sampling_helpers_cover_light_types_and_material_filters()
    {
        var calculateSamples = GetPrivateMethod("CalculateNumSamples");
        Assert.Equal(1, calculateSamples.Invoke(null, [
            new BakedLight { LightType = LightType.Directional, Angle = 0.0f }, 8u]));
        Assert.Equal(8, calculateSamples.Invoke(null, [
            new BakedLight { LightType = LightType.Directional, Angle = 1.0f }, 8u]));
        Assert.Equal(1, calculateSamples.Invoke(null, [
            new BakedLight { LightType = LightType.Point, Radius = 0.0f }, 8u]));
        Assert.Equal(8, calculateSamples.Invoke(null, [
            new BakedLight { LightType = LightType.Point, Radius = 1.0f }, 8u]));

        var generateRay = GetPrivateMethod("GenerateDirectRay");
        Assert.True(InvokeGenerateRay(generateRay, new BakedLight
        {
            LightType = LightType.Directional,
            Direction = Vector3f.Down,
            Color = Color.White,
            HalfAngleTan = 0.0f
        }, Vector3f.Zero, 10.0f));
        Assert.True(InvokeGenerateRay(generateRay, new BakedLight
        {
            LightType = LightType.Point,
            Position = new Vector3f(0.0f, 0.0f, 5.0f),
            Distance = 10.0f,
            Color = Color.White
        }, Vector3f.Zero, 10.0f));
        Assert.False(InvokeGenerateRay(generateRay, new BakedLight
        {
            LightType = LightType.Point,
            Position = new Vector3f(0.0f, 0.0f, 5.0f),
            Distance = 1.0f,
            Color = Color.White
        }, Vector3f.Zero, 10.0f));
        Assert.True(InvokeGenerateRay(generateRay, new BakedLight
        {
            LightType = LightType.Spot,
            Position = new Vector3f(0.0f, 0.0f, 5.0f),
            Direction = Vector3f.Back,
            Distance = 10.0f,
            Cutoff = 0.25f,
            Color = Color.White
        }, Vector3f.Zero, 10.0f));
        Assert.False(InvokeGenerateRay(generateRay, new BakedLight
        {
            LightType = LightType.Spot,
            Position = new Vector3f(0.0f, 0.0f, 5.0f),
            Direction = Vector3f.Up,
            Distance = 10.0f,
            Cutoff = 0.25f,
            Color = Color.White
        }, Vector3f.Zero, 10.0f));

        var current = CreateRaytracerGeometry(2, 3, 0);
        var otherLod = CreateRaytracerGeometry(2, 3, 1);
        var otherObject = CreateRaytracerGeometry(4, 3, 1);
        var unwantedLod = GetPrivateMethod("IsUnwantedLod");
        Assert.True((bool)unwantedLod.Invoke(null, [current, otherLod])!);
        Assert.True((bool)unwantedLod.Invoke(null, [current, otherObject])!);
        Assert.False((bool)unwantedLod.Invoke(null, [current, CreateRaytracerGeometry(4, 3, 0)])!);

        var transparent = CreateRaytracerGeometry(0, 0, 0, new RaytracingGeometryMaterial
        {
            DiffuseColor = Vector3f.One,
            Alpha = 0.5f
        });
        var opaque = CreateRaytracerGeometry(0, 0, 0, new RaytracingGeometryMaterial
        {
            Opaque = true
        });
        var directFilter = GetPrivateMethod("IsTransparentForDirect");
        var incoming = new object[] { transparent, default(RedBolt.Bindings.Embree.RTCHit)!, Vector3f.One };
        Assert.True((bool)directFilter.Invoke(null, incoming)!);
        Assert.NotEqual(Vector3f.One, (Vector3f)incoming[2]!);
        incoming[2] = Vector3f.One;
        Assert.False((bool)directFilter.Invoke(null, [opaque, default(RedBolt.Bindings.Embree.RTCHit), incoming[2]])!);

        var indirectFilter = GetPrivateMethod("IsTransparentForIndirect");
        transparent = CreateRaytracerGeometry(0, 0, 0, new RaytracingGeometryMaterial
        {
            DiffuseColor = Vector3f.One,
            Alpha = 0.0f
        });
        Assert.True((bool)indirectFilter.Invoke(null, [
            transparent, default(RedBolt.Bindings.Embree.RTCHit)])!);
        transparent = CreateRaytracerGeometry(0, 0, 0, new RaytracingGeometryMaterial
        {
            DiffuseColor = Vector3f.One,
            Alpha = 1.0f
        });
        Assert.False((bool)indirectFilter.Invoke(null, [
            transparent, default(RedBolt.Bindings.Embree.RTCHit)])!);
        Assert.False((bool)indirectFilter.Invoke(null, [
            opaque, default(RedBolt.Bindings.Embree.RTCHit)])!);
    }

    [Fact]
    public void Incremental_baker_rejects_scene_without_an_output_directory_and_formats_names()
    {
        using var scene = new Scene();
        scene.CreateComponent<GlobalIllumination>();
        using var collector = new DefaultBakedSceneCollector();
        using var cache = new BakedLightMemoryCache();
        using var baker = new IncrementalLightBaker();

        Assert.False(baker.Initialize(new LightBakingSettings(), scene, collector, cache));

        var format = typeof(IncrementalLightBaker).GetMethod(
            "Format", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal("Lightmap-2-3", format.Invoke(null, ["Lightmap-{}-{}", new object[] { 2, 3 }]));
        Assert.Throws<TargetInvocationException>(() => format.Invoke(null, ["Lightmap", new object[] { 2 }]));

        var swizzle = typeof(IncrementalLightBaker).GetMethod(
            "Swizzle", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotEqual(0UL, swizzle.Invoke(null, [new Vector3i(1, 2, 3), Vector3i.Zero]));

        var outputRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "RedBoltGlowOutput-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(outputRoot);
        try
        {
            using var namedScene = new Scene();
            namedScene.CreateComponent<GlobalIllumination>();
            namedScene.SetFileName(System.IO.Path.Combine(outputRoot, "Scene.xml"));
            using var namedCollector = new DefaultBakedSceneCollector();
            using var namedCache = new BakedLightMemoryCache();
            using var namedBaker = new IncrementalLightBaker();
            Assert.True(namedBaker.Initialize(new LightBakingSettings(), namedScene,
                namedCollector, namedCache));
        }
        finally
        {
            System.IO.Directory.Delete(outputRoot, true);
        }
    }

    [Fact]
    public void Geometry_buffer_generation_covers_missing_material_and_terrain_paths()
    {
        using var glowResources = GlowTestData.MountResourceDirs(_fixture.Resources);
        var missingMaterialSettings = new LightmapGeometryBakingSettings
        {
            MaterialName = "GlowTests/MissingBakerMaterial.xml"
        };
        var missing = LightmapGeometryBuffer.GenerateLightmapGeometryBakingScenes(
            _fixture.Context, [], 4, missingMaterialSettings);
        Assert.Empty(missing.BakingScenes);

        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        using var heightMap = CreateHeightMap();
        using var terrainMaterial = new Material();
        terrainMaterial.SetShaderParameter("MatDiffColor", Vector4f.One);
        terrainMaterial.SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        terrainMaterial.SetShaderParameter("UOffset", Vector4f.Zero);
        terrainMaterial.SetShaderParameter("VOffset", Vector4f.Zero);
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetPatchSize(4);
        terrain.SetScaleInLightmap(1.0f);
        terrain.SetBakeLightmap(true);
        terrain.SetLightmapIndex(0);
        terrain.SetLightmapScaleOffset(Vector4f.One);
        terrain.SetMaterial(terrainMaterial);
        Assert.True(terrain.SetHeightMap(heightMap));

        var result = LightmapGeometryBuffer.GenerateLightmapGeometryBakingScenes(
            _fixture.Context, [terrain], 4, new LightmapGeometryBakingSettings());
        Assert.Single(result.BakingScenes);
        Assert.Equal(2, result.IdToObject.Count);
        foreach (var bakingScene in result.BakingScenes)
            bakingScene.Scene?.Dispose();

        using var invalidModel = CreateExportedModel(
            PrimitiveType.TriangleList,
            new ModelVertexFormat { Position = VertexElementType.Vector3 },
            [0, 1]);
        using var missingAttributesModel = CreateExportedModel(
            PrimitiveType.LineList,
            new ModelVertexFormat(),
            [0, 1]);
        var collectSeams = typeof(LightmapGeometryBuffer).GetMethod(
            "CollectModelSeams", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Empty((List<LightmapSeam>)collectSeams.Invoke(null, [invalidModel, 1])!);
        Assert.Empty((List<LightmapSeam>)collectSeams.Invoke(null, [missingAttributesModel, 1])!);

        using var seamModel = CreateCollinearSeamModel();
        var seams = (List<LightmapSeam>)collectSeams.Invoke(null, [seamModel, 1])!;
        Assert.Empty(seams);
    }

    private static MethodInfo GetPrivateMethod(string name) =>
        typeof(LightTracer).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    private static bool InvokeGenerateRay(MethodInfo method, BakedLight light, Vector3f position,
        float maxDistance)
    {
        var arguments = new object?[] { light, maxDistance, position, null, null, null };
        return (bool)method.Invoke(null, arguments)!;
    }

    private static RaytracerGeometry CreateRaytracerGeometry(int objectIndex, int geometryIndex, int lodIndex,
        RaytracingGeometryMaterial? material = null)
    {
        return new RaytracerGeometry
        {
            ObjectIndex = objectIndex,
            GeometryIndex = geometryIndex,
            LodIndex = lodIndex,
            NumLods = 1,
            EmbreeGeometry = default!,
            Material = material ?? new RaytracingGeometryMaterial()
        };
    }

    private static Image CreateHeightMap()
    {
        var image = new Image();
        image.SetSize(5, 5, 1);
        image.SetData(new byte[25]);
        return image;
    }

    private static Model CreateExportedModel(PrimitiveType primitiveType, ModelVertexFormat format,
        IReadOnlyList<uint> indices)
    {
        var lod = new GeometryLODView
        {
            PrimitiveType = primitiveType,
            VertexFormat = format
        };
        for (var i = 0; i < 6; ++i)
        {
            var vertex = new ModelVertex();
            vertex.SetPosition(new Vector3f(i, 0.0f, 0.0f));
            vertex.SetNormal(Vector3f.Up);
            lod.Vertices.Add(vertex);
        }
        lod.Indices.AddRange(indices);
        using var view = new ModelView();
        view.SetGeometries([new GeometryView { Lods = [lod] }]);
        return view.ExportModel(ModelViewExportFlag.Headless);
    }

    private static Model CreateCollinearSeamModel()
    {
        var format = new ModelVertexFormat
        {
            Position = VertexElementType.Vector3,
            Normal = VertexElementType.Vector3,
            Uv = [ModelVertexFormat.Undefined, VertexElementType.Vector2,
                ModelVertexFormat.Undefined, ModelVertexFormat.Undefined]
        };
        var lod = new GeometryLODView
        {
            PrimitiveType = PrimitiveType.TriangleList,
            VertexFormat = format
        };
        var vertices = new[]
        {
            (new Vector3f(0, 0, 0), new Vector2f(0, 0)),
            (new Vector3f(1, 0, 0), new Vector2f(1, 0)),
            (new Vector3f(0, 1, 0), new Vector2f(0, 1)),
            (new Vector3f(1, 0, 0), new Vector2f(2, 0)),
            (new Vector3f(0, 0, 0), new Vector2f(3, 0)),
            (new Vector3f(1, -1, 0), new Vector2f(2, 1))
        };
        foreach (var (position, uv) in vertices)
        {
            var vertex = new ModelVertex();
            vertex.SetPosition(position);
            vertex.SetNormal(Vector3f.Up);
            vertex.Uv[1] = new Vector4f(uv.X, uv.Y, 0.0f, 0.0f);
            lod.Vertices.Add(vertex);
        }
        lod.Indices.AddRange([0, 1, 2, 3, 4, 5]);
        using var view = new ModelView();
        view.SetGeometries([new GeometryView { Lods = [lod] }]);
        using var shared = new Variant(true);
        view.AddMetadata(LightmapUVGenerationSettings.LightmapSharedUV, shared);
        return view.ExportModel(ModelViewExportFlag.Headless);
    }

    private static Model CreateTwoLodModel()
    {
        var format = new ModelVertexFormat
        {
            Position = VertexElementType.Vector3,
            Normal = VertexElementType.Vector3,
            Uv = [VertexElementType.Vector2, VertexElementType.Vector2,
                ModelVertexFormat.Undefined, ModelVertexFormat.Undefined]
        };
        var geometry = new GeometryView();
        for (var lodIndex = 0; lodIndex < 2; ++lodIndex)
        {
            var lod = new GeometryLODView
            {
                PrimitiveType = PrimitiveType.TriangleList,
                VertexFormat = format
            };
            foreach (var (position, uv) in new[]
            {
                (new Vector3f(-1, 0, -1), new Vector2f(0, 0)),
                (new Vector3f(1, 0, -1), new Vector2f(1, 0)),
                (new Vector3f(0, 0, 1), new Vector2f(0.5f, 1))
            })
            {
                var vertex = new ModelVertex();
                vertex.SetPosition(position);
                vertex.SetNormal(Vector3f.Up);
                vertex.Uv[0] = new Vector4f(uv.X, uv.Y, 0.0f, 0.0f);
                vertex.Uv[1] = new Vector4f(uv.X, uv.Y, 0.0f, 0.0f);
                lod.Vertices.Add(vertex);
            }
            lod.Indices.AddRange([0, 1, 2]);
            geometry.Lods.Add(lod);
        }
        using var view = new ModelView();
        view.SetGeometries([geometry]);
        return view.ExportModel(ModelViewExportFlag.Headless);
    }
}

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class GlowStitcherCoverageTests
{
    [VisualFact]
    public void Stitcher_initializes_formats_builds_seams_and_runs_a_render_iteration()
    {
        using var fixture = new VisualRegressionFixture(32, 32);
        var context = fixture.EngineFixture.Context;
        using var oneChannel = LightmapStitcher.Initialize(context, 4, 1);
        using var twoChannels = LightmapStitcher.Initialize(context, 4, 2);
        using var fourChannels = LightmapStitcher.Initialize(context, 4, 4);

        var seam = new LightmapSeam();
        seam.Positions[0] = new Vector2f(0.1f, 0.1f);
        seam.Positions[1] = new Vector2f(0.9f, 0.1f);
        seam.OtherPositions[0] = new Vector2f(0.1f, 0.9f);
        seam.OtherPositions[1] = new Vector2f(0.9f, 0.9f);
        using var seamsModel = LightmapStitcher.CreateSeamsModel(context, [seam]);

        var input = new List<Vector3f>(16);
        for (var i = 0; i < 16; ++i)
            input.Add(new Vector3f(0.25f, 0.5f, 0.75f));
        var output = new Vector4f[16];
        var settings = new LightmapStitchingSettings
        {
            NumIterations = 1,
            BlendFactor = 0.5f
        };

        LightmapStitcher.StitchLightmapSeams(fourChannels, input, output, settings, seamsModel);

        Assert.All(output, value => Assert.True(float.IsFinite(value.X) && float.IsFinite(value.Y)
            && float.IsFinite(value.Z) && float.IsFinite(value.W)));
    }
}
