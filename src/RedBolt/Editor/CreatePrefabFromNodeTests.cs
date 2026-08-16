// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
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
public sealed class CreatePrefabFromNodeTests
{
    private readonly GraphicsEngineFixture _fixture;

    public CreatePrefabFromNodeTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Factory_builds_prefabs_selects_names_and_replaces_nodes_with_undoable_references()
    {
        WithSceneView((project, tab, page, dataPath) =>
        {
            using var factory = new PrefabFromNodeFactory();
            Assert.Equal(0, factory.Group);
            Assert.Equal("Prefab from Node", factory.Title);
            Assert.Equal("(none)", factory.DefaultFileName);
            Assert.False(factory.IsFileNameEditable);

            var basePrefab = GetField<PrefabResource>(factory, "_prefab").GetScenePrefab();
            Assert.NotEmpty(basePrefab.Components);
            Assert.Equal(3, basePrefab.Children.Count);
            Assert.Equal(string.Empty, basePrefab.Children[0].GetNodeName());
            Assert.Equal("Default Skybox", basePrefab.Children[1].GetNodeName());
            Assert.Equal("Default Zone", basePrefab.Children[2].GetNodeName());

            var parent = page.Scene.CreateChild("Parent");
            var node = parent.CreateChild("Hero");
            var nodeId = node.Id;
            node.SetEnabled(false);
            node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
            node.SetRotation(new Quaternion(10.0f, 20.0f, 30.0f));
            node.SetScale(2.0f);
            node.CreateComponent<Camera>();
            node.CreateChild("Nested");

            factory.Setup(tab, [new WeakReference<Node>(node)]);
            Assert.Equal("Hero.prefab", factory.DefaultFileName);
            Assert.True(factory.IsFileNameEditable);

            using (var generated = Invoke<NodePrefab>(
                       factory, "CreatePrefabFromNode", node))
            {
                var discarded = new HashSet<StringHash>
                {
                    new("Is Enabled"), new("Position"), new("Rotation"), new("Name"),
                };
                Assert.DoesNotContain(
                    generated.Node.Attributes, attribute => discarded.Contains(attribute.NameHash));
                Assert.Contains(
                    generated.Node.Attributes,
                    attribute => attribute.NameHash == new StringHash("Scale"));
                Assert.Single(generated.Components);
                Assert.Equal("Nested", Assert.Single(generated.Children).GetNodeName());
            }

            var prefabDirectory = Path.Combine(dataPath, "Prefabs");
            Directory.CreateDirectory(prefabDirectory);
            factory.Open(prefabDirectory, "Prefabs");
            project.UndoManager.NewFrame();
            factory.CommitAndClose();

            var prefabFileName = Path.Combine(prefabDirectory, "Hero.prefab");
            Assert.True(File.Exists(prefabFileName));
            using (var savedPrefab = new PrefabResource())
            {
                Assert.True(savedPrefab.LoadFile(new FileIdentifier(prefabFileName)));
                var savedNode = savedPrefab.GetNodePrefab();
                Assert.DoesNotContain(
                    savedNode.Node.Attributes,
                    attribute => attribute.NameHash == new StringHash("Name")
                        || attribute.NameHash == new StringHash("Position")
                        || attribute.NameHash == new StringHash("Rotation")
                        || attribute.NameHash == new StringHash("Is Enabled"));
                Assert.Single(savedNode.Components);
                Assert.Equal("Nested", Assert.Single(savedNode.Children).GetNodeName());
            }

            var referenceNode = Assert.IsType<Node>(page.Scene.GetNode(nodeId));
            Assert.NotSame(node, referenceNode);
            Assert.Equal("Hero", referenceNode.Name);
            var reference = Assert.IsType<PrefabReference>(
                referenceNode.GetComponent<PrefabReference>());
            Assert.Equal("Prefabs/Hero.prefab", reference.GetPrefabAttr().Name);
            Assert.NotNull(reference.GetPrefab());

            Assert.True(project.UndoManager.Undo());
            var restoredNode = Assert.IsType<Node>(page.Scene.GetNode(nodeId));
            Assert.Equal("Hero", restoredNode.Name);
            Assert.NotNull(restoredNode.GetComponent<Camera>());
            Assert.NotNull(restoredNode.GetChild("Nested"));
            Assert.Null(restoredNode.GetComponent<PrefabReference>());

            Assert.True(project.UndoManager.Redo());
            referenceNode = Assert.IsType<Node>(page.Scene.GetNode(nodeId));
            Assert.NotNull(referenceNode.GetComponent<PrefabReference>());

            var first = page.Scene.CreateChild("Bad/Name");
            var second = page.Scene.CreateChild("Bad/Name");
            factory.Setup(tab,
            [
                new WeakReference<Node>(first),
                new WeakReference<Node>(second),
            ]);
            Assert.Equal("(automatic)", factory.DefaultFileName);
            Assert.False(factory.IsFileNameEditable);
            SetField(factory, "_replaceWithReference", false);
            factory.Open(prefabDirectory, "Prefabs");
            var (canCommit, shouldCommit) = RenderFactory(factory);
            Assert.True(canCommit);
            Assert.False(shouldCommit);
            factory.CommitAndClose();
            Assert.True(File.Exists(Path.Combine(prefabDirectory, "Bad_Name.prefab")));
            Assert.True(File.Exists(Path.Combine(prefabDirectory, "Bad_Name_1.prefab")));
            Assert.Same(first, page.Scene.GetNode(first.Id));
            Assert.Same(second, page.Scene.GetNode(second.Id));
        });
    }

