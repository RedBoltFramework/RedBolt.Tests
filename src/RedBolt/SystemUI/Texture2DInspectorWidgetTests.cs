// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Texture2DInspectorWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Texture2DInspectorWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_properties_map_to_resource_and_render_read_only()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        using var texture = new Texture2D { Name = "Textures/Test.png" };
        Assert.True(texture.SetSize(8, 8, TextureFormat.TexFormatRgba8Unorm));
        var widget = new Texture2DInspectorWidget([texture]);
        try
        {
            Assert.False(widget.CanSave());
            var properties = (ResourceInspectorWidget.PropertyDesc[])typeof(Texture2DInspectorWidget)
                .GetField("_properties", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            Assert.Equal(
                ["SRGB", "Linear", "Filter Mode", "U Address Mode", "V Address Mode"],
                properties.Select(static property => property.Name));
            Assert.Equal(
                ["NEAREST", "BILINEAR", "TRILINEAR", "ANISOTROPIC", "NEAREST_ANISOTROPIC", "DEFAULT"],
                properties[2].Options.IntToString);
            Assert.Equal(
                ["WRAP", "MIRROR", "CLAMP", "BORDER"],
                properties[3].Options.IntToString);
            Assert.Same(properties[3].Options.IntToString, properties[4].Options.IntToString);

            using (var value = new Variant(true))
            {
                properties[0].Setter(texture, in value);
                properties[1].Setter(texture, in value);
            }
            using (var value = new Variant((int)TextureFilterMode.Anisotropic))
                properties[2].Setter(texture, in value);
            using (var value = new Variant((int)TextureAddressMode.Mirror))
                properties[3].Setter(texture, in value);
            using (var value = new Variant((int)TextureAddressMode.Clamp))
                properties[4].Setter(texture, in value);

            Assert.True(texture.SetSize(8, 8, TextureFormat.TexFormatRgba8Unorm));
            Assert.True(texture.GetSRGB());
            Assert.True(texture.GetLinear());
            Assert.Equal(TextureFilterMode.Anisotropic, texture.GetFilterMode());
            Assert.Equal(TextureAddressMode.Mirror, texture.GetAddressMode(TextureCoordinate.U));
            Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));

            using (var value = properties[0].Getter(texture))
                Assert.True(value.GetBool());
            using (var value = properties[1].Getter(texture))
                Assert.True(value.GetBool());
            using (var value = properties[2].Getter(texture))
                Assert.Equal((int)TextureFilterMode.Anisotropic, value.GetInt());
            using (var value = properties[3].Getter(texture))
                Assert.Equal((int)TextureAddressMode.Mirror, value.GetInt());
            using (var value = properties[4].Getter(texture))
                Assert.Equal((int)TextureAddressMode.Clamp, value.GetInt());

            var editBeginCount = 0;
            var editEndCount = 0;
            widget.OnEditBegin += () => ++editBeginCount;
            widget.OnEditEnd += () => ++editEndCount;
            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(320.0f, 240.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(280.0f, 220.0f));
                var isOpen = true;
                ImGui.Begin("Texture2D Inspector"u8, ref isOpen);
                windowStarted = true;

                widget.RenderContent();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }
            Assert.Equal(0, editBeginCount);
            Assert.Equal(0, editEndCount);

            widget.Dispose();
            Assert.Empty(widget.GetResources());
        }
        finally
        {
            widget.Dispose();
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
        }
    }
}
