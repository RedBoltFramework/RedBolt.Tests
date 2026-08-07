namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class PrefabInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public PrefabInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Prefab_selection_creates_inspector_and_framed_rendered_preview()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltPrefabInspector-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        using (var sourceNode = new Node())
        using (var sourcePrefab = new PrefabResource())
        using (var buffer = new VectorBuffer())
        {
            sourceNode.SetName("Source");
            var sourceModel = sourceNode.CreateComponent<StaticModel>();
            sourceModel.SetModel(_fixture.Resources.Get<Model>("Models/Box.mdl"));
            sourceNode.GeneratePrefab(sourcePrefab.GetMutableNodePrefab());
            Assert.True(sourcePrefab.Save(buffer, InternalResourceFormat.Json));
            File.WriteAllBytes(Path.Combine(dataPath, "Test.prefab"), buffer.GetBuffer());
        }
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        PrefabInspector? inspector = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            MountCompleteCoreData(virtualFileSystem, project.CoreDataPath);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".prefab"))
                    descriptor.AddObjectType<PrefabResource>();
            });

            inspector = new PrefabInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            using (var request = new InspectResourceRequest(["Test.prefab"]))
            {
                project.ProcessRequest(request);
                typeof(Project).GetMethod("ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(project, null);
            }

            Assert.Equal(1, activationCount);
            var widget = Assert.IsType<ModelInspectorWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var prefab = Assert.IsType<PrefabResource>(Assert.Single(widget.GetResources()));
            Assert.Equal("Test.prefab", prefab.GetName());
            Assert.False(widget.CanSave());

            var preview = Assert.IsType<SceneWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var scene = Assert.IsType<RedBolt.Scene.Scene>(preview.GetScene());
            var prefabNode = Assert.IsType<Node>(scene.GetChild("Prefab"));
            var staticModel = Assert.IsType<StaticModel>(prefabNode.GetComponent<StaticModel>());
            Assert.Equal("Models/Box.mdl", staticModel.Model!.GetName());

            var sourceBounds = staticModel.Model.GetBoundingBox();
            var sourceRadius = sourceBounds.Size.Length * 0.5f;
            var expectedScale = 10.0f / sourceRadius;
            Assert.True(prefabNode.GetScale().ApproximatelyEquivalent(Vector3f.One * expectedScale, 0.0001f));
            Assert.True(prefabNode.GetPosition().ApproximatelyEquivalent(-sourceBounds.Center * expectedScale, 0.0001f));

            var camera = Assert.IsType<Camera>(preview.GetCamera());
            var minFov = MathF.Min(
                camera.GetFov(),
                2.0f * MathDefs.Atan(MathDefs.Tan(camera.GetFov() * 0.5f) * camera.GetAspectRatio()));
            var baseDistance = 10.0f / MathDefs.Tan(minFov * 0.5f);
            var moveAndOrbit = Assert.IsType<MoveAndOrbitComponent>(camera.Node.GetComponent<MoveAndOrbitComponent>());
            Assert.True(MathDefs.Equals(baseDistance * 0.1f, moveAndOrbit.GetMinDistance(), 0.0001f));
            Assert.True(MathDefs.Equals(baseDistance * 1.9f, moveAndOrbit.GetMaxDistance(), 0.0001f));
            Assert.True(MathDefs.Equals(baseDistance * 1.1f, camera.Node.GetPosition().Length, 0.0001f));
            var expectedCameraDirection = new Quaternion(-30.0f, Vector3f.Up)
                * (new Quaternion(20.0f, Vector3f.Forward) * Vector3f.Right);
            Assert.True(camera.Node.GetPosition().Normalized.ApproximatelyEquivalent(expectedCameraDirection, 0.0001f));

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(128.0f, 128.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(96.0f, 96.0f));
                var isOpen = true;
                ImGui.Begin("Prefab Inspector"u8, ref isOpen);
                windowStarted = true;

                preview.RenderContent();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }
            Assert.True(preview.GetRenderer()!.Texture.Width > 0);
            Assert.True(preview.GetRenderer()!.Texture.Height > 0);

            inspector.Dispose();
            inspector = null;
            Assert.Empty(widget.GetResources());
            Assert.Null(preview.GetScene());
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
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static void MountCompleteCoreData(VirtualFileSystem virtualFileSystem, string currentCoreDataPath)
    {
        MountPoint? currentCoreDataMount = null;
        for (var i = 0u; i < virtualFileSystem.NumMountPoints(); ++i)
        {
            if (virtualFileSystem.GetMountPoint(i) is MountedDirectory directory
                && string.Equals(directory.GetDirectory(), currentCoreDataPath, StringComparison.Ordinal))
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
}