    [NativeBindingFact]
    public void Addon_filters_nested_selection_and_queues_factory_request()
    {
        WithSceneView((project, tab, page, _) =>
        {
            var addon = tab.RegisterAddon(new CreatePrefabFromNode(tab));
            Assert.Equal("CreatePrefabFrom", addon.GetUniqueName());

            var parent = page.Scene.CreateChild("Parent");
            var child = parent.CreateChild("Child");
            var sibling = page.Scene.CreateChild("Sibling");
            page.Selection.SetSelected(parent, true);
            page.Selection.SetSelected(child, true);
            page.Selection.SetSelected(sibling, true);

            var pendingRequests = GetField<IList>(project, "_pendingRequests");
            var originalRequestCount = pendingRequests.Count;
            Invoke<object?>(addon, "CreatePrefabs", page.Selection);
            Assert.Equal(originalRequestCount + 1, pendingRequests.Count);

            var factory = GetField<PrefabFromNodeFactory>(addon, "_factory");
            var nodes = GetField<IList>(factory, "_nodes");
            Assert.Equal(2, nodes.Count);
            var names = nodes.Cast<WeakReference<Node>>()
                .Select(reference =>
                {
                    reference.TryGetTarget(out var selectedNode);
                    return selectedNode!.Name;
                })
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(["Parent", "Sibling"], names);

            var pending = pendingRequests[^1]!;
            var request = Assert.IsType<CreateResourceRequest>(
                pending.GetType().GetField("Request")!.GetValue(pending));
            Assert.Same(factory, request.Factory);
            var sender = Assert.IsType<WeakReference<EditorTab>>(
                pending.GetType().GetField("Sender")!.GetValue(pending));
            Assert.True(sender.TryGetTarget(out var senderTab));
            Assert.Same(tab, senderTab);
        });
    }

    private void WithSceneView(
        Action<Project, TestSceneViewTab, SceneViewPage, string> action)
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.CreatePrefabFromNodeTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        _fixture.Resources.Get<Model>("Models/Box.mdl");
        _fixture.Resources.Get<Material>("Materials/DefaultSkybox.xml");
        _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
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
            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/PrefabSource.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "PrefabSource.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            action(project, tab, page, dataPath);
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

    private static T Invoke<T>(object instance, string name, params object?[] arguments)
    {
        return (T)instance.GetType()
            .GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments)!;
    }

    private static (bool CanCommit, bool ShouldCommit) RenderFactory(
        PrefabFromNodeFactory factory)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var canCommit = false;
        var shouldCommit = false;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Prefab from node factory test"u8, ref open);
            windowStarted = true;
            factory.Render(
                static (_, _) => (false, "Name unavailable"),
                ref canCommit, ref shouldCommit);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
        return (canCommit, shouldCommit);
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
