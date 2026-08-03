namespace RedBolt.Tests;

using System.Collections;

using RedBolt.Core;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GlowResidualCoverageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GlowResidualCoverageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Glow_value_types_cover_default_and_explicit_clone_paths()
    {
        var bakedLight = new BakedLight();
        Assert.Equal(default, bakedLight.LightType);
        Assert.Equal(Vector3f.Zero, bakedLight.Position);

        var lightmap = new BakedLightmap(2);
        var lightmapClone = Assert.IsType<BakedLightmap>(((ICloneable)lightmap).Clone());
        Assert.NotSame(lightmap.Lightmap, lightmapClone.Lightmap);

        var chart = new LightmapChart(4, 8);
        var chartClone = Assert.IsType<LightmapChart>(((ICloneable)chart).Clone());
        Assert.Equal(chart.Index, chartClone.Index);
        Assert.NotSame(chart.Elements, chartClone.Elements);

        var seam = new LightmapSeam();
        seam.Positions[0] = Vector2f.One;
        var bakingScene = new LightmapGeometryBakingScene();
        bakingScene.Seams.Add(seam);
        var bakingSceneClone = Assert.IsType<LightmapGeometryBakingScene>(((ICloneable)bakingScene).Clone());
        Assert.Single(bakingSceneClone.Seams);

        var scenes = new LightmapGeometryBakingScenesArray
        {
            BakingScenes = [bakingScene],
            IdToObject = [new GeometryIdToObjectMapping()]
        };
        var scenesClone = Assert.IsType<LightmapGeometryBakingScenesArray>(((ICloneable)scenes).Clone());
        Assert.Single(scenesClone.BakingScenes);
        Assert.Single(scenesClone.IdToObject);

        var buffer = new LightmapChartGeometryBuffer(1, 1);
        buffer.Seams.Add(seam);
        var bufferClone = Assert.IsType<LightmapChartGeometryBuffer>(((ICloneable)buffer).Clone());
        Assert.Single(bufferClone.Seams);
    }

    [Fact]
    public void Helpers_cover_unknown_components_and_baking_material_texture_defines()
    {
        var component = new Component();
        Assert.ThrowsAny<Exception>(() => Helpers.SetLightmapIndex(component, 12));
        Assert.ThrowsAny<Exception>(() => Helpers.GetLightmapIndex(component));
        Assert.ThrowsAny<Exception>(() => Helpers.SetLightmapScaleOffset(component, Vector4f.One));
        Assert.ThrowsAny<Exception>(() => Helpers.GetLightmapScaleOffset(component));

        using var sourceMaterial = new Material();
        using var diffuse = new Texture2D();
        using var emission = new Texture2D();
        sourceMaterial.SetShaderParameter("MatDiffColor", Vector4f.One);
        sourceMaterial.SetShaderParameter("MatEmissiveColor", Vector3f.One);
        sourceMaterial.SetShaderParameter("UOffset", Vector4f.Zero);
        sourceMaterial.SetShaderParameter("VOffset", Vector4f.Zero);
        sourceMaterial.SetTexture(ShaderResources.Albedo, diffuse);
        sourceMaterial.SetTexture(ShaderResources.Emission, emission);

        var bakingMaterial = _fixture.Resources.Get<Material>("Materials/LightmapBaker.xml");
        using var result = Helpers.CreateBakingMaterial(
            bakingMaterial, sourceMaterial, new Vector4f(1, 2, 3, 4), 0, 2,
            new Vector2f(0.25f, 0.5f), 7, new Vector2f(0.01f, 0.02f));

        Assert.Same(diffuse, result.GetTexture(ShaderResources.Albedo));
        Assert.Same(emission, result.GetTexture(ShaderResources.Emission));
        Assert.Equal("DIFFMAP EMISSIVEMAP ", result.GetVertexShaderDefines());
        Assert.Equal(result.GetVertexShaderDefines(), result.GetPixelShaderDefines());
    }

    [Fact]
    public void Lightmap_charter_covers_model_fallback_terrain_and_unknown_component_paths()
    {
        using var scene = new Scene();
        var model = new Model();
        var fallbackModel = scene.CreateChild("FallbackModel").CreateComponent<StaticModel>();
        fallbackModel.SetModel(model);

        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetSpacing(new Vector3f(2, 3, 4));

        var unknownNode = scene.CreateChild("Unknown");
        var unknown = new Component();
        unknownNode.AddComponent(unknown);

        var settings = new LightmapChartingSettings
        {
            LightmapSize = 32,
            Padding = 1,
            TexelDensity = 2.0f,
            MinObjectScale = 1.0f,
            DefaultChartSize = 6
        };

        Component?[] geometries = [fallbackModel, terrain, unknown];
        var charts = LightmapCharter.GenerateLightmapCharts(geometries, settings, 2);

        Assert.NotEmpty(charts);
        Assert.Contains(charts.SelectMany(chart => chart.Elements), element =>
            ReferenceEquals(element.Component, fallbackModel));
        Assert.Contains(charts.SelectMany(chart => chart.Elements), element =>
            ReferenceEquals(element.Component, terrain));
        Assert.Contains(charts.SelectMany(chart => chart.Elements), element =>
            ReferenceEquals(element.Component, unknown));
    }

    [Fact]
    public void Lightmap_charter_adjusts_oversized_regions_and_creates_followup_charts()
    {
        using var scene = new Scene();
        var largeModel = CreateModel(new Vector2i(64, 32), 1.0f);
        var largeObject = scene.CreateChild("Large").CreateComponent<StaticModel>();
        largeObject.SetModel(largeModel);

        var settings = new LightmapChartingSettings
        {
            LightmapSize = 16,
            Padding = 1,
            TexelDensity = 1.0f,
            MinObjectScale = 1.0f,
            DefaultChartSize = 4
        };

        var oversizedCharts = LightmapCharter.GenerateLightmapCharts([largeObject], settings);
        var oversizedRegion = Assert.Single(oversizedCharts).Elements[0].Region.RectTexels.Size;
        Assert.True(oversizedRegion.X <= 14);
        Assert.True(oversizedRegion.Y <= 14);

        var exactModel = CreateModel(new Vector2i(8, 8), 1.0f);
        var first = scene.CreateChild("First").CreateComponent<StaticModel>();
        var second = scene.CreateChild("Second").CreateComponent<StaticModel>();
        first.SetModel(exactModel);
        second.SetModel(exactModel);
        var exactSettings = new LightmapChartingSettings
        {
            LightmapSize = 8,
            Padding = 0,
            TexelDensity = 1.0f,
            MinObjectScale = 1.0f,
            DefaultChartSize = 4
        };

        var charts = LightmapCharter.GenerateLightmapCharts([first, second], exactSettings, 7);
        Assert.Equal(2, charts.Count);
        Assert.Equal(7, charts[0].Index);
        Assert.Equal(8, charts[1].Index);
    }

    [Fact]
    public void Static_model_for_lightmap_dispose_path_is_callable_without_initialization()
    {
        var staticModel = new StaticModelForLightmap();
        staticModel.Dispose();
        staticModel.Dispose();
    }

    [NativeBindingFact]
    public void Lightmap_uv_generation_returns_false_for_an_invalid_mesh_index()
    {
        using var modelView = new ModelView();
        var lod = new GeometryLODView { PrimitiveType = PrimitiveType.TriangleList };
        lod.VertexFormat.Position = VertexElementType.Vector3;
        lod.VertexFormat.Normal = VertexElementType.Vector3;
        lod.Vertices.Add(CreateVertex(new Vector3f(-1, 0, -1)));
        lod.Vertices.Add(CreateVertex(new Vector3f(1, 0, -1)));
        lod.Vertices.Add(CreateVertex(new Vector3f(0, 0, 1)));
        lod.Indices.AddRange([0, 1]);
        var geometry = new GeometryView();
        geometry.Lods.Add(lod);
        modelView.SetGeometries([geometry]);

        Assert.False(LightmapUVGenerator.GenerateLightmapUV(modelView, new LightmapUVGenerationSettings()));
    }

    private static Model CreateModel(Vector2i lightmapSize, float density)
    {
        var model = new Model();
        using var size = new Variant(lightmapSize);
        using var modelDensity = new Variant(density);
        model.AddMetadata(LightmapUVGenerationSettings.LightmapSizeKey, size);
        model.AddMetadata(LightmapUVGenerationSettings.LightmapDensityKey, modelDensity);
        return model;
    }

    private static ModelVertex CreateVertex(Vector3f position)
    {
        var vertex = new ModelVertex();
        vertex.SetPosition(position);
        vertex.SetNormal(Vector3f.Up);
        return vertex;
    }
}
