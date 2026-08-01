namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

public sealed class ActionManagerEdgeCoverageTests
{
    [Fact]
    public void Null_inputs_and_empty_manager_operations_are_noops()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new FiniteTimeAction();

        Assert.Null(manager.AddAction(null, target));
        Assert.Null(manager.AddAction(action, null));
        Assert.Equal(0, manager.GetNumActions(null));

        manager.CancelAction(null);
        manager.CancelAllActionsFromTarget(null);
        manager.CompleteAllActionsOnTarget(null);
        manager.CancelAllActions();
        manager.CompleteAllActions();
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Cancel_action_removes_pending_states_without_completing_them()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var first = new TrackingAction(2.0f);
        using var second = new TrackingAction(2.0f);

        var firstState = Assert.IsType<TrackingState>(manager.AddAction(first, target));
        var secondState = Assert.IsType<TrackingState>(manager.AddAction(second, target));
        Assert.Equal(2, manager.GetNumActions(target));

        manager.CancelAction(secondState);
        Assert.Equal(1, manager.GetNumActions(target));
        Assert.Empty(secondState.Updates);

        manager.CancelAction(firstState);
        Assert.Equal(0, manager.GetNumActions(target));
        Assert.Empty(firstState.Updates);
    }

    [Fact]
    public void Current_action_can_cancel_itself_during_update()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new SelfCancellingAction(manager);

        var state = Assert.IsType<SelfCancellingState>(manager.AddAction(action, target));
        manager.Update(0.25f);

        Assert.True(state.Updated);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Non_finite_action_completion_uses_zero_step_duration()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new BaseAction();

        Assert.NotNull(manager.AddAction(action, target));
        manager.CompleteAllActionsOnTarget(target);

        Assert.Equal(0, manager.GetNumActions(target));
    }

    private sealed class TrackingAction : FiniteTimeAction
    {
        public TrackingAction(float duration)
        {
            SetDuration(duration);
        }

        protected override ActionState StartAction(RedObject? target) => new TrackingState(this, target);
    }

    private sealed class TrackingState : FiniteTimeActionState
    {
        public TrackingState(FiniteTimeAction action, RedObject? target)
            : base(action, target)
        {
        }

        public List<float> Updates { get; } = [];

        public override void Update(float time) => Updates.Add(time);
    }

    private sealed class SelfCancellingAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;

        public SelfCancellingAction(ActionManager manager)
        {
            _manager = manager;
            SetDuration(2.0f);
        }

        protected override ActionState StartAction(RedObject? target) =>
            new SelfCancellingState(this, target, _manager);
    }

    private sealed class SelfCancellingState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;

        public SelfCancellingState(FiniteTimeAction action, RedObject? target, ActionManager manager)
            : base(action, target)
        {
            _manager = manager;
        }

        public bool Updated { get; private set; }

        public override void Update(float time)
        {
            Updated = true;
            _manager.CancelAction(this);
        }
    }
}
