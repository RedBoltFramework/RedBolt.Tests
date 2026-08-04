namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsCoverageGapTests
{
    [Fact]
    public void Shader_parameter_parser_covers_scalar_vector_and_fallback_forms()
    {
        Assert.Equal(true, Material.ParseShaderParameterValue("true"));
        Assert.Equal(false, Material.ParseShaderParameterValue("false"));
        Assert.Equal(1.25f, Material.ParseShaderParameterValue("1.25"));
        Assert.Equal(new Vector2f(1.0f, 2.0f), Material.ParseShaderParameterValue("1, 2"));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), Material.ParseShaderParameterValue("1 2 3"));
        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), Material.ParseShaderParameterValue("1\t2\t3\t4"));
        Assert.Equal("not-a-number", Material.ParseShaderParameterValue("not-a-number"));
        Assert.Equal("1 2 3 4 5", Material.ParseShaderParameterValue("1 2 3 4 5"));
    }

    [Fact]
    public void Material_technique_selection_covers_empty_quality_lod_and_fallback_paths()
    {
        using var material = new Material();
        material.SetTechniques([]);
        Assert.Null(material.FindTechnique(MaterialQuality.Low));

        material.SetNumTechniques(0);
        material.SetNumTechniques(3);
        material.SetTechnique(99, null);

        using var low = new Technique { Name = "Low" };
        using var high = new Technique { Name = "High" };
        material.SetTechnique(0, null, MaterialQuality.Low, 0.0f);
        material.SetTechnique(1, high, MaterialQuality.High, 10.0f);
        material.SetTechnique(2, low, MaterialQuality.Low, 20.0f);

        Assert.Same(low, material.FindTechnique(MaterialQuality.Low, 5.0f));
        Assert.Same(high, material.FindTechnique(MaterialQuality.High, 10.0f));
        Assert.Same(low, material.FindTechnique(MaterialQuality.Low, 0.0f));
        Assert.Same(low, material.FindTechnique(MaterialQuality.Low, 100.0f));
        Assert.Equal(default, material.GetTechniqueEntry(99));
        Assert.Null(material.GetPass(99, "base"));

        material.SetTechniques([
            new TechniqueEntry(high, MaterialQuality.High, 30.0f),
            new TechniqueEntry(low, MaterialQuality.Low, 1.0f)
        ]);
        Assert.Equal(30.0f, material.GetTechniqueEntry(0).LodDistance);
        Assert.Same(low, material.FindTechnique(MaterialQuality.Low, 1.0f));
    }

    [Fact]
    public void Material_shader_parameters_cover_replace_remove_specular_and_missing_paths()
    {
        using var material = new Material();
        material.SetShaderParameter("MatSpecColor", new Vector3f(0.2f, 0.0f, 0.0f));
        Assert.True(material.GetSpecular());
        material.SetShaderParameter("MatSpecColor", Vector4f.Zero);
        Assert.False(material.GetSpecular());

        material.SetShaderParameter("Custom", 4, isCustom: true);
        material.SetShaderParameter("Custom", 5, isCustom: false);
        Assert.True(material.TryGetShaderParameter("Custom", out var value));
        Assert.Equal(5, value);
        Assert.False(material.TryGetShaderParameter("Missing", out _));

        material.RemoveShaderParameter("Missing");
        material.RemoveShaderParameter("MatSpecColor");
        Assert.False(material.GetSpecular());
        Assert.Null(material.GetShaderParameter("MatSpecColor"));
        Assert.Equal(WrapMode.Loop, material.GetShaderParameterAnimationWrapMode("Missing"));
        Assert.Equal(0.0f, material.GetShaderParameterAnimationSpeed("Missing"));
    }

    [Fact]
    public void Technique_entry_comparison_and_material_state_helpers_cover_value_paths()
    {
        using var first = new Technique { Name = "First" };
        using var second = new Technique { Name = "Second" };
        var lower = new TechniqueEntry(first, MaterialQuality.Low, 1.0f);
        var higher = new TechniqueEntry(second, MaterialQuality.High, 2.0f);

        Assert.True(lower.CompareTo(higher) > 0);
        Assert.True(higher.CompareTo(lower) < 0);
        Assert.False(lower.Equals(higher));
        Assert.NotEqual(lower, higher);
        Assert.Equal(lower, lower);
        Assert.Equal(lower.GetHashCode(), lower.GetHashCode());

        using var material = new Material();
        material.SetAlphaToCoverage(true);
        material.SetLineAntiAlias(true);
        material.SetOcclusion(false);
        material.SetRenderOrder(300u);
        material.SetVertexShaderDefines("VS");
        material.SetPixelShaderDefines("PS");
        Assert.True(material.GetAlphaToCoverage());
        Assert.True(material.GetLineAntiAlias());
        Assert.False(material.GetOcclusion());
        Assert.Equal(44, material.GetRenderOrder());
        Assert.Equal("VS", material.GetVertexShaderDefines());
        Assert.Equal("PS", material.GetPixelShaderDefines());
        Assert.NotEqual(0u, material.GetPipelineStateHash());
    }
}
