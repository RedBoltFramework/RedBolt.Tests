// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
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
public sealed class ModelViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ModelViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Model_tab_loads_fits_renders_and_unloads_model()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBolt.ModelViewTabTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var model = _fixture.Resources.Get<Model>("Models/Box.mdl");
        _ = _fixture.Resources.Get<Material>("Materials/DefaultSkybox.xml");
        _ = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        CameraController.SettingsPage? settingsPage = null;
        TestModelViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            settingsPage = new CameraController.SettingsPage();
            project.SettingsManager.AddPage(settingsPage);
            tab = new TestModelViewTab();

            Assert.Equal("Model", tab.GetResourceTitle());
            Assert.False(tab.SupportMultipleResources());
            var modelDescriptor = new ResourceFileDescriptor();
            modelDescriptor.AddObjectType<Model>();
            Assert.True(tab.CanOpenResource(modelDescriptor));
            var materialDescriptor = new ResourceFileDescriptor();
            materialDescriptor.AddObjectType<Material>();
            Assert.False(tab.CanOpenResource(materialDescriptor));

            var scene = Assert.IsType<RedBolt.Scene.Scene>(tab.GetScene());
            var modelNode = Assert.IsType<RedBolt.Scene.Node>(scene.FindChild("Model"));
            var staticModel = Assert.IsType<StaticModel>(modelNode.FindComponent<StaticModel>());
            Assert.True(staticModel.GetCastShadows());
            Assert.Null(staticModel.Model);
            var camera = Assert.IsType<Camera>(tab.GetCamera());
            var cameraOperator = Assert.IsType<CameraOperator>(camera.Node.FindComponent<CameraOperator>());
            Assert.True(cameraOperator.IsBoundingBoxTrackingEnabled());
            Assert.False(cameraOperator.Enabled);

            tab.OpenResource("Models/Box.mdl");
            tab.LoadResource("Models/Box.mdl");

            Assert.Same(model, staticModel.Model);
            Assert.Equal(model.GetBoundingBox(), cameraOperator.GetBoundingBox());
            var fittedPosition = camera.Node.GetPosition();
            Assert.Equal(fittedPosition, tab.State.LastCameraPosition);

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
                ImGui.Begin("Model view test"u8, ref open);
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

            Assert.Equal(fittedPosition, camera.Node.GetPosition());
            var renderer = Assert.IsType<RedBolt.Utility.SceneRendererToTexture>(tab.Preview.GetRenderer());
            Assert.True(renderer.Texture.Width > 0);
            Assert.True(renderer.Texture.Height > 0);

            tab.UnloadResource("Models/Box.mdl");
            Assert.Null(staticModel.Model);

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

    private sealed class TestModelViewTab : ModelViewTab
    {
        public SceneWidget Preview => _preview;

        public CameraController.PageState State => _state;

        public void LoadResource(string resourceName)
        {
            OnResourceLoaded(resourceName);
        }

        public void UnloadResource(string resourceName)
        {
            OnResourceUnloaded(resourceName);
        }
    }
}
