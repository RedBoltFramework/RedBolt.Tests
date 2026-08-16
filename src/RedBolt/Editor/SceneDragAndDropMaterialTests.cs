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
public sealed class SceneDragAndDropMaterialTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneDragAndDropMaterialTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_previews_reverts_and_commits_undoable_material_assignment()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneDragAndDropMaterialTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var model = _fixture.Resources.Get<Model>("Models/Box.mdl");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TestSceneViewTab? tab = null;
        SceneDragAndDropMaterial? addon = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            var oldMaterial = new Material { Name = "Materials/Old.xml" };
            var newMaterial = new Material { Name = "Materials/New.xml" };
            Assert.True(_fixture.Resources.AddManualResource(oldMaterial));
            Assert.True(_fixture.Resources.AddManualResource(newMaterial));

            tab = new TestSceneViewTab();
            addon = new SceneDragAndDropMaterial(tab);
            Assert.Equal("DragAndDropMaterial", addon.GetUniqueName());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/MaterialDrop.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "MaterialDrop.scene"),
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
            emptyPayload.Resources[0].AddObjectType<Animation>();
            Assert.False(addon.IsDragDropPayloadSupported(page, emptyPayload));

            using var payload = new ResourceDragDropPayload();
            var descriptor = new ResourceFileDescriptor
            {
                LocalName = "New.xml",
                ResourceName = newMaterial.Name,
            };
            descriptor.AddObjectType<Material>();
            payload.Resources.Add(descriptor);
            Assert.True(addon.IsDragDropPayloadSupported(page, payload));

            var staticModel = page.Scene.CreateChild("Model").CreateComponent<StaticModel>();
            staticModel.SetModel(model);
            staticModel.SetMaterial(oldMaterial);
            octree.Update(new FrameInfo());
            var hitRay = new Ray(new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Forward);
            var missRay = new Ray(new Vector3f(20.0f, 20.0f, -5.0f), Vector3f.Forward);

            page.CameraRay = hitRay;
            addon.BeginDragDrop(page, payload);
            addon.UpdateDragDrop(payload);
            Assert.Same(newMaterial, staticModel.GetMaterial());

            page.CameraRay = missRay;
            addon.UpdateDragDrop(payload);
            Assert.Same(oldMaterial, staticModel.GetMaterial());

            page.CameraRay = hitRay;
            addon.UpdateDragDrop(payload);
            Assert.Same(newMaterial, staticModel.GetMaterial());
            addon.CancelDragDrop();
            Assert.Same(oldMaterial, staticModel.GetMaterial());

            addon.BeginDragDrop(page, payload);
            addon.UpdateDragDrop(payload);
            project.UndoManager.NewFrame();
            addon.CompleteDragDrop(payload);
            Assert.Same(newMaterial, staticModel.GetMaterial());

            Assert.True(project.UndoManager.Undo());
            Assert.Same(oldMaterial, staticModel.GetMaterial());
            Assert.True(project.UndoManager.Redo());
            Assert.Same(newMaterial, staticModel.GetMaterial());

            addon.CancelDragDrop();
            Assert.Same(newMaterial, staticModel.GetMaterial());

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

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
