// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SerializableResourceInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SerializableResourceInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Serializable_resources_are_inspected_edited_saved_and_filtered()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SerializableResourceInspectorTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(Path.Combine(dataPath, "Other.txt"), "other");
        File.WriteAllText(Path.Combine(dataPath, "A.serializable"), string.Empty);
        File.WriteAllText(Path.Combine(dataPath, "B.serializable"), string.Empty);
        File.WriteAllText(Path.Combine(dataPath, "Empty.serializable"), string.Empty);
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        SerializableResourceInspector? inspector = null;
        SerializableResource? firstResource = null;
        SerializableResource? secondResource = null;
        SerializableResource? emptyResource = null;
        SerializableInspectorWidgetTestObject? firstValue = null;
        SerializableInspectorWidgetTestObject? secondValue = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            _fixture.Context.RegisterAssembly(typeof(SerializableInspectorWidgetTestObject).Assembly);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".serializable"))
                    descriptor.AddObjectType<SerializableResource>();
            });

            firstValue = new SerializableInspectorWidgetTestObject();
            secondValue = new SerializableInspectorWidgetTestObject { Value = 2 };
            firstResource = CreateResource("A.serializable", dataPath, firstValue);
            secondResource = CreateResource("B.serializable", dataPath, secondValue);
            emptyResource = CreateResource("Empty.serializable", dataPath, null);
            Assert.True(_fixture.Resources.AddManualResource(firstResource));
            Assert.True(_fixture.Resources.AddManualResource(secondResource));
            Assert.True(_fixture.Resources.AddManualResource(emptyResource));

            inspector = new SerializableResourceInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            using (var otherRequest = new InspectResourceRequest(["Other.txt"]))
            {
                project.ProcessRequest(otherRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(0, activationCount);

            using (var resourcesRequest = new InspectResourceRequest(
                ["B.serializable", "A.serializable"]))
            {
                project.ProcessRequest(resourcesRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            Assert.Equal(
                ["A.serializable", "B.serializable"],
                GetField<IReadOnlyList<string>>(inspector, "_resourceNames"));
            var widget = Assert.IsType<SerializableInspectorWidget>(
                GetField<object?>(inspector, "_widget"));
            Assert.Equal("2x SerializableInspectorWidgetTestObject", widget.GetTitle());

            using (var sameResourcesRequest = new InspectResourceRequest(
                ["A.serializable", "B.serializable"]))
            {
                project.ProcessRequest(sameResourcesRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Same(widget, GetField<object?>(inspector, "_widget"));

            InspectorSource source = inspector;
            Assert.True(source.IsUndoSupported());
            source.RenderContextMenuItems();
            source.RenderMenu();
            source.ApplyHotkeys(project.HotkeyManager);

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
            SerializableInspectorWidget.RegisterAttributeHook<SerializableInspectorWidgetTestObject>(
                "Value", EditValue);
            try
            {
                RenderInspectorFrame(inspector, activateRun: true);

                Assert.Equal(9, firstValue.Value);
                Assert.Equal(9, secondValue.Value);
                var pendingAction = Assert.IsType<ModifyResourceAction>(
                    GetField<object?>(inspector, "_pendingAction"));
                Assert.False(pendingAction.IsComplete());
                Assert.Equal(2, GetDelayedSaveCount(project));

                RenderInspectorFrame(inspector);

                Assert.Equal(1, firstValue.CallCount);
                Assert.Equal(1, secondValue.CallCount);
                Assert.Same(pendingAction, GetField<object?>(inspector, "_pendingAction"));
                Assert.Equal(2, GetDelayedSaveCount(project));
            }
            finally
            {
                SerializableInspectorWidget.UnregisterAttributeHook<SerializableInspectorWidgetTestObject>(
                    "Value");
            }

            using (var emptyRequest = new InspectResourceRequest(["Empty.serializable"]))
            {
                project.ProcessRequest(emptyRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(3, activationCount);
            Assert.Null(GetField<object?>(inspector, "_widget"));
            Assert.Empty(GetField<IReadOnlyList<string>>(inspector, "_resourceNames"));
            Assert.Empty(widget.GetObjects());
            inspector.RenderContent();

            inspector.Dispose();
            inspector = null;
            using var requestAfterDispose = new InspectResourceRequest(["A.serializable"]);
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(3, activationCount);
        }
        finally
        {
            inspector?.Dispose();
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
            _fixture.Resources.ReleaseResource<SerializableResource>("A.serializable", true);
            _fixture.Resources.ReleaseResource<SerializableResource>("B.serializable", true);
            _fixture.Resources.ReleaseResource<SerializableResource>("Empty.serializable", true);
            firstResource?.Dispose();
            secondResource?.Dispose();
            emptyResource?.Dispose();
            firstValue?.Dispose();
            secondValue?.Dispose();
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static SerializableResource CreateResource(
        string resourceName, string dataPath, Serializable? value)
    {
        var resource = new SerializableResource { Name = resourceName };
        resource.SetAbsoluteFileName(Path.Combine(dataPath, resourceName));
        resource.SetValue(value);
        return resource;
    }

    private static void RenderInspectorFrame(
        SerializableResourceInspector inspector, bool activateRun = false)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(500.0f, 360.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(440.0f, 320.0f));
            var isOpen = true;
            ImGui.Begin("Serializable Resource Inspector"u8, ref isOpen);
            windowStarted = true;

            inspector.RenderContent();

            if (activateRun)
            {
                using var guard = new IdScopeGuard("Run"u8);
                ImGui.ActivateItemByID(ImGui.GetID("Run"u8));
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

    private static int GetDelayedSaveCount(Project project)
    {
        var delayedFileSaves = GetField<object>(project, "_delayedFileSaves");
        return ((ICollection)delayedFileSaves).Count;
    }
}
