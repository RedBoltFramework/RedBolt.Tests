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
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TextureCubeInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TextureCubeInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_selection_creates_inspector_and_rendered_skybox_preview()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBoltTextureCubeInspector-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        using (var image = new Image())
        {
            Assert.True(image.SetSize(8, 8, 4));
            image.Clear(new Color(0.25f, 0.5f, 0.75f, 1.0f));
            Assert.True(image.SavePNG(Path.Combine(dataPath, "Face.png")));
        }
        File.WriteAllText(
            Path.Combine(dataPath, "TestCube.xml"),
            """
            <cubemap>
                <face name="Face.png" />
                <face name="Face.png" />
                <face name="Face.png" />
                <face name="Face.png" />
                <face name="Face.png" />
                <face name="Face.png" />
            </cubemap>
            """);
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TextureCubeInspector? inspector = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            MountCompleteCoreData(virtualFileSystem, project.CoreDataPath);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".xml"))
                    descriptor.AddObjectType<TextureCube>();
            });

            inspector = new TextureCubeInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            using (var request = new InspectResourceRequest(["TestCube.xml"]))
            {
                project.ProcessRequest(request);
                typeof(Project).GetMethod("ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(project, null);
            }

            Assert.Equal(1, activationCount);
            var widget = Assert.IsType<TextureCubeInspectorWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var texture = Assert.IsType<TextureCube>(Assert.Single(widget.GetResources()));
            Assert.Equal("TestCube.xml", texture.GetName());
            Assert.Equal(8, texture.Width);
            Assert.Equal(8, texture.Height);
            Assert.False(widget.CanSave());

            var preview = Assert.IsType<SceneWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var scene = Assert.IsType<RedBolt.Scene.Scene>(preview.GetScene());
            var zone = Assert.IsType<Zone>(scene.GetComponent<Zone>());
            var skybox = Assert.IsType<Skybox>(scene.GetComponent<Skybox>());
            Assert.Same(texture, zone.GetZoneTexture());
            var material = Assert.IsType<Material>(skybox.GetMaterial());
            Assert.Same(texture, material.GetTexture(ShaderResources.Albedo));
            Assert.Equal(CullMode.None, material.GetCullMode());

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
                ImGui.Begin("TextureCube Inspector"u8, ref isOpen);
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
