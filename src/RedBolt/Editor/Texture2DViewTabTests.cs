namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Texture2DViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Texture2DViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_tab_replaces_renders_and_disposes_image_preview()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.Texture2DViewTabTests", Guid.NewGuid().ToString("N"));
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
            Assert.True(image.SetSize(16, 8, 4));
            image.Clear(new Color(0.25f, 0.5f, 0.75f, 1.0f));
            Assert.True(image.SavePNG(Path.Combine(dataPath, "Test.png")));
        }
        _ = _fixture.Resources.Get<Model>("Models/Box.mdl");
        _ = _fixture.Resources.Get<Material>("Materials/DefaultSkybox.xml");
        _ = _fixture.Resources.Get<TextureCube>("Textures/DefaultSkybox.xml");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        TestTexture2DViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            tab = new TestTexture2DViewTab();

            Assert.Equal("Texture2D", tab.GetResourceTitle());
            Assert.False(tab.SupportMultipleResources());
            var textureDescriptor = new ResourceFileDescriptor();
            textureDescriptor.AddObjectType<Texture2D>();
            Assert.True(tab.CanOpenResource(textureDescriptor));
            var cubemapDescriptor = new ResourceFileDescriptor();
            cubemapDescriptor.AddObjectType<TextureCube>();
            Assert.False(tab.CanOpenResource(cubemapDescriptor));

            Assert.Null(GetPreview(tab));
            tab.ReadIniSettings("ResourceNames=");
            tab.OnProjectLoaded();
            RenderFrame(tab.RenderContent);
            Assert.Equal(0, tab.RenderTitleCount);

            tab.OpenResource("Test.png");
            tab.LoadResource("Test.png");
            var firstPreview = Assert.IsType<Texture2DWidget>(GetPreview(tab));
            var texture = Assert.IsType<Texture2D>(firstPreview.GetTexture2D());
            Assert.Equal(16, texture.Width);
            Assert.Equal(8, texture.Height);

            RenderFrame(tab.RenderContent);
            Assert.Equal(1, tab.RenderTitleCount);

            tab.LoadResource("Test.png");
            var secondPreview = Assert.IsType<Texture2DWidget>(GetPreview(tab));
            Assert.NotSame(firstPreview, secondPreview);
            Assert.Null(firstPreview.GetTexture2D());
            Assert.Same(texture, secondPreview.GetTexture2D());

            tab.UnloadResource("Test.png");
            Assert.Null(GetPreview(tab));
            Assert.Null(secondPreview.GetTexture2D());
            RenderFrame(tab.RenderContent);
            Assert.Equal(1, tab.RenderTitleCount);

            tab.Dispose();
            Assert.Null(tab.GetScene());
            Assert.Null(tab.GetCamera());
            tab = null;
        }
        finally
        {
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
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static Texture2DWidget? GetPreview(Texture2DViewTab tab)
    {
        return (Texture2DWidget?)typeof(Texture2DViewTab)
            .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tab);
    }

    private static void RenderFrame(Action render)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(320.0f, 320.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(256.0f, 256.0f));
            var open = true;
            ImGui.Begin("Texture2D view test"u8, ref open);
            windowStarted = true;
            render();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private sealed class TestTexture2DViewTab : Texture2DViewTab
    {
        public int RenderTitleCount { get; private set; }

        public void LoadResource(string resourceName)
        {
            OnResourceLoaded(resourceName);
        }

        public void UnloadResource(string resourceName)
        {
            OnResourceUnloaded(resourceName);
        }

        protected override void RenderTitle()
        {
            ++RenderTitleCount;
            base.RenderTitle();
        }
    }
}
