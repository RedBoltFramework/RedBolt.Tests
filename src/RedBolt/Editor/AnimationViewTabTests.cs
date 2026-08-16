// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

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
public sealed class AnimationViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public AnimationViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Animation_tab_plays_loops_frames_and_retains_playback_on_unload()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.AnimationViewTabTests", Guid.NewGuid().ToString("N"));
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
        TestAnimationViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            settingsPage = new CameraController.SettingsPage();
            project.SettingsManager.AddPage(settingsPage);
            var animation = new Animation { Name = "Animations/Test.ani" };
            animation.SetLength(2.0f);
            Assert.True(_fixture.Resources.AddManualResource(animation));
            tab = new TestAnimationViewTab();

            Assert.Equal("Animation", tab.GetResourceTitle());
            Assert.False(tab.SupportMultipleResources());
            var animationDescriptor = new ResourceFileDescriptor();
            animationDescriptor.AddObjectType<Animation>();
            Assert.True(tab.CanOpenResource(animationDescriptor));
            var modelDescriptor = new ResourceFileDescriptor();
            modelDescriptor.AddObjectType<Model>();
            Assert.False(tab.CanOpenResource(modelDescriptor));

            var scene = Assert.IsType<RedBolt.Scene.Scene>(tab.GetScene());
            var modelNode = Assert.IsType<RedBolt.Scene.Node>(scene.FindChild("Model"));
            var animatedModel = Assert.IsType<AnimatedModel>(modelNode.FindComponent<AnimatedModel>());
            Assert.True(animatedModel.GetCastShadows());
            var controller = Assert.IsType<AnimationController>(modelNode.FindComponent<AnimationController>());
            RenderFrame(tab.RenderContent);
            Assert.Equal(0, tab.RenderTitleCount);

            tab.OpenResource("Animations/Test.ani");
            tab.LoadResource("Animations/Test.ani");

            Assert.Equal(1, controller.GetNumAnimations());
            var parameters = controller.GetAnimationParameters(0);
            Assert.Same(animation, parameters.GetAnimation());
            Assert.True(parameters.IsLooped);
            Assert.Equal(0, parameters.LayerIndex);

            SetModel(tab, model);
            animatedModel.SetModel(model);
            tab.ResetCamera();
            var bounds = model.GetBoundingBox();
            var expectedPosition = bounds.Center + bounds.Size.Length * Vector3f.One;
            Assert.Equal(expectedPosition, tab.State.LastCameraPosition);

            RenderFrame(tab.RenderContent);
            Assert.Equal(1, tab.RenderTitleCount);
            var camera = Assert.IsType<Camera>(tab.GetCamera());
            Assert.Equal(expectedPosition, camera.Node.GetPosition());
            var renderer = Assert.IsType<RedBolt.Utility.SceneRendererToTexture>(tab.Preview.GetRenderer());
            Assert.True(renderer.Texture.Width > 0);
            Assert.True(renderer.Texture.Height > 0);

            tab.LoadResource("Animations/Test.ani");
            Assert.Equal(2, controller.GetNumAnimations());
            var stoppedParameters = controller.GetAnimationParameters(0);
            Assert.True(stoppedParameters.RemoveOnZeroWeight);
            Assert.Equal(0.0f, stoppedParameters.TargetWeight);
            Assert.Same(animation, controller.GetAnimationParameters(1).GetAnimation());

            tab.UnloadResource("Animations/Test.ani");
            Assert.Equal(2, controller.GetNumAnimations());
            Assert.True(controller.IsPlaying(animation));
            RenderFrame(tab.RenderContent);
            Assert.Equal(1, tab.RenderTitleCount);

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

    private static void SetModel(AnimationViewTab tab, Model model)
    {
        typeof(AnimationViewTab)
            .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(tab, model);
    }

    private static void RenderFrame(Action render)
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
            ImGui.Begin("Animation view test"u8, ref open);
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

    private sealed class TestAnimationViewTab : AnimationViewTab
    {
        public SceneWidget Preview => _preview;

        public CameraController.PageState State => _state;

        public int RenderTitleCount { get; private set; }

        public void LoadResource(string resourceName)
        {
            OnResourceLoaded(resourceName);
        }

        public void UnloadResource(string resourceName)
        {
            OnResourceUnloaded(resourceName);
        }

        protected override void RenderTitle()
        {
            ++RenderTitleCount;
            base.RenderTitle();
        }
    }
}
