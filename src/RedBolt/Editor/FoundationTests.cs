namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Math;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class FoundationTests
{
    private readonly HeadlessEngineFixture _fixture;

    public FoundationTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Hierarchy_browser_source_defaults_are_no_ops_and_owner_is_forwarded()
    {
        HierarchyBrowserSource source = new TestHierarchyBrowserSource();

        Assert.Null(source.GetOwnerTab());
        source.RenderContent();
        source.RenderContextMenuItems();
        source.RenderMenu();
        source.ApplyHotkeys(null);
    }

    [Fact]
    public void Inspector_source_defaults_are_no_ops_and_activation_is_signaled()
    {
        var activated = false;
        var concreteSource = new TestInspectorSource();
        InspectorSource source = concreteSource;
        source.OnActivated += () => activated = true;

        Assert.Null(source.GetOwnerTab());
        Assert.False(source.IsUndoSupported());
        source.RenderContent();
        source.RenderContextMenuItems();
        source.RenderMenu();
        source.ApplyHotkeys(null);
        concreteSource.Activate();

        Assert.True(activated);
    }

    [Fact]
    public void Hierarchy_browser_tab_forwards_to_the_connected_source()
    {
        WithProject((project, _) =>
        {
            using var tab = new HierarchyBrowserTab();
            using var source = new TestHierarchyBrowserSource(tab);

            tab.ConnectToSource(source);
            tab.RenderContent();
            tab.RenderContextMenuItems();
            tab.RenderMenu();
            tab.ApplyHotkeys(project.HotkeyManager);

            Assert.Same(tab, tab.GetOwnerTab());
            Assert.Equal(1, source.RenderContentCount);
            Assert.Equal(1, source.RenderContextMenuItemsCount);
            Assert.Equal(1, source.RenderMenuCount);
            Assert.Equal(1, source.ApplyHotkeysCount);
            Assert.Same(project.HotkeyManager, source.HotkeyManager);

            tab.ConnectToSource(null, null);
            tab.RenderContent();

            Assert.Null(tab.GetOwnerTab());
            Assert.Equal(1, source.RenderContentCount);
        });
    }

    [NativeBindingFact]
    public void Inspector_tab_connects_registered_addons_on_activation_and_forwards_behavior()
    {
        WithProject((project, _) =>
        {
            var tab = new InspectorTab();
            var source = tab.RegisterAddon(new TestActiveInspectorSource(tab));

            Assert.False(tab.IsUndoSupported());
            Assert.Null(tab.GetOwnerTab());

            source.Activate();
            tab.RenderContent();
            tab.RenderContextMenuItems();
            tab.RenderMenu();

            var imguiContext = ImGui.CreateContext();
            var frameStarted = false;
            try
            {
                ImGui.SetCurrentContext(imguiContext);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(640.0f, 480.0f);
                io.DeltaTime = 1.0f / 60.0f;
                io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                ImGui.NewFrame();
                frameStarted = true;

                tab.ApplyHotkeys(project.HotkeyManager);
            }
            finally
            {
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(imguiContext);
            }

            Assert.True(tab.IsUndoSupported());
            Assert.Same(tab, tab.GetOwnerTab());
            Assert.Equal(1, source.RenderContentCount);
            Assert.Equal(1, source.RenderContextMenuItemsCount);
            Assert.Equal(1, source.RenderMenuCount);
            Assert.Equal(1, source.ApplyHotkeysCount);
            Assert.Same(project.HotkeyManager, source.HotkeyManager);

            tab.ConnectToSource(null, null);
            tab.RenderContent();

            Assert.False(tab.IsUndoSupported());
            Assert.Null(tab.GetOwnerTab());
            Assert.Equal(1, source.RenderContentCount);

            tab.Dispose();
            Assert.True(source.IsDisposed);
        });
    }

    [Fact]
    public void Empty_inspector_activates_only_as_the_lowest_priority_inspect_request_sink()
    {
        WithProject((project, _) =>
        {
            var inspector = new EmptyInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            using (var inspectRequest = new BaseInspectRequest())
            {
                project.ProcessRequest(inspectRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);

            var higherPriorityProcessed = false;
            using (var claimedRequest = new BaseInspectRequest())
            {
                claimedRequest.QueueProcessCallback(() => higherPriorityProcessed = true);
                project.ProcessRequest(claimedRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.True(higherPriorityProcessed);
            Assert.Equal(1, activationCount);

            using (var otherRequest = new ProjectRequest())
            {
                project.ProcessRequest(otherRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);

            inspector.Dispose();
            using var requestAfterDispose = new BaseInspectRequest();
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(1, activationCount);
        });
    }

    [Fact]
    public void Placeholder_resource_inspector_tracks_non_empty_resource_selections_at_fallback_priority()
    {
        WithProject((project, dataPath) =>
        {
            File.WriteAllText(Path.Combine(dataPath, "Resource.txt"), "resource");
            Directory.CreateDirectory(Path.Combine(dataPath, "Folder"));

            var inspector = new PlaceholderResourceInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            var processPendingRequests = typeof(Project).GetMethod(
                "ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;

            using (var emptyRequest = new InspectResourceRequest([]))
            {
                project.ProcessRequest(emptyRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(0, activationCount);

            using (var singleRequest = new InspectResourceRequest(["Resource.txt"]))
            {
                project.ProcessRequest(singleRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);

            var singleResource = GetPrivateField(inspector, "_singleResource");
            Assert.NotNull(singleResource);
            Assert.Equal("File", GetProperty<string>(singleResource, "ResourceType"));
            Assert.Equal("Resource.txt", GetProperty<string>(singleResource, "ResourceName"));
            Assert.Null(GetPrivateField(inspector, "_multipleResources"));

            var higherPriorityProcessed = false;
            using (var claimedRequest = new InspectResourceRequest(["Folder"]))
            {
                claimedRequest.QueueProcessCallback(() => higherPriorityProcessed = true, int.MinValue + 2);
                project.ProcessRequest(claimedRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.True(higherPriorityProcessed);
            Assert.Equal(1, activationCount);

            using (var multipleRequest = new InspectResourceRequest(["Resource.txt", "Folder"]))
            {
                project.ProcessRequest(multipleRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Null(GetPrivateField(inspector, "_singleResource"));

            var multipleResources = GetPrivateField(inspector, "_multipleResources");
            Assert.NotNull(multipleResources);
            Assert.Equal(1u, GetProperty<uint>(multipleResources, "NumFiles"));
            Assert.Equal(1u, GetProperty<uint>(multipleResources, "NumFolders"));

            inspector.Dispose();
            using var requestAfterDispose = new InspectResourceRequest(["Resource.txt"]);
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(2, activationCount);
        });
    }

    private void WithProject(Action<Project, string> test)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltFoundation-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        try
        {
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            test(project, dataPath);
        }
        finally
        {
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            Directory.Delete(projectPath, true);
        }
    }

    private static object? GetPrivateField(object instance, string name)
    {
        return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    }

    private static T GetProperty<T>(object instance, string name)
    {
        return (T)instance.GetType().GetProperty(name)!.GetValue(instance)!;
    }

    private sealed class TestHierarchyBrowserSource : RedObject, HierarchyBrowserSource
    {
        public TestHierarchyBrowserSource(EditorTab? owner = null)
        {
            _owner = owner;
        }

        public EditorTab? GetOwnerTab() => _owner;

        public void RenderContent() => ++RenderContentCount;

        public void RenderContextMenuItems() => ++RenderContextMenuItemsCount;

        public void RenderMenu() => ++RenderMenuCount;

        public void ApplyHotkeys(HotkeyManager? hotkeyManager)
        {
            ++ApplyHotkeysCount;
            HotkeyManager = hotkeyManager;
        }

        public int RenderContentCount { get; private set; }

        public int RenderContextMenuItemsCount { get; private set; }

        public int RenderMenuCount { get; private set; }

        public int ApplyHotkeysCount { get; private set; }

        public HotkeyManager? HotkeyManager { get; private set; }

        private readonly EditorTab? _owner;
    }

    private sealed class TestInspectorSource : InspectorSource
    {
        public event Action? OnActivated;

        public void Activate() => OnActivated?.Invoke();
    }

    private sealed class TestActiveInspectorSource : RedObject, InspectorSource
    {
        public TestActiveInspectorSource(EditorTab owner)
        {
            _owner = owner;
        }

        public event Action? OnActivated;

        public EditorTab? GetOwnerTab() => _owner;

        public bool IsUndoSupported() => true;

        public void RenderContent() => ++RenderContentCount;

        public void RenderContextMenuItems() => ++RenderContextMenuItemsCount;

        public void RenderMenu() => ++RenderMenuCount;

        public void ApplyHotkeys(HotkeyManager? hotkeyManager)
        {
            ++ApplyHotkeysCount;
            HotkeyManager = hotkeyManager;
        }

        public void Activate() => OnActivated?.Invoke();

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public int RenderContentCount { get; private set; }

        public int RenderContextMenuItemsCount { get; private set; }

        public int RenderMenuCount { get; private set; }

        public int ApplyHotkeysCount { get; private set; }

        public HotkeyManager? HotkeyManager { get; private set; }

        public bool IsDisposed { get; private set; }

        private readonly EditorTab _owner;
    }
}
