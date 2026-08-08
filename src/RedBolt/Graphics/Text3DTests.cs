namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Text3DTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Text3DTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Ttf_font_preserves_codepoint_layout_and_builds_gpu_geometry()
    {
        var font = _fixture.Resources.Get<Font>("Fonts/Anonymous Pro.ttf");
        using var scene = new Scene();
        var text = scene.CreateChild("Text").CreateComponent<Text3D>();

        Assert.True(text.SetFont(font, 32.0f));
        text.SetText("HarfBuzz");
        text.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
        text.SetColor(new Color(1.0f, 0.5f, 0.25f));

        Assert.Equal("Fonts/Anonymous Pro.ttf", text.GetFontAttr().Name);
        Assert.Equal(8, text.GetNumChars());
        Assert.Equal(1, text.GetNumRows());
        Assert.True(text.GetRowWidth(0) > 0);
        Assert.Equal(48u, text.GetNumVertices(0));
        var material = text.GetMaterial();
        Assert.NotNull(material);
        Assert.Equal("HarfBuzzGpuText", material.GetTechnique(0)!.GetPass("alpha")!.PixelShader);
        Assert.All(text.GetVertices()[0], static vertex =>
        {
            Assert.True(MathF.Floor(vertex.TexCoord.X) >= 2.0f);
            Assert.True(MathF.Floor(vertex.TexCoord.Y) >= 2.0f);
            Assert.InRange(vertex.TexCoord.X - MathF.Floor(vertex.TexCoord.X), 0.125f, 0.875f);
            Assert.InRange(vertex.TexCoord.Y - MathF.Floor(vertex.TexCoord.Y), 0.125f, 0.875f);
        });
        Assert.True(text.GetBoundingBox().Defined);
    }

    [Fact]
    public void Sdf_font_uses_prebaked_pages_and_distance_field_shader()
    {
        var font = _fixture.Resources.Get<Font>("Fonts/Anonymous Pro.sdf");
        using var scene = new Scene();
        var text = scene.CreateChild("Text").CreateComponent<Text3D>();

        Assert.True(font.IsSdfFont());
        Assert.True(text.SetFont(font, 25.0f));
        text.SetText("HarfBuzz");

        Assert.Equal(2u, text.GetNumGeometries());
        Assert.Equal(66, text.GetRowHeight());
        Assert.Equal(48u, text.GetNumVertices(0) + text.GetNumVertices(1));
        Assert.Equal(
            "SIGNED_DISTANCE_FIELD",
            text.GetMaterial(0)!.GetTechnique(0)!.GetPass("alpha")!.PixelShaderDefines);
        Assert.NotNull(text.GetMaterial(1));
        Assert.True(text.GetBoundingBox().Defined);
    }

    [Fact]
    public void Sdf_font_accepts_legacy_decimal_kerning()
    {
        var font = _fixture.Resources.Get<Font>("Fonts/BlueHighway.sdf");

        Assert.True(font.IsSdfFont());
    }

    [Fact]
    public void Baked_lighting_scene_restores_all_text_components()
    {
        var xmlFile = _fixture.Resources.Get<XMLFile>("Scenes/BakedLightingExample.xml");
        using var root = xmlFile.GetRoot();
        using var scene = new Scene();

        Assert.True(scene.LoadXML(root));

        var camera = scene.FindComponent<Camera>();
        Assert.NotNull(camera);
        Assert.Equal("Camera", camera.Node!.Name);
        Assert.Equal(-3.0f, camera.Node.Position.Z);

        var texts = scene.FindComponents<Text3D>();
        Assert.Equal(5, texts.Count);
        Assert.All(texts, static text =>
        {
            Assert.NotNull(text.GetFont());
            Assert.True(text.GetFont()!.IsSdfFont());
            Assert.NotEmpty(text.GetText());
            var numVertices = 0u;
            for (var i = 0u; i < text.GetNumGeometries(); ++i)
                numVertices += text.GetNumVertices(i);
            Assert.True(numVertices > 0);
        });
    }
}
