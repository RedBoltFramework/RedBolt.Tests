// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using IOEvents = RedBolt.IO.IOEvents;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LogSinkTests
{
    [Fact]
    public void Open_rotates_existing_files_in_numbered_order()
    {
        using var scope = new TestArtifactScope(nameof(Open_rotates_existing_files_in_numbered_order));
        var fileName = Path.Combine(scope.DirectoryPath, "engine.log");
        File.WriteAllText(fileName, "current");
        File.WriteAllText(fileName + ".1", "previous");

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLogFormat("%v");
            var parameters = new LogFileParams
            {
                RotateOnOpen = true,
                RotateBySize = false,
                MaxFiles = 3,
                MaxSize = 1024
            };

            log.Open(fileName, in parameters);
            log.Close();
        });

        Assert.Empty(File.ReadAllText(fileName));
        Assert.Equal("current", File.ReadAllText(fileName + ".1"));
        Assert.Equal("previous", File.ReadAllText(fileName + ".2"));
    }

    [Fact]
    public void Rotate_by_size_preserves_complete_entries_in_numbered_sink()
    {
        using var scope = new TestArtifactScope(nameof(Rotate_by_size_preserves_complete_entries_in_numbered_sink));
        var fileName = Path.Combine(scope.DirectoryPath, "size.log");
        var first = "first-entry-" + Guid.NewGuid().ToString("N");
        var second = "second-entry-" + Guid.NewGuid().ToString("N");

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLevel(LogLevel.Trace);
            log.SetLogFormat("%v");
            var parameters = new LogFileParams
            {
                RotateOnOpen = false,
                RotateBySize = true,
                MaxFiles = 2,
                MaxSize = (uint)(first.Length + Environment.NewLine.Length + 1)
            };

            log.Open(fileName, in parameters);
            Log.GetLogger("rotation").Info(first);
            Log.GetLogger("rotation").Info(second);
            log.Close();
        });

        Assert.Equal(first + Environment.NewLine, File.ReadAllText(fileName + ".1"));
        Assert.Equal(second + Environment.NewLine, File.ReadAllText(fileName));
    }

    [Fact]
    public void Duplicate_errors_are_suppressed_and_summary_is_emitted_on_next_error()
    {
        using var scope = new TestArtifactScope(nameof(Duplicate_errors_are_suppressed_and_summary_is_emitted_on_next_error));
        var fileName = Path.Combine(scope.DirectoryPath, "duplicates.log");
        var duplicate = "duplicate-error-" + Guid.NewGuid().ToString("N");
        var distinct = "distinct-error-" + Guid.NewGuid().ToString("N");

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLevel(LogLevel.Trace);
            log.SetLogFormat("%v");
            var parameters = new LogFileParams { RotateOnOpen = false, MaxFiles = 0 };
            log.Open(fileName, in parameters);
            var logger = Log.GetLogger("duplicate");
            logger.Error(duplicate);
            logger.Error(duplicate);
            logger.Error(duplicate);
            logger.Error(distinct);
            log.Close();
        });

        Assert.Equal(
        [
            duplicate,
            "Skipped 2 duplicate messages..",
            distinct
        ], File.ReadAllLines(fileName));
    }

    [Fact]
    public void Concurrent_loggers_share_one_serialized_sink_without_lost_entries()
    {
        using var scope = new TestArtifactScope(nameof(Concurrent_loggers_share_one_serialized_sink_without_lost_entries));
        var fileName = Path.Combine(scope.DirectoryPath, "concurrent.log");
        const int workerCount = 4;
        const int messagesPerWorker = 40;

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLevel(LogLevel.Trace);
            log.SetLogFormat("%v");
            var parameters = new LogFileParams { RotateOnOpen = false, MaxFiles = 0 };
            log.Open(fileName, in parameters);

            var threads = Enumerable.Range(0, workerCount)
                .Select(worker => new Thread(() =>
                {
                    var logger = Log.GetLogger("worker");
                    for (var index = 0; index < messagesPerWorker; ++index)
                        logger.Info($"worker-{worker:D2}-{index:D3}");
                }))
                .ToArray();
            foreach (var thread in threads)
                thread.Start();
            foreach (var thread in threads)
                thread.Join();
            log.Close();
        });

        var lines = File.ReadAllLines(fileName);
        Assert.Equal(workerCount * messagesPerWorker, lines.Length);
        Assert.Equal(lines.Length, lines.Distinct(StringComparer.Ordinal).Count());
        Assert.All(lines, line => Assert.Matches("^worker-\\d{2}-\\d{3}$", line));
    }

    [Fact]
    public void Invalid_paths_fall_back_without_leaking_a_sink_and_dispose_is_repeatable()
    {
        using var scope = new TestArtifactScope(nameof(Invalid_paths_fall_back_without_leaking_a_sink_and_dispose_is_repeatable));
        var directoryPath = Path.Combine(scope.DirectoryPath, "not-a-file");
        Directory.CreateDirectory(directoryPath);

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLevel(LogLevel.Trace);
            log.Open(directoryPath);
            log.Close();
            log.Dispose();
            log.Dispose();
        });

        Assert.True(Directory.Exists(directoryPath));
        Assert.Empty(Directory.EnumerateFiles(scope.DirectoryPath));
    }

    [Fact]
    public void Logger_formatting_and_invalid_levels_cover_managed_logging_edges()
    {
        using var scope = new TestArtifactScope(nameof(Logger_formatting_and_invalid_levels_cover_managed_logging_edges));
        var fileName = Path.Combine(scope.DirectoryPath, "format.log");

        WithLog(log =>
        {
            log.SetQuiet(true);
            log.SetLevel(LogLevel.Trace);
            log.SetLogFormat("%v");
            var parameters = new LogFileParams { RotateOnOpen = false, MaxFiles = 0 };
            log.Open(fileName, in parameters);

            var logger = Log.GetLogger("format");
            logger.Info("plain");
            logger.Info("escaped {{value}} {0} {1}", "inserted", null);
            logger.Info("unclosed {", "ignored");
            logger.Write((LogLevel)999, "invalid-level");
            log.SetLevel((LogLevel)999);

            logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, default, "microsoft", null,
                static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "microsoft-error",
                new InvalidOperationException("expected"), static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, default, "microsoft-trace", null,
                static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.Debug, default, "microsoft-debug", null,
                static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.Warning, default, "microsoft-warning", null,
                static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.Critical, default, "microsoft-critical", null,
                static (state, _) => state);
            logger.Log(Microsoft.Extensions.Logging.LogLevel.None, default, "microsoft-none", null,
                static (state, _) => state);
            logger.Log(LogLevel.Info, default, "native", null, static (state, _) => state);
            logger.Log(LogLevel.Error, default, "native-error", new InvalidOperationException("expected"),
                static (state, _) => state);
            logger.Write(LogLevel.None, "native-none");
            logger.Write(LogLevel.MaxLogLevels, "native-max");
            logger.Write((LogLevel)(-1), "native-negative");

            var receiver = new LogEventReceiver(log);
            logger.Info("recursive");
            receiver.Dispose();

            var worker = new Thread(log.PumpThreadMessages);
            worker.Start();
            worker.Join();
            log.SendMessageEvent(LogLevel.None, 0, "invalid", "ignored");
            log.SendMessageEvent((LogLevel)(-1), 0, "invalid-negative", "ignored");
            log.Close();
        });

        Assert.Contains("plain", File.ReadAllLines(fileName));

        var previous = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            Log.GetLogger("without-log").Info("ignored");
            using var noLog = new Log();
            noLog.SendMessageEvent(LogLevel.Info, 0, "no-log-subsystem", "ignored");
        }
        finally
        {
            Context.SetInstance(previous);
            context.Dispose();
        }
    }

    private static void WithLog(Action<Log> action)
    {
        using var mainThreadScope = new WorkQueue();
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

    private sealed class LogEventReceiver : RedObject
    {
        private readonly Log _log;

        public LogEventReceiver(Log log)
        {
            _log = log;
            SubscribeToEvent<IOEvents.LogMessage>(_log, HandleLogMessage);
        }

        private void HandleLogMessage(ref IOEvents.LogMessage _)
        {
            _log.SendMessageEvent(LogLevel.Info, 0, "recursive", "ignored");
        }
    }

}
