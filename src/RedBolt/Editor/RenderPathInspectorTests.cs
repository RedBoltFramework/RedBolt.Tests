// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Reflected(ObjectCategory.Category_RenderPass)]
public partial class RenderPathInspectorTestPass : RenderPass
{
    [Serialized("Value")]
    public int Value { get; set; } = 1;

    public int ApplyCount { get; private set; }

    public override void ApplyAttributes() => ++ApplyCount;

    public void SetPassName(string value) => _passName = value;
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderPathInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public RenderPathInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Render_path_is_inspected_edited_reordered_removed_added_and_reloaded()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.RenderPathInspectorTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        var resourceName = "Test.renderpath";
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(Path.Combine(dataPath, resourceName), string.Empty);
        File.WriteAllText(Path.Combine(dataPath, "Other.txt"), "other");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        RenderPathInspector? inspector = null;
        ReloadObservingEditorTab? tab = null;
        RenderPath? resource = null;
        RenderPathInspectorTestPass? firstPass = null;
        RenderPathInspectorTestPass? secondPass = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            _fixture.Context.RegisterAssembly(typeof(RenderPathInspectorTestPass).Assembly);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".renderpath"))
                    descriptor.AddObjectType<RenderPath>();
            });

            resource = new RenderPath();
            resource.SetName(resourceName);
            resource.SetAbsoluteFileName(Path.Combine(dataPath, resourceName));
            firstPass = new RenderPathInspectorTestPass();
            firstPass.SetPassName("First");
            secondPass = new RenderPathInspectorTestPass { Value = 2 };
            secondPass.SetPassName("Second");
            resource.AddPass(firstPass);
            resource.AddPass(secondPass);
            Assert.True(_fixture.Resources.AddManualResource(resource));

            inspector = new RenderPathInspector(project);
            tab = new ReloadObservingEditorTab();
            tab.Observe(resource);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            using (var requestWithoutSender = new InspectResourceRequest([resourceName]))
            {
                project.ProcessRequest(requestWithoutSender);
                processPendingRequests.Invoke(project, null);
            }
            using (var otherRequest = new InspectResourceRequest(["Other.txt"]))
            {
                project.ProcessRequest(otherRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(0, activationCount);

            using (var request = new InspectResourceRequest([resourceName]))
            {
                project.ProcessRequest(request, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            Assert.Null(inspector.GetOwnerTab());
            Assert.True(inspector.IsUndoSupported());
            Assert.Same(resource, GetField<object?>(inspector, "_resource"));

            RenderInspectorFrame(inspector, null, Activation.None);
            var widgets = GetInspectorWidgets(inspector);
            Assert.Equal(2, widgets.Count);
            var firstWidget = widgets[0];

            using (var sameRequest = new InspectResourceRequest([resourceName]))
            {
                project.ProcessRequest(sameRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Same(firstWidget, GetInspectorWidgets(inspector)[0]);

            InspectorSource source = inspector;
            source.RenderContextMenuItems();
            source.RenderMenu();
            source.ApplyHotkeys(project.HotkeyManager);

            resource.SendEvent<ResourceEvents.ReloadFinished>();
            Assert.Equal(1, tab.ReloadCount);
            Assert.Empty(GetInspectorWidgets(inspector));
            Assert.Empty(firstWidget.GetObjects());
            RenderInspectorFrame(inspector, null, Activation.None);
            widgets = GetInspectorWidgets(inspector);
            Assert.Equal(2, widgets.Count);
            firstWidget = widgets[0];

            RenderInspectorFrame(inspector, firstWidget, Activation.OpenHeader);
            var editValue = true;
            bool EditValue(in AttributeHookContext context, ref Variant boxedValue)
            {
                if (!editValue)
                    return false;
                editValue = false;
                boxedValue.Dispose();
                boxedValue = new Variant(9);
                return true;
            }
            SerializableInspectorWidget.RegisterAttributeHook<RenderPathInspectorTestPass>(
                "Value", EditValue);
            var reloadCount = tab.ReloadCount;
            try
            {
                RenderInspectorFrame(inspector, null, Activation.None);
            }
            finally
            {
                SerializableInspectorWidget.UnregisterAttributeHook<RenderPathInspectorTestPass>(
                    "Value");
            }
            Assert.Equal(9, firstPass.Value);
            Assert.Equal(2, secondPass.Value);
            Assert.Equal(reloadCount + 1, tab.ReloadCount);
            Assert.Same(firstWidget, GetInspectorWidgets(inspector)[0]);
            var pendingAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.False(pendingAction.IsComplete());
            Assert.True(project.UndoManager.CanUndo());

            reloadCount = tab.ReloadCount;
            RenderInspectorFrame(inspector, firstWidget, Activation.MoveDown);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Equal([secondPass, firstPass], resource.Passes);
            Assert.Empty(GetInspectorWidgets(inspector));
            Assert.Equal(reloadCount + 2, tab.ReloadCount);

            RenderInspectorFrame(inspector, null, Activation.None);
            widgets = GetInspectorWidgets(inspector);
            var secondWidget = widgets[0];
            RenderInspectorFrame(inspector, secondWidget, Activation.Remove);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Equal([firstPass], resource.Passes);
            Assert.Empty(GetInspectorWidgets(inspector));

            RenderInspectorFrame(inspector, null, Activation.AddPass);
            RenderInspectorFrame(inspector, null, Activation.SelectTestPass);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Equal(2, resource.Passes.Count);
            Assert.Same(firstPass, resource.Passes[0]);

            var delayedSaveCount = GetDelayedSaveCount(project);
            pendingAction.Complete(true);
            Assert.True(pendingAction.IsComplete());
            Assert.Equal(delayedSaveCount + 1, GetDelayedSaveCount(project));
            reloadCount = tab.ReloadCount;
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.Equal(reloadCount, tab.ReloadCount);

            var lastWidget = GetInspectorWidgets(inspector)[0];
            inspector.Dispose();
            inspector = null;
            Assert.Empty(lastWidget.GetObjects());
            using var requestAfterDispose = new InspectResourceRequest([resourceName]);
            project.ProcessRequest(requestAfterDispose, tab);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(2, activationCount);
        }
        finally
        {
            inspector?.Dispose();
            tab?.Dispose();
            if (resource is not null && secondPass is not null && !resource.Passes.Contains(secondPass))
                secondPass.Dispose();
            _fixture.Resources.ReleaseResource<RenderPath>(resourceName, true);
            resource?.Dispose();
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
        RenderPathInspector inspector, SerializableInspectorWidget? widget, Activation activation)
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
            ImGui.Begin("Render Path Inspector"u8, ref isOpen);
            windowStarted = true;

            inspector.RenderContent();

            if (widget is not null)
            {
                using var guard = new IdScopeGuard(RuntimeHelpers.GetHashCode(widget));
                if (activation == Activation.OpenHeader)
                    ImGui.ActivateItemByID(ImGui.GetID("First"u8));
                else if (activation == Activation.MoveDown)
                    ImGui.ActivateItemByID(ImGui.GetID("\uf063##MovePassDown"u8));
                else if (activation == Activation.Remove)
                    ImGui.ActivateItemByID(ImGui.GetID("\uf2ed##RemovePass"u8));
            }
            else if (activation == Activation.AddPass)
            {
                ImGui.ActivateItemByID(ImGui.GetID("\uf0fe Add Pass"u8));
            }
            else if (activation == Activation.SelectTestPass)
            {
                var popupId = ImGui.GetID("##AddPass"u8);
                using var popupName = new CString($"##Popup_{popupId:x8}");
                var popupWindow = ImGui.FindWindowByName(popupName.ContentBytes);
                ImGui.ActivateItemByID(popupWindow.GetID("RenderPathInspectorTestPass"u8));
                GC.KeepAlive(popupWindow);
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

    private static IReadOnlyList<SerializableInspectorWidget> GetInspectorWidgets(
        RenderPathInspector inspector)
    {
        return GetField<IReadOnlyList<SerializableInspectorWidget>>(inspector, "_inspectorWidgets");
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static int GetDelayedSaveCount(Project project)
    {
        var delayedFileSaves = GetField<object>(project, "_delayedFileSaves");
        return ((ICollection)delayedFileSaves).Count;
    }

    private sealed class ReloadObservingEditorTab : EditorTab
    {
        public ReloadObservingEditorTab()
            : base("Reload Observer", "35388994-5879-4bd2-b8ce-c441e484e431",
                EditorTabFlag.None, EditorTabPlacement.Floating)
        {
        }

        public int ReloadCount { get; private set; }

        public void Observe(RenderPath resource)
        {
            SubscribeToEvent<ResourceEvents.ReloadFinished>(resource, OnResourceReloaded);
        }

        private void OnResourceReloaded(ref ResourceEvents.ReloadFinished eventData)
        {
            ++ReloadCount;
        }
    }

    private enum Activation
    {
        None,
        OpenHeader,
        MoveDown,
        Remove,
        AddPass,
        SelectTestPass,
    }
}
