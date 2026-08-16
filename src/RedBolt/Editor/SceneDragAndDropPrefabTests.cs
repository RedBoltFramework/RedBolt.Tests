// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneDragAndDropPrefabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneDragAndDropPrefabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_creates_places_cancels_and_commits_model_and_prefab_nodes()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneDragAndDropPrefabTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var boxModel = _fixture.Resources.Get<Model>("Models/Box.mdl");
        var animatedModel = _fixture.Resources.Get<Model>("Models/Jack.mdl");
        var defaultMaterial = _fixture.Resources.Get<Material>("Materials/DefaultWhite.xml");
        Assert.Equal(0, boxModel.GetSkeleton().GetNumBones());
        Assert.True(animatedModel.GetSkeleton().GetNumBones() > 0);
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TestSceneViewTab? tab = null;
        SceneDragAndDropPrefab? addon = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            var prefab = new PrefabResource { Name = "Prefabs/Tree.prefab" };
            using (var sourceNode = new Node())
            {
                var child = sourceNode.CreateChild("GeneratedModel");
                child.CreateComponent<StaticModel>().SetModel(boxModel);
                sourceNode.GeneratePrefab(prefab.GetMutableNodePrefab());
            }
            Assert.True(_fixture.Resources.AddManualResource(prefab));

            tab = new TestSceneViewTab();
            addon = new SceneDragAndDropPrefab(tab);
            Assert.Equal("DragAndDropPrefab", addon.GetUniqueName());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/PrefabDrop.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "PrefabDrop.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            var octree = resource.Scene.CreateComponent<Octree>();
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            using var emptyPayload = new ResourceDragDropPayload();
            Assert.False(addon.IsDragDropPayloadSupported(page, null));
            Assert.False(addon.IsDragDropPayloadSupported(page, emptyPayload));
            emptyPayload.Resources.Add(new ResourceFileDescriptor());
            emptyPayload.Resources[0].AddObjectType<Material>();
            Assert.False(addon.IsDragDropPayloadSupported(page, emptyPayload));

            var groundNode = page.Scene.CreateChild("Ground");
            var ground = groundNode.CreateComponent<CustomGeometry>();
            ground.BeginGeometry(0u, PrimitiveType.TriangleList);
            ground.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
            ground.DefineVertex(new Vector3f(1.0f, 0.0f, 0.0f));
            ground.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
            ground.Commit();
            octree.Update(new FrameInfo());

            using (var modelPayload = CreatePayload<Model>("Box.mdl", boxModel.Name))
            {
                Assert.True(addon.IsDragDropPayloadSupported(page, modelPayload));
                addon.BeginDragDrop(page, modelPayload);

                var modelNode = Assert.IsType<Node>(page.Scene.GetChild("Box"));
                var staticModel = Assert.IsType<StaticModel>(modelNode.GetComponent<StaticModel>());
                Assert.Same(boxModel, staticModel.GetModel());
                Assert.True(staticModel.GetCastShadows());
                for (var i = 0u; i < staticModel.GetNumGeometries(); ++i)
                    Assert.Same(defaultMaterial, staticModel.GetMaterial(i));

                page.CameraRay = new Ray(
                    new Vector3f(20.0f, 20.0f, -5.0f), Vector3f.Forward);
                addon.UpdateDragDrop(modelPayload);
                Assert.True(modelNode.GetPosition().ApproximatelyEquivalent(
                    new Vector3f(20.0f, 20.0f, 5.0f)));

                page.CameraRay = new Ray(
                    new Vector3f(0.25f, 0.25f, 2.0f),
                    new Vector3f(0.0f, 0.0f, -1.0f));
                addon.UpdateDragDrop(modelPayload);
                Assert.True(modelNode.GetPosition().ApproximatelyEquivalent(
                    new Vector3f(0.25f, 0.25f, 0.0f)));
                Assert.True(modelNode.GetRotation().Equivalent(Quaternion.IDENTITY));

                addon.CancelDragDrop();
                Assert.Null(page.Scene.GetChild("Box"));
            }

            using (var animatedPayload = CreatePayload<Model>("Jack.mdl", animatedModel.Name))
            {
                addon.BeginDragDrop(page, animatedPayload);
                var animatedNode = Assert.IsType<Node>(page.Scene.GetChild("Jack"));
                var drawable = Assert.IsType<AnimatedModel>(
                    animatedNode.GetComponent<AnimatedModel>());
                Assert.Same(animatedModel, drawable.GetModel());
                Assert.True(drawable.GetCastShadows());
                addon.CancelDragDrop();
                Assert.Null(page.Scene.GetChild("Jack"));
            }

            using (var prefabPayload = CreatePayload<PrefabResource>("Tree.prefab", prefab.Name))
            {
                Assert.True(addon.IsDragDropPayloadSupported(page, prefabPayload));
                addon.BeginDragDrop(page, prefabPayload);
                var prefabNode = Assert.IsType<Node>(page.Scene.GetChild("Tree"));
                Assert.Same(prefab,
                    Assert.IsType<PrefabReference>(prefabNode.GetComponent<PrefabReference>())
                        .GetPrefab());
                var generatedNode = Assert.IsType<Node>(
                    prefabNode.GetChild("GeneratedModel"));
                Assert.NotNull(generatedNode.GetComponent<StaticModel>());

                page.CameraRay = new Ray(
                    new Vector3f(0.25f, 0.25f, 2.0f),
                    new Vector3f(0.0f, 0.0f, -1.0f));
                octree.Update(new FrameInfo());
                addon.UpdateDragDrop(prefabPayload);
                Assert.True(prefabNode.GetPosition().ApproximatelyEquivalent(
                    new Vector3f(0.25f, 0.25f, 0.0f)));

                project.UndoManager.NewFrame();
                addon.CompleteDragDrop(prefabPayload);
                Assert.True(page.Selection.IsSelected(prefabNode));

                Assert.True(project.UndoManager.Undo());
                Assert.Null(page.Scene.GetChild("Tree"));
                Assert.True(project.UndoManager.Redo());
                var restoredNode = Assert.IsType<Node>(page.Scene.GetChild("Tree"));
                Assert.NotNull(restoredNode.GetComponent<PrefabReference>());
            }

            addon.CancelDragDrop();
            addon.Dispose();
            addon = null;
        }
        finally
        {
            addon?.Dispose();
            tab?.Dispose();
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
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static ResourceDragDropPayload CreatePayload<T>(
        string localName, string resourceName)
    {
        var payload = new ResourceDragDropPayload();
        var descriptor = new ResourceFileDescriptor
        {
            LocalName = localName,
            ResourceName = resourceName,
        };
        descriptor.AddObjectType<T>();
        payload.Resources.Add(descriptor);
        return payload;
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
