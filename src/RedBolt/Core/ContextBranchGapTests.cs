// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Container;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ContextBranchGapTests
{
    [Fact]
    public void Context_event_filters_blocking_and_subsystem_cleanup_cover_remaining_managed_paths()
    {
        using var context = new Context();
        var senderA = new ProbeObject();
        var senderB = new ProbeObject();
        var receiver = new ProbeObject();
        var generalCalls = 0;
        var specificCalls = 0;
        var otherCalls = 0;

        context.SubscribeToEvent<PingEvent>(receiver, (ref PingEvent _) => ++generalCalls);
        context.SubscribeToEvent<PingEvent>(receiver, senderA, (ref PingEvent _) => ++specificCalls);
        context.SubscribeToEvent<OtherEvent>(receiver, (ref OtherEvent _) => ++otherCalls);

        Assert.True(context.HasSubscribedToEvent(receiver));
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver));
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver, senderA));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver, senderB));

        var ping = new PingEvent();
        context.SendEvent(senderA, ref ping);
        Assert.Equal(0, generalCalls);
        Assert.Equal(1, specificCalls);

        receiver.SetBlockEvents(true);
        context.SendEvent(senderB, ref ping);
        receiver.SetBlockEvents(false);
        context.SendEvent(senderB, ref ping);
        Assert.Equal(1, generalCalls);

        var other = new OtherEvent();
        context.SendEvent(senderB, ref other);
        Assert.Equal(1, otherCalls);

        context.UnsubscribeAllExcept(receiver, new[] { senderA });
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver, senderA));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver, senderB));
        context.UnsubscribeAllExcept(receiver, new[] { typeof(PingEvent) });
        Assert.False(context.HasSubscribedToEvent<OtherEvent>(receiver));
        context.UnsubscribeAllExcept(receiver, Array.Empty<Type>());
        Assert.False(context.HasSubscribedToEvent(receiver));

        context.RegisterSubsystem(new object(), new StringHash("ManagedOnly"));
        context.RemoveSubsystem(new StringHash("Missing"));
        context.Reset();
    }

    private sealed class ProbeObject : RedObject
    {
    }

    private struct PingEvent
    {
    }

    private struct OtherEvent
    {
    }
}
