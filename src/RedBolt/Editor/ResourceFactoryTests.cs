namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Math;
using RedBolt.Utility;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class ResourceFactoryTests
{
    [Fact]
    public void Factory_exposes_defaults_and_orders_by_group_then_title()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var beta = new TestResourceFactory(1, "Beta");
            using var alpha = new TestResourceFactory(1, "Alpha");
            using var earlierGroup = new TestResourceFactory(0, "Zulu");
            var factories = new List<ResourceFactory> { beta, alpha, earlierGroup };
            factories.Sort(static (lhs, rhs) =>
                ResourceFactory.Compare(lhs, rhs) ? -1 : ResourceFactory.Compare(rhs, lhs) ? 1 : 0);

            Assert.Equal([earlierGroup, alpha, beta], factories);
            Assert.True(alpha.IsEnabled(new FileSystemEntry()));
            Assert.Equal(1, alpha.Group);
            Assert.Equal("Alpha", alpha.Title);
            alpha.DiscardAndClose();
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Simple_factory_commits_final_file_and_resource_names()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            string? committedFileName = null;
            string? committedResourceName = null;
            using var factory = new SimpleResourceFactory(
                0,
                "Folder",
                "New Folder",
                (fileName, resourceName) =>
                {
                    committedFileName = fileName;
                    committedResourceName = resourceName;
                });

            factory.Open(" /tmp/project/Data\\ ", " Data\\ ");
            factory.CommitAndClose();

            Assert.Equal("New Folder", factory.DefaultFileName);
            Assert.Equal("/tmp/project/Data/New Folder", committedFileName);
            Assert.Equal("Data/New Folder", committedResourceName);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [NativeBindingFact]
    public void Base_factory_opens_renders_and_exposes_final_paths()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var imguiContext = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;
        var factory = new TestBaseResourceFactory();

        try
        {
            factory.Open(" /tmp/project/Scenes\\ ", " Scenes\\ ");

            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Resource factory test"u8, ref open);
            windowStarted = true;

            var checkedFilePath = string.Empty;
            var checkedFileName = string.Empty;
            var canCommit = false;
            var shouldCommit = true;
            factory.Render(
                (filePath, fileName) =>
                {
                    checkedFilePath = filePath;
                    checkedFileName = fileName;
                    return (true, "Name is available");
                },
                ref canCommit,
                ref shouldCommit);

            Assert.Equal("/tmp/project/Scenes/", checkedFilePath);
            Assert.Equal("Scene.xml", checkedFileName);
            Assert.True(canCommit);
            Assert.False(shouldCommit);
            Assert.Equal(1, factory.AuxilaryRenderCount);
            Assert.Equal("/tmp/project/Scenes/", factory.ExposedFinalFilePath);
            Assert.Equal("Scenes/", factory.ExposedFinalResourcePath);
            Assert.Equal("/tmp/project/Scenes/Scene.xml", factory.ExposedFinalFileName);
            Assert.Equal("Scenes/Scene.xml", factory.ExposedFinalResourceName);

            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource factory test"u8, ref open);
            windowStarted = true;
            factory.Render(
                static (_, _) => (true, string.Empty),
                ref canCommit,
                ref shouldCommit);
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddInputCharactersUTF8("VeryLongSceneFileName.xml"u8);
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource factory test"u8, ref open);
            windowStarted = true;
            factory.Render(
                static (_, _) => (true, string.Empty),
                ref canCommit,
                ref shouldCommit);

            Assert.Equal("/tmp/project/Scenes/VeryLongSceneFileName.xml", factory.ExposedFinalFileName);
            Assert.Equal("Scenes/VeryLongSceneFileName.xml", factory.ExposedFinalResourceName);

            factory.CommitAndClose();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            factory.Dispose();
            ImGui.DestroyContext(imguiContext);
            Context.SetInstance(previousContext);
        }
    }

    private sealed class TestResourceFactory(int group, string title) : ResourceFactory(group, title)
    {
        public override void Open(string baseFilePath, string baseResourcePath)
        {
        }

        public override void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit)
        {
        }

        public override void CommitAndClose()
        {
        }
    }

    private sealed class TestBaseResourceFactory : BaseResourceFactory
    {
        public TestBaseResourceFactory()
            : base(0, "Scene")
        {
        }

        public override string DefaultFileName => "Scene.xml";

        public int AuxilaryRenderCount { get; private set; }

        public string ExposedFinalFilePath => FinalFilePath;

        public string ExposedFinalResourcePath => FinalResourcePath;

        public string ExposedFinalFileName => FinalFileName;

        public string ExposedFinalResourceName => FinalResourceName;

        public override void RenderAuxilary()
        {
            ++AuxilaryRenderCount;
        }
    }
}
