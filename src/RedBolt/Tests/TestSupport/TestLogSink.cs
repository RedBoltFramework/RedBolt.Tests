// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;

/// <summary>Captures engine log events for test diagnostics without writing them to the console.</summary>
public sealed class TestLogSink : RedObject
{
    private readonly object _gate = new();
    private readonly List<StoredLogMessage> _messages = [];

    public TestLogSink(Log log)
    {
        ArgumentNullException.ThrowIfNull(log);
        SubscribeToEvent<IOEvents.LogMessage>(log, HandleLogMessage);
    }

    public IReadOnlyList<StoredLogMessage> Messages
    {
        get
        {
            lock (_gate)
                return _messages.ToArray();
        }
    }

    private void HandleLogMessage(ref IOEvents.LogMessage eventData)
    {
        lock (_gate)
        {
            _messages.Add(new StoredLogMessage(
                (LogLevel)eventData.Level,
                eventData.Time,
                eventData.Logger,
                eventData.Message));
        }
    }
}
