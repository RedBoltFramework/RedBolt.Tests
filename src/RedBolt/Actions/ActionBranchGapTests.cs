namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionBranchGapTests
{
    [Fact]
    public void Sequence_state_covers_zero_and_unit_splits_reverse_switch_and_infinite_tail()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var zero = new DelayTime();
            zero.SetDuration(0.0f);
            using var one = new DelayTime();
            one.SetDuration(1.0f);
            using var sequence = new ExposedSequence();
            sequence.SetFirstAction(zero);
            sequence.SetSecondAction(one);

            using var state = sequence.Start(target);
            state.Update(1.0f);
            state.Update(0.0f);
            state.Stop();

            using var unitSequence = new ExposedSequence();
            unitSequence.SetFirstAction(one);
            unitSequence.SetSecondAction(zero);
            using var unitState = unitSequence.Start(target);
            unitState.Update(0.0f);
            unitState.Update(1.0f);

            using var infinite = new RepeatForever();
            infinite.SetInnerAction(new DelayTime());
            using var infiniteSequence = new ExposedSequence();
            infiniteSequence.SetFirstAction(one);
            infiniteSequence.SetSecondAction(infinite);
            using var infiniteState = infiniteSequence.Start(target);
            infiniteState.Update(1.0f);
            Assert.False(infiniteState.IsDone);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Parallel_mutation_and_state_stop_paths_cover_existing_and_removed_actions()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var first = new DelayTime();
            first.SetDuration(1.0f);
            using var second = new DelayTime();
            second.SetDuration(2.0f);
            using var parallel = new Parallel();
            parallel.AddAction(first);
            parallel.AddAction(second);
            parallel.SetNumActions(1);
            Assert.Equal(1, parallel.NumActions);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Action_manager_rejects_duplicate_actions_and_honors_paused_targets()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new TrackingAction();
        var first = Assert.IsType<TrackingState>(manager.AddAction(action, target));
        Assert.Null(manager.AddAction(action, target));

        using var pausedTarget = new Node();
        using var pausedAction = new TrackingAction();
        var paused = Assert.IsType<TrackingState>(manager.AddAction(pausedAction, pausedTarget, paused: true));
        manager.Update(1.0f);
        Assert.Empty(paused.Updates);
        Assert.NotEmpty(first.Updates);
        manager.CancelAllActions();
    }

    [Fact]
    public void Action_manager_completion_normalizes_maximum_finite_duration()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new TrackingAction(float.MaxValue);

        Assert.NotNull(manager.AddAction(action, target));
        manager.CompleteAllActionsOnTarget(target);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    private sealed class ExposedSequence : Sequence
    {
        public FiniteTimeActionState Start(RedObject target) => (FiniteTimeActionState)base.StartAction(target);
    }

    private sealed class TrackingAction : FiniteTimeAction
    {
        public TrackingAction(float duration = 1.0f)
        {
            SetDuration(duration);
        }

        protected override ActionState StartAction(RedObject? target) => new TrackingState(this, target);
    }

    private sealed class TrackingState(FiniteTimeAction action, RedObject? target)
        : FiniteTimeActionState(action, target)
    {
        public List<float> Updates { get; } = [];

        public override void Update(float time) => Updates.Add(time);
    }
}
