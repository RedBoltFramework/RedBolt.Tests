namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TransformManipulatorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TransformManipulatorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_persists_renders_rebuilds_selection_and_pushes_transform_actions()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.TransformManipulatorTests", Guid.NewGuid().ToString("N"));
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
        TransformManipulator.SettingsPage? settings = null;
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

            settings = new TransformManipulator.SettingsPage();
            project.SettingsManager.AddPage(settings);
            AssertSettingsDefaults(settings.Values);

            using (var file = new JSONFile())
            {
                Assert.True(file.SaveObjectCallback(archive =>
                {
                    using var block = archive.OpenUnorderedBlock("Settings");
                    settings.SerializeInBlock(archive);
                }));
                Assert.DoesNotContain("SnapPosition", file.ToString(), StringComparison.Ordinal);
            }

            var values = settings.Values;
            values.ScreenRotation = true;
            SetSettingsValues(settings, values);

            tab = new TestSceneViewTab();
            var addon = tab.RegisterAddon(new TransformManipulator(tab, settings));
            Assert.Equal("TransformGizmo", addon.GetUniqueName());
            Assert.Equal(0, addon.GetToolbarPriority());
            Assert.True(project.HotkeyManager.Bindings.ContainsKey("TransformGizmo.ToggleLocal"));
            Assert.True(project.HotkeyManager.Bindings.ContainsKey("TransformGizmo.Scale"));

            Assert.True(addon.IsTranslate());
            addon.ToggleSpace();
            addon.TogglePivoted();
            Assert.True(addon.IsLocal());
            Assert.True(addon.IsPivoted());
            addon.SetSelect();
            Assert.True(addon.IsSelect());
            addon.SetRotate();
            Assert.True(addon.IsRotate());
            Assert.Equal(
                TransformGizmoAxis.X | TransformGizmoAxis.Y | TransformGizmoAxis.Z
                    | TransformGizmoAxis.Screen,
                GetCurrentAxes(addon));
            addon.SetScale();
            Assert.True(addon.IsScale());
            addon.SetTranslate();

            AssertIni(addon, isLocal: true, isPivoted: true, TransformGizmoOperation.Translate);
            addon.ReadIniSettings("TransformGizmo.IsLocal=0");
            addon.ReadIniSettings("TransformGizmo.IsPivoted=0");
            addon.ReadIniSettings("TransformGizmo.Operation=999");
            Assert.False(addon.IsLocal());
            Assert.False(addon.IsPivoted());
            Assert.True(addon.IsScale());
            addon.ReadIniSettings("TransformGizmo.Operation=-5");
            Assert.True(addon.IsSelect());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Transform.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Transform.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());
            var first = page.Scene.CreateChild("First");
            var second = page.Scene.CreateChild("Second");
            page.Selection.SetSelected(first, true);

            RenderFrame(addon, settings, page);
            var firstGizmo = GetNodeGizmo(addon);
            Assert.NotNull(firstGizmo);

            page.Selection.SetSelected(second, true);
            RenderFrame(addon, settings, page);
            var secondGizmo = GetNodeGizmo(addon);
            Assert.NotNull(secondGizmo);
            Assert.NotSame(firstGizmo, secondGizmo);

            var oldTransform = first.GetTransform();
            first.Position = new Vector3f(3.0f, 4.0f, 5.0f);
            project.UndoManager.NewFrame();
            Invoke(addon, "OnNodeTransformChanged", first, oldTransform);
            Assert.True(project.UndoManager.Undo());
            Assert.Equal(oldTransform.Position, first.Position);
            Assert.True(project.UndoManager.Redo());
            Assert.Equal(new Vector3f(3.0f, 4.0f, 5.0f), first.Position);

            addon.ApplyHotkeys(project.HotkeyManager);
        }
        finally
        {
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

    [Fact]
    public void Settings_return_operation_specific_snap_values()
    {
        var settings = new TransformManipulator.Settings();

        AssertSettingsDefaults(settings);
        Assert.Equal(Vector3f.Zero, settings.GetSnapValue(TransformGizmoOperation.None));
        Assert.Equal(settings.SnapPosition, settings.GetSnapValue(TransformGizmoOperation.Translate));
        Assert.Equal(Vector3f.One * settings.SnapRotation,
            settings.GetSnapValue(TransformGizmoOperation.Rotate));
        Assert.Equal(Vector3f.One * settings.SnapScale,
            settings.GetSnapValue(TransformGizmoOperation.Scale));
    }

    private static void RenderFrame(
        TransformManipulator addon, TransformManipulator.SettingsPage settings, SceneViewPage page)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            ImGui.BeginFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(720.0f, 520.0f));
            var open = true;
            ImGui.Begin("Transform manipulator test"u8, ref open);
            windowStarted = true;
            settings.RenderSettings();
            Assert.True(addon.RenderToolbar());
            Assert.True(addon.RenderTabContextMenu());

            page.ContentArea = new Rect(
                ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
            var mouseConsumed = false;
            addon.SetSelect();
            addon.ProcessInput(page, ref mouseConsumed);
            Assert.False(mouseConsumed);
            addon.SetTranslate();
            addon.ProcessInput(page, ref mouseConsumed);
            Assert.False(mouseConsumed);
            addon.Render(page);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static void AssertIni(TransformManipulator addon, bool isLocal, bool isPivoted,
        TransformGizmoOperation operation)
    {
        using var output = new ImGuiTextBuffer();
        addon.WriteIniSettings(output);
        Assert.Equal(
            $"TransformGizmo.IsLocal={(isLocal ? 1 : 0)}\n"
            + $"TransformGizmo.IsPivoted={(isPivoted ? 1 : 0)}\n"
            + $"TransformGizmo.Operation={(int)operation}\n",
            Encoding.UTF8.GetString(output.CStr()));
    }

    private static void AssertSettingsDefaults(TransformManipulator.Settings settings)
    {
        Assert.Equal("Editor.Scene:TransformGizmo", settings.UniqueName);
        Assert.Equal(0.5f * Vector3f.One, settings.SnapPosition);
        Assert.Equal(5.0f, settings.SnapRotation);
        Assert.Equal(0.1f, settings.SnapScale);
        Assert.False(settings.ScreenRotation);
    }

    private static void SetSettingsValues(
        TransformManipulator.SettingsPage page, TransformManipulator.Settings values)
    {
        typeof(SimpleSettingsPage<TransformManipulator.Settings>).GetField(
                "_values", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, values);
    }

    private static TransformGizmoAxis GetCurrentAxes(TransformManipulator addon)
    {
        return Assert.IsType<TransformGizmoAxis>(Invoke(addon, "GetCurrentAxes"));
    }

    private static TransformNodesGizmo? GetNodeGizmo(TransformManipulator addon)
    {
        return (TransformNodesGizmo?)typeof(TransformManipulator).GetField(
                "_transformNodesGizmo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(addon);
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
