namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;

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
}
