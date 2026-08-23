// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using RedBolt.Audio;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

using Archive = RedBolt.IO.Archive;

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
            var virtualFileSystem = _fixture.Context.GetSubsystem<RedBolt.IO.VirtualFileSystem>()!;
            var mountedDirectories = Enumerable.Range(0, checked((int)virtualFileSystem.NumMountPoints()))
                .Select(index => virtualFileSystem.GetMountPoint(checked((uint)index))!)
                .OfType<RedBolt.IO.MountedDirectory>()
                .ToArray();
            Assert.DoesNotContain(mountedDirectories, mountPoint =>
                mountPoint.GetDirectory() == Path.GetPathRoot(mountPoint.GetDirectory()));
            var configMountPoint = Assert.Single(
                mountedDirectories.Where(mountPoint => mountPoint.AcceptsScheme("conf")));
            Assert.Equal(
                Path.TrimEndingDirectorySeparator(_fixture.Engine.GetAppPreferencesDir()),
                Path.TrimEndingDirectorySeparator(configMountPoint.GetDirectory()));

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

    [Fact]
    public void Project_preserves_and_watches_module_resource_directories()
    {
        var modulePath = Path.Combine(
            Path.GetTempPath(), "RedBoltModuleResources-" + Guid.NewGuid().ToString("N"), "SampleData.Glow");
        Directory.CreateDirectory(modulePath);
        var virtualFileSystem = _fixture.Context.GetSubsystem<RedBolt.IO.VirtualFileSystem>()!;
        var originalMount = virtualFileSystem.MountDir(modulePath);
        try
        {
            WithProject((_, _) =>
            {
                var mountedDirectory = Assert.Single(
                    Enumerable.Range(0, checked((int)virtualFileSystem.NumMountPoints()))
                        .Select(index => virtualFileSystem.GetMountPoint(checked((uint)index))!)
                        .OfType<RedBolt.IO.MountedDirectory>()
                        .Where(mountPoint => string.Equals(
                            Path.TrimEndingDirectorySeparator(mountPoint.GetDirectory()),
                            Path.GetFullPath(modulePath), StringComparison.Ordinal)));

                Assert.True(mountedDirectory.IsWatching());
            });
        }
        finally
        {
            virtualFileSystem.Unmount(originalMount);
            originalMount.Dispose();
            Directory.Delete(Path.GetDirectoryName(modulePath)!, true);
        }
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
    public void Inspector_with_preview_claims_resources_reuses_selection_and_tracks_edit_boundaries()
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

            var inspector = new TestInspectorWithPreview(project);
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

            using (var soundRequest = new InspectResourceRequest(["B.raw"]))
            {
                project.ProcessRequest(soundRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            Assert.Equal(["B.raw"], inspector.ResourceNames);
            Assert.Equal(1, inspector.MakeInspectorCount);
            Assert.Equal(1, inspector.MakePreviewCount);
            var firstWidget = Assert.IsType<TestResourceInspectorWidget>(inspector.Inspector);
            var firstPreview = Assert.IsType<TestPreviewWidget>(inspector.Preview);

            using (var sameSoundRequest = new InspectResourceRequest(["B.raw"]))
            {
                project.ProcessRequest(sameSoundRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(2, activationCount);
            Assert.Equal(1, inspector.MakeInspectorCount);
            Assert.Same(firstWidget, inspector.Inspector);

            var higherPriorityProcessed = false;
            using (var claimedRequest = new InspectResourceRequest(["B.raw"]))
            {
                claimedRequest.QueueProcessCallback(() => higherPriorityProcessed = true, int.MinValue + 2);
                project.ProcessRequest(claimedRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.True(higherPriorityProcessed);
            Assert.Equal(2, activationCount);

            var beginEdit = typeof(InspectorWithPreview).GetMethod(
                "BeginEdit", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var endEdit = typeof(InspectorWithPreview).GetMethod(
                "EndEdit", BindingFlags.Instance | BindingFlags.NonPublic)!;
            beginEdit.Invoke(inspector, null);
            var pendingAction = inspector.PendingAction;
            Assert.NotNull(pendingAction);
            beginEdit.Invoke(inspector, null);
            Assert.Same(pendingAction, inspector.PendingAction);
            endEdit.Invoke(inspector, null);
            var delayedFileSaves = GetPrivateField(project, "_delayedFileSaves");
            Assert.Equal(1, GetProperty<int>(delayedFileSaves!, "Count"));

            InspectorSource source = inspector;
            Assert.Null(source.GetOwnerTab());
            Assert.False(source.IsUndoSupported());
            source.RenderContextMenuItems();
            source.RenderMenu();
            source.ApplyHotkeys(project.HotkeyManager);

            foreach (var windowHeight in new[] { 900.0f, 420.0f })
            {
                var imguiContext = ImGui.CreateContext();
                var frameStarted = false;
                var windowStarted = false;
                try
                {
                    ImGui.SetCurrentContext(imguiContext);
                    var io = ImGui.GetIO();
                    io.DisplaySize = new Vector2f(640.0f, 1000.0f);
                    io.DeltaTime = 1.0f / 60.0f;
                    io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                    ImGui.NewFrame();
                    frameStarted = true;
                    ImGui.SetNextWindowSize(new Vector2f(360.0f, windowHeight));
                    var open = true;
                    ImGui.Begin("Inspector with preview"u8, ref open);
                    windowStarted = true;

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
            }
            Assert.Equal(2, firstWidget.RenderCount);
            Assert.Equal(2, firstPreview.RenderCount);
            using (var soundsRequest = new InspectResourceRequest(["B.raw", "A.raw"]))
            {
                project.ProcessRequest(soundsRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(3, activationCount);
            Assert.Equal(["A.raw", "B.raw"], inspector.ResourceNames);
            Assert.Equal(2, inspector.MakeInspectorCount);
            Assert.Equal(1, inspector.MakePreviewCount);
            Assert.True(firstWidget.IsDisposed);
            Assert.True(firstPreview.IsDisposed);
            Assert.Null(inspector.Preview);
            inspector.Dispose();
            Assert.True(inspector.InspectorDisposed);
            using var requestAfterDispose = new InspectResourceRequest(["A.raw"]);
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(3, activationCount);
        });
    }

    [Fact]
    public void Animation_inspector_claims_animations_and_creates_the_read_only_widget()
    {
        WithProject((project, dataPath) =>
        {
            using (var animation = new Animation())
            using (var destination = new RedBolt.IO.VectorBuffer())
            {
                animation.SetAnimationName("Test");
                animation.SetLength(2.5f);
                Assert.True(animation.Save(destination));
                File.WriteAllBytes(Path.Combine(dataPath, "Test.ani"), destination.GetBuffer());
            }
            File.WriteAllText(Path.Combine(dataPath, "Other.txt"), "other");
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".ani"))
                    descriptor.AddObjectType<Animation>();
            });

            var inspector = new AnimationInspector(project);
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

            using (var animationRequest = new InspectResourceRequest(["Test.ani"]))
            {
                project.ProcessRequest(animationRequest);
                processPendingRequests.Invoke(project, null);
            }
            Assert.Equal(1, activationCount);
            var widget = Assert.IsType<AnimationInspectorWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var resource = Assert.IsType<Animation>(Assert.Single(widget.GetResources()));
            Assert.Equal("Test.ani", resource.GetName());
            Assert.Equal(2.5f, resource.Length);
            Assert.False(widget.CanSave());
            Assert.Null(typeof(InspectorWithPreview)
                .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(inspector));

            inspector.Dispose();
            Assert.Empty(widget.GetResources());
            using var requestAfterDispose = new InspectResourceRequest(["Test.ani"]);
            project.ProcessRequest(requestAfterDispose);
            processPendingRequests.Invoke(project, null);
            Assert.Equal(1, activationCount);
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

    private sealed class TestInspectorWithPreview : InspectorWithPreview
    {
        public TestInspectorWithPreview(Project project)
            : base(project)
        {
        }

        public IReadOnlyList<string> ResourceNames =>
            (IReadOnlyList<string>)typeof(InspectorWithPreview)
                .GetField("_resourceNames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!;

        public ResourceInspectorWidget? Inspector =>
            (ResourceInspectorWidget?)typeof(InspectorWithPreview)
                .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this);

        public BaseWidget? Preview =>
            (BaseWidget?)typeof(InspectorWithPreview)
                .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this);

        public ModifyResourceAction? PendingAction =>
            (ModifyResourceAction?)typeof(InspectorWithPreview)
                .GetField("_pendingAction", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this);

        public int MakeInspectorCount { get; private set; }

        public int MakePreviewCount { get; private set; }

        public bool InspectorDisposed => _lastInspector?.IsDisposed ?? false;

        protected override StringHash GetResourceType() => typeof(Sound).GetTypeId();

        protected override ResourceInspectorWidget? MakeInspectorWidget(IReadOnlyList<Resource> resources)
        {
            ++MakeInspectorCount;
            _lastInspector = new TestResourceInspectorWidget(resources);
            return _lastInspector;
        }

        protected override BaseWidget? MakePreviewWidget(Resource resource)
        {
            ++MakePreviewCount;
            return new TestPreviewWidget();
        }

        private TestResourceInspectorWidget? _lastInspector;
    }

    private sealed class TestResourceInspectorWidget : ResourceInspectorWidget
    {
        public TestResourceInspectorWidget(IReadOnlyList<Resource> resources)
            : base(resources, Array.Empty<PropertyDesc>())
        {
        }

        public override void RenderContent()
        {
            ++RenderCount;
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public int RenderCount { get; private set; }

        public bool IsDisposed { get; private set; }
    }

    private sealed class TestPreviewWidget : BaseWidget
    {
        public override void RenderContent()
        {
            ++RenderCount;
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public int RenderCount { get; private set; }

        public bool IsDisposed { get; private set; }
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
