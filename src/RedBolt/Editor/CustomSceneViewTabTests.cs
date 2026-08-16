// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class CustomSceneViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public CustomSceneViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Custom_scene_tab_resets_and_renders_preview_then_disposes_owned_state()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.CustomSceneViewTabTests", Guid.NewGuid().ToString("N"));
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
        _ = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        CameraController.SettingsPage? settingsPage = null;
        TestCustomSceneViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            settingsPage = new CameraController.SettingsPage();
            project.SettingsManager.AddPage(settingsPage);
            Assert.True(_fixture.Resources.Exists<Model>("Models/Box.mdl"));
            tab = new TestCustomSceneViewTab();
            tab.OpenResource("Models/Test.mdl");

            Assert.Equal("Models/Test.mdl", tab.ActiveResourceName);
            Assert.NotNull(tab.GetScene());
            var camera = Assert.IsType<RedBolt.Graphics.Camera>(tab.GetCamera());

            tab.ResetCamera();
            var expectedDistance = new Vector3f(1.0f, 1.0f, 1.0f).Length;
            Assert.Equal(
                new Vector3f(expectedDistance, expectedDistance, expectedDistance),
                camera.Node.Position);

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(320.0f, 320.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(256.0f, 256.0f));
                var open = true;
                ImGui.Begin("Custom scene view test"u8, ref open);
                windowStarted = true;

                tab.RenderContent();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }

            Assert.Equal(1, tab.RenderTitleCount);
            Assert.Equal(1, tab.ResetCameraCount);
            Assert.Equal(new Vector3f(0.0f, 5.0f, -10.0f), camera.Node.Position);
            var renderer = Assert.IsType<RedBolt.Utility.SceneRendererToTexture>(tab.Preview.GetRenderer());
            Assert.True(renderer.Texture.Width > 0);
            Assert.True(renderer.Texture.Height > 0);

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

    private sealed class TestCustomSceneViewTab : CustomSceneViewTab
    {
        public TestCustomSceneViewTab()
            : base("Custom Scene", "935a7a90-f5e3-4dc4-bd75-3cd4bb7e2aa1",
                EditorTabFlag.None, EditorTabPlacement.DockCenter)
        {
        }

        public RedBolt.SystemUI.SceneWidget Preview => _preview;

        public int RenderTitleCount { get; private set; }

        public int ResetCameraCount { get; private set; }

        public override bool CanOpenResource(ResourceFileDescriptor descriptor) => true;

        public override bool SupportMultipleResources() => true;

        public override void ResetCamera()
        {
            ++ResetCameraCount;
            base.ResetCamera();
        }

        protected override void RenderTitle()
        {
            ++RenderTitleCount;
            base.RenderTitle();
        }

        protected override void OnResourceLoaded(string resourceName)
        {
        }

        protected override void OnResourceUnloaded(string resourceName)
        {
        }

        protected override void OnActiveResourceChanged(string oldResourceName, string newResourceName)
        {
        }

        protected override void OnResourceSaved(string resourceName)
        {
        }

        protected override void OnResourceShallowSaved(string resourceName)
        {
        }
    }
}
