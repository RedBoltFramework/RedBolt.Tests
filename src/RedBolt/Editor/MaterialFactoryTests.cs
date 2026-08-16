// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;
using SystemFile = global::System.IO.File;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class MaterialFactoryTests
{
    private readonly GraphicsEngineFixture _fixture;

    public MaterialFactoryTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Factory_renders_selection_and_saves_native_material_variants()
    {
        _fixture.ResetGraphics();
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBolt.MaterialFactoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var imguiContext = IntPtr.Zero;
        MaterialFactory? factory = null;
        try
        {
            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            factory = new MaterialFactory();
            Assert.Equal(0, factory.Group);
            Assert.Equal("Material", factory.Title);
            Assert.Equal("Material.material", factory.DefaultFileName);

            SetOptions(factory, 3, true, true, true);
            factory.Open(Path.Combine(directory, "Render"), string.Empty);
            Directory.CreateDirectory(Path.Combine(directory, "Render"));
            RenderFactory(factory);
            Assert.Equal(3, GetField<int>(factory, "_type"));

            var variants = new[]
            {
                new Variant(0, true, true, true, "Techniques/LitOpaqueNormalMap.xml", "PBR "),
                new Variant(1, true, true, true, "Techniques/LitOpaqueNormalMap.xml", "PBR ALPHAMASK "),
                new Variant(2, true, false, true, "Techniques/LitTransparentNormalMap.xml", string.Empty),
                new Variant(3, true, false, true, "Techniques/LitTransparentFadeNormalMap.xml", string.Empty),
                new Variant(0, true, false, false, "Techniques/LitOpaque.xml", string.Empty),
                new Variant(2, true, false, false, "Techniques/LitTransparent.xml", string.Empty),
                new Variant(3, true, false, false, "Techniques/LitTransparentFade.xml", string.Empty),
                new Variant(0, false, true, true, "Techniques/UnlitOpaque.xml", string.Empty),
                new Variant(1, false, true, true, "Techniques/UnlitOpaque.xml", "ALPHAMASK "),
                new Variant(2, false, true, true, "Techniques/UnlitTransparent.xml", string.Empty)
            };

            for (var i = 0; i < variants.Length; ++i)
            {
                var variant = variants[i];
                var variantDirectory = Path.Combine(directory, i.ToString());
                Directory.CreateDirectory(variantDirectory);
                SetOptions(factory, variant.Type, variant.Lit, variant.Pbr, variant.Normal);
                factory.Open(variantDirectory, string.Empty);
                factory.CommitAndClose();

                var fileName = Path.Combine(variantDirectory, factory.DefaultFileName);
                Assert.True(SystemFile.Exists(fileName));
                using var material = new Material();
                Assert.True(material.LoadFile(new FileIdentifier(fileName)));
                Assert.Equal(variant.TechniqueName, material.GetTechniqueEntry(0).Original?.Name);
                Assert.Equal(variant.ShaderDefines, material.GetVertexShaderDefines());
                Assert.Equal(variant.ShaderDefines, material.GetPixelShaderDefines());
            }
        }
        finally
        {
            factory?.Dispose();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            Directory.Delete(directory, true);
        }
    }

    private static void RenderFactory(MaterialFactory factory)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Material factory test"u8, ref open);
            windowStarted = true;
            var canCommit = false;
            var shouldCommit = true;
            factory.Render(static (_, _) => (true, string.Empty), ref canCommit, ref shouldCommit);
            Assert.True(canCommit);
            Assert.False(shouldCommit);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static void SetOptions(MaterialFactory factory, int type, bool lit, bool pbr, bool normal)
    {
        SetField(factory, "_type", type);
        SetField(factory, "_lit", lit);
        SetField(factory, "_pbr", pbr);
        SetField(factory, "_normal", normal);
    }

    private static void SetField<T>(MaterialFactory factory, string name, T value)
    {
        typeof(MaterialFactory).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(factory, value);
    }

    private static T GetField<T>(MaterialFactory factory, string name)
    {
        return (T)typeof(MaterialFactory).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(factory)!;
    }

    private readonly record struct Variant(
        int Type, bool Lit, bool Pbr, bool Normal, string TechniqueName, string ShaderDefines);
}
