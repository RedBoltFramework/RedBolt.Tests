namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Tab_orders_addons_runs_scene_commands_and_renders()
    {
        WithSceneView((project, tab, resource) =>
        {
            var calls = new List<string>();
            var zulu = tab.RegisterAddon(new TestAddon(
                tab, "Zulu", inputPriority: 0, toolbarPriority: 0, calls));
            var alpha = tab.RegisterAddon(new TestAddon(
                tab, "Alpha", inputPriority: -1, toolbarPriority: -2, calls));
            var middle = tab.RegisterAddon(new TestAddon(
                tab, "Middle", inputPriority: 10, toolbarPriority: 5, calls)
            {
                ConsumesMouse = true,
            });

            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            Assert.True(_fixture.Context.IsReflected<SceneResourceForEditor>());
            Assert.Equal("Scene", tab.Title);
            Assert.Equal("Scene###9f4f7432-dd60-4c83-aecd-2f6cf69d3549", tab.UniqueId);
            Assert.Equal(
                EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault | EditorTabFlag.FocusOnStart,
                tab.Flags);
            Assert.Equal(EditorTabPlacement.DockCenter, tab.Placement);
            Assert.Equal("Scene", tab.GetResourceTitle());
            Assert.True(tab.SupportMultipleResources());
            Assert.True(tab.IsUndoSupported());
            Assert.Equal(["Alpha", "Middle", "Zulu"],
                tab.GetAddonsByName().Select(static addon => addon.GetUniqueName()));
            Assert.Equal(["initialize:Zulu", "initialize:Alpha", "initialize:Middle"], calls);

            var sceneDescriptor = new ResourceFileDescriptor();
            sceneDescriptor.AddObjectType<Scene>();
            Assert.True(tab.CanOpenResource(sceneDescriptor));
            var prefabDescriptor = new ResourceFileDescriptor();
            prefabDescriptor.AddObjectType<PrefabResource>();
            Assert.True(tab.CanOpenResource(prefabDescriptor));
            var materialDescriptor = new ResourceFileDescriptor();
            materialDescriptor.AddObjectType<Material>();
            Assert.False(tab.CanOpenResource(materialDescriptor));

            calls.Clear();
            RenderFrame(tab);
            Assert.Equal(
                [
                    "toolbar:Alpha", "toolbar:Zulu", "toolbar:Middle",
                    "input:Middle:False", "input:Zulu:True", "input:Alpha:True",
                    "render:Zulu", "render:Alpha", "render:Middle",
                ],
                calls);
            Assert.True(page.Renderer.Texture.Width > 0);
            Assert.True(page.Renderer.Texture.Height > 0);
            Assert.True(page.ContentArea.Size.X > 0.0f);
            Assert.True(page.Scene.GetComponent<DebugRenderer>()!.IsTemporary);

            tab.ApplyHotkeys(project.HotkeyManager);
            tab.ReadIniSettings("AddonSetting=1");
            using (var output = new ImGuiTextBuffer())
                tab.WriteIniSettings(output);
            Assert.All(new[] { zulu, alpha, middle }, static addon =>
            {
                Assert.Equal(1, addon.ApplyHotkeysCount);
                Assert.Equal(1, addon.ReadIniCount);
                Assert.Equal(1, addon.WriteIniCount);
            });

            var scene = page.Scene;
            var first = scene.CreateChild("First");
            var second = scene.CreateChild("Second");
            first.SetWorldTransform(
                new Vector3f(-1.0f, -2.0f, -3.0f), new Quaternion(1.0f, 2.0f, 3.0f),
                new Vector3f(0.5f, 0.75f, 1.25f));
            second.SetWorldTransform(
                new Vector3f(4.0f, 5.0f, 6.0f), new Quaternion(10.0f, 20.0f, 30.0f),
                new Vector3f(2.0f, 3.0f, 4.0f));
            page.Selection.SetSelected(first, true);
            page.Selection.SetSelected(second, true);

            tab.MoveSelectionToLatest(page.Selection);
            Assert.True(first.WorldPosition.ApproximatelyEquivalent(second.WorldPosition));
            Assert.True(first.WorldRotation.Equivalent(second.WorldRotation));
            Assert.True(first.WorldScale.ApproximatelyEquivalent(second.WorldScale));

            first.SetWorldPosition(Vector3f.Zero);
            second.SetWorldPosition(new Vector3f(7.0f, 8.0f, 9.0f));
            tab.MoveSelectionPositionToLatest(page.Selection);
            Assert.True(first.WorldPosition.ApproximatelyEquivalent(second.WorldPosition));
            first.SetWorldRotation(Quaternion.IDENTITY);
            second.SetWorldRotation(new Quaternion(35.0f, 45.0f, 55.0f));
            tab.MoveSelectionRotationToLatest(page.Selection);
            Assert.True(first.WorldRotation.Equivalent(second.WorldRotation));
            first.SetWorldScale(Vector3f.One);
            second.SetWorldScale(new Vector3f(3.0f, 4.0f, 5.0f));
            tab.MoveSelectionScaleToLatest(page.Selection);
            Assert.True(first.WorldScale.ApproximatelyEquivalent(second.WorldScale));

            var focusedPosition = Vector3f.Zero;
            tab.OnLookAt += (_, position) => focusedPosition = position;
            tab.FocusSelection(page.Selection);
            Assert.Equal(second.WorldPosition, focusedPosition);

            var temporaryCamera = first.CreateComponent<Camera>();
            first.SetTemporary(true);
            temporaryCamera.SetTemporary(true);
            page.Selection.Clear();
            page.Selection.SetSelected(first, true);
            page.Selection.SetSelected(temporaryCamera, true);
            tab.MakePersistent(page.Selection);
            Assert.False(first.IsTemporary);
            Assert.False(temporaryCamera.IsTemporary);

            var firstParent = scene.CreateChild("FirstParent");
            var secondParent = scene.CreateChild("SecondParent");
            page.Selection.Clear();
            page.Selection.SetSelected(firstParent, true);
            page.Selection.SetSelected(secondParent, true);
            project.UndoManager.NewFrame();
            tab.CreateNodeInSelection(scene, page.Selection);
            Assert.Equal(1, firstParent.GetNumChildren());
            Assert.Equal(1, secondParent.GetNumChildren());
            Assert.True(project.UndoManager.Undo());
            Assert.Equal(0, firstParent.GetNumChildren());
            Assert.Equal(0, secondParent.GetNumChildren());
            Assert.True(project.UndoManager.Redo());
            Assert.Equal(1, firstParent.GetNumChildren());
            Assert.Equal(1, secondParent.GetNumChildren());

            page.Selection.Clear();
            page.Selection.SetSelected(firstParent, true);
            page.Selection.SetSelected(secondParent, true);
            project.UndoManager.NewFrame();
            tab.CreateComponentInSelection(
                scene, page.Selection, ObjectReflection<Camera>.TypeId);
            Assert.NotNull(firstParent.GetComponent<Camera>());
            Assert.NotNull(secondParent.GetComponent<Camera>());
            Assert.True(project.UndoManager.Undo());
            Assert.Null(firstParent.GetComponent<Camera>());
            Assert.Null(secondParent.GetComponent<Camera>());
            Assert.True(project.UndoManager.Redo());

            page.Selection.Clear();
            page.Selection.SetSelected(firstParent.GetComponent<Camera>(), true);
            page.Selection.SetSelected(secondParent.GetComponent<Camera>(), true);
            project.UndoManager.NewFrame();
            tab.ReplaceSelectedComponents(page.Selection, ObjectReflection<Light>.TypeId);
            Assert.Null(firstParent.GetComponent<Camera>());
            Assert.Null(secondParent.GetComponent<Camera>());
            Assert.NotNull(firstParent.GetComponent<Light>());
            Assert.NotNull(secondParent.GetComponent<Light>());
            Assert.True(project.UndoManager.Undo());
            Assert.NotNull(firstParent.GetComponent<Camera>());
            Assert.NotNull(secondParent.GetComponent<Camera>());
            Assert.True(project.UndoManager.Redo());

            var copySource = scene.CreateChild("CopySource");
            copySource.CreateChild("Nested");
            page.Selection.Clear();
            page.Selection.SetSelected(copySource, true);
            tab.CopySelection(page.Selection);
            page.Selection.Clear();
            page.Selection.SetSelected(firstParent, true);
            page.Selection.SetSelected(secondParent, true);
            project.UndoManager.NewFrame();
            tab.PasteIntoSelection(scene, page.Selection);
            Assert.NotNull(firstParent.FindChild("CopySource"));
            Assert.NotNull(secondParent.FindChild("CopySource"));
            Assert.True(project.UndoManager.Undo());
            Assert.Null(firstParent.FindChild("CopySource"));
            Assert.Null(secondParent.FindChild("CopySource"));
            Assert.True(project.UndoManager.Redo());

            var deleteFirst = scene.CreateChild("DeleteFirst");
            var deleteSecond = scene.CreateChild("DeleteSecond");
            page.Selection.Clear();
            page.Selection.SetSelected(deleteFirst, true);
            page.Selection.SetSelected(deleteSecond, true);
            project.UndoManager.NewFrame();
            tab.DeleteSelection(page.Selection);
            Assert.Null(scene.FindChild("DeleteFirst"));
            Assert.Null(scene.FindChild("DeleteSecond"));
            Assert.True(project.UndoManager.Undo());
            Assert.NotNull(scene.FindChild("DeleteFirst"));
            Assert.NotNull(scene.FindChild("DeleteSecond"));
            Assert.True(project.UndoManager.Redo());

            var simulated = scene.CreateChild("Simulated");
            page.Selection.Clear();
            page.Selection.SetSelected(simulated, true);
            project.UndoManager.NewFrame();
            tab.ResumeSimulation();
            Assert.True(page.IsSimulationActive());
            Assert.True(scene.IsUpdateEnabled());
            simulated.SetName("ChangedBySimulation");
            tab.PauseSimulation();
            Assert.False(page.IsSimulationActive());
            Assert.False(scene.IsUpdateEnabled());
            Assert.True(project.UndoManager.Undo());
            Assert.NotNull(scene.FindChild("Simulated"));
            Assert.Null(scene.FindChild("ChangedBySimulation"));
            Assert.True(project.UndoManager.Redo());
            Assert.NotNull(scene.FindChild("ChangedBySimulation"));
        });
    }

    [NativeBindingFact]
    public void Page_addon_state_and_selection_actions_follow_managed_ownership()
    {
        WithSceneView((_, tab, resource) =>
        {
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());
            var scene = page.Scene;
            var first = scene.CreateChild("First");
            var second = scene.CreateChild("Second");

            page.Selection.SetSelected(first, true);
            var oldSelection = page.Selection.Pack();
            page.Selection.Clear();
            page.Selection.SetSelected(second, true);
            var newSelection = page.Selection.Pack();
            var selectionAction = new ChangeSceneSelectionAction(page, oldSelection, newSelection);
            oldSelection.Clear();
            newSelection.Clear();
            selectionAction.Undo();
            Assert.Same(first, page.Selection.ActiveNode);
            selectionAction.Redo();
            Assert.Same(second, page.Selection.ActiveNode);

            page.Selection.Clear();
            page.Selection.SetSelected(first, true);
            var preservedSelection = page.Selection.Pack();
            page.Selection.Clear();
            page.Selection.SetSelected(second, true);
            var mergedSelection = page.Selection.Pack();
            var mergedAction = new ChangeSceneSelectionAction(
                page, preservedSelection, mergedSelection);
            Assert.True(selectionAction.MergeWith(mergedAction));
            mergedSelection.Clear();
            selectionAction.Redo();
            Assert.Same(second, page.Selection.ActiveNode);

            page.Selection.Clear();
            page.Selection.SetSelected(first, true);
            var counter = new CounterAction();
            var wrapper = new PreserveSceneSelectionWrapper(counter, page);
            page.Selection.Clear();
            page.Selection.SetSelected(second, true);
            Assert.True(wrapper.CanRedo());
            wrapper.Redo();
            Assert.Equal(1, counter.Value);
            Assert.Same(first, page.Selection.ActiveNode);
            page.Selection.Clear();
            page.Selection.SetSelected(second, true);
            Assert.True(wrapper.CanUndo());
            wrapper.Undo();
            Assert.Equal(0, counter.Value);
            Assert.Same(first, page.Selection.ActiveNode);

            page.Selection.Clear();
            page.Selection.SetSelected(first, true);
            var simulationAction = new SimulateSceneAction(page);
            Assert.False(simulationAction.IsComplete());
            first.SetName("SimulatedFirst");
            page.Selection.Clear();
            page.Selection.SetSelected(second, true);
            simulationAction.Complete(false);
            Assert.False(simulationAction.IsComplete());
            simulationAction.Complete(true);
            Assert.True(simulationAction.IsComplete());
            Assert.True(simulationAction.CanUndoRedo());
            simulationAction.Undo();
            Assert.NotNull(scene.FindChild("First"));
            Assert.Same(scene.FindChild("First"), page.Selection.ActiveNode);
            simulationAction.Redo();
            Assert.NotNull(scene.FindChild("SimulatedFirst"));
            Assert.Same(scene.FindChild("Second"), page.Selection.ActiveNode);

            var firstAddon = new TestAddon(tab, "Shared", 0, 0, []);
            var secondAddon = new TestAddon(tab, "Shared", 0, 0, []);
            var replacedState = new DisposableState();
            var disposedState = new DisposableState();
            ref var firstState = ref page.GetAddonData(firstAddon);
            firstState = replacedState;
            ref var secondState = ref page.GetAddonData(secondAddon);
            Assert.Null(secondState);
            Assert.False(replacedState.IsDisposed);
            secondState = disposedState;

            tab.UnloadResource(resource.Name);
            Assert.Null(tab.GetPage(resource.Name));
            Assert.False(replacedState.IsDisposed);
            Assert.False(disposedState.IsDisposed);

            firstAddon.Dispose();
            secondAddon.Dispose();
            Assert.Equal(1, firstAddon.DisposeCount);
            Assert.Equal(1, secondAddon.DisposeCount);
        });
    }

    [NativeBindingFact]
    public void Geometry_query_uses_scene_octree_and_triangle_level_by_default()
    {
        _fixture.ResetGraphics();
        using var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        var geometry = scene.CreateChild("Geometry").CreateComponent<CustomGeometry>();
        geometry.BeginGeometry(0u, PrimitiveType.TriangleList);
        geometry.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
        geometry.DefineVertex(new Vector3f(1.0f, 0.0f, 0.0f));
        geometry.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
        geometry.Commit();
        octree.Update(new FrameInfo());

        var results = SceneViewFunctions.QueryGeometriesFromScene(
            scene,
            new Ray(new Vector3f(0.25f, 0.25f, 1.0f), new Vector3f(0.0f, 0.0f, -1.0f)));

        var result = Assert.Single(results);
        Assert.Same(geometry, result.Drawable);
        Assert.Equal(1.0f, result.Distance);
    }

    private void WithSceneView(Action<Project, TestSceneViewTab, SceneResourceForEditor> callback)
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneViewTabTests", Guid.NewGuid().ToString("N"));
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

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Test.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Test.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            resource.Scene.CreateComponent<Octree>();
            Assert.True(_fixture.Resources.AddManualResource(resource));

            tab = new TestSceneViewTab();
            callback(project, tab, resource);
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

    private static void RenderFrame(SceneViewTab tab)
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
            ImGui.Begin("Scene view test"u8, ref open);
            windowStarted = true;
            tab.RenderToolbar();
            tab.RenderContent();
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

        public void UnloadResource(string resourceName) => OnResourceUnloaded(resourceName);
    }

    private sealed class TestAddon : SceneViewAddon
    {
        public TestAddon(
            SceneViewTab owner,
            string name,
            int inputPriority,
            int toolbarPriority,
            List<string> calls)
            : base(owner)
        {
            _name = name;
            _inputPriority = inputPriority;
            _toolbarPriority = toolbarPriority;
            _calls = calls;
        }

        public bool ConsumesMouse { get; init; }

        public int ApplyHotkeysCount { get; private set; }

        public int ReadIniCount { get; private set; }

        public int WriteIniCount { get; private set; }

        public int DisposeCount { get; private set; }

        public override string GetUniqueName() => _name;

        public override int GetInputPriority() => _inputPriority;

        public override int GetToolbarPriority() => _toolbarPriority;

        public override void Initialize(SceneViewPage page)
        {
            _calls.Add($"initialize:{_name}");
        }

        public override void ProcessInput(SceneViewPage page, ref bool mouseConsumed)
        {
            _calls.Add($"input:{_name}:{mouseConsumed}");
            if (ConsumesMouse)
                mouseConsumed = true;
        }

        public override void Render(SceneViewPage page)
        {
            _calls.Add($"render:{_name}");
        }

        public override bool RenderToolbar()
        {
            _calls.Add($"toolbar:{_name}");
            return false;
        }

        public override void ApplyHotkeys(HotkeyManager hotkeyManager)
        {
            ++ApplyHotkeysCount;
        }

        public override void ReadIniSettings(string line)
        {
            ++ReadIniCount;
        }

        public override void WriteIniSettings(ImGuiTextBuffer output)
        {
            ++WriteIniCount;
        }

        public override void Dispose()
        {
            ++DisposeCount;
            base.Dispose();
        }

        private readonly string _name;
        private readonly int _inputPriority;
        private readonly int _toolbarPriority;
        private readonly List<string> _calls;
    }

    private sealed class CounterAction : EditorAction
    {
        public int Value { get; private set; }

        public override void Redo() => ++Value;

        public override void Undo() => --Value;
    }

    private sealed class DisposableState : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
