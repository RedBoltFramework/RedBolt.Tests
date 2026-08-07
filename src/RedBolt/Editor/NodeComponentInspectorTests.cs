namespace RedBolt.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class NodeComponentInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public NodeComponentInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Requests_widgets_edits_actions_components_and_summary_are_forwarded()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.NodeComponentInspectorTests", Guid.NewGuid().ToString("N"));
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

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        NodeComponentInspector? inspector = null;
        CapturingEditorTab? tab = null;
        Scene? scene = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            _fixture.Context.RegisterAssembly(typeof(NodeInspectorSharedTestComponent).Assembly);
            inspector = new NodeComponentInspector(project);
            tab = new CapturingEditorTab();
            scene = new Scene();
            var firstNode = scene.CreateChild("Original");
            var secondNode = scene.CreateChild("Original");
            var firstComponent = firstNode.CreateComponent<NodeInspectorSharedTestComponent>();
            var secondComponent = secondNode.CreateComponent<NodeInspectorSharedTestComponent>();
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            using (var requestWithoutSender = new InspectNodeComponentRequest(
                new[] { firstNode }, Array.Empty<Component>()))
            {
                project.ProcessRequest(requestWithoutSender);
                processPendingRequests.Invoke(project, null);
            }
            using (var detachedNode = new Node())
            using (var detachedRequest = new InspectNodeComponentRequest(
                new[] { detachedNode }, Array.Empty<Component>()))
            {
                project.ProcessRequest(detachedRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(0, activationCount);

            using (var nodeRequest = new InspectNodeComponentRequest(
                new[] { secondNode, firstNode }, Array.Empty<Component>()))
            {
                project.ProcessRequest(nodeRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            Assert.Same(tab, inspector.GetOwnerTab());
            Assert.True(inspector.IsUndoSupported());
            var nodeWidget = Assert.IsType<NodeInspectorWidget>(
                GetField<object?>(inspector, "_nodeWidget"));
            Assert.Null(GetField<object?>(inspector, "_componentWidget"));

            using (var sameNodeRequest = new InspectNodeComponentRequest(
                new[] { firstNode, secondNode }, Array.Empty<Component>()))
            {
                project.ProcessRequest(sameNodeRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Same(nodeWidget, GetField<object?>(inspector, "_nodeWidget"));

            InspectorSource source = inspector;
            source.RenderContextMenuItems();
            source.RenderMenu();
            source.ApplyHotkeys(project.HotkeyManager);
            Assert.Equal(1, tab.RenderMenuCount);

            var editName = true;
            bool EditNodeName(in AttributeHookContext context, ref Variant boxedValue)
            {
                if (!editName)
                    return false;
                editName = false;
                boxedValue.Dispose();
                boxedValue = new Variant("Edited");
                return true;
            }
            SerializableInspectorWidget.RegisterAttributeHook<Node>("Name", EditNodeName);
            try
            {
                RenderInspectorFrame(inspector, null, Activation.None);
            }
            finally
            {
                SerializableInspectorWidget.UnregisterAttributeHook<Node>("Name");
            }
            Assert.Equal("Edited", firstNode.Name);
            Assert.Equal("Edited", secondNode.Name);
            var editAction = Assert.IsType<ChangeNodeAttributesAction>(tab.Actions.Single());
            editAction.Undo();
            Assert.Equal("Original", firstNode.Name);
            Assert.Equal("Original", secondNode.Name);
            editAction.Redo();

            RenderInspectorFrame(inspector, firstComponent, Activation.Action);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Equal(1, firstComponent.CallCount);
            Assert.Equal(1, secondComponent.CallCount);
            Assert.Equal(2, tab.Actions.Skip(1).OfType<ChangeNodeSubtreeAction>().Count());

            RenderInspectorFrame(inspector, firstComponent, Activation.Remove);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Null(firstComponent.TryGetNode());
            Assert.Null(secondComponent.TryGetNode());
            Assert.Equal(2, tab.Actions.OfType<CreateRemoveComponentAction>().Count());

            var actionCount = tab.Actions.Count;
            InvokePrivate(inspector, "AddComponentToNodes",
                ObjectReflection<NodeInspectorSkippedTestComponent>.TypeId);
            Assert.Single(firstNode.GetComponents<NodeInspectorSkippedTestComponent>());
            Assert.Single(secondNode.GetComponents<NodeInspectorSkippedTestComponent>());
            Assert.Equal(actionCount + 2, tab.Actions.Count);

            var firstCamera = firstNode.CreateComponent<Camera>();
            var secondCamera = secondNode.CreateComponent<Camera>();
            using (var componentRequest = new InspectNodeComponentRequest(
                Array.Empty<Node>(), new Component[] { secondCamera, firstCamera }))
            {
                project.ProcessRequest(componentRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Null(GetField<object?>(inspector, "_nodeWidget"));
            Assert.IsType<SerializableInspectorWidget>(GetField<object?>(inspector, "_componentWidget"));
            RenderInspectorFrame(inspector, null, Activation.None);

            var skippedComponent = firstNode.GetComponent<NodeInspectorSkippedTestComponent>()!;
            using (var mixedRequest = new InspectNodeComponentRequest(
                Array.Empty<Node>(), new Component[] { firstCamera, secondCamera, skippedComponent }))
            {
                project.ProcessRequest(mixedRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Null(GetField<object?>(inspector, "_nodeWidget"));
            Assert.Null(GetField<object?>(inspector, "_componentWidget"));
            var summary = GetField<IReadOnlyDictionary<string, uint>>(inspector, "_componentSummary");
            Assert.Equal(2u, summary[nameof(Camera)]);
            Assert.Equal(1u, summary[nameof(NodeInspectorSkippedTestComponent)]);
            RenderInspectorFrame(inspector, null, Activation.None);

            using (var sceneRequest = new InspectNodeComponentRequest(
                new Node[] { firstNode, scene }, Array.Empty<Component>()))
            {
                project.ProcessRequest(sceneRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            nodeWidget = Assert.IsType<NodeInspectorWidget>(GetField<object?>(inspector, "_nodeWidget"));
            Assert.Single(nodeWidget.GetNodes());
            Assert.Same(scene, GetTarget(nodeWidget.GetNodes()[0]!));

            actionCount = tab.Actions.Count;
            IReadOnlyList<WeakReference<Serializable>?> sceneObjects =
                [new WeakReference<Serializable>(scene)];
            InvokePrivate(inspector, "BeginAction", sceneObjects);
            InvokePrivate(inspector, "EndAction", sceneObjects);
            Assert.Equal(actionCount, tab.Actions.Count);

            inspector.Dispose();
            inspector = null;
            using var requestAfterDispose = new InspectNodeComponentRequest(
                new[] { firstNode }, Array.Empty<Component>());
            project.ProcessRequest(requestAfterDispose, tab);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(5, activationCount);
        }
        finally
        {
            inspector?.Dispose();
            scene?.Dispose();
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
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static void RenderInspectorFrame(
        NodeComponentInspector inspector, Component? component, Activation activation)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 760.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(560.0f, 700.0f));
            var isOpen = true;
            ImGui.Begin("Node Component Inspector"u8, ref isOpen);
            windowStarted = true;

            inspector.RenderContent();

            if (component is not null)
            {
                using var componentGuard = new IdScopeGuard(RuntimeHelpers.GetHashCode(component));
                if (activation == Activation.Action)
                {
                    using var attributeGuard = new IdScopeGuard("Run"u8);
                    ImGui.ActivateItemByID(ImGui.GetID("Run"u8));
                }
                else if (activation == Activation.Remove)
                {
                    ImGui.ActivateItemByID(ImGui.GetID("\uf2ed##RemoveComponent"u8));
                }
            }
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void InvokePrivate(object instance, string name, params object?[] arguments)
    {
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);
    }

    private static T GetTarget<T>(WeakReference<T> reference)
        where T : class
    {
        Assert.True(reference.TryGetTarget(out var target));
        return target;
    }

    private sealed class CapturingEditorTab : EditorTab
    {
        public CapturingEditorTab()
            : base("Capture", "d98adad2-4d2c-4e36-858e-cf81ee9f3bf0",
                EditorTabFlag.None, EditorTabPlacement.Floating)
        {
        }

        public List<EditorAction> Actions { get; } = [];

        public int RenderMenuCount { get; private set; }

        public override ulong? PushAction(EditorAction action)
        {
            Actions.Add(action);
            return base.PushAction(action);
        }

        public override void RenderMenu() => ++RenderMenuCount;
    }

    private enum Activation
    {
        None,
        Action,
        Remove,
    }
}
