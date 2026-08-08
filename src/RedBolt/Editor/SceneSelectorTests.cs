namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

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
public sealed class SceneSelectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneSelectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_queries_geometry_promotes_temporary_nodes_and_applies_selection_modes()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneSelectorTests", Guid.NewGuid().ToString("N"));
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
            var selector = new SceneSelector(tab);
            Assert.Equal("Selector", selector.GetUniqueName());
            Assert.Equal(int.MinValue, selector.GetInputPriority());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Selector.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Selector.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            var octree = resource.Scene.CreateComponent<Octree>();
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            var selectedParent = page.Scene.CreateChild("SelectedParent");
            var temporary = selectedParent.CreateChild("TemporaryGeometry");
            temporary.SetTemporary(true);
            var geometry = temporary.CreateComponent<CustomGeometry>();
            geometry.BeginGeometry(0u, PrimitiveType.TriangleList);
            geometry.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
            geometry.DefineVertex(new Vector3f(1.0f, 0.0f, 0.0f));
            geometry.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
            geometry.Commit();
            octree.Update(new FrameInfo());

            var ray = new Ray(
                new Vector3f(0.25f, 0.25f, 1.0f),
                new Vector3f(0.0f, 0.0f, -1.0f));
            var queriedNode = InvokePrivate<Node?>(
                selector, "QuerySelectedNode", page.Scene, ray);
            Assert.Same(selectedParent, queriedNode);

            var first = page.Scene.CreateChild("First");
            var second = page.Scene.CreateChild("Second");
            var component = first.CreateComponent<Camera>();
            page.Selection.SetSelected(component, true);
            InvokePrivate<object?>(
                selector, "SelectNode", page.Selection, second, false, false);
            Assert.Empty(page.Selection.Components);
            Assert.False(page.Selection.IsSelected(first));
            Assert.True(page.Selection.IsSelected(second));

            InvokePrivate<object?>(
                selector, "SelectNode", page.Selection, first, false, true);
            Assert.True(page.Selection.IsSelected(first));
            Assert.True(page.Selection.IsSelected(second));
            InvokePrivate<object?>(
                selector, "SelectNode", page.Selection, second, true, false);
            Assert.True(page.Selection.IsSelected(first));
            Assert.False(page.Selection.IsSelected(second));
            InvokePrivate<object?>(
                selector, "SelectNode", page.Selection, second, true, false);
            Assert.True(page.Selection.IsSelected(second));

            var mouseConsumed = true;
            selector.ProcessInput(page, ref mouseConsumed);
            Assert.True(mouseConsumed);
            Assert.True(page.Selection.IsSelected(first));
            Assert.True(page.Selection.IsSelected(second));

            InvokePrivate<object?>(
                selector, "SelectNode", page.Selection, null, false, false);
            Assert.True(page.Selection.IsEmpty);

            selector.Dispose();
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

    private static T InvokePrivate<T>(object instance, string name, params object?[] arguments)
    {
        return (T)instance.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments)!;
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
