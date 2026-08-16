// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;

using SystemConsole = RedBolt.SystemUI.Console;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ConsoleTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ConsoleTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Console_tracks_history_filters_visibility_and_update_subscription()
    {
        _fixture.ResetGraphics();
        var console = new SystemConsole();
        try
        {
            Assert.False(console.IsVisible());
            Assert.False(console.IsAutoVisibleOnError());
            Assert.Equal(512u, console.GetNumHistoryRows());
            Assert.False(console.GetLevelVisible(LogLevel.Trace));
#if DEBUG
            Assert.True(console.GetLevelVisible(LogLevel.Debug));
#else
            Assert.False(console.GetLevelVisible(LogLevel.Debug));
#endif
            Assert.True(console.GetLevelVisible(LogLevel.Info));
            Assert.True(console.GetLevelVisible(LogLevel.Warning));
            Assert.True(console.GetLevelVisible(LogLevel.Error));
            Assert.False(console.GetLevelVisible(LogLevel.None));
            Assert.False(console.GetLevelVisible((LogLevel)(-1)));

            console.SetLevelVisible(LogLevel.Trace, true);
            console.SetLevelVisible(LogLevel.None, true);
            console.SetLevelVisible((LogLevel)(-1), true);
            Assert.True(console.GetLevelVisible(LogLevel.Trace));
            Assert.False(console.GetLevelVisible(LogLevel.None));

            Assert.True(console.GetLoggerVisible("alpha"));
            console.SetLoggerVisible("alpha", false);
            Assert.False(console.GetLoggerVisible("alpha"));
            console.SetLoggerVisible("alpha", true);
            Assert.True(console.GetLoggerVisible("alpha"));

            SendLog(LogLevel.Info, "alpha", "first\nsecond", 1);
            SendLog(LogLevel.Warning, "beta", "third", 2);
            Assert.Equal(["alpha", "beta"], console.GetLoggers());

            console.SetNumHistoryRows(1);
            Assert.Equal(1u, console.GetNumHistoryRows());
            Assert.Equal(["alpha"], console.GetLoggers());

            console.Clear();
            Assert.Empty(console.GetLoggers());

            for (var i = 0; i <= 2000; ++i)
                SendLog(LogLevel.Info, $"logger{i:D4}", "row", (uint)i);
            var ringLoggers = console.GetLoggers();
            Assert.Equal(2000, ringLoggers.Count);
            Assert.DoesNotContain("logger0000", ringLoggers);
            Assert.Contains("logger2000", ringLoggers);
            console.Clear();

            console.SetAutoVisibleOnError(true);
            Assert.True(console.IsAutoVisibleOnError());
            SendLog(LogLevel.Error, "error", "failure", 3);
            Assert.True(console.IsVisible());
            Assert.True(_fixture.Context.HasSubscribedToEvent<CoreEvents.UpdateEvent>(console));

            console.Dispose();
            Assert.False(_fixture.Context.HasSubscribedToEvent<CoreEvents.UpdateEvent>(console));
        }
        finally
        {
            console.Dispose();
        }
    }

    [NativeBindingFact]
    public void Console_discovers_only_non_specific_command_interpreters()
    {
        _fixture.ResetGraphics();
        using var sender = new EventSender();
        using var alpha = new AlphaInterpreter();
        using var zulu = new ZuluInterpreter();
        using var specific = new SpecificInterpreter(sender);
        var console = new SystemConsole();
        try
        {
            var receivers = _fixture.Context.GetEventReceivers<EngineEvents.ConsoleCommandEvent>();
            Assert.NotNull(receivers);
            Assert.Equal([alpha, zulu], receivers);

            Assert.Equal(nameof(AlphaInterpreter), console.GetCommandInterpreter());
            console.SetCommandInterpreter(nameof(ZuluInterpreter));
            Assert.Equal(nameof(ZuluInterpreter), console.GetCommandInterpreter());
            console.SetCommandInterpreter("MissingInterpreter");
            Assert.Equal(nameof(AlphaInterpreter), console.GetCommandInterpreter());

            alpha.Dispose();
            console.RefreshInterpreters();
            Assert.Equal(nameof(ZuluInterpreter), console.GetCommandInterpreter());
        }
        finally
        {
            console.Dispose();
        }
    }

    [NativeBindingFact]
    public void Console_renders_log_and_preserves_combo_selection()
    {
        _fixture.ResetGraphics();
        using var alpha = new AlphaInterpreter();
        using var zulu = new ZuluInterpreter();
        var console = new SystemConsole();
        var log = _fixture.Context.GetSubsystem<Log>()!;
        var previousLevel = log.Level;
        var imguiContext = ImGui.CreateContext();
        try
        {
            console.SetCommandInterpreter(nameof(ZuluInterpreter));
            console.SetLevelVisible(LogLevel.Trace, true);
            SendLog(LogLevel.Warning, "render", "https://example.test/path", 4);

            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            console.SetVisible(true);
            RenderConsoleFrame(console);
            RenderConsoleFrame(console);
            io.AddInputCharactersUTF8("status"u8);
            RenderConsoleFrame(console);
            io.AddKeyEvent((ImGuiKey)525, true);
            RenderConsoleFrame(console);

            Assert.Equal(nameof(ZuluInterpreter), console.GetCommandInterpreter());
            Assert.Equal(LogLevel.Trace, log.Level);
            Assert.Equal(1, zulu.CommandCount);
            Assert.Equal("status", zulu.LastCommand);
            Assert.Equal(nameof(ZuluInterpreter), zulu.LastInterpreter);
            Assert.Equal(1, alpha.CommandCount);
        }
        finally
        {
            console.Dispose();
            ImGui.DestroyContext(imguiContext);
            log.SetLevel(previousLevel);
        }
    }

    private static void RenderConsoleFrame(SystemConsole console)
    {
        ImGui.NewFrame();
        ImGui.SetNextWindowSize(new Vector2f(600.0f, 400.0f));
        var open = true;
        ImGui.Begin("Console test"u8, ref open);
        console.RenderContent();
        ImGui.End();
        ImGui.EndFrame();
    }

    private void SendLog(LogLevel level, string logger, string message, uint timestamp)
    {
        var eventData = new IOEvents.LogMessage
        {
            Level = (int)level,
            Time = timestamp,
            Logger = logger,
            Message = message,
        };
        _fixture.Context.SendEvent(ref eventData);
    }

    private sealed class EventSender : RedObject
    {
    }

    private sealed class AlphaInterpreter : RedObject
    {
        public int CommandCount { get; private set; }

        public AlphaInterpreter()
        {
            SubscribeToEvent<EngineEvents.ConsoleCommandEvent>(HandleCommand);
        }

        private void HandleCommand(ref EngineEvents.ConsoleCommandEvent eventData)
        {
            ++CommandCount;
        }
    }

    private sealed class ZuluInterpreter : RedObject
    {
        public int CommandCount { get; private set; }

        public string LastCommand { get; private set; } = string.Empty;

        public string LastInterpreter { get; private set; } = string.Empty;

        public ZuluInterpreter()
        {
            SubscribeToEvent<EngineEvents.ConsoleCommandEvent>(HandleCommand);
        }

        private void HandleCommand(ref EngineEvents.ConsoleCommandEvent eventData)
        {
            ++CommandCount;
            LastCommand = eventData.Command;
            LastInterpreter = eventData.Id;
        }
    }

    private sealed class SpecificInterpreter : RedObject
    {
        public SpecificInterpreter(RedObject sender)
        {
            SubscribeToEvent<EngineEvents.ConsoleCommandEvent>(sender, HandleCommand);
        }

        private static void HandleCommand(ref EngineEvents.ConsoleCommandEvent eventData)
        {
        }
    }
}
