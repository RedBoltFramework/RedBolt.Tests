// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Core;
using RedBolt.Engine;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineExitDelayTests
{
    // The tests move the start of the delay timer instead of waiting for the limit of the engine.
    private static readonly uint ExitDelayLimitMs = (uint)typeof(Engine)
        .GetField("ExitDelayLimitMs", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetRawConstantValue()!;

    [Fact]
    public void Exit_waits_while_a_subscriber_delays_it_and_happens_when_the_delay_ends()
    {
        using var engine = CreateHeadlessEngine();
        using var probe = new ExitDelayProbe { Delay = true };

        RequestExit(engine);
        engine.RunFrame();
        engine.RunFrame();

        Assert.False(engine.IsExiting);
        Assert.Equal(2, probe.Queries);

        probe.Delay = false;
        engine.RunFrame();

        Assert.True(engine.IsExiting);
        Assert.Equal(3, probe.Queries);
    }

    [Fact]
    public void Exit_happens_in_the_same_frame_when_no_subscriber_delays_it()
    {
        using var engine = CreateHeadlessEngine();

        RequestExit(engine);
        engine.RunFrame();

        Assert.True(engine.IsExiting);
    }

    [Fact]
    public void Exit_does_not_query_subscribers_while_no_exit_is_pending()
    {
        using var engine = CreateHeadlessEngine();
        using var probe = new ExitDelayProbe { Delay = true };

        engine.RunFrame();

        Assert.False(engine.IsExiting);
        Assert.Equal(0, probe.Queries);
    }

    [Fact]
    public void Exit_happens_after_the_limit_although_a_subscriber_still_delays_it()
    {
        using var engine = CreateHeadlessEngine();
        using var probe = new ExitDelayProbe { Delay = true };

        RequestExit(engine);
        BackdateExitDelayTimer(engine, ExitDelayLimitMs - 2000);
        engine.RunFrame();

        Assert.False(engine.IsExiting);

        BackdateExitDelayTimer(engine, ExitDelayLimitMs + 1);
        engine.RunFrame();

        Assert.True(engine.IsExiting);
        Assert.Equal(2, probe.Queries);
    }

    private static void RequestExit(Engine engine)
    {
        engine.SetAutoExit(true);
        var method = typeof(Engine).GetMethod("HandleExitRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(engine, null);
    }

    private static void BackdateExitDelayTimer(Engine engine, uint elapsedMs)
    {
        var timerField = typeof(Engine).GetField("_exitDelayTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(timerField);
        var timer = timerField!.GetValue(engine);
        var startField = typeof(RedBolt.Core.Timer).GetField("_startTime", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(startField);
        startField!.SetValue(timer, unchecked(Time.GetSystemTime() - elapsedMs));
    }

    private static Engine CreateHeadlessEngine()
    {
        var engine = new Engine();
        try
        {
            var parameters = new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.EngineExitDelay",
                ApplicationPreferencesDir = Path.GetTempPath(),
                AutoloadPaths = string.Empty,
                ConfigName = string.Empty,
                FrameLimiter = false,
                Headless = true,
                LogName = "none://",
                PsoCache = string.Empty,
                ResourcePackages = string.Empty,
                ResourcePaths = string.Empty,
                ResourcePrefixPaths = string.Empty,
                ResourceRootFile = string.Empty
            };

            if (!engine.Initialize(parameters))
                throw new InvalidOperationException("The headless exit delay engine did not initialize.");

            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    private sealed class ExitDelayProbe : RedObject
    {
        public ExitDelayProbe()
        {
            SubscribeToEvent<CoreEvents.ExitDelayQueryEvent>(OnExitDelayQuery);
        }

        public bool Delay { get; set; }

        public int Queries { get; private set; }

        private void OnExitDelayQuery(ref CoreEvents.ExitDelayQueryEvent eventData)
        {
            ++Queries;
            eventData.Delay |= Delay;
        }
    }
}
