namespace RedBolt.Tests;

using System.Reflection;
using SystemFile = global::System.IO.File;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Assets;
using RedBolt.Editor.Foundation;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Utility;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class AssetPipelineFactoryTests
{
    [NativeBindingFact]
    public void Factory_renders_and_saves_optional_model_importer()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBolt.AssetPipelineFactoryTests", Guid.NewGuid().ToString("N"));
        var emptyDirectory = Path.Combine(directory, "Empty");
        var importerDirectory = Path.Combine(directory, "Importer");
        Directory.CreateDirectory(emptyDirectory);
        Directory.CreateDirectory(importerDirectory);

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var imguiContext = IntPtr.Zero;
        AssetPipelineFactory? factory = null;
        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountRoot();
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(new ResourceCache());
            context.RegisterAssembly(typeof(AssetPipeline).Assembly);
            context.RegisterAssembly(typeof(ModelImporter).Assembly);

            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            factory = new AssetPipelineFactory();
            Assert.Equal(0, factory.Group);
            Assert.Equal("Asset Pipeline", factory.Title);
            Assert.Equal("Default.assetpipeline", factory.DefaultFileName);

            factory.Open(emptyDirectory, string.Empty);
            RenderFactory(factory);
            factory.CommitAndClose();

            var emptyFileName = Path.Combine(emptyDirectory, factory.DefaultFileName);
            Assert.True(SystemFile.Exists(emptyFileName));
            using (var pipeline = new AssetPipeline())
            {
                Assert.True(pipeline.LoadFile(new FileIdentifier(emptyFileName)));
                Assert.Empty(pipeline.GetTransformers());
            }

            SetModelImporter(factory, true);
            factory.Open(importerDirectory, string.Empty);
            factory.CommitAndClose();

            var importerFileName = Path.Combine(importerDirectory, factory.DefaultFileName);
            Assert.True(SystemFile.Exists(importerFileName));
            using (var pipeline = new AssetPipeline())
            {
                Assert.True(pipeline.LoadFile(new FileIdentifier(importerFileName)));
                var importer = Assert.IsType<ModelImporter>(Assert.Single(pipeline.GetTransformers()));
                importer.Dispose();
            }
        }
        finally
        {
            factory?.Dispose();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }

    private static void RenderFactory(AssetPipelineFactory factory)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Asset pipeline factory test"u8, ref open);
            windowStarted = true;
            var canCommit = false;
            var shouldCommit = true;
            factory.Render(static (_, _) => (true, string.Empty), ref canCommit, ref shouldCommit);
            Assert.True(canCommit);
            Assert.False(shouldCommit);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static void SetModelImporter(AssetPipelineFactory factory, bool value)
    {
        typeof(AssetPipelineFactory).GetField(
            "_modelImporter", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(factory, value);
    }
}
