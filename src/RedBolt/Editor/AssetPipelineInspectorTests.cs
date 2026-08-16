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
using RedBolt.Resource;
using RedBolt.SystemUI;
using RedBolt.Utility;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Reflected(ObjectCategory.Category_Transformer)]
public partial class AssetPipelineInspectorTestTransformer : AssetTransformer
{
    [Serialized("Value")]
    public int Value { get; set; } = 1;

    public int ApplyCount { get; private set; }

    public override void ApplyAttributes() => ++ApplyCount;
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class AssetPipelineInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public AssetPipelineInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Asset_pipeline_is_inspected_edited_applied_discarded_removed_added_and_reloaded()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.AssetPipelineInspectorTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        var resourceName = "Folder/Test.assetpipeline";
        Directory.CreateDirectory(Path.Combine(dataPath, "Folder"));
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
        AssetPipelineInspector? inspector = null;
        ReloadObservingEditorTab? tab = null;
        AssetPipeline? resource = null;
        AssetPipelineInspectorTestTransformer? firstTransformer = null;
        AssetPipelineInspectorTestTransformer? secondTransformer = null;
        var observedTransformers = new HashSet<AssetTransformer>(ReferenceEqualityComparer.Instance);
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            _fixture.Context.RegisterAssembly(typeof(AssetPipelineInspectorTestTransformer).Assembly);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (AssetPipeline.CheckExtension(descriptor.ResourceName))
                    descriptor.AddObjectType<AssetPipeline>();
            });

            resource = new AssetPipeline();
            resource.SetName(resourceName);
            resource.SetAbsoluteFileName(Path.Combine(dataPath, resourceName));
            firstTransformer = new AssetPipelineInspectorTestTransformer();
            secondTransformer = new AssetPipelineInspectorTestTransformer { Value = 2 };
            resource.AddTransformer(firstTransformer);
            resource.AddTransformer(secondTransformer);
            observedTransformers.Add(firstTransformer);
            observedTransformers.Add(secondTransformer);
            Assert.True(_fixture.Resources.AddManualResource(resource));

            inspector = new AssetPipelineInspector(project);
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
            SerializableInspectorWidget.RegisterAttributeHook<AssetPipelineInspectorTestTransformer>(
                "Value", EditValue);
            try
            {
                RenderInspectorFrame(inspector, null, Activation.None);
            }
            finally
            {
                SerializableInspectorWidget.UnregisterAttributeHook<AssetPipelineInspectorTestTransformer>(
                    "Value");
            }
            Assert.Equal(9, firstTransformer.Value);
            Assert.Equal(2, secondTransformer.Value);
            var pendingAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.False(pendingAction.IsComplete());
            Assert.True(project.UndoManager.CanUndo());

            Assert.False(GetField<bool>(project.AssetManager, "_hasInvalidAssets"));
            RenderInspectorFrame(inspector, null, Activation.UpdateAssets);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.True(GetField<bool>(project.AssetManager, "_hasInvalidAssets"));

            var delayedSaveCount = GetDelayedSaveCount(project);
            RenderInspectorFrame(inspector, null, Activation.Apply);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.True(pendingAction.IsComplete());
            Assert.Equal(delayedSaveCount + 1, GetDelayedSaveCount(project));

            project.UndoManager.NewFrame();
            editValue = true;
            bool EditAgain(in AttributeHookContext context, ref Variant boxedValue)
            {
                if (!editValue)
                    return false;
                editValue = false;
                boxedValue.Dispose();
                boxedValue = new Variant(15);
                return true;
            }
            SerializableInspectorWidget.RegisterAttributeHook<AssetPipelineInspectorTestTransformer>(
                "Value", EditAgain);
            try
            {
                RenderInspectorFrame(inspector, null, Activation.None);
            }
            finally
            {
                SerializableInspectorWidget.UnregisterAttributeHook<AssetPipelineInspectorTestTransformer>(
                    "Value");
            }
            var discardedAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.NotSame(pendingAction, discardedAction);
            Assert.False(discardedAction.IsComplete());
            Assert.Equal(15, firstTransformer.Value);

            RenderInspectorFrame(inspector, null, Activation.Discard);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.True(discardedAction.IsComplete());
            TrackTransformers(resource, observedTransformers);
            Assert.Equal(2, resource.GetTransformers().Count);
            Assert.Equal(9, Assert.IsType<AssetPipelineInspectorTestTransformer>(
                resource.GetTransformers()[0]).Value);

            project.UndoManager.NewFrame();
            RenderInspectorFrame(inspector, null, Activation.None);
            widgets = GetInspectorWidgets(inspector);
            firstWidget = widgets[0];
            RenderInspectorFrame(inspector, firstWidget, Activation.Remove);
            RenderInspectorFrame(inspector, null, Activation.None);
            TrackTransformers(resource, observedTransformers);
            Assert.Single(resource.GetTransformers());
            var removeAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.False(removeAction.IsComplete());

            RenderInspectorFrame(inspector, null, Activation.Discard);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.True(removeAction.IsComplete());
            TrackTransformers(resource, observedTransformers);
            Assert.Equal(2, resource.GetTransformers().Count);

            project.UndoManager.NewFrame();
            RenderInspectorFrame(inspector, null, Activation.AddTransformer);
            RenderInspectorFrame(inspector, null, Activation.SelectTestTransformer);
            RenderInspectorFrame(inspector, null, Activation.None);
            TrackTransformers(resource, observedTransformers);
            Assert.Equal(3, resource.GetTransformers().Count);
            Assert.IsType<AssetPipelineInspectorTestTransformer>(resource.GetTransformers()[2]);
            var addAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.False(addAction.IsComplete());

            RenderInspectorFrame(inspector, null, Activation.Apply);
            RenderInspectorFrame(inspector, null, Activation.None);
            Assert.True(addAction.IsComplete());

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
            if (resource is not null)
                TrackTransformers(resource, observedTransformers);
            foreach (var transformer in observedTransformers)
                transformer.Dispose();
            _fixture.Resources.ReleaseResource<AssetPipeline>(resourceName, true);
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
        AssetPipelineInspector inspector, SerializableInspectorWidget? widget, Activation activation)
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
            ImGui.Begin("Asset Pipeline Inspector"u8, ref isOpen);
            windowStarted = true;

            inspector.RenderContent();

            if (widget is not null && activation == Activation.Remove)
            {
                using var guard = new IdScopeGuard(RuntimeHelpers.GetHashCode(widget));
                ImGui.ActivateItemByID(ImGui.GetID("\uf2ed##RemoveTransformer"u8));
            }
            else if (activation == Activation.UpdateAssets)
            {
                ImGui.ActivateItemByID(ImGui.GetID("\uf021 Update Assets"u8));
            }
            else if (activation == Activation.Apply)
            {
                ImGui.ActivateItemByID(ImGui.GetID("\uf14a Apply"u8));
            }
            else if (activation == Activation.Discard)
            {
                ImGui.ActivateItemByID(ImGui.GetID("\uf2d3 Discard"u8));
            }
            else if (activation == Activation.AddTransformer)
            {
                ImGui.ActivateItemByID(ImGui.GetID("\uf0fe Add Transformer"u8));
            }
            else if (activation == Activation.SelectTestTransformer)
            {
                var popupId = ImGui.GetID("##AddTransformer"u8);
                using var popupName = new CString($"##Popup_{popupId:x8}");
                var popupWindow = ImGui.FindWindowByName(popupName.ContentBytes);
                ImGui.ActivateItemByID(
                    popupWindow.GetID("AssetPipelineInspectorTestTransformer"u8));
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
        AssetPipelineInspector inspector)
    {
        return GetField<IReadOnlyList<SerializableInspectorWidget>>(inspector, "_inspectorWidgets");
    }

    private static void TrackTransformers(
        AssetPipeline resource, HashSet<AssetTransformer> observedTransformers)
    {
        foreach (var transformer in resource.GetTransformers())
        {
            if (transformer is not null)
                observedTransformers.Add(transformer);
        }
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
            : base("Reload Observer", "c080bcb3-3b70-4df0-a4fb-b040a1882358",
                EditorTabFlag.None, EditorTabPlacement.Floating)
        {
        }

        public int ReloadCount { get; private set; }

        public void Observe(AssetPipeline resource)
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
        Remove,
        UpdateAssets,
        Apply,
        Discard,
        AddTransformer,
        SelectTestTransformer,
    }
}
