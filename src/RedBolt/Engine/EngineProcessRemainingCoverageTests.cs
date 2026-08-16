// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineProcessRemainingCoverageTests
{
    [Fact]
    public void Engine_guards_uninitialized_frame_update_profiler_and_render_calls()
    {
        using var engine = new Engine();

        Assert.Throws<ArgumentNullException>(() => engine.RunFrame((Action<float>)null!));
        Assert.Throws<InvalidOperationException>(() => engine.RunFrame());
        Assert.Throws<InvalidOperationException>(() => engine.Update());

        engine.Context.GetSubsystem<Profiler>()!.DumpData();
        engine.Render();
    }

    [Fact]
    public void Engine_headless_frames_cover_negative_step_pause_callback_and_pending_exit_paths()
    {
        using var engine = CreateHeadlessEngine();
        var input = engine.Context.GetSubsystem<Input>();
        Assert.NotNull(input);

        SetPrivateField(engine, "_timeStep", -1.0f);
        SetPrivateField(input!, "_isMinimized", true);
        engine.SetPauseMinimized(true);

        var callbackInvoked = false;
        engine.RunFrame(_ => callbackInvoked = true);

        Assert.False(callbackInvoked);

        SetPrivateField(input!, "_isMinimized", false);
        engine.SetPauseMinimized(false);
        engine.SetNextTimeStep(0.01f);
        var observedTimeStep = -1.0f;
        engine.RunFrame(timeStep => observedTimeStep = timeStep);

        Assert.True(observedTimeStep >= 0.0f);

        SetPrivateField(engine, "_exitRequired", true);
        engine.RunFrame();
        Assert.True(engine.IsExiting);

        // The exiting guard returns before touching the frame subsystems.
        engine.RunFrame();
    }

    [Fact]
    public void Engine_housekeeping_and_log_uri_fallbacks_are_side_effect_free()
    {
        using var engine = CreateHeadlessEngine();

        engine.Context.GetSubsystem<Profiler>()!.DumpData();
        engine.DumpResources();
        engine.DumpResources(dumpFileName: true);
        engine.DumpMemory();
        engine.Update();

        Assert.EndsWith("engine.log", engine.GetLogFileName("engine.log"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, engine.GetLogFileName("unknown://engine.log"));
    }

    [Fact]
    public void Engine_canvas_resize_handles_missing_optional_subsystems()
    {
        using var engine = new Engine();
        var input = engine.Context.GetSubsystem<Input>();
        var graphics = engine.Context.GetSubsystem<Graphics>();
        Assert.NotNull(input);
        Assert.NotNull(graphics);

        Assert.True(engine.Context.RemoveSubsystem<Input>());
        Assert.True(engine.Context.RemoveSubsystem<Graphics>());
        try
        {
            engine.OnCanvasResize(320, 240, isFullScreen: true, dpiScale: 1.5f);
        }
        finally
        {
            // Shutdown expects the normal engine subsystem set, so restore the
            // objects after exercising the nullable canvas path.
            engine.Context.RegisterSubsystem<Input>(input!);
            engine.Context.RegisterSubsystem<Graphics>(graphics!);
        }
    }

    [Fact]
    public void Engine_virtual_file_system_covers_empty_root_absolute_resource_and_autoload_paths()
    {
        using var engine = new Engine();
        var resourcePath = Path.Combine(Path.GetTempPath(), "redbolt-engine-coverage-resource");
        engine.Parameters.ResourceRootFile = string.Empty;
        engine.Parameters.ResourcePrefixPaths = string.Empty;
        engine.Parameters.ResourcePaths = resourcePath;
        engine.Parameters.ResourcePackages = string.Empty;
        engine.Parameters.AutoloadPaths = Path.Combine(Path.GetTempPath(), "redbolt-engine-coverage-autoload");

        engine.InitializeVirtualFileSystem(enableResourceRootFile: true);

        var resources = engine.Context.GetSubsystem<ResourceCache>();
        Assert.NotNull(resources);
        Assert.Contains(Path.GetFullPath(resourcePath), resources!.GetResourceDirs());
    }

    [Fact]
    public void Engine_frame_limit_covers_no_input_focus_and_expired_timeout_paths()
    {
        using var engine = CreateHeadlessEngine();
        var input = engine.Context.GetSubsystem<Input>();
        Assert.NotNull(input);

        // A sub-millisecond target takes the no-sleep branch without a long wait.
        engine.SetMaxFps(2_000);
        engine.ApplyFrameLimit();

        Assert.True(engine.Context.RemoveSubsystem<Input>());
        try
        {
            engine.SetMaxFps(0);
            engine.ApplyFrameLimit();
        }
        finally
        {
            engine.Context.RegisterSubsystem<Input>(input!);
        }

        SetPrivateField(engine, "_timeOut", 1L);
        Thread.Sleep(1);
        engine.ApplyFrameLimit();

        Assert.True(engine.IsExiting);
    }

    [Fact]
    public void Process_utils_covers_portable_queries_and_rejects_url_input_without_spawning()
    {
#if REDBOLT_PLATFORM_WINDOWS
        Assert.Equal(".dll", ProcessUtils.DynLibSuffix);
#else
        Assert.Equal(".so", ProcessUtils.DynLibSuffix);
#endif

        var parsed = ProcessUtils.ParseArguments(new[] { "program", "--win32-console", "--flag" });
#if REDBOLT_PLATFORM_WINDOWS
        Assert.Equal(["--flag"], parsed);
#else
        Assert.Equal(["--win32-console", "--flag"], parsed);
#endif
        Assert.Same(parsed, ProcessUtils.GetArguments());

#if REDBOLT_PLATFORM_WINDOWS
        Assert.Equal(PlatformId.Windows, ProcessUtils.GetPlatform());
        Assert.Equal("Windows", ProcessUtils.GetPlatformName());
#elif REDBOLT_PLATFORM_LINUX
        Assert.Equal(PlatformId.Linux, ProcessUtils.GetPlatform());
        Assert.Equal("Linux", ProcessUtils.GetPlatformName());
#endif

        Assert.True(ProcessUtils.GetNumLogicalCPUs() >= 1);
        Assert.True(ProcessUtils.GetNumPhysicalCPUs() >= 1);
        Assert.True(ProcessUtils.GetTotalMemory() > 0);
        Assert.Throws<ArgumentNullException>(() => ProcessUtils.OpenURL(null!));
        Assert.False(ProcessUtils.OpenURL(string.Empty));
        Assert.False(ProcessUtils.OpenURL("not a URL"));
    }

    private static Engine CreateHeadlessEngine()
    {
        var engine = new Engine();
        try
        {
            var parameters = new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.EngineProcessRemainingCoverage",
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

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
