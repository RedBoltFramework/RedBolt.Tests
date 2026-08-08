namespace RedBolt.Tests;

using System.Reflection;
using SystemFile = global::System.IO.File;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneFactoryTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneFactoryTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Factory_renders_and_creates_scene_and_prefab_variants()
    {
        _fixture.ResetGraphics();
        var directory = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneFactoryTests", Guid.NewGuid().ToString("N"));
        var sceneDirectory = Path.Combine(directory, "Scene");
        var prefabDirectory = Path.Combine(directory, "Prefab");
        Directory.CreateDirectory(sceneDirectory);
        Directory.CreateDirectory(prefabDirectory);
        var imguiContext = IntPtr.Zero;
        SceneFactory? sceneFactory = null;
        SceneFactory? prefabFactory = null;
        try
        {
            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            sceneFactory = new SceneFactory(false);
            Assert.Equal(0, sceneFactory.Group);
            Assert.Equal("Scene", sceneFactory.Title);
            Assert.Equal("Scene.scene", sceneFactory.DefaultFileName);
            SetField(sceneFactory, "_highQuality", false);
            SetField(sceneFactory, "_defaultObjects", false);
            sceneFactory.Open(sceneDirectory, string.Empty);
            RenderFactory(sceneFactory, "Scene factory test"u8);
            sceneFactory.CommitAndClose();

            var sceneFileName = Path.Combine(sceneDirectory, sceneFactory.DefaultFileName);
            Assert.True(SystemFile.Exists(sceneFileName));
            using (var sceneResource = new SceneResource())
            {
                Assert.True(sceneResource.LoadFile(new FileIdentifier(sceneFileName)));
                Assert.NotNull(sceneResource.Scene.GetComponent<Octree>());
                Assert.Null(sceneResource.Scene.GetComponent<RenderPipeline>());
                Assert.Equal(0, sceneResource.Scene.GetNumChildren());
            }

            prefabFactory = new SceneFactory(true);
            Assert.Equal(0, prefabFactory.Group);
            Assert.Equal("Prefab", prefabFactory.Title);
            Assert.Equal("Prefab.prefab", prefabFactory.DefaultFileName);
            prefabFactory.Open(prefabDirectory, string.Empty);
            RenderFactory(prefabFactory, "Prefab factory test"u8);
            prefabFactory.CommitAndClose();

            var prefabFileName = Path.Combine(prefabDirectory, prefabFactory.DefaultFileName);
            Assert.True(SystemFile.Exists(prefabFileName));
            using var prefabResource = new PrefabResource();
            Assert.True(prefabResource.LoadFile(new FileIdentifier(prefabFileName)));
            var scenePrefab = prefabResource.GetScenePrefab();
            Assert.Equal(4, scenePrefab.Children.Count);
            Assert.Equal("[Prefab Node]", scenePrefab.Children[0].GetNodeName());
            Assert.Equal("Skybox", scenePrefab.Children[1].GetNodeName());
            Assert.Equal("Global Zone", scenePrefab.Children[2].GetNodeName());
            Assert.Equal("Ground Plane", scenePrefab.Children[3].GetNodeName());
        }
        finally
        {
            prefabFactory?.Dispose();
            sceneFactory?.Dispose();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            Directory.Delete(directory, true);
        }
    }

    private static void RenderFactory(SceneFactory factory, ReadOnlySpan<byte> title)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin(title, ref open);
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

    private static void SetField<T>(SceneFactory factory, string name, T value)
    {
        typeof(SceneFactory).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(factory, value);
    }
}
