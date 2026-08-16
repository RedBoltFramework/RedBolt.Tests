// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class ActionBoundaryTests
{
    [Fact]
    public void Finite_time_state_clamps_time_and_stop_preserves_original_target()
    {
        using var action = new FiniteTimeAction();
        action.SetDuration(2.0f);
        using var target = new Node();
        using var state = new ProbeState(action, target);

        Assert.Same(action, state.Action);
        Assert.Same(target, state.Target);
        Assert.Same(target, state.OriginalTarget);

        state.CallStep(0.5f);
        state.CallStep(-1.0f);
        state.CallStep(10.0f);

        Assert.Equal([0.0f, 0.0f, 1.0f], state.Updates);
        Assert.True(state.IsDone);

        state.Stop();

        Assert.Null(state.Target);
        Assert.Same(target, state.OriginalTarget);
    }

    [Fact]
    public void Action_manager_keeps_paused_actions_until_explicit_completion()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new ProbeAction(2.0f);

        var state = Assert.IsType<ProbeState>(manager.AddAction(action, target, paused: true));

        manager.Update(2.0f);

        Assert.Empty(state.Updates);
        Assert.Equal(1, manager.GetNumActions(target));

        manager.CompleteAllActionsOnTarget(target);

        Assert.Equal([0.0f, 1.0f], state.Updates);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Action_manager_runs_action_added_during_current_target_update()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var receiver = new SchedulingReceiver(manager, target);
        using var first = new CallFunc();
        using var second = new CallFunc();
        using var firstHandler = new ActionCallHandlerImpl<SchedulingReceiver>(
            receiver, static (instance, actionTarget) => instance.Schedule(actionTarget));
        using var secondHandler = new ActionCallHandlerImpl<SchedulingReceiver>(
            receiver, static (instance, actionTarget) => instance.Record(actionTarget));

        second.SetCallHandler(secondHandler);
        receiver.ActionToSchedule = second;
        first.SetCallHandler(firstHandler);

        Assert.NotNull(manager.AddAction(first, target));
        manager.Update(0.0f);

        Assert.Equal(2, receiver.CallCount);
        Assert.Equal(2, manager.GetNumActions(target));

        manager.Update(1.0f);

        Assert.Equal(1, manager.GetNumActions(target));

        manager.Update(1.0f);

        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Action_library_registration_is_idempotent_for_a_fresh_context()
    {
        using var context = new Context();

        ActionLibrary.RegisterActionLibrary(context);
        ActionLibrary.RegisterActionLibrary(context);

        Assert.NotNull(context.GetReflection(typeof(MoveBy).GetTypeId()));
        Assert.NotNull(context.GetReflection(typeof(Sequence).GetTypeId()));
    }

    [Fact]
    public void Finite_time_action_uses_shared_empty_action_for_null_defaults()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var manager = new ActionManager(false);
            context.RegisterSubsystem(manager);
            using var action = new FiniteTimeAction();

            Assert.Same(manager.GetEmptyAction(), action.GetOrDefault(null));
            Assert.Same(action, action.GetOrDefault(action));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private sealed class ProbeAction : FiniteTimeAction
    {
        public ProbeAction(float duration)
        {
            SetDuration(duration);
        }

        protected override ActionState StartAction(RedObject? target) => new ProbeState(this, target);
    }

    private sealed class ProbeState : FiniteTimeActionState
    {
        public ProbeState(FiniteTimeAction action, RedObject? target)
            : base(action, target)
        {
        }

        public List<float> Updates { get; } = [];

        public void CallStep(float dt) => Step(dt);

        public override void Update(float time) => Updates.Add(time);
    }

    private sealed class SchedulingReceiver : RedObject
    {
        private readonly ActionManager _manager;
        private readonly RedObject _target;

        public SchedulingReceiver(ActionManager manager, RedObject target)
        {
            _manager = manager;
            _target = target;
        }

        public CallFunc? ActionToSchedule { get; set; }

        public int CallCount { get; private set; }

        public void Schedule(RedObject? target)
        {
            ++CallCount;
            if (ActionToSchedule is not null)
            {
                _manager.AddAction(ActionToSchedule, _target);
                ActionToSchedule = null;
            }
        }

        public void Record(RedObject? target) => ++CallCount;
    }
}
