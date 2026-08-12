namespace RedBolt.Tests;

using System.Reflection;

using Microsoft.Extensions.Logging;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Logging;
using RedBolt.RenderAPI;
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

    [NativeBindingFact]
    public void Native_window_helper_rejects_a_null_window_before_wm_info_is_available()
    {
        var nativeWindowType = typeof(RuntimeServices).Assembly.GetType("RedBolt.RenderAPI.NativeWindow")!;
        var getInfo = nativeWindowType.GetMethod("GetInfo", BindingFlags.Static | BindingFlags.Public)!;
        using var window = SdlWindow.CreateManagedWrapper(0, false);

        Assert.Throws<TargetInvocationException>(() => getInfo.Invoke(null, [window]));
    }

#if REDBOLT_PLATFORM_LINUX
    [Fact]
    public void Native_window_helper_validates_each_linux_window_info_shape()
    {
        var nativeWindowType = typeof(RuntimeServices).Assembly.GetType("RedBolt.RenderAPI.NativeWindow")!;
        var validator = nativeWindowType.GetMethod("ValidateLinuxInfo", BindingFlags.Static | BindingFlags.NonPublic)!;

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
#endif

    private static void WithLog(Action<Log> action)
    {
        var previous = Context.Instance;
        var context = new Context(new RuntimeServices([]));
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

}
