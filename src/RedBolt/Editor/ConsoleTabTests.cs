namespace RedBolt.Tests;

using System.Linq;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;

using ConsoleSubsystem = RedBolt.SystemUI.Console;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ConsoleTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ConsoleTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Console_tab_renders_context_menu_and_round_trips_level_visibility()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.ConsoleTabTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        ConsoleSubsystem? console = null;
        ConsoleTab? tab = null;
        var imguiContext = IntPtr.Zero;
        var frameStarted = false;
        var windowStarted = false;
        var log = _fixture.Context.GetSubsystem<Log>()!;
        var previousLogLevel = log.Level;
        try
        {
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            console = new ConsoleSubsystem();
            _fixture.Context.RegisterSubsystem(console);
            tab = new ConsoleTab();

            Assert.Equal("Console", tab.Title);
            Assert.Equal("Console###2c1b8e59-3e21-4a14-bc20-d35af0ba5031", tab.UniqueId);
            Assert.Equal(EditorTabFlag.OpenByDefault, tab.Flags);
            Assert.Equal(EditorTabPlacement.DockBottom, tab.Placement);

            console.SetLevelVisible(LogLevel.Trace, true);
            console.SetLevelVisible(LogLevel.Debug, false);
            console.SetLevelVisible(LogLevel.Info, true);
            console.SetLevelVisible(LogLevel.Warning, false);
            console.SetLevelVisible(LogLevel.Error, true);
            using (var output = new ImGuiTextBuffer())
            {
                tab.WriteIniSettings(output);
                Assert.Equal(
                    "IsOpen=0\nShow_TRACE=1\nShow_DEBUG=0\nShow_INFO=1\nShow_WARN=0\nShow_ERROR=1\n",
                    Encoding.UTF8.GetString(output.CStr()));
            }

            tab.ReadIniSettings("IsOpen=1");
            tab.ReadIniSettings("Show_TRACE=0");
            tab.ReadIniSettings("Show_DEBUG=1");
            tab.ReadIniSettings("Show_INFO=0");
            tab.ReadIniSettings("Show_WARN=1");
            tab.ReadIniSettings("Show_ERROR=0");
            Assert.True(tab.IsOpen);
            Assert.False(console.GetLevelVisible(LogLevel.Trace));
            Assert.True(console.GetLevelVisible(LogLevel.Debug));
            Assert.False(console.GetLevelVisible(LogLevel.Info));
            Assert.True(console.GetLevelVisible(LogLevel.Warning));
            Assert.False(console.GetLevelVisible(LogLevel.Error));

            console.SetLevelVisible(LogLevel.Trace, true);
            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(600.0f, 400.0f));
            var open = true;
            ImGui.Begin("Console tab test"u8, ref open);
            windowStarted = true;

            Project.SetMonoFont(ImGui.GetFont());
            tab.RenderContent();
            tab.RenderContextMenuItems();

            Assert.Equal(LogLevel.Trace, log.Level);
        }
        finally
        {
            Project.SetMonoFont(null);
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            tab?.Dispose();
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            if (console is not null)
            {
                _fixture.Context.RemoveSubsystem<ConsoleSubsystem>();
                console.Dispose();
            }
            log.SetLevel(previousLogLevel);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            Directory.Delete(projectPath, true);
        }
    }
}
