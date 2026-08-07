namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Resource;
using RedBolt.SystemUI;

using File = global::System.IO.File;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ProjectLayerTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ProjectLayerTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Resource_requests_resolve_project_descriptors_and_sort_names()
    {
        WithProject((project, dataPath) =>
        {
            File.WriteAllText(Path.Combine(dataPath, "z.txt"), "z");
            File.WriteAllText(Path.Combine(dataPath, "a.txt"), "a");

            using var openRequest = new OpenResourceRequest("z.txt");
            using var inspectRequest = new InspectResourceRequest(["z.txt", "a.txt"]);

            Assert.Equal("z.txt", openRequest.Resource.ResourceName);
            Assert.False(openRequest.Resource.IsDirectory);
            Assert.Equal(["a.txt", "z.txt"], inspectRequest.GetSortedResourceNames());
            Assert.All(inspectRequest.Resources, static descriptor => Assert.False(descriptor.IsDirectory));
        });
    }

    [Fact]
    public void Resource_tab_tracks_save_frames_and_focuses_resource_during_undo_redo()
    {
        WithProject((project, _) =>
        {
            using var tab = new TestResourceTab();
            tab.OpenResource("B.resource");
            tab.OpenResource("A.resource");
            tab.SetCurrentAction("A.resource", 7);

            Assert.True(tab.IsResourceUnsaved("A.resource"));
            tab.SaveResource("A.resource");
            Assert.False(tab.IsResourceUnsaved("A.resource"));
            Assert.Equal(["A.resource"], tab.SavedResources);

            var action = new TestAction();
            tab.PushAction(action);
            tab.SetActiveResource("B.resource");

            Assert.True(project.UndoManager.Undo());
            Assert.Equal(1, action.UndoCount);
            Assert.Equal("A.resource", tab.ActiveResourceName);
            Assert.False(tab.IsResourceUnsaved("A.resource"));

            Assert.True(project.UndoManager.Redo());
            Assert.Equal(1, action.RedoCount);
            Assert.Equal("A.resource", tab.ActiveResourceName);
            Assert.True(tab.IsResourceUnsaved("A.resource"));
        });
    }

    [Fact]
    public void Project_file_ignore_patterns_match_complete_file_names()
    {
        WithProject((project, _) =>
        {
            Assert.True(project.IsFileNameIgnored("Settings.user.json"));
            Assert.True(project.IsFileNameIgnored("Models/Model.blend1"));
            Assert.False(project.IsFileNameIgnored("Models/Model.blend"));

            project.IgnoreFileNamePattern("Temp/?a*.tmp");
            Assert.True(project.IsFileNameIgnored("Temp/value.tmp"));
            Assert.False(project.IsFileNameIgnored("Temp/a.tmp"));
        });
    }

    [Fact]
    public void Asset_manager_initializes_read_only_and_processes_an_empty_batch_synchronously()
    {
        WithProject((project, _) =>
        {
            var initialized = false;
            project.AssetManager.OnInitialized += _ => initialized = true;
            project.AssetManager.Initialize(true);

            var callbackInvoked = false;
            project.AssetManager.ProcessAssetBatch([], (input, output, message) =>
            {
                callbackInvoked = true;
                Assert.Empty(input);
                Assert.Empty(output);
                Assert.Empty(message);
            });

            Assert.True(initialized);
            Assert.True(callbackInvoked);
            Assert.False(project.AssetManager.IsProcessing);
            Assert.Equal(new AssetManager.ProgressInfo(), project.AssetManager.Progress);
        });
    }

    [Fact]
    public void Modify_resource_action_captures_and_restores_cached_resource_data()
    {
        WithProject((project, dataPath) =>
        {
            using var resource = new JSONFile();
            resource.SetName("State.json");
            resource.SetAbsoluteFileName(Path.Combine(dataPath, "State.json"));
            SetJsonValue(resource, 1);
            Assert.True(_fixture.Resources.AddManualResource(resource));

            var action = new ModifyResourceAction(project);
            action.AddResource(resource);
            SetJsonValue(resource, 2);
            action.Complete(false);

            action.Undo();
            Assert.Equal(1, resource.Root.Get("value").GetInt());

            action.Redo();
            Assert.Equal(2, resource.Root.Get("value").GetInt());
        });
    }

    private void WithProject(Action<Project, string> test)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltProjectLayer-" + Guid.NewGuid().ToString("N"));
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

    private static void SetJsonValue(JSONFile file, int value)
    {
        using var jsonValue = new JSONValue(value);
        file.Root.Set("value", jsonValue);
    }

    private sealed class TestResourceTab()
        : ResourceEditorTab("Resources", "ProjectLayerTests", EditorTabFlag.None, EditorTabPlacement.Floating)
    {
        public List<string> SavedResources { get; } = [];

        public override bool CanOpenResource(ResourceFileDescriptor descriptor) => true;

        public override bool SupportMultipleResources() => true;

        protected override void OnResourceLoaded(string resourceName)
        {
        }

        protected override void OnResourceUnloaded(string resourceName)
        {
        }

        protected override void OnActiveResourceChanged(string oldResourceName, string newResourceName)
        {
        }

        protected override void OnResourceSaved(string resourceName)
        {
            SavedResources.Add(resourceName);
        }

        protected override void OnResourceShallowSaved(string resourceName)
        {
        }
    }

    private sealed class TestAction : EditorAction
    {
        public int RedoCount { get; private set; }

        public int UndoCount { get; private set; }

        public override void Redo() => ++RedoCount;

        public override void Undo() => ++UndoCount;
    }
}
