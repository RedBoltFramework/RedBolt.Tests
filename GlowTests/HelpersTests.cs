namespace RedBolt.Tests;

using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class HelpersTests
{
    [Fact]
    public void ParallelFor_processes_each_element_once()
    {
        var hits = new int[17];
        var callbackCount = 0;

        Helpers.ParallelFor(hits.Length, 4, (fromIndex, toIndex) =>
        {
            Interlocked.Increment(ref callbackCount);
            for (var i = fromIndex; i < toIndex; ++i)
                Interlocked.Increment(ref hits[i]);
        });

        Assert.Equal(4, callbackCount);
        Assert.All(hits, hit => Assert.Equal(1, hit));
    }

    [Fact]
    public void Material_helpers_match_material_state()
    {
        var material = new Material();
        material.SetShaderParameter("MatDiffColor", new Vector4f(0.25f, 0.5f, 0.75f, 1.0f));
        material.SetShaderParameter("UOffset", new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));
        material.SetShaderParameter("VOffset", new Vector4f(5.0f, 6.0f, 7.0f, 8.0f));

        Assert.True(Helpers.IsMaterialOpaque(material));
        Assert.Equal(new Color(0.25f, 0.5f, 0.75f, 1.0f), Helpers.GetMaterialDiffuseColor(material));

        material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 1.0f, 1.0f, 0.5f));
        Assert.False(Helpers.IsMaterialOpaque(material));

        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetPixelShaderDefines("ALPHAMASK");
        Assert.False(Helpers.IsMaterialOpaque(material));
    }

    [Fact]
    public void Material_helpers_cover_technique_and_diffuse_texture_paths()
    {
        using var material = new Material();
        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetShaderParameter("UOffset", new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));
        material.SetShaderParameter("VOffset", new Vector4f(5.0f, 6.0f, 7.0f, 8.0f));
        material.SetNumTechniques(1);

        using var opaqueTechnique = new Technique { Name = "Opaque" };
        material.SetTechnique(0, opaqueTechnique);
        Assert.True(Helpers.IsMaterialOpaque(material));

        opaqueTechnique.Name = "AlphaBlend";
        Assert.False(Helpers.IsMaterialOpaque(material));

        var uOffset = Vector4f.Zero;
        var vOffset = Vector4f.Zero;
        Assert.Null(Helpers.GetMaterialDiffuseTexture(material, ref uOffset, ref vOffset));

        using var texture = new Texture2D();
        material.SetTexture(ShaderResources.Albedo, texture);
        Assert.Same(texture, Helpers.GetMaterialDiffuseTexture(material, ref uOffset, ref vOffset));
        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), uOffset);
        Assert.Equal(new Vector4f(5.0f, 6.0f, 7.0f, 8.0f), vOffset);
    }

    [Fact]
    public void Lightmap_helpers_support_static_models_and_terrain()
    {
        var scene = new Scene();
        var staticModel = scene.CreateChild("StaticModel").CreateComponent<StaticModel>();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var scaleOffset = new Vector4f(0.5f, 0.25f, 0.125f, 0.0625f);

        Helpers.SetLightmapIndex(staticModel, 3);
        Helpers.SetLightmapScaleOffset(staticModel, scaleOffset);
        Assert.Equal(3, Helpers.GetLightmapIndex(staticModel));
        Assert.Equal(scaleOffset, Helpers.GetLightmapScaleOffset(staticModel));

        Helpers.SetLightmapIndex(terrain, 4);
        Helpers.SetLightmapScaleOffset(terrain, scaleOffset);
        Assert.Equal(4, Helpers.GetLightmapIndex(terrain));
        Assert.Equal(scaleOffset, Helpers.GetLightmapScaleOffset(terrain));
    }
}
