// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TextureCubeViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TextureCubeViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Cubemap_tab_loads_renders_and_retains_preview_when_resource_is_unloaded()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.TextureCubeViewTabTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        _ = _fixture.Resources.Get<Model>("Models/Box.mdl");
        _ = _fixture.Resources.Get<Material>("Materials/DefaultSkybox.xml");
        var texture = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
        _ = _fixture.Resources.Get<Technique>("Techniques/DiffSkybox.xml");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        CameraController.SettingsPage? settingsPage = null;
        TestTextureCubeViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            settingsPage = new CameraController.SettingsPage();
            project.SettingsManager.AddPage(settingsPage);
            tab = new TestTextureCubeViewTab();

            Assert.Equal("Cubemap", tab.GetResourceTitle());
            Assert.False(tab.SupportMultipleResources());
            var cubemapDescriptor = new ResourceFileDescriptor();
            cubemapDescriptor.AddObjectType<TextureCube>();
            Assert.True(tab.CanOpenResource(cubemapDescriptor));
            var textureDescriptor = new ResourceFileDescriptor();
            textureDescriptor.AddObjectType<Texture2D>();
            Assert.False(tab.CanOpenResource(textureDescriptor));

            tab.OpenResource("Textures/DefaultSkybox.xml");
            tab.LoadResource("Textures/DefaultSkybox.xml");

            var scene = Assert.IsType<RedBolt.Scene.Scene>(tab.GetScene());
            var zone = Assert.IsType<Zone>(scene.FindComponent<Zone>());
            var skybox = Assert.IsType<Skybox>(scene.FindComponent<Skybox>());
            Assert.Same(texture, zone.GetZoneTexture());
            var material = Assert.IsType<Material>(skybox.GetMaterial());
            Assert.Same(texture, material.GetTexture(ShaderResources.Albedo));
            Assert.Equal(CullMode.None, material.GetCullMode());

            RenderFrame("Cubemap view", tab.RenderContent);
            var renderer = Assert.IsType<RedBolt.Utility.SceneRendererToTexture>(tab.Preview.GetRenderer());
            Assert.True(renderer.Texture.Width > 0);
            Assert.True(renderer.Texture.Height > 0);
            Assert.Equal(1, tab.RenderTitleCount);

            tab.UnloadResource("Textures/DefaultSkybox.xml");
            Assert.Same(texture, zone.GetZoneTexture());
            Assert.Same(texture, material.GetTexture(ShaderResources.Albedo));

            RenderFrame("Unloaded cubemap view", tab.RenderContent);
            Assert.Equal(1, tab.RenderTitleCount);

            RenderFrame("Explicit cubemap view", () => tab.RenderTexture(null));
            Assert.Equal(2, tab.RenderTitleCount);

            tab.Dispose();
            Assert.Null(tab.GetScene());
            Assert.Null(tab.GetCamera());
            tab = null;
        }
        finally
        {
            tab?.Dispose();
            settingsPage?.Dispose();
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static void RenderFrame(string title, Action render)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(320.0f, 320.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(256.0f, 256.0f));
            var open = true;
            using var titleText = new CString(title);
            ImGui.Begin(titleText.ContentBytes, ref open);
            windowStarted = true;
            render();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private sealed class TestTextureCubeViewTab : TextureCubeViewTab
    {
        public SceneWidget Preview => _preview;

        public int RenderTitleCount { get; private set; }

        public void LoadResource(string resourceName)
        {
            OnResourceLoaded(resourceName);
        }

        public void UnloadResource(string resourceName)
        {
            OnResourceUnloaded(resourceName);
        }

        public void RenderTexture(TextureCube? texture)
        {
            RenderTextureCube(texture);
        }

        protected override void RenderTitle()
        {
            ++RenderTitleCount;
            base.RenderTitle();
        }
    }
}
