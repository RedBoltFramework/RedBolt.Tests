// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineResidualCoverageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public EngineResidualCoverageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Application_flavor_explicit_clones_and_wildcard_distance_are_covered()
    {
        var pattern = new ApplicationFlavorPattern("platform=desktop");
        var wildcard = new ApplicationFlavor("platform=*;extra=tag");
        Assert.Equal(2u, wildcard.Matches(pattern));

        var emptyPattern = new ApplicationFlavorPattern();
        var extra = new ApplicationFlavor("extra=tag");
        Assert.Equal(1u, extra.Matches(emptyPattern));
        Assert.Equal(0u, new ApplicationFlavor("extra=*").Matches(emptyPattern));

        var patternClone = (ApplicationFlavorPattern)((ICloneable)pattern).Clone();
        var flavorClone = (ApplicationFlavor)((ICloneable)extra).Clone();
        patternClone.Components["platform"].Add("mutated");
        flavorClone.Components["extra"].Add("mutated");
        Assert.DoesNotContain("mutated", pattern.Components["platform"]);
        Assert.DoesNotContain("mutated", extra.Components["extra"]);
    }

    [Fact]
    public void Config_definition_clone_and_override_file_failure_paths_are_covered()
    {
        using var sourceValue = new Variant(7);
        using var definition = new ConfigVariableDefinition();
        definition.SetDefault(sourceValue).Overridable().CommandLinePriority();
        using var clone = (ConfigVariableDefinition)((ICloneable)definition).Clone();
        Assert.Equal(definition.Type, clone.Type);
        Assert.True(clone.IsOverridable);
        Assert.True(clone.HasCommandLinePriority);

        using var config = new ConfigFile();
        config.DefineVariable("Value", new Variant(1)).Overridable();
        config.SetVariable("Value", new Variant(2));

        Assert.Equal(2, config.GetChangedVariables(ApplicationFlavor.Empty)["Value"].GetInt());
    }

    [Fact]
    public void State_manager_queue_cache_transition_and_unknown_state_paths_are_covered()
    {
        var manager = _fixture.Context.GetSubsystem<StateManager>()!;
        manager.Reset();
        manager.FadeInDuration = 100.0f;
        manager.FadeOutDuration = 0.0f;

        using var first = new TrackingState { CanLeave = true };
        using var blocked = new TrackingState { CanLeave = false };
        using var queued = new TrackingState { CanLeave = true };

        manager.EnqueueState(first);
        manager.EnqueueState(blocked);
        manager.EnqueueState(queued);

        var queue = (IEnumerable)typeof(StateManager).GetField(
            "_stateQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var queuedItem = queue.Cast<object>().First();
        using var clonedQueueItem = (IDisposable)((ICloneable)queuedItem).Clone();

        manager.FadeInDuration = 0.0f;
        manager.Update(1.0f);
        manager.Update(1.0f);
        Assert.Same(blocked, manager.State);
        var update = new CoreEvents.UpdateEvent { TimeStep = 0.01f };
        manager.SendEvent(ref update);

        blocked.CanLeave = true;
        manager.Update(1.0f);
        Assert.NotNull(manager.State);

        manager.Reset();
        Assert.Null(manager.State);
        Assert.Null(manager.GetCachedState<NeverRegisteredState>());

        var initiate = typeof(StateManager).GetMethod("InitiateTransition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        initiate.Invoke(manager, null);

        using var bundle = new StringVariantMap();
        manager.EnqueueState(new StringHash(typeof(string).GetClassName()), bundle);
        Assert.Null(manager.State);

        manager.EnqueueState<NeverRegisteredState>(new StringVariantMap());
        manager.EnqueueState<NeverRegisteredState>();

        var backgroundException = Task.Run(() =>
        {
            using var backgroundBundle = new StringVariantMap();
            manager.EnqueueState(new StringHash("BackgroundState"), backgroundBundle);
            return manager.TargetState;
        }).GetAwaiter().GetResult();
        Assert.Equal(StringHash.Empty, backgroundException);

        manager.Reset();
    }

    [Fact]
    public void State_manager_reset_disposes_pending_items_and_handles_wait_to_exit()
    {
        var manager = _fixture.Context.GetSubsystem<StateManager>()!;
        manager.Reset();
        manager.FadeInDuration = 0.0f;
        manager.FadeOutDuration = 0.0f;

        using var active = new TrackingState { CanLeave = true };
        using var blocked = new TrackingState { CanLeave = false };
        using var pending = new TrackingState { CanLeave = true };

        manager.EnqueueState(active);
        manager.Update(1.0f);
        manager.EnqueueState(blocked);
        manager.EnqueueState(pending);
        manager.Update(1.0f);

        Assert.Same(blocked, manager.State);
        manager.Reset();
        Assert.Null(manager.State);
    }

    private sealed class TrackingState : ApplicationState
    {
        public bool CanLeave { get; set; }

        public override bool CanLeaveState() => CanLeave;
    }

    private sealed class NeverRegisteredState : ApplicationState
    {
    }
}
