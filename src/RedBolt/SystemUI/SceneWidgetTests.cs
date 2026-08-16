// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Default_scene_renders_to_texture_and_supports_camera_and_skybox_controls()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        var widget = new SceneWidget();
        try
        {
            Assert.Null(widget.GetScene());
            Assert.Null(widget.GetRenderer());
            Assert.Null(widget.GetCamera());
            widget.RenderContent();
            widget.SetSkyboxTexture(null);
            widget.LookAt(new BoundingBox(-1.0f, 1.0f));

            var scene = widget.CreateDefaultScene();
            Assert.Same(scene, widget.GetScene());
            Assert.NotNull(scene.GetComponent<Octree>());
            var zone = Assert.IsType<Zone>(scene.GetComponent<Zone>());
            var skybox = Assert.IsType<Skybox>(scene.GetComponent<Skybox>());
            Assert.NotNull(skybox.Model);
            Assert.NotNull(skybox.GetMaterial());
            Assert.NotNull(zone.GetZoneTexture());
            var lightPivot = Assert.IsType<RedBolt.Scene.Node>(scene.GetChild("DirectionalLightPivot"));
            var lightNode = Assert.IsType<RedBolt.Scene.Node>(lightPivot.GetChild("DirectionalLight"));
            var light = Assert.IsType<Light>(lightNode.GetComponent<Light>());
            Assert.Equal(LightType.Directional, light.GetLightType());
            Assert.True(light.GetCastShadows());

            var renderer = Assert.IsType<RedBolt.Utility.SceneRendererToTexture>(widget.GetRenderer());
            Assert.Same(renderer, widget.GetRenderer());
            Assert.True(renderer.IsActive);
            var camera = Assert.IsType<Camera>(widget.GetCamera());
            widget.LookAt(new BoundingBox(-1.0f, 1.0f));
            var expectedDistance = new Vector3f(2.0f, 2.0f, 2.0f).Length;
            Assert.Equal(
                new Vector3f(expectedDistance, expectedDistance, expectedDistance),
                camera.Node.GetPosition());

            var texture = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
            widget.SetSkyboxTexture(texture);
            Assert.Same(texture, zone.GetZoneTexture());
            var skyboxMaterial = Assert.IsType<Material>(skybox.GetMaterial());
            Assert.Same(texture, skyboxMaterial.GetTexture(ShaderResources.Albedo));
            Assert.Equal(CullMode.None, skyboxMaterial.GetCullMode());

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(128.0f, 128.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(96.0f, 96.0f));
                var isOpen = true;
                ImGui.Begin("Scene Widget"u8, ref isOpen);
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

            var debugRenderer = Assert.IsType<DebugRenderer>(scene.GetComponent<DebugRenderer>());
            Assert.True(debugRenderer.IsTemporary);
            Assert.True(debugRenderer.GetLineAntiAlias());
            Assert.True(renderer.Texture.Width > 0);
            Assert.True(renderer.Texture.Height > 0);
            Assert.NotNull(camera.Node.GetComponent<MoveAndOrbitComponent>());

            widget.Dispose();
            Assert.Null(widget.GetScene());
            Assert.Null(widget.GetRenderer());
        }
        finally
        {
            widget.Dispose();
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
        }
    }
}
