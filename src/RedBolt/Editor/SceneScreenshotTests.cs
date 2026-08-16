// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneScreenshotTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneScreenshotTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_selects_cameras_opens_popup_and_captures_offscreen_image()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneScreenshotTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TestSceneViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            tab = new TestSceneViewTab();
            var addon = tab.RegisterAddon(new SceneScreenshot(tab));
            Assert.Equal("Screenshot", addon.GetUniqueName());
            Assert.True(project.HotkeyManager.Bindings.ContainsKey("SceneScreenshot.Take"));

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Screenshot.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Screenshot.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());
            page.Scene.CreateComponent<Octree>();

            var componentNode = page.Scene.CreateChild("ComponentCamera");
            var componentCamera = componentNode.CreateComponent<Camera>();
            page.Selection.SetSelected(componentCamera, true);
            Assert.Same(componentCamera, FindCamera(addon, page.Selection));

            var directNode = page.Scene.CreateChild("DirectCamera");
            var directCamera = directNode.CreateComponent<Camera>();
            page.Selection.Clear();
            page.Selection.SetSelected(directNode, true);
            Assert.Same(directCamera, FindCamera(addon, page.Selection));

            var parentNode = page.Scene.CreateChild("Parent");
            var childNode = parentNode.CreateChild("ChildCamera");
            var childCamera = childNode.CreateComponent<Camera>();
            page.Selection.Clear();
            page.Selection.SetSelected(parentNode, true);
            Assert.Same(childCamera, FindCamera(addon, page.Selection));

            addon.TakeScreenshotWithPopup();
            RenderPopupFrame(addon, page);
            Assert.False(GetField<bool>(addon, "_openPending"));
            AssertWeakTarget(addon, "_scene", page.Scene);
            AssertWeakTarget(addon, "_sceneCamera", childCamera);
            AssertWeakTarget(addon, "_editorCamera", page.Renderer.Camera!);
            addon.ApplyHotkeys(project.HotkeyManager);

            var firstFileName = GenerateFileName(addon, new Vector2i(1920, 1080));
            Directory.CreateDirectory(Path.GetDirectoryName(firstFileName)!);
            File.WriteAllBytes(firstFileName, []);
            var secondFileName = GenerateFileName(addon, new Vector2i(1920, 1080));
            if (secondFileName != GetCollisionFileName(firstFileName))
            {
                // The wall clock crossed a second boundary between the two calls.
                firstFileName = secondFileName;
                Directory.CreateDirectory(Path.GetDirectoryName(firstFileName)!);
                File.WriteAllBytes(firstFileName, []);
                secondFileName = GenerateFileName(addon, new Vector2i(1920, 1080));
            }
            Assert.Equal(
                GetCollisionFileName(firstFileName),
                secondFileName);

            var camera = page.Renderer.Camera!;
            camera.SetDrawDebugGeometry(true);
            camera.SetAspectRatio(1.25f);
            camera.SetAutoAspectRatio(true);

            addon.TakeScreenshotNow(page.Scene, camera, new Vector2i(64, 32));
            Assert.Single(GetRenderers(addon));
            Assert.False(camera.GetDrawDebugGeometry());
            Assert.False(camera.GetAutoAspectRatio());
            Assert.True(MathDefs.Equals(2.0f, camera.GetAspectRatio()));

            RenderQueuedSurfaces();

            Assert.Empty(GetRenderers(addon));
            var screenshot = Assert.Single(Directory.GetFiles(
                Path.Combine(project.ArtifactsPath, "Screenshots"),
                "*_64x32.png",
                SearchOption.AllDirectories));
            using (var image = new Image())
            {
                Assert.True(image.LoadFile(FileIdentifier.FromUri(screenshot)));
                Assert.Equal(64, image.Width);
                Assert.Equal(32, image.Height);
            }

            Assert.False(camera.GetDrawDebugGeometry());
            Assert.False(camera.GetAutoAspectRatio());
            Assert.True(MathDefs.Equals(2.0f, camera.GetAspectRatio()));

            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
            Assert.True(camera.GetDrawDebugGeometry());
            Assert.True(camera.GetAutoAspectRatio());
            Assert.True(MathDefs.Equals(1.25f, camera.GetAspectRatio()));

            addon.TakeScreenshotDelayed(page.Scene, camera, new Vector2i(16, 8));
            Assert.Empty(GetRenderers(addon));
            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
            Assert.Single(GetRenderers(addon));
            RenderQueuedSurfaces();
            Assert.Empty(GetRenderers(addon));
            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
            Assert.Single(Directory.GetFiles(
                Path.Combine(project.ArtifactsPath, "Screenshots"),
                "*_16x8.png",
                SearchOption.AllDirectories));
            Assert.True(camera.GetDrawDebugGeometry());
            Assert.True(camera.GetAutoAspectRatio());
            Assert.True(MathDefs.Equals(1.25f, camera.GetAspectRatio()));
        }
        finally
        {
            tab?.Dispose();
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
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static void RenderPopupFrame(SceneScreenshot addon, SceneViewPage page)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Scene screenshot test"u8, ref open);
            windowStarted = true;
            addon.Render(page);
            Assert.True(addon.RenderTabContextMenu());
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private void RenderQueuedSurfaces()
    {
        Assert.True(_fixture.Graphics.BeginFrame());
        try
        {
            _fixture.Renderer.Update(0.0f);
            _fixture.Renderer.Render();
            _fixture.Graphics.ResetRenderTargets();
        }
        finally
        {
            _fixture.Graphics.EndFrame();
        }
    }

    private static Camera? FindCamera(SceneScreenshot addon, SceneSelection selection)
    {
        return (Camera?)Invoke(addon, "FindCameraInSelection", selection);
    }

    private static string GenerateFileName(SceneScreenshot addon, Vector2i resolution)
    {
        return Assert.IsType<string>(Invoke(addon, "GenerateFileName", resolution));
    }

    private static string GetCollisionFileName(string fileName)
    {
        return Path.Combine(
            Path.GetDirectoryName(fileName)!, Path.GetFileNameWithoutExtension(fileName) + "_1.png");
    }

    private static ICollection GetRenderers(SceneScreenshot addon)
    {
        var value = addon.GetType().GetField(
                "_screenshotRenderers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(addon);
        return Assert.IsAssignableFrom<ICollection>(value);
    }

    private static void AssertWeakTarget<T>(SceneScreenshot addon, string fieldName, T expected)
        where T : class
    {
        var reference = GetField<WeakReference<T>>(addon, fieldName);
        Assert.True(reference.TryGetTarget(out var actual));
        Assert.Same(expected, actual);
    }

    private static T GetField<T>(object instance, string name)
    {
        return Assert.IsType<T>(instance.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance));
    }

    private static object? Invoke(object instance, string name, params object?[] arguments)
    {
        return instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
