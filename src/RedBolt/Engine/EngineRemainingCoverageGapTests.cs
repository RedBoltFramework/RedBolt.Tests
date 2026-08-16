// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Engine;
using RedBolt.IO;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineRemainingCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public EngineRemainingCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Engine_runtime_edges_cover_parameter_limits_frame_paths_and_uri_mapping()
    {
        using var ownedEngine = CreateHeadlessEngine();
        var engine = ownedEngine;

        engine.SetMinFps(-1);
        engine.SetMaxFps(-1);
        engine.SetMaxInactiveFps(-1);
        engine.SetTimeStepSmoothing(0);
        Assert.Equal(0, engine.GetMinFps());
        Assert.Equal(0, engine.GetMaxFps());
        Assert.Equal(0, engine.GetMaxInactiveFps());
        Assert.Equal(1, engine.GetTimeStepSmoothing());

        engine.SetPauseMinimized(true);
        Assert.True(engine.GetPauseMinimized());
        engine.SetPauseMinimized(false);
        engine.SetAutoExit(false);
        Assert.False(engine.GetAutoExit());
        engine.SetAutoExit(true);

        engine.SetNextTimeStep(-1.0f);
        Assert.Equal(0.0f, engine.GetNextTimeStep());
        var callbackCount = 0;
        engine.RunFrame(_ => ++callbackCount);
        Assert.Equal(1, callbackCount);
        engine.Update();
        engine.Render();
        // engine.Context.GetSubsystem<Profiler>()!.DumpData();
        engine.DumpResources();
        engine.DumpResources(true);
        engine.DumpMemory();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "relative/file.txt"),
            engine.GetLogFileName("relative/file.txt"));
        Assert.Equal(Path.Combine(engine.GetAppPreferencesDir(), "file.txt"),
            engine.GetLogFileName("conf://file.txt"));
        Assert.Equal(string.Empty, engine.GetLogFileName("memory://file.txt"));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "file.txt"),
            engine.GetLogFileName("file.txt"));

        engine.OnCanvasResize(80, 40, false, 1.25f);
    }

    [Fact]
    public void Engine_private_path_and_screenshot_helpers_cover_empty_and_multi_file_cases()
    {
        var split = InvokePrivateStatic<IReadOnlyList<string>>("SplitPathList", " ; first ; ; second ");
        Assert.Equal(["first", "second"], split);
        Assert.Empty(InvokePrivateStatic<IReadOnlyList<string>>("SplitPathList", "  ;  "));

        var normalizedAbsolute = InvokePrivateStatic<string>("NormalizeConfiguredResourcePath", Path.GetTempPath());
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), normalizedAbsolute);
        var normalizedRelative = InvokePrivateStatic<string>("NormalizeConfiguredResourcePath", "relative-assets");
        Assert.True(Path.IsPathRooted(normalizedRelative));

        var engine = _fixture.Engine;
        var oldCount = engine.Parameters.ScreenshotCount;
        try
        {
            engine.Parameters.ScreenshotCount = 1;
            Assert.Equal("shot.png", InvokePrivate<string>(engine, "GetScreenshotPath", "shot.png"));

            engine.Parameters.ScreenshotCount = 3;
            var plain = InvokePrivate<string>(engine, "GetScreenshotPath", "shot.png");
            var nested = InvokePrivate<string>(engine, "GetScreenshotPath", Path.Combine("shots", "shot.png"));
            Assert.Equal("shot-0001.png", plain);
            Assert.Equal(Path.Combine("shots", "shot-0001.png"), nested);
        }
        finally
        {
            engine.Parameters.ScreenshotCount = oldCount;
        }
    }

    [Fact]
    public void Engine_exit_and_resource_root_branches_cover_disabled_and_requested_paths()
    {
        var engine = _fixture.Engine;
        SetPrivate(engine, "_exitRequired", false);

        engine.SetAutoExit(false);
        InvokePrivate(engine, "HandleExitRequested");
        Assert.False(GetPrivate<bool>(engine, "_exitRequired"));

        engine.SetAutoExit(true);
        InvokePrivate(engine, "HandleExitRequested");
        Assert.True(GetPrivate<bool>(engine, "_exitRequired"));
        SetPrivate(engine, "_exitRequired", false);

        using var uninitialized = new Engine();
        uninitialized.InitializeVirtualFileSystem(enableResourceRootFile: false);
        Assert.Equal(string.Empty, uninitialized.GetAppPreferencesDir());
    }

    [Fact]
    public void Engine_frame_limit_covers_disabled_limiter_and_smoothing_window()
    {
        var engine = _fixture.Engine;
        engine.SetMaxFps(0);
        engine.SetMaxInactiveFps(0);
        engine.SetMinFps(1);
        engine.SetTimeStepSmoothing(3);

        engine.ApplyFrameLimit();
        engine.ApplyFrameLimit();
        engine.ApplyFrameLimit();
        engine.ApplyFrameLimit();

        Assert.True(engine.GetNextTimeStep() >= 0.0f);
    }

    private static void InvokePrivate(object instance, string name, params object?[] arguments)
    {
        typeof(Engine).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);
    }

    private static T InvokePrivate<T>(object instance, string name, params object?[] arguments)
    {
        return (T)typeof(Engine).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments)!;
    }

    private static T InvokePrivateStatic<T>(string name, params object?[] arguments)
    {
        return (T)typeof(Engine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!;
    }

    private static T GetPrivate<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetPrivate(object instance, string name, object? value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static Engine CreateHeadlessEngine()
    {
        var engine = new Engine();
        try
        {
            var parameters = new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.EngineRemainingCoverageGap",
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
                throw new InvalidOperationException("The headless coverage engine did not initialize.");

            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }
}
