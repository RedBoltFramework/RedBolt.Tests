namespace RedBolt.Tests;

using System.Linq;
using System.Text;

using RedBolt.Bindings.DearImGui;
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
public sealed class SceneDebugInfoTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneDebugInfoTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Addon_persists_toggles_and_renders_hud_and_wireframe()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.SceneDebugInfoTests", Guid.NewGuid().ToString("N"));
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
        DebugHud? debugHud = null;
        SceneDebugInfo.SettingsPage? settings = null;
        TestSceneViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            debugHud = new DebugHud();
            _fixture.Context.RegisterSubsystem(debugHud);
            project = new Project(
                projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var preservedResource in preservedResources)
                _fixture.Resources.AddManualResource(preservedResource);
            _fixture.Context.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);

            settings = new SceneDebugInfo.SettingsPage();
            project.SettingsManager.AddPage(settings);
            Assert.Equal("Editor.Scene:DebugInfo", settings.UniqueName);
            Assert.True(settings.IsSerializable);
            Assert.True(settings.CanResetToDefault);
            using (var file = new JSONFile())
            {
                Assert.True(file.SaveObjectCallback(archive =>
                {
                    using var block = archive.OpenUnorderedBlock("Settings");
                    settings.SerializeInBlock(archive);
                }));
                Assert.DoesNotContain("HudVisible", file.ToString(), StringComparison.Ordinal);
            }

            tab = new TestSceneViewTab();
            var addon = tab.RegisterAddon(new SceneDebugInfo(tab, settings));
            Assert.Equal("DebugInfo", addon.GetUniqueName());
            Assert.Equal(100, addon.GetToolbarPriority());
            Assert.True(addon.RenderTabContextMenu());

            var resource = new SceneResourceForEditor
            {
                Name = "Scenes/Debug.scene",
                AbsoluteFileName = Path.Combine(dataPath, "Scenes", "Debug.scene"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(resource.AbsoluteFileName)!);
            Assert.True(_fixture.Resources.AddManualResource(resource));
            tab.OpenResource(resource.Name);
            tab.LoadResource(resource.Name);
            var page = Assert.IsType<SceneViewPage>(tab.GetActivePage());

            AssertIni(addon, hudVisible: false, drawWireframe: false);
            addon.ToggleHud();
            addon.ReadIniSettings("SceneDebugInfo.DrawWireframe=1");
            AssertIni(addon, hudVisible: true, drawWireframe: true);

            RenderFrame(tab, settings);
            Assert.Equal(FillMode.Wireframe, page.Renderer.Camera!.FillMode);

            addon.ReadIniSettings("SceneDebugInfo.HudVisible=0");
            addon.ReadIniSettings("SceneDebugInfo.DrawWireframe=0");
            RenderFrame(tab, settings);
            Assert.Equal(FillMode.Solid, page.Renderer.Camera!.FillMode);

            addon.ApplyHotkeys(project.HotkeyManager);
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
            if (debugHud is not null)
            {
                _fixture.Context.RemoveSubsystem<DebugHud>();
                debugHud.Dispose();
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

    private static void AssertIni(
        SceneDebugInfo addon, bool hudVisible, bool drawWireframe)
    {
        using var output = new ImGuiTextBuffer();
        addon.WriteIniSettings(output);
        Assert.Equal(
            $"SceneDebugInfo.HudVisible={(hudVisible ? 1 : 0)}\n"
            + $"SceneDebugInfo.DrawWireframe={(drawWireframe ? 1 : 0)}\n",
            Encoding.UTF8.GetString(output.CStr()));
    }

    private static void RenderFrame(
        SceneViewTab tab, SceneDebugInfo.SettingsPage settings)
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
            ImGui.SetNextWindowSize(new Vector2f(720.0f, 520.0f));
            var open = true;
            ImGui.Begin("Scene debug info test"u8, ref open);
            windowStarted = true;
            settings.RenderSettings();
            Assert.True(tab.GetAddon<SceneDebugInfo>()!.RenderToolbar());
            tab.RenderContent();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private sealed class TestSceneViewTab : SceneViewTab
    {
        public void LoadResource(string resourceName) => OnResourceLoaded(resourceName);
    }
}
