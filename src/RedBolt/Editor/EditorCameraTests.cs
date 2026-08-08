namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class EditorCameraTests
{
    private readonly GraphicsEngineFixture _fixture;

    public EditorCameraTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_processes_camera_focus_serialization_and_event_lifetime()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorCameraTests", Guid.NewGuid().ToString("N"));
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
        CameraController.SettingsPage? settings = null;
        TestSceneViewTab? tab = null;
        EditorCamera? addon = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            settings = new CameraController.SettingsPage();
            project.SettingsManager.AddPage(settings);
            tab = new TestSceneViewTab();
            addon = new EditorCamera(tab, settings);

            Assert.Equal("Camera", addon.GetUniqueName());
            Assert.Equal(int.MaxValue, addon.GetInputPriority());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Camera.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Camera.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            var mouseConsumed = true;
            RenderFrame(tab, () => addon.ProcessInput(page, ref mouseConsumed));
            Assert.True(mouseConsumed);
            var camera = page.Renderer.Camera!;
            Assert.True(camera.Node.Position.ApproximatelyEquivalent(
                new Vector3f(0.0f, 5.0f, -10.0f)));

            var stateObject = page.GetAddonData(addon);
            var initialState = Assert.IsType<CameraController.PageState>(stateObject);
            Assert.True(initialState.LastCameraPosition.ApproximatelyEquivalent(camera.Node.Position));
            Assert.True(initialState.LastCameraRotation.Equivalent(camera.Node.Rotation));

            var target = page.Scene.CreateChild("Target");
            target.SetWorldPosition(new Vector3f(4.0f, 3.0f, 2.0f));
            page.Selection.SetSelected(target, true);
            var expectedPosition = target.WorldPosition
                - camera.Node.Rotation
                * new Vector3f(0.0f, 0.0f, settings.Values.FocusDistance);
            var expectedOffset = expectedPosition - initialState.LastCameraPosition;

            tab.FocusSelection(page.Selection);
            stateObject = page.GetAddonData(addon);
            var focusedState = Assert.IsType<CameraController.PageState>(stateObject);
            Assert.True(focusedState.PendingOffset.ApproximatelyEquivalent(expectedOffset));

            using (var file = new JSONFile())
            {
                object? serializedState = stateObject;
                Assert.True(file.SaveObjectCallback(archive =>
                    addon.SerializePageState(archive, "Camera", ref serializedState)));
                Assert.Contains("Position", file.ToString(), StringComparison.Ordinal);

                object? loadedState = null;
                Assert.True(file.LoadObjectCallback(archive =>
                    addon.SerializePageState(archive, "Camera", ref loadedState)));
                var loaded = Assert.IsType<CameraController.PageState>(loadedState);
                Assert.True(loaded.LastCameraPosition.ApproximatelyEquivalent(
                    focusedState.LastCameraPosition, MathDefs.LargeEpsilon));
                Assert.True(loaded.LastCameraRotation.Equivalent(focusedState.LastCameraRotation));
                Assert.Equal(loaded.LastCameraRotation.YawAngle, loaded.Yaw);
                Assert.Equal(loaded.LastCameraRotation.PitchAngle, loaded.Pitch);
            }

            var disposedAddon = addon;
            addon.Dispose();
            addon = null;
            target.SetWorldPosition(new Vector3f(-8.0f, 7.0f, 6.0f));
            tab.FocusSelection(page.Selection);
            stateObject = page.GetAddonData(disposedAddon);
            var stateAfterDispose = Assert.IsType<CameraController.PageState>(stateObject);
            Assert.True(stateAfterDispose.PendingOffset.ApproximatelyEquivalent(
                focusedState.PendingOffset));
        }
        finally
        {
            addon?.Dispose();
            tab?.Dispose();
            settings?.Dispose();
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

    private static void RenderFrame(SceneViewTab tab, Action afterContent)
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
            ImGui.SetNextWindowSize(new Vector2f(720.0f, 520.0f));
            var open = true;
            ImGui.Begin("Editor camera test"u8, ref open);
            windowStarted = true;
            tab.RenderContent();
            afterContent();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
