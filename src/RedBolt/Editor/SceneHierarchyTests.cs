namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneHierarchyTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneHierarchyTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_persists_renders_forwards_and_commits_hierarchy_actions()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneHierarchyTests", Guid.NewGuid().ToString("N"));
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
            var addon = tab.RegisterAddon(new SceneHierarchy(tab));
            Assert.Equal("SceneHierarchy", addon.GetUniqueName());
            Assert.Same(tab, addon.GetOwnerTab());

            addon.RenderContent();
            addon.RenderContextMenuItems();

            AssertIni(addon, showComponents: true, showTemporary: false, string.Empty);
            addon.ReadIniSettings("SceneHierarchy.ShowComponents=0");
            addon.ReadIniSettings("SceneHierarchy.ShowTemporary=1");
            addon.ReadIniSettings("SceneHierarchy.FilterByName=Cube");
            AssertIni(addon, showComponents: false, showTemporary: true, "Cube");

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Hierarchy.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Hierarchy.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            var parent = page.Scene.CreateChild("Parent");
            var first = parent.CreateChild("First");
            var second = parent.CreateChild("Second");
            var camera = first.CreateComponent<Camera>();
            var light = first.CreateComponent<Light>();

            RenderFrame(addon);
            Assert.False(GetField<bool>(tab, "_componentSelection"));

            addon.RenderMenu();
            Assert.Equal(1, tab.RenderMenuCount);
            addon.ApplyHotkeys(project.HotkeyManager);
            Assert.Equal(1, tab.ApplyHotkeysCount);

            project.UndoManager.NewFrame();
            Invoke(addon, "ReorderNode", second, 1, 0);
            Assert.Same(second, parent.Children[0]);
            Assert.True(project.UndoManager.Undo());
            Assert.Same(first, parent.Children[0]);
            Assert.True(project.UndoManager.Redo());
            Assert.Same(second, parent.Children[0]);

            project.UndoManager.NewFrame();
            Invoke(addon, "ReorderComponent", light, 1, 0);
            Assert.Same(light, first.Components[0]);
            Assert.True(project.UndoManager.Undo());
            Assert.Same(camera, first.Components[0]);
            Assert.True(project.UndoManager.Redo());
            Assert.Same(light, first.Components[0]);

            var oldParent = page.Scene.CreateChild("OldParent");
            var newParent = page.Scene.CreateChild("NewParent");
            var child = oldParent.CreateChild("Child");
            project.UndoManager.NewFrame();
            Invoke(addon, "ReparentNode", newParent, child);
            Assert.Same(newParent, child.Parent);
            Assert.True(project.UndoManager.Undo());
            Assert.Same(oldParent, child.Parent);
            Assert.True(project.UndoManager.Redo());
            Assert.Same(newParent, child.Parent);
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

    private static void AssertIni(
        SceneHierarchy addon, bool showComponents, bool showTemporary, string filter)
    {
        using var output = new ImGuiTextBuffer();
        addon.WriteIniSettings(output);
        Assert.Equal(
            $"SceneHierarchy.ShowComponents={(showComponents ? 1 : 0)}\n"
            + $"SceneHierarchy.ShowTemporary={(showTemporary ? 1 : 0)}\n"
            + $"SceneHierarchy.FilterByName={filter}\n",
            Encoding.UTF8.GetString(output.CStr()));
    }

    private static void RenderFrame(SceneHierarchy addon)
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
            ImGui.Begin("Scene hierarchy addon test"u8, ref open);
            windowStarted = true;
            addon.RenderContent();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static object? Invoke(object instance, string name, params object?[] arguments)
    {
        return instance.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().BaseType!
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public int RenderMenuCount { get; private set; }
        public int ApplyHotkeysCount { get; private set; }

        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);

        public override void RenderMenu()
        {
            ++RenderMenuCount;
        }

        public override void ApplyHotkeys(HotkeyManager hotkeyManager)
        {
            ++ApplyHotkeysCount;
            base.ApplyHotkeys(hotkeyManager);
        }
    }
}
