namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Assets;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;
using RedBolt.Utility;

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

    [NativeBindingFact]
    public void Project_reset_layout_docks_tabs_and_opens_defaults()
    {
        WithProject((project, _) =>
        {
            var context = ImGui.CreateContext();
            var frameStarted = false;
            try
            {
                ImGui.SetCurrentContext(context);
                using (var io = ImGui.GetIO())
                {
                    io.DisplaySize = new Vector2f(1000.0f, 800.0f);
                    io.DeltaTime = 1.0f / 60.0f;
                    io.ConfigFlags |= ImGuiConfigFlags.ImGuiConfigFlagsDockingEnable;
                    io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                }

                ImGui.NewFrame();
                frameStarted = true;

                var dockspaceId = ImGui.GetID("Project dockspace test"u8);
                ImGui.DockSpace(dockspaceId);
                typeof(Project).GetField("_dockspaceId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(project, dockspaceId);

                var tabs = (List<EditorTab>)typeof(Project)
                    .GetField("_tabs", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(project)!;
                var center = AddTab(tabs, "Center", EditorTabPlacement.DockCenter);
                var left = AddTab(tabs, "Left", EditorTabPlacement.DockLeft);
                var right = AddTab(tabs, "Right", EditorTabPlacement.DockRight);
                var bottom = AddTab(tabs, "Bottom", EditorTabPlacement.DockBottom);
                var closed = new EditorTab(
                    "Closed", "ProjectLayerTests.Closed", EditorTabFlag.None, EditorTabPlacement.Floating);
                tabs.Add(closed);

                typeof(Project).GetMethod("ResetLayout", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(project, null);

                center.Render();
                left.Render();
                right.Render();
                bottom.Render();
                closed.Render();

                Assert.True(center.IsOpen);
                Assert.True(left.IsOpen);
                Assert.True(right.IsOpen);
                Assert.True(bottom.IsOpen);
                Assert.False(closed.IsOpen);

                using var rootNode = ImGui.DockBuilderGetNode(dockspaceId);
                using var centerWindow = FindWindow(center);
                using var leftWindow = FindWindow(left);
                using var rightWindow = FindWindow(right);
                using var bottomWindow = FindWindow(bottom);
                using var closedWindow = FindWindow(closed);
                using var centerNode = ImGui.DockBuilderGetNode(centerWindow.DockId);
                using var leftNode = ImGui.DockBuilderGetNode(leftWindow.DockId);
                using var rightNode = ImGui.DockBuilderGetNode(rightWindow.DockId);
                using var bottomNode = ImGui.DockBuilderGetNode(bottomWindow.DockId);

                Assert.False(rootNode.IsNull);
                Assert.True(rootNode.IsSplitNode());
                Assert.Equal(new Vector2f(1000.0f, 800.0f), rootNode.Size);
                Assert.Equal(4, new[]
                {
                    centerWindow.DockId,
                    leftWindow.DockId,
                    rightWindow.DockId,
                    bottomWindow.DockId,
                }.Distinct().Count());
                Assert.True(leftNode.Rect().Max.X <= centerNode.Rect().Min.X);
                Assert.True(rightNode.Rect().Min.X >= centerNode.Rect().Max.X);
                Assert.True(bottomNode.Rect().Min.Y >= centerNode.Rect().Max.Y);
                Assert.True(closedWindow.IsNull);
            }
            finally
            {
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(context);
            }
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

    [Fact]
    public void Model_importer_reflects_native_defaults_and_accepts_only_supported_source_extensions()
    {
        _fixture.Context.RegisterAssembly(typeof(ModelImporter).Assembly);
        WithProject((_, _) =>
        {
            using var importer = Assert.IsType<ModelImporter>(
                _fixture.Context.CreateObject(new StringHash(nameof(ModelImporter))));

            Assert.Equal(
            [
                "Mirror X",
                "Scale",
                "Rotation",
                "Fade Transparency",
                "Cleanup Bone Names",
                "Cleanup Root Nodes",
                "Combine LODs",
                "Skip Tags",
                "Keep Names On Merge",
                "Add Empty Nodes To Skeleton",
                "Repair Looping",
                "Artificial Skin Nodes",
                "Blender: Apply Modifiers",
                "Blender: Deforming Bones Only",
                "LightMap UV: Generate",
                "LightMap UV: Texels per Unit",
                "LightMap UV: Channel",
            ], importer.GetAttributes().Select(static attribute => attribute.Name));

            Assert.Equal(1.0f, importer.GetAttributes()[1].DefaultValue.GetFloat());
            Assert.Equal(["[skip]"], importer.GetAttributes()[7].DefaultValue.GetStringVector());
            Assert.True(importer.GetAttributes()[12].DefaultValue.GetBool());
            Assert.True(importer.GetAttributes()[13].DefaultValue.GetBool());
            Assert.Equal(10.0f, importer.GetAttributes()[15].DefaultValue.GetFloat());
            Assert.Equal(1u, importer.GetAttributes()[16].DefaultValue.GetUInt());

            importer.GetAttributes()[1].Accessor!.SetObject(importer, 2.5f);
            Assert.Equal(2.5f, importer.GetAttributes()[1].Accessor!.GetObject(importer));

            Assert.True(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.gltf" }));
            Assert.True(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.GLB" }));
            Assert.False(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.gltf_" }));
            Assert.False(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.prefab" }));
            Assert.False(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.fbx" }));
            Assert.False(importer.IsApplicable(new AssetTransformerInput { ResourceName = "Models/Box.blend" }));
        });
    }

    [Fact]
    public void Model_importer_executes_a_native_gltf_and_saves_the_generated_prefab()
    {
        _fixture.Context.RegisterAssembly(typeof(ModelImporter).Assembly);
        WithProject((project, dataPath) =>
        {
            var modelsPath = Path.Combine(dataPath, "Models");
            Directory.CreateDirectory(modelsPath);
            var sourceFileName = Path.Combine(modelsPath, "Empty.gltf");
            File.WriteAllText(sourceFileName,
                "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[]}],\"nodes\":[]}");
            var parametersPath = sourceFileName + ".d";
            Directory.CreateDirectory(parametersPath);
            File.WriteAllText(Path.Combine(parametersPath, "ModelImporter.json"),
                "{\"params\":{\"scale\":2.0}}");

            var resourceName = "Models/Empty.gltf";
            var tempPath = project.GetRandomTemporaryPath();
            var outputResourceName = resourceName + ".d";
            var outputFileName = tempPath + outputResourceName;
            Directory.CreateDirectory(tempPath);
            var input = new AssetTransformerInput(false, new ApplicationFlavor(),
                resourceName, sourceFileName, 0)
            {
                TempPath = tempPath,
                OutputFileName = outputFileName,
                OutputResourceName = outputResourceName,
            };
            var output = new AssetTransformerOutput();
            using var importer = new ModelImporter();

            Assert.True(importer.Execute(input, output, [importer]), string.Join(
                Environment.NewLine, _fixture.LogSink.Messages.Select(static message => message.Message)));
            Assert.True(File.Exists(Path.Combine(outputFileName, "Prefab.prefab")));
            Assert.Contains("Models/Empty.gltf.d/ModelImporter.json", output.DependencyModificationTimes.Keys);
        });
    }

    [Fact]
    public void Model_importer_resets_root_motion_and_preserves_the_original_track()
    {
        using var animation = new Animation();
        animation.SetName("Walk.ani");
        var rootTrack = animation.CreateTrack("Root");
        rootTrack.ChannelMask = AnimationChannel.Position;
        rootTrack.KeyFrames.Add(new AnimationKeyFrame(0.0f, Vector3f.Zero));
        rootTrack.KeyFrames.Add(new AnimationKeyFrame(1.0f, new Vector3f(2.0f, 0.0f, 0.0f)));

        using var parentTracks = new StringVariantMap();
        using (var parent = new Variant(string.Empty))
            parentTracks.Add("Root", parent);
        using (var metadata = new Variant(parentTracks))
            animation.AddMetadata(AnimationMetadata.ParentTracks, metadata);

        using var importer = new ModelImporterProbe();
        importer.ResetRootMotion(animation);

        var originalTrack = animation.GetTrack("Root_Original");
        Assert.NotNull(originalTrack);
        Assert.Equal(Vector3f.Zero, originalTrack!.KeyFrames[0].Position);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), originalTrack.KeyFrames[1].Position);
        Assert.All(rootTrack.KeyFrames, static frame => Assert.Equal(Vector3f.Zero, frame.Position));
        Assert.Equal("Root", animation.GetMetadata(AnimationMetadata.RootTrack).GetString());
        Assert.Equal("Root_Original", animation.GetMetadata(AnimationMetadata.OriginalRootTrack).GetString());
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f),
            animation.GetMetadata(AnimationMetadata.RootLinearVelocity).GetVector3());
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

    private static EditorTab AddTab(List<EditorTab> tabs, string title, EditorTabPlacement placement)
    {
        var tab = new EditorTab(
            title, $"ProjectLayerTests.{title}", EditorTabFlag.OpenByDefault, placement);
        tabs.Add(tab);
        return tab;
    }

    private static ImGuiWindow FindWindow(EditorTab tab) =>
        ImGui.FindWindowByName(Encoding.UTF8.GetBytes(tab.UniqueId));

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

    private sealed class ModelImporterProbe : ModelImporter
    {
        public void ResetRootMotion(Animation animation)
        {
            var info = new ResetRootMotionInfo();
            ResetRootMotion(animation, info);
        }
    }
}
