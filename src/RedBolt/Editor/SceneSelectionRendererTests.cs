namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneSelectionRendererTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneSelectionRendererTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_builds_direct_and_indirect_outlines_and_draws_debug_geometry()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneSelectionRendererTests", Guid.NewGuid().ToString("N"));
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
        SceneSelectionRenderer.SettingsPage? settings = null;
        TestSceneViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            settings = new SceneSelectionRenderer.SettingsPage();
            project.SettingsManager.AddPage(settings);
            AssertSettingsDefaults(settings.Values);
            using (var file = new JSONFile())
            {
                Assert.True(file.SaveObjectCallback(archive =>
                {
                    using var block = archive.OpenUnorderedBlock("Settings");
                    settings.SerializeInBlock(archive);
                }));
                Assert.DoesNotContain("DirectSelectionColor", file.ToString(), StringComparison.Ordinal);
            }
            var values = settings.Values;
            values.DebugGeometryDepthTest = true;
            SetSettingsValues(settings, values);

            tab = new TestSceneViewTab();
            var addon = tab.RegisterAddon(new SceneSelectionRenderer(tab, settings));
            Assert.Equal("SelectionRenderer", addon.GetUniqueName());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Selection.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Selection.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());
            page.Scene.GetOrCreateComponent<DebugRenderer>().SetTemporary(true);

            var staleGroup = page.Scene.CreateComponent<OutlineGroup>();
            staleGroup.SetTemporary(true);
            staleGroup.SetDebug(true);

            var parent = page.Scene.CreateChild("Parent");
            var directDrawable = parent.CreateComponent<StaticModel>();
            var directDebug = new TestDebugComponent();
            parent.AddComponent(directDebug);
            var child = parent.CreateChild("Child");
            var indirectDrawable = child.CreateComponent<StaticModel>();
            var indirectDebug = new TestDebugComponent();
            child.AddComponent(indirectDebug);
            page.Selection.SetSelected(parent, true);

            RenderFrame(settings, addon, page);

            var state = Assert.IsType<SceneSelectionRenderer.PageState>(page.GetAddonData(addon));
            Assert.True(state.DirectSelection!.TryGetTarget(out var directGroup));
            Assert.True(state.IndirectSelection!.TryGetTarget(out var indirectGroup));
            Assert.Null(staleGroup.TryGetNode());
            Assert.Equal(SceneSelectionRenderer.DirectSelectionRenderOrder, directGroup.GetRenderOrder());
            Assert.Equal(SceneSelectionRenderer.IndirectSelectionRenderOrder, indirectGroup.GetRenderOrder());
            Assert.True(directGroup.IsTemporary);
            Assert.True(indirectGroup.IsTemporary);
            Assert.True(directGroup.IsDebug());
            Assert.True(indirectGroup.IsDebug());
            Assert.Equal(settings.Values.DirectSelectionColor, directGroup.GetColor());
            Assert.Equal(settings.Values.IndirectSelectionColor, indirectGroup.GetColor());
            Assert.True(directGroup.ContainsDrawable(directDrawable));
            Assert.False(directGroup.ContainsDrawable(indirectDrawable));
            Assert.True(indirectGroup.ContainsDrawable(indirectDrawable));
            Assert.False(indirectGroup.ContainsDrawable(directDrawable));
            Assert.Equal(1, directDebug.DrawCount);
            Assert.Equal(1, indirectDebug.DrawCount);
            Assert.True(directDebug.LastDepthTest);
            Assert.True(indirectDebug.LastDepthTest);

            page.Selection.SetSelected(indirectDrawable, true);
            addon.Render(page);
            Assert.True(directGroup.ContainsDrawable(indirectDrawable));
            Assert.False(indirectGroup.ContainsDrawable(indirectDrawable));

            AssertIni(addon, true);
            addon.ReadIniSettings("SceneSelectionRenderer.DrawDebugGeometry=0");
            var directCount = directDebug.DrawCount;
            var indirectCount = indirectDebug.DrawCount;
            addon.Render(page);
            Assert.Equal(directCount, directDebug.DrawCount);
            Assert.Equal(indirectCount, indirectDebug.DrawCount);
            AssertIni(addon, false);
        }
        finally
        {
            tab?.Dispose();
            settings?.Dispose();
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

    private static void RenderFrame(SceneSelectionRenderer.SettingsPage settings,
        SceneSelectionRenderer addon, SceneViewPage page)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Scene selection renderer test"u8, ref open);
            windowStarted = true;
            settings.RenderSettings();
            Assert.True(addon.RenderTabContextMenu());
            addon.Render(page);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static void AssertSettingsDefaults(SceneSelectionRenderer.Settings settings)
    {
        Assert.Equal("Editor.Scene:Selection", settings.UniqueName);
        Assert.Equal(new Color(0xfd5602u, Color.RGB), settings.DirectSelectionColor);
        Assert.Equal(new Color(0x009dffu, Color.RGB), settings.IndirectSelectionColor);
        Assert.False(settings.DebugGeometryDepthTest);
    }

    private static void AssertIni(SceneSelectionRenderer addon, bool drawDebugGeometry)
    {
        using var output = new ImGuiTextBuffer();
        addon.WriteIniSettings(output);
        Assert.Equal(
            $"SceneSelectionRenderer.DrawDebugGeometry={(drawDebugGeometry ? 1 : 0)}\n",
            Encoding.UTF8.GetString(output.CStr()));
    }

    private static void SetSettingsValues(
        SceneSelectionRenderer.SettingsPage page, SceneSelectionRenderer.Settings values)
    {
        typeof(SimpleSettingsPage<SceneSelectionRenderer.Settings>).GetField(
                "_values", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, values);
    }

    private sealed class TestDebugComponent : Component
    {
        public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
        {
            ++DrawCount;
            LastDepthTest = depthTest;
        }

        public int DrawCount { get; private set; }
        public bool LastDepthTest { get; private set; }
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
