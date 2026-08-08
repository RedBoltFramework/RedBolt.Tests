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
public sealed class SceneDragAndDropAnimationTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneDragAndDropAnimationTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_accepts_animation_hover_and_commits_undoable_controller_change()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneDragAndDropAnimationTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TestSceneViewTab? tab = null;
        SceneDragAndDropAnimation? addon = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            var oldAnimation = new Animation { Name = "Animations/Old.ani" };
            oldAnimation.SetLength(2.0f);
            var newAnimation = new Animation { Name = "Animations/New.ani" };
            newAnimation.SetLength(3.0f);
            Assert.True(_fixture.Resources.AddManualResource(oldAnimation));
            Assert.True(_fixture.Resources.AddManualResource(newAnimation));

            tab = new TestSceneViewTab();
            addon = new SceneDragAndDropAnimation(tab);
            Assert.Equal("DragAndDropAnimation", addon.GetUniqueName());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/AnimationDrop.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "AnimationDrop.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            var octree = resource.Scene.CreateComponent<Octree>();
            resource.Scene.CreateComponent<DebugRenderer>();
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

            using var payload = new ResourceDragDropPayload();
            var descriptor = new ResourceFileDescriptor
            {
                LocalName = "New.ani",
                ResourceName = newAnimation.Name,
            };
            descriptor.AddObjectType<Animation>();
            payload.Resources.Add(descriptor);
            Assert.True(addon.IsDragDropPayloadSupported(page, payload));

            var node = page.Scene.CreateChild("Animated");
            var geometry = node.CreateComponent<CustomGeometry>();
            geometry.BeginGeometry(0u, PrimitiveType.TriangleList);
            geometry.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
            geometry.DefineVertex(new Vector3f(1.0f, 0.0f, 0.0f));
            geometry.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
            geometry.Commit();
            var controller = node.CreateComponent<AnimationController>();
            controller.AddAnimation(new AnimationParameters(oldAnimation).Looped());
            octree.Update(new FrameInfo());
            page.CameraRay = new Ray(
                new Vector3f(0.25f, 0.25f, 1.0f),
                new Vector3f(0.0f, 0.0f, -1.0f));

            addon.BeginDragDrop(page, payload);
            addon.UpdateDragDrop(payload);
            project.UndoManager.NewFrame();
            addon.CompleteDragDrop(payload);

            Assert.Equal(1, controller.GetNumAnimations());
            var parameters = controller.GetAnimationParameters(0);
            Assert.Same(newAnimation, parameters.GetAnimation());
            Assert.True(parameters.IsLooped);

            Assert.True(project.UndoManager.Undo());
            Assert.Equal(1, controller.GetNumAnimations());
            parameters = controller.GetAnimationParameters(0);
            Assert.Same(oldAnimation, parameters.GetAnimation());
            Assert.True(parameters.IsLooped);

            Assert.True(project.UndoManager.Redo());
            Assert.Same(newAnimation,
                controller.GetAnimationParameters(0).GetAnimation());

            addon.BeginDragDrop(page, payload);
            addon.UpdateDragDrop(payload);
            addon.CancelDragDrop();
            addon.CompleteDragDrop(payload);
            Assert.Same(newAnimation,
                controller.GetAnimationParameters(0).GetAnimation());

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
