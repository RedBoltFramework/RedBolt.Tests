namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using RedBolt.Audio;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using Archive = RedBolt.IO.Archive;
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

    [NativeBindingFact]
    public void Sound_inspector_claims_sound_resources_reuses_the_selection_and_renders_playback()
    {
        WithProject((project, dataPath) =>
        {
            File.WriteAllBytes(Path.Combine(dataPath, "B.raw"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(dataPath, "A.raw"), [5, 6, 7, 8]);
            File.WriteAllText(Path.Combine(dataPath, "Other.txt"), "other");
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".raw"))
                    descriptor.AddObjectType<Sound>();
            });

            var inspector = new SoundInspector(project);
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

            using (var soundsRequest = new InspectResourceRequest(["B.raw", "A.raw"]))
            {
                project.ProcessRequest(soundsRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);

            var resourceNames = Assert.IsType<List<string>>(GetPrivateField(inspector, "_resourceNames"));
            Assert.Equal(["A.raw", "B.raw"], resourceNames);
            var sounds = Assert.IsType<List<Sound>>(GetPrivateField(inspector, "_sounds"));
            Assert.Equal(["A.raw", "B.raw"], sounds.Select(static sound => sound.GetName()));
            var firstSound = sounds[0];

            using (var sameSoundsRequest = new InspectResourceRequest(["A.raw", "B.raw"]))
            {
                project.ProcessRequest(sameSoundsRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Same(firstSound, sounds[0]);

            var higherPriorityProcessed = false;
            using (var claimedRequest = new InspectResourceRequest(["A.raw"]))
            {
                claimedRequest.QueueProcessCallback(() => higherPriorityProcessed = true, 1);
                project.ProcessRequest(claimedRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.True(higherPriorityProcessed);
            Assert.Equal(2, activationCount);

            var soundSource = Assert.IsType<SoundSource>(GetPrivateField(inspector, "_soundSource"));
            soundSource.Play(firstSound);
            Assert.True(soundSource.IsPlaying());

            var imguiContext = ImGui.CreateContext();
            var frameStarted = false;
            var windowStarted = false;
            try
            {
                ImGui.SetCurrentContext(imguiContext);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(640.0f, 480.0f);
                io.DeltaTime = 1.0f / 60.0f;
                io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                ImGui.NewFrame();
                frameStarted = true;
                var open = true;
                ImGui.Begin("Sound inspector test"u8, ref open);
                windowStarted = true;

                var sliderValue = 0.5f;
                Assert.False(ImGui.SliderFloat("Binding direction"u8, ref sliderValue, 0.0f, 1.0f));
                Assert.Equal(0.5f, sliderValue);
                inspector.RenderContent();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(imguiContext);
            }

            inspector.Dispose();
            using var requestAfterDispose = new InspectResourceRequest(["A.raw"]);
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(2, activationCount);
        });
    }

    [NativeBindingFact]
    public void Settings_tab_selects_the_first_populated_group_and_forwards_page_behavior()
    {
        WithProject((project, _) =>
        {
            var generalPage = new TestSettingsPage("A.Rendering:General");
            var advancedPage = new TestSettingsPage("A.Rendering:Advanced");
            var toolsPage = new TestSettingsPage("ZTools:General");
            project.SettingsManager.AddPage(generalPage);
            project.SettingsManager.AddPage(advancedPage);
            project.SettingsManager.AddPage(toolsPage);
            var firstGroup = project.SettingsManager.PageTree.Children["A"];
            var renderingGroup = firstGroup.Children["Rendering"];
            Assert.Equal(2, renderingGroup.Pages.Count);

            using var tab = new TestSettingsTab();
            Assert.Equal("Settings", tab.Title);
            Assert.Equal(EditorTabFlag.None, tab.Flags);
            Assert.Equal(EditorTabPlacement.DockRight, tab.Placement);
            Assert.False(tab.MarkedUnsaved);

            tab.ApplyHotkeys(project.HotkeyManager);
            Assert.Equal(0, generalPage.ApplyHotkeysCount);
            var renderSettingsSubtree = typeof(SettingsTab).GetMethod(
                "RenderSettingsSubtree", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var renderCurrentGroup = typeof(SettingsTab).GetMethod(
                "RenderCurrentGroup", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var imguiContext = ImGui.CreateContext();
            var frameStarted = false;
            var windowStarted = false;
            try
            {
                ImGui.SetCurrentContext(imguiContext);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(1024.0f, 768.0f);
                io.DeltaTime = 1.0f / 60.0f;
                io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                for (var frame = 0; frame < 2; ++frame)
                {
                    ImGui.NewFrame();
                    frameStarted = true;
                    ImGui.SetNextWindowPos(new Vector2f(0.0f, 0.0f));
                    ImGui.SetNextWindowSize(new Vector2f(600.0f, 600.0f));
                    var open = true;
                    Assert.True(ImGui.Begin("Settings tab test"u8, ref open));
                    windowStarted = true;

                    tab.RenderContent();
                    if (frame == 1)
                    {
                        renderSettingsSubtree.Invoke(tab, [renderingGroup, "Rendering"]);
                        renderCurrentGroup.Invoke(tab, null);
                    }

                    ImGui.End();
                    windowStarted = false;
                    ImGui.EndFrame();
                    frameStarted = false;
                }
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(imguiContext);
            }

            tab.ApplyHotkeys(project.HotkeyManager);
            Assert.Equal(1, generalPage.ApplyHotkeysCount);
            Assert.Equal(1, advancedPage.ApplyHotkeysCount);
            Assert.Equal(0, toolsPage.ApplyHotkeysCount);

            Assert.True(generalPage.RenderSettingsCount >= 1);
            Assert.True(advancedPage.RenderSettingsCount >= 1);
            Assert.Equal(0, toolsPage.RenderSettingsCount);

            project.MarkUnsaved();
            Assert.True(tab.MarkedUnsaved);

            generalPage.Dispose();
            advancedPage.Dispose();
            toolsPage.Dispose();
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

    private sealed class TestSettingsTab : SettingsTab
    {
        public bool MarkedUnsaved => IsMarkedUnsaved();
    }

    private sealed class TestSettingsPage : SettingsPage
    {
        public TestSettingsPage(string uniqueName)
        {
            UniqueName = uniqueName;
        }

        public override string UniqueName { get; }

        public override bool IsSerializable => false;

        public override bool CanResetToDefault => true;

        public override void SerializeInBlock(Archive archive)
        {
        }

        public override void RenderSettings()
        {
            ++RenderSettingsCount;
        }

        public override void ResetToDefaults()
        {
            ++ResetToDefaultsCount;
        }

        public override void ApplyHotkeys(HotkeyManager? hotkeyManager)
        {
            ++ApplyHotkeysCount;
        }

        public int RenderSettingsCount { get; private set; }

        public int ResetToDefaultsCount { get; private set; }

        public int ApplyHotkeysCount { get; private set; }
    }
}
