namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TextureCubeInspectorWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TextureCubeInspectorWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_properties_map_and_generation_is_deferred_to_begin_rendering()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        var resourceName = $"Textures/TextureCubeInspectorWidgetTests-{Guid.NewGuid():N}.xml";
        var tempDirectory = Path.Combine(
            Path.GetTempPath(), "RedBolt.TextureCubeInspectorWidgetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var fileName = Path.Combine(tempDirectory, "Cube.xml");
        var texture = new TextureCube { Name = resourceName };
        var imageXml = new XMLFile { Name = resourceName };
        imageXml.SetAbsoluteFileName(fileName);
        using (imageXml.CreateRoot("cubemap"))
        {
        }
        Assert.True(_fixture.Resources.AddManualResource(texture));
        Assert.True(_fixture.Resources.AddManualResource(imageXml));
        var widget = new TextureCubeInspectorWidget([texture]);
        try
        {
            Assert.False(widget.CanSave());
            var properties = (ResourceInspectorWidget.PropertyDesc[])typeof(TextureCubeInspectorWidget)
                .GetField("_properties", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            Assert.Equal(
                ["SRGB", "Linear", "Filter Mode", "U Address Mode", "V Address Mode", "W Address Mode"],
                properties.Select(static property => property.Name));
            Assert.Equal(
                ["NEAREST", "BILINEAR", "TRILINEAR", "ANISOTROPIC", "NEAREST_ANISOTROPIC", "DEFAULT"],
                properties[2].Options.IntToString);
            Assert.Equal(
                ["WRAP", "MIRROR", "CLAMP", "BORDER"],
                properties[3].Options.IntToString);
            Assert.Same(properties[3].Options.IntToString, properties[4].Options.IntToString);
            Assert.Same(properties[3].Options.IntToString, properties[5].Options.IntToString);

            using (var value = new Variant(true))
                properties[0].Setter(texture, in value);
            Assert.True(texture.SetSize(4, TextureFormat.TexFormatRgba8Unorm));
            Assert.True(texture.GetSRGB());
            using (var value = new Variant(false))
                properties[0].Setter(texture, in value);
            using (var value = new Variant(true))
                properties[1].Setter(texture, in value);
            using (var value = new Variant((int)TextureFilterMode.Anisotropic))
                properties[2].Setter(texture, in value);
            using (var value = new Variant((int)TextureAddressMode.Mirror))
                properties[3].Setter(texture, in value);
            using (var value = new Variant((int)TextureAddressMode.Clamp))
                properties[4].Setter(texture, in value);
            using (var value = new Variant((int)TextureAddressMode.Wrap))
                properties[5].Setter(texture, in value);
            SetUniformCube(texture, 64, 128, 192);

            Assert.False(texture.GetSRGB());
            Assert.True(texture.GetLinear());
            Assert.Equal(TextureFilterMode.Anisotropic, texture.GetFilterMode());
            Assert.Equal(TextureAddressMode.Mirror, texture.GetAddressMode(TextureCoordinate.U));
            Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));
            Assert.Equal(TextureAddressMode.Wrap, texture.GetAddressMode(TextureCoordinate.W));
            using (var value = properties[0].Getter(texture))
                Assert.False(value.GetBool());
            using (var value = properties[1].Getter(texture))
                Assert.True(value.GetBool());
            using (var value = properties[2].Getter(texture))
                Assert.Equal((int)TextureFilterMode.Anisotropic, value.GetInt());
            using (var value = properties[3].Getter(texture))
                Assert.Equal((int)TextureAddressMode.Mirror, value.GetInt());
            using (var value = properties[4].Getter(texture))
                Assert.Equal((int)TextureAddressMode.Clamp, value.GetInt());
            using (var value = properties[5].Getter(texture))
                Assert.Equal((int)TextureAddressMode.Wrap, value.GetInt());

            var buttonPosition = RenderWidgetFrame(widget);
            var io = ImGui.GetIO();
            io.AddMousePosEvent(buttonPosition.X, buttonPosition.Y);
            io.AddMouseButtonEvent(0, true);
            RenderWidgetFrame(widget);
            io.AddMouseButtonEvent(0, false);
            RenderWidgetFrame(widget);

            Assert.False(File.Exists(fileName));
            _fixture.Context.SendEvent<GraphicsEvents.BeginRenderingEvent>();

            var document = XDocument.Load(fileName);
            var savedHarmonics = Assert.Single(document.Root!.Elements("sh"));
            foreach (var attributeName in new[] { "ar", "ag", "ab", "br", "bg", "bb", "c" })
                Assert.False(string.IsNullOrWhiteSpace(savedHarmonics.Attribute(attributeName)?.Value));

            using (var root = imageXml.GetRoot())
                Assert.True(root.RemoveChildren("sh"));
            _fixture.Context.SendEvent<GraphicsEvents.BeginRenderingEvent>();
            using (var root = imageXml.GetRoot())
            using (var shElement = root.GetChild("sh"))
                Assert.False(shElement.NotNull);

            widget.Dispose();
            Assert.Empty(widget.GetResources());
        }
        finally
        {
            widget.Dispose();
            _fixture.Resources.ReleaseResource<TextureCube>(resourceName, force: true);
            _fixture.Resources.ReleaseResource<XMLFile>(resourceName, force: true);
            texture.Dispose();
            imageXml.Dispose();
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static Vector2f RenderWidgetFrame(TextureCubeInspectorWidget widget)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(360.0f, 300.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(320.0f, 260.0f));
            var isOpen = true;
            ImGui.Begin("TextureCube Inspector"u8, ref isOpen);
            windowStarted = true;

            widget.RenderContent();
            return (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static void SetUniformCube(TextureCube texture, byte red, byte green, byte blue)
    {
        using var image = new Image();
        Assert.True(image.SetSize(4, 4, 4));
        var data = new byte[4 * 4 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = red;
            data[i + 1] = green;
            data[i + 2] = blue;
            data[i + 3] = 255;
        }
        image.SetData(data);

        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces;
             face = (CubeMapFace)((int)face + 1))
        {
            Assert.True(texture.SetData(face, image));
        }
    }
}
