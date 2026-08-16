// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt.Container;
using RedBolt.Core;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ContextResetCoverageGapTests
{
    [Fact]
    public void Context_reset_disposes_named_optional_subsystems_and_clears_state()
    {
        using var context = new Context();
        var disposed = 0;

        context.SetGlobalVar(new StringHash("reset"), 1);
        context.RegisterSubsystem(new object(), new StringHash("VirtualReality"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("PluginManager"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("Audio"));
        context.RegisterSubsystem(new object(), new StringHash("DearImGui"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("Input"));
        context.RegisterSubsystem(new object(), new StringHash("Renderer"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("RenderDevice"));
        context.RegisterSubsystem(new object(), new StringHash("Graphics"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("StateManager"));
        context.RegisterSubsystem(new TrackedDisposable(() => ++disposed), new StringHash("Other"));

        context.Reset();

        Assert.Equal(6, disposed);
        Assert.Empty(context.GetSubsystems().GetContainer());
        Assert.Empty(context.GetGlobalVars());
        Assert.Null(context.GetSubsystem(new StringHash("Other")));
    }

    [Fact]
    public void Context_typed_subsystem_removal_and_missing_reflection_paths_are_deterministic()
    {
        using var context = new Context();
        var subsystem = new TrackedDisposable(static () => { });
        context.RegisterSubsystem(subsystem);

        Assert.Same(subsystem, context.GetSubsystem<TrackedDisposable>());
        Assert.True(context.RemoveSubsystem<TrackedDisposable>());
        Assert.False(context.RemoveSubsystem<TrackedDisposable>());
        Assert.Null(context.GetSubsystem<TrackedDisposable>());

        Assert.Null(context.GetAttributes(new StringHash("Missing.Reflection.Type")));
        Assert.Equal(string.Empty, context.GetTypeName(new StringHash("Missing.Reflection.Type")));
        Assert.Null(context.GetAttribute(new StringHash("Missing.Reflection.Type"), "Missing"));
    }

    [Fact]
    public void Context_registers_untyped_subsystems_and_balances_sdl_lifetime()
    {
        using var context = new Context();
        var subsystem = new object();
        context.RegisterSubsystem(subsystem);

        Assert.Same(subsystem, context.GetSubsystem<object>());
        Assert.True(context.RemoveSubsystem<object>());
        Assert.False(context.RemoveSubsystem<object>());

        Assert.True(context.RequireSdl(0));
        context.ReleaseSdl();
    }

    [Fact]
    public void Context_prunes_expired_event_receivers_from_subscription_buckets()
    {
        using var context = new Context();
        var receiver = new EventReceiver();
        context.SubscribeToEvent<CoreEvents.UpdateEvent>(
            receiver, static (ref CoreEvents.UpdateEvent _) => { });

        var bucketsField = typeof(Context).GetField(
            "_structEventSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(bucketsField);
        var buckets = Assert.IsAssignableFrom<IDictionary>(bucketsField!.GetValue(context));
        var bucket = buckets[typeof(CoreEvents.UpdateEvent)];
        Assert.NotNull(bucket);
        var subscriptionsProperty = bucket!.GetType().GetProperty(
            "Subscriptions", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(subscriptionsProperty);
        var subscriptions = Assert.IsAssignableFrom<IList>(subscriptionsProperty!.GetValue(bucket));
        var handler = Assert.IsType<EventHandler>(subscriptions[0]);

        var receiverField = typeof(EventHandler).GetField(
            "_receiver", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(receiverField);
        var weakReceiver = Assert.IsType<WeakReference<RedObject>>(receiverField!.GetValue(handler));
        weakReceiver.SetTarget(null!);

        Assert.False(context.HasSubscribedToEvent<CoreEvents.UpdateEvent>(receiver));
        Assert.Null(context.GetEventHandler());
    }

    private sealed class TrackedDisposable : IDisposable
    {
        private readonly Action _onDispose;

        public TrackedDisposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose() => _onDispose();
    }

    private sealed class EventReceiver : RedObject
    {
    }
}
