namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Utility;
using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using EngineSubsystem = RedBolt.Engine.Engine;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;
using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class GameViewTabTests
{
    private readonly GraphicsEngineFixture _fixture;

    public GameViewTabTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Game_tab_routes_backbuffer_grabs_input_and_orders_simulation_signals()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.GameViewTabTests", Guid.NewGuid().ToString("N"));
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
        RmlUISubsystem? rmlUI = null;
        RedBolt.SystemUI.DebugHud? debugHud = null;
        GameViewTab? tab = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            using (Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi"))
                rmlUI = new RmlUISubsystem();
            _fixture.Context.RegisterSubsystem(rmlUI);
            debugHud = new RedBolt.SystemUI.DebugHud();
            _fixture.Context.RegisterSubsystem(debugHud);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            tab = new GameViewTab();
            tab.RenderTargetChanged += (_, target) => rmlUI.SetRenderTarget(target);
            var backbuffer = GetPrivateField<CustomBackbufferTexture>(tab, "_backbuffer");
            var renderer = _fixture.Context.GetSubsystem<Renderer>()!;
            var engine = _fixture.Context.GetSubsystem<EngineSubsystem>()!;

            Assert.Equal("Game", tab.Title);
            Assert.Equal("Game###212a6577-8a2a-42d6-aaed-042d226c724c", tab.UniqueId);
            Assert.Equal(EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault, tab.Flags);
            Assert.Equal(EditorTabPlacement.DockCenter, tab.Placement);
            Assert.False(tab.IsPlaying());
            Assert.False(tab.IsInputGrabbed());
            Assert.False(backbuffer.IsActive);

            tab.ReadIniSettings("IsHudVisible=1");
            using (var output = new ImGuiTextBuffer())
            {
                tab.WriteIniSettings(output);
                Assert.Equal("IsHudVisible=1\n", Encoding.UTF8.GetString(output.CStr()));
            }

            var started = 0;
            var stopped = 0;
            tab.OnSimulationStarted += () => ++started;
            tab.OnSimulationStopped += () => ++stopped;

            tab.Play();
            Assert.True(tab.IsPlaying());
            Assert.True(tab.IsInputGrabbed());
            Assert.True(backbuffer.IsActive);
            Assert.Equal(1, started);
            Assert.Equal(0, stopped);
            Assert.Equal(true, engine.GetParameter(RedBolt.Engine.EngineParameter.IsRunningInEditor));
            Assert.True(systemUI.PassThroughEvents);
            Assert.False(GetPrivateField<bool>(project, "_areGlobalHotkeysEnabled"));
            Assert.True(GetPrivateField<bool>(project, "_isHighlightEnabled"));

            RenderFrame(tab);
            _fixture.Context.SendEvent<GraphicsEvents.BeginRenderingEvent>();

            Assert.True(backbuffer.Texture.Width > 0);
            Assert.True(backbuffer.Texture.Height > 0);
            var backbufferSurface = Assert.IsType<RenderSurface>(backbuffer.Texture.GetRenderSurface());
            Assert.Same(backbufferSurface, GetPrivateField<RenderSurface?>(renderer, "_backbufferSurface"));
            Assert.NotNull(GetPrivateField<IntRect?>(_fixture.Input, "_explicitWindowRect"));
            var rmlRenderSurface = GetPrivateField<WeakReference<RenderSurface>?>(rmlUI, "_renderSurface");
            Assert.NotNull(rmlRenderSurface);
            Assert.True(rmlRenderSurface.TryGetTarget(out var rmlTarget));
            Assert.Same(backbufferSurface, rmlTarget);

            tab.ReleaseInput();
            Assert.False(tab.IsInputGrabbed());
            Assert.False(systemUI.PassThroughEvents);
            Assert.True(GetPrivateField<bool>(project, "_areGlobalHotkeysEnabled"));
            Assert.False(GetPrivateField<bool>(project, "_isHighlightEnabled"));

            tab.Play();
            Assert.True(tab.IsPlaying());
            Assert.True(tab.IsInputGrabbed());
            Assert.Equal(2, started);
            Assert.Equal(1, stopped);

            tab.TogglePlayed();
            Assert.False(tab.IsPlaying());
            Assert.False(backbuffer.IsActive);
            Assert.Equal(2, stopped);
            Assert.Null(engine.GetParameter(RedBolt.Engine.EngineParameter.IsRunningInEditor));
            Assert.Null(GetPrivateField<RenderSurface?>(renderer, "_backbufferSurface"));
            Assert.Null(GetPrivateField<IntRect?>(_fixture.Input, "_explicitWindowRect"));
            Assert.Null(GetPrivateField<WeakReference<RenderSurface>?>(rmlUI, "_renderSurface"));

            tab.TogglePlayed();
            Assert.True(tab.IsPlaying());
            Assert.Equal(3, started);
            tab.Stop();
            tab.Stop();
            Assert.Equal(3, stopped);

            tab.Play();
            Assert.Equal(4, started);
            tab.Dispose();
            Assert.False(tab.IsPlaying());
            Assert.False(backbuffer.IsActive);
            Assert.Equal(3, stopped);
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
            if (debugHud is not null)
            {
                _fixture.Context.RemoveSubsystem<RedBolt.SystemUI.DebugHud>();
                debugHud.Dispose();
            }
            if (rmlUI is not null)
            {
                _fixture.Context.RemoveSubsystem<RmlUISubsystem>();
                using (Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi"))
                    rmlUI.Dispose();
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

    private static void RenderFrame(GameViewTab tab)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(640.0f, 480.0f);
        io.DeltaTime = 1.0f / 60.0f;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(600.0f, 400.0f));
            var open = true;
            ImGui.Begin("Game view test"u8, ref open);
            windowStarted = true;

            tab.RenderToolbar();
            tab.RenderContent();
            tab.RenderContextMenuItems();
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static T GetPrivateField<T>(object instance, string name)
    {
        for (var type = instance.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
                return (T)field.GetValue(instance)!;
        }

        throw new MissingFieldException(instance.GetType().FullName, name);
    }
}
