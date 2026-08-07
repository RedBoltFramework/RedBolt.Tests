namespace RedBolt.Tests;

using System.Collections;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class MaterialInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public MaterialInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Materials_are_requested_sorted_edited_saved_refreshed_and_disposed()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.MaterialInspectorTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        var firstName = "Materials/First.xml";
        var secondName = "Materials/Second.xml";
        Directory.CreateDirectory(Path.Combine(dataPath, "Materials"));
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(Path.Combine(dataPath, firstName), string.Empty);
        File.WriteAllText(Path.Combine(dataPath, secondName), string.Empty);
        File.WriteAllText(Path.Combine(dataPath, "Other.txt"), "other");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        MaterialInspector? inspector = null;
        TestEditorTab? tab = null;
        Material? first = null;
        Material? second = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            MountCompleteCoreData(virtualFileSystem, project.CoreDataPath);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.ResourceName.StartsWith("Materials/", StringComparison.Ordinal)
                    && descriptor.ResourceName.EndsWith(".xml", StringComparison.Ordinal))
                {
                    descriptor.AddObjectType<Material>();
                }
            });

            var technique = _fixture.Resources.Get<Technique>("Techniques/LitOpaque.xml");
            first = CreateMaterial(firstName, Path.Combine(dataPath, firstName), technique);
            second = CreateMaterial(secondName, Path.Combine(dataPath, secondName), technique);
            Assert.True(_fixture.Resources.AddManualResource(first));
            Assert.True(_fixture.Resources.AddManualResource(second));

            inspector = new MaterialInspector(project);
            tab = new TestEditorTab();
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            RenderInspectorFrame(inspector, Activation.None);
            using (var emptyRequest = new InspectResourceRequest([]))
            {
                project.ProcessRequest(emptyRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            using (var mixedRequest = new InspectResourceRequest([firstName, "Other.txt"]))
            {
                project.ProcessRequest(mixedRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(0, activationCount);

            using (var request = new InspectResourceRequest([secondName, firstName]))
            {
                project.ProcessRequest(request);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            Assert.True(inspector.IsUndoSupported());
            var widget = GetField<MaterialInspectorWidget>(inspector, "_widget");
            Assert.Equal([firstName, secondName], widget.GetMaterials().Select(static x => x.GetName()));

            using (var sameRequest = new InspectResourceRequest([firstName, secondName]))
            {
                project.ProcessRequest(sameRequest, tab);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Same(widget, GetField<MaterialInspectorWidget>(inspector, "_widget"));

            InspectorSource source = inspector;
            source.RenderContextMenuItems();
            source.RenderMenu();
            source.ApplyHotkeys(project.HotkeyManager);

            var delayedSaveCount = GetDelayedSaveCount(project);
            RenderInspectorFrame(inspector, Activation.ToggleAlphaToCoverage);
            RenderInspectorFrame(inspector, Activation.None);
            Assert.True(first.GetAlphaToCoverage());
            Assert.True(second.GetAlphaToCoverage());
            Assert.Equal(delayedSaveCount + 2, GetDelayedSaveCount(project));
            var pendingAction = Assert.IsType<ModifyResourceAction>(
                GetField<object?>(inspector, "_pendingAction"));
            Assert.False(pendingAction.IsComplete());
            Assert.True(project.UndoManager.CanUndo());

            RenderInspectorFrame(inspector, Activation.ForceSave);
            RenderInspectorFrame(inspector, Activation.None);
            Assert.Same(pendingAction, GetField<object?>(inspector, "_pendingAction"));
            Assert.Equal(delayedSaveCount + 2, GetDelayedSaveCount(project));

            var sortedTechniques = GetField<IList>(widget, "_sortedTechniques");
            sortedTechniques.Clear();
            Assert.Empty(sortedTechniques);
            var updateTimer = GetField<Timer>(inspector, "_updateTimer");
            SetField(updateTimer, "_startTime", 0u);
            RenderInspectorFrame(inspector, Activation.None);
            Assert.NotEmpty(sortedTechniques);

            inspector.Dispose();
            inspector = null;
            Assert.Empty(widget.GetMaterials());
            using var requestAfterDispose = new InspectResourceRequest([firstName]);
            project.ProcessRequest(requestAfterDispose, tab);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(2, activationCount);
        }
        finally
        {
            inspector?.Dispose();
            tab?.Dispose();
            _fixture.Resources.ReleaseResource<Material>(secondName, true);
            _fixture.Resources.ReleaseResource<Material>(firstName, true);
            second?.Dispose();
            first?.Dispose();
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
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static Material CreateMaterial(string name, string absoluteFileName, Technique technique)
    {
        var material = new Material();
        material.SetName(name);
        material.SetAbsoluteFileName(absoluteFileName);
        material.SetNumTechniques(1);
        material.SetTechnique(0, technique);
        return material;
    }

    private static void MountCompleteCoreData(
        VirtualFileSystem virtualFileSystem, string currentCoreDataPath)
    {
        MountPoint? currentCoreDataMount = null;
        for (var index = 0u; index < virtualFileSystem.NumMountPoints(); ++index)
        {
            if (virtualFileSystem.GetMountPoint(index) is MountedDirectory directory
                && string.Equals(
                    directory.GetDirectory(), currentCoreDataPath, StringComparison.Ordinal))
            {
                currentCoreDataMount = directory;
                break;
            }
        }

        Assert.NotNull(currentCoreDataMount);
        virtualFileSystem.Unmount(currentCoreDataMount);
        var completeCoreDataMount = virtualFileSystem.MountDir(RbfxTestData.CoreData);
        virtualFileSystem.MountAlias("res:CoreData", completeCoreDataMount);
    }

    private static void RenderInspectorFrame(MaterialInspector inspector, Activation activation)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(800.0f, 1000.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(720.0f, 920.0f));
            var isOpen = true;
            ImGui.Begin("Material Inspector"u8, ref isOpen);
            windowStarted = true;

            inspector.RenderContent();

            if (activation == Activation.ToggleAlphaToCoverage)
            {
                using var properties = new IdScopeGuard("RenderProperties"u8);
                using var property = new IdScopeGuard("Alpha To Coverage"u8);
                ImGui.ActivateItemByID(ImGui.GetID(ReadOnlySpan<byte>.Empty));
            }
            else if (activation == Activation.ForceSave)
                ImGui.ActivateItemByID(ImGui.GetID("\uf0c7 Force Save"u8));
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

    private static void SetField<T>(object instance, string name, T value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private sealed class TestEditorTab : EditorTab
    {
        public TestEditorTab()
            : base("Material Inspector Test", "c99060c3-6419-49c9-b663-86c803807e67",
                EditorTabFlag.None, EditorTabPlacement.Floating)
        {
        }
    }

    private enum Activation
    {
        None,
        ToggleAlphaToCoverage,
        ForceSave,
    }
}
