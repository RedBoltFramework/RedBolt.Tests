// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BakedSceneCollectorCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public BakedSceneCollectorCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Collector_filters_scene_objects_and_serves_all_query_shapes()
    {
        using var scene = CreateScene();

        var bakedModel = AddStaticModel(scene, "BakedModel", new Vector3f(-4.0f, 0.0f, 0.0f), true);
        var dynamicModel = AddStaticModel(scene, "DynamicModel", new Vector3f(4.0f, 0.0f, 0.0f), false);
        _ = AddStaticModel(scene, "DisabledModel", new Vector3f(0.0f, 0.0f, -4.0f), true, enabled: false);

        using var activeHeightMap = CreateHeightMap();
        var activeTerrainNode = scene.CreateChild("BakedTerrain");
        activeTerrainNode.SetPosition(new Vector3f(0.0f, 0.0f, 4.0f));
        var bakedTerrain = activeTerrainNode.CreateComponent<Terrain>();
        bakedTerrain.SetPatchSize(4);
        Assert.True(bakedTerrain.SetHeightMap(activeHeightMap));
        bakedTerrain.SetBakeLightmap(true);

        using var disabledHeightMap = CreateHeightMap();
        var disabledTerrainNode = scene.CreateChild("DisabledTerrain");
        disabledTerrainNode.SetPosition(new Vector3f(0.0f, 0.0f, -8.0f));
        var disabledTerrain = disabledTerrainNode.CreateComponent<Terrain>();
        disabledTerrain.SetPatchSize(4);
        Assert.True(disabledTerrain.SetHeightMap(disabledHeightMap));
        disabledTerrain.SetBakeLightmap(true);
        disabledTerrainNode.SetEnabled(false);

        var bakedProbe = AddProbe(scene, "BakedProbe", Vector3f.Zero, enabled: true);
        var disabledProbe = AddProbe(scene, "DisabledProbe", new Vector3f(20.0f, 0.0f, 0.0f), enabled: false);

        var bakedLight = scene.CreateChild("BakedLight").CreateComponent<Light>();
        bakedLight.SetLightMode(LightMode.Mixed);
        bakedLight.Node!.SetPosition(new Vector3f(-8.0f, 0.0f, 0.0f));
        var realtimeLight = scene.CreateChild("RealtimeLight").CreateComponent<Light>();
        realtimeLight.SetLightMode(LightMode.Realtime);
        realtimeLight.Node!.SetPosition(new Vector3f(8.0f, 0.0f, 0.0f));

        var probeZone = scene.CreateChild("ProbeZone").CreateComponent<Zone>();
        probeZone.SetBoundingBox(new BoundingBox(-2.0f, 2.0f));
        probeZone.SetBackgroundStatic(true);
        probeZone.SetFogColor(new Color(0.2f, 0.3f, 0.4f));
        probeZone.SetBackgroundBrightness(0.75f);

        var textureZone = scene.CreateChild("TextureZone");
        textureZone.SetPosition(new Vector3f(16.0f, 0.0f, 0.0f));
        var staticTextureZone = textureZone.CreateComponent<Zone>();
        staticTextureZone.SetBoundingBox(new BoundingBox(-2.0f, 2.0f));
        staticTextureZone.SetBackgroundStatic(true);
        staticTextureZone.SetBackgroundBrightness(1.25f);
        var imageCube = new ImageCube { Name = "GlowTests/EmptySkybox.xml" };
        Assert.True(Resources.AddManualResource(imageCube));
        using var texture = new TextureCube { Name = imageCube.Name };
        staticTextureZone.SetZoneTexture(texture);

        var dynamicZoneNode = scene.CreateChild("DynamicZone");
        dynamicZoneNode.SetPosition(new Vector3f(-16.0f, 0.0f, 0.0f));
        var dynamicZone = dynamicZoneNode.CreateComponent<Zone>();
        dynamicZone.SetBoundingBox(new BoundingBox(-2.0f, 2.0f));

        UpdateOctree(scene);

        using var collector = new DefaultBakedSceneCollector();
        collector.LockScene(scene, new Vector3f(8.0f, 8.0f, 8.0f));

        var chunks = collector.GetChunks();
        Assert.NotEmpty(chunks);
        var geometryChunk = Assert.Single(chunks.Where(chunk => collector.GetUniqueGeometries(chunk).Count > 0));
        var probeChunk = Assert.Single(chunks.Where(chunk => collector.GetUniqueLightProbeGroups(chunk).Count > 0));

        var uniqueGeometries = chunks.SelectMany(collector.GetUniqueGeometries).ToList();
        Assert.Contains(bakedModel, uniqueGeometries);
        Assert.Contains(bakedTerrain, uniqueGeometries);
        Assert.DoesNotContain(dynamicModel, uniqueGeometries);
        Assert.DoesNotContain(disabledTerrain, uniqueGeometries);

        var uniqueCopy = collector.GetUniqueGeometries(geometryChunk);
        uniqueCopy.Clear();
        Assert.NotEmpty(collector.GetUniqueGeometries(geometryChunk));
        Assert.Empty(collector.GetUniqueGeometries(new Vector3i(100, 100, 100)));

        var probeGroups = collector.GetUniqueLightProbeGroups(probeChunk);
        Assert.Same(bakedProbe, Assert.Single(probeGroups));
        probeGroups.Clear();
        Assert.Same(bakedProbe, Assert.Single(collector.GetUniqueLightProbeGroups(probeChunk)));
        Assert.Empty(collector.GetUniqueLightProbeGroups(new Vector3i(100, 100, 100)));

        Assert.Same(bakedModel, Assert.Single(
            collector.GetGeometriesInBoundingBox(geometryChunk, new BoundingBox(-100.0f, 100.0f))
                .Where(component => ReferenceEquals(component, bakedModel))));
        var boxGeometries = collector.GetGeometriesInBoundingBox(
            geometryChunk, new BoundingBox(-100.0f, 100.0f));
        Assert.Contains(bakedTerrain, boxGeometries);
        Assert.DoesNotContain(dynamicModel, boxGeometries);

        var frustum = new Frustum();
        frustum.Define(new BoundingBox(-100.0f, 100.0f));
        var frustumGeometries = collector.GetGeometriesInFrustum(geometryChunk, frustum);
        Assert.Contains(bakedModel, frustumGeometries);
        Assert.Contains(bakedTerrain, frustumGeometries);
        Assert.DoesNotContain(dynamicModel, frustumGeometries);

        var allLights = collector.GetLightsInBoundingBox(geometryChunk, new BoundingBox(-100.0f, 100.0f));
        Assert.Contains(bakedLight, allLights);
        Assert.DoesNotContain(realtimeLight, allLights);

        Assert.Contains(bakedProbe, collector.GetLightProbeGroupsInBoundingBox(
            probeChunk, new BoundingBox(-1.0f, 1.0f)));
        Assert.DoesNotContain(disabledProbe, collector.GetLightProbeGroupsInBoundingBox(
            probeChunk, new BoundingBox(-100.0f, 100.0f)));
        Assert.Empty(collector.GetLightProbeGroupsInBoundingBox(
            probeChunk, new BoundingBox(30.0f, 40.0f)));
        Assert.Same(probeZone, collector.GetLightProbeGroupZone(probeChunk, bakedProbe));

        var backgrounds = Assert.IsAssignableFrom<IReadOnlyList<BakedSceneBackground>>(collector.GetBackgrounds());
        Assert.Equal(3, backgrounds.Count);
        Assert.Equal(0.75f, backgrounds[(int)collector.GetZoneBackground(geometryChunk, probeZone)].Intensity);
        var textureBackground = backgrounds[(int)collector.GetZoneBackground(geometryChunk, staticTextureZone)];
        Assert.NotNull(textureBackground.Image);
        Assert.Equal(1.25f, textureBackground.Intensity);
        Assert.Equal(0U, collector.GetZoneBackground(geometryChunk, dynamicZone));
        Assert.Equal(0U, collector.GetZoneBackground(geometryChunk, null));
        Assert.Equal(0U, collector.GetZoneBackground(geometryChunk, new Zone()));

        Assert.True(collector.GetChunkBoundingBox(geometryChunk).Defined);
        Assert.False(collector.GetChunkBoundingBox(new Vector3i(100, 100, 100)).Defined);
        collector.CommitGeometries(geometryChunk);

        collector.UnlockScene();
        Assert.Empty(collector.GetChunks());
        Assert.Same(backgrounds, collector.GetBackgrounds());
    }

    [Fact]
    public void Collector_handles_empty_scene_and_default_query_results()
    {
        using var scene = CreateScene();
        using var collector = new DefaultBakedSceneCollector();

        Assert.Empty(collector.GetChunks());
        Assert.Null(collector.GetBackgrounds());

        collector.LockScene(scene, new Vector3f(16.0f, 16.0f, 16.0f));

        var missingChunk = new Vector3i(7, 8, 9);
        Assert.Empty(collector.GetChunks());
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BakedSceneBackground>>(collector.GetBackgrounds()));
        Assert.Empty(collector.GetUniqueGeometries(missingChunk));
        Assert.Empty(collector.GetUniqueLightProbeGroups(missingChunk));
        Assert.False(collector.GetChunkBoundingBox(missingChunk).Defined);
        Assert.Empty(collector.GetLightsInBoundingBox(missingChunk, new BoundingBox(-1.0f, 1.0f)));
        Assert.Empty(collector.GetGeometriesInBoundingBox(missingChunk, new BoundingBox(-1.0f, 1.0f)));
        Assert.Empty(collector.GetLightProbeGroupsInBoundingBox(missingChunk, new BoundingBox(-1.0f, 1.0f)));

        var frustum = new Frustum();
        frustum.Define(new BoundingBox(-1.0f, 1.0f));
        Assert.Empty(collector.GetGeometriesInFrustum(missingChunk, frustum));
        Assert.Equal(0U, collector.GetZoneBackground(missingChunk, null));
        collector.CommitGeometries(missingChunk);

        collector.UnlockScene();
        Assert.Empty(collector.GetChunks());
    }

    [Fact]
    public void Baked_background_samples_loaded_image_in_linear_space()
    {
        var image = Resources.Get<ImageCube>("Textures/Skybox.xml");
        var background = new BakedSceneBackground
        {
            Image = image,
            Intensity = 1.5f
        };

        var direction = Vector3f.Right;
        Assert.Equal(1.5f * image.SampleNearest(direction).GammaToLinear().ToVector3(),
            background.SampleLinear(direction));
    }

    private StaticModel AddStaticModel(Scene scene, string name, Vector3f position, bool bakeLightmap,
        bool enabled = true)
    {
        var node = scene.CreateChild(name);
        node.SetPosition(position);
        if (!enabled)
            node.SetEnabled(false);

        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        model.SetBakeLightmap(bakeLightmap);
        return model;
    }

    private static LightProbeGroup AddProbe(Scene scene, string name, Vector3f position, bool enabled)
    {
        var node = scene.CreateChild(name);
        node.SetPosition(position);
        var group = node.CreateComponent<LightProbeGroup>();
        group.SetLightProbes([new LightProbe { Position = Vector3f.Zero }]);
        if (!enabled)
            group.SetEnabled(false);
        return group;
    }

    private static Scene CreateScene()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4);
        return scene;
    }

    private static void UpdateOctree(Scene scene)
    {
        var octree = scene.GetComponent<Octree>()!;
        var drawables = new List<Drawable>();
        scene.GetComponents(drawables);
        foreach (var drawable in drawables)
            octree.QueueUpdate(drawable);
        octree.Update(new FrameInfo());
    }

    private static Image CreateHeightMap()
    {
        var image = new Image();
        Assert.True(image.SetSize(5, 5, 1));
        var data = new byte[25];
        for (var i = 0; i < data.Length; ++i)
            data[i] = (byte)(i * 3);
        image.SetData(data);
        return image;
    }
}
