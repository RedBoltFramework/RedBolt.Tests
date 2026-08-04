namespace RedBolt.Tests;

using System.Reflection;

using Microsoft.Extensions.Logging;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Logging;
using RedBolt.Platform;
using RedBoltLogLevel = RedBolt.Logging.LogLevel;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class IOResidualCoverageTests
{
    [Fact]
    public void Logging_adapter_state_and_level_fallback_edges_are_covered()
    {
        var defaultMessage = new StoredLogMessage();
        Assert.Equal(default, defaultMessage.Level);
        Assert.Equal(0L, defaultMessage.Timestamp);
        Assert.Equal(string.Empty, defaultMessage.Logger);
        Assert.Equal(string.Empty, defaultMessage.Message);

        var detached = new Logger();
        detached.Trace("trace");
        detached.Debug("debug");
        detached.Info("info");
        detached.Warning("warning");
        detached.Error("error");
        detached.Write(RedBoltLogLevel.Info, "formatted {}", "message");

        var microsoftLogger = (ILogger)detached;
        Assert.Null(microsoftLogger.BeginScope("scope"));
        Assert.False(microsoftLogger.IsEnabled(MicrosoftLogLevel.Information));
        detached.Log(RedBoltLogLevel.Info, default, "disabled", null, static (state, _) => state);

        var implType = typeof(Log).Assembly.GetType("RedBolt.Logging.LogImpl")!;
        var levelNameMethod = implType.GetMethod("GetLogLevelName", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(string.Empty, levelNameMethod.Invoke(null, [(RedBoltLogLevel)999]));

        WithLog(log =>
        {
            Assert.Equal(log.Level, log.Level);
            Assert.False(log.IsQuiet);
            log.SetQuiet(true);
            Assert.True(log.IsQuiet);
            log.Open(string.Empty);

            log.SetLevel(RedBoltLogLevel.None);
            Log.GetLogger("disabled-final").Info("this message is filtered");
        });
    }

    [Fact]
    public void Empty_platform_runtime_paths_are_observable_through_the_internal_singleton()
    {
        var property = typeof(RuntimeServices).GetProperty("Empty", BindingFlags.Static | BindingFlags.NonPublic)!;
        var empty = Assert.IsType<RuntimeServices>(property.GetValue(null));
        var platform = empty.Platform;

        Assert.True(platform.SupportsFileLogging);
        Assert.False(platform.UseHighDensityWindow);
        platform.InitializeApplicationParameters(new EngineParameters());

        using var window = SdlWindow.CreateManagedWrapper(0, false);
        Assert.Throws<PlatformNotSupportedException>(() => platform.GetNativeWindowInfo(window));
    }

    [NativeBindingFact]
    public void Linux_runtime_rejects_a_null_native_window_before_wm_info_is_available()
    {
        var platform = Context.Instance.RuntimeServices.Platform;
        using var window = SdlWindow.CreateManagedWrapper(0, false);

        Assert.ThrowsAny<Exception>(() => platform.GetNativeWindowInfo(window));
    }

    [Fact]
    public void Linux_runtime_validates_each_native_window_info_failure_and_success_shape()
    {
        var runtimeType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("RedBolt.Platform.LinuxPlatformRuntime"))
            .First(type => type is not null)!;
        var validator = runtimeType.GetMethod("ValidateNativeWindowInfo", BindingFlags.Static | BindingFlags.NonPublic)!;

        using var failureInfo = new SdlSysWMinfo();
        Assert.Throws<TargetInvocationException>(() => validator.Invoke(null, [SdlBool.SdlFalse, failureInfo]));

        failureInfo.Subsystem = (SdlSyswmType)999;
        Assert.Throws<TargetInvocationException>(() => validator.Invoke(null, [SdlBool.SdlTrue, failureInfo]));

        failureInfo.Subsystem = SdlSyswmType.SdlSyswmX11;
        failureInfo.InfoX11Display = 1;
        Assert.Throws<TargetInvocationException>(() => validator.Invoke(null, [SdlBool.SdlTrue, failureInfo]));

        failureInfo.InfoX11Window = 2;
        var result = (NativeWindowInfo)validator.Invoke(null, [SdlBool.SdlTrue, failureInfo])!;
        Assert.Equal((nint)1, result.Display);
        Assert.Equal(2u, result.WindowId);
    }

    private static void WithLog(Action<Log> action)
    {
        var previous = Context.Instance;
        var context = new Context(new RuntimeServices(new FakePlatformRuntime(), []));
        Context.SetInstance(context);
        var log = new Log();
        context.RegisterSubsystem(log);
        try
        {
            action(log);
        }
        finally
        {
            context.RemoveSubsystem<Log>();
            log.Dispose();
            Context.SetInstance(previous);
            context.Dispose();
        }
    }

    private sealed class FakePlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => true;

        public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

        public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) => default;
    }
}
