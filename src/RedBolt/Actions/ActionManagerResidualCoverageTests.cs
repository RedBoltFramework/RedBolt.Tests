// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionManagerResidualCoverageTests
{
    [Fact]
    public void Action_instant_serializes_and_starts_an_instant_state()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new ActionInstant();

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        var state = Assert.IsType<ActionInstantState>(manager.AddAction(action, target));
        manager.Update(0.0f);

        Assert.True(state.IsDone);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Action_manager_bulk_cleanup_removes_expired_targets()
    {
        using var manager = new ActionManager(false);

        AddExpiredEntry(manager);
        manager.CompleteAllActions();

        AddExpiredEntry(manager);
        manager.CancelAllActions();

        CompleteExpiredPair(manager);
        CancelExpiredPair(manager);

        Assert.Empty(GetTargets(manager));
    }

    [Fact]
    public void Action_manager_expired_cleanup_handles_target_removal_during_disposal()
    {
        using var manager = new ActionManager(false);
        using var target = new OrphanTarget();

        using (var action = new RemoveAllOnDisposeAction(manager))
        {
            Assert.NotNull(manager.AddAction(action, target));
            ExpireTargetReference(manager, 0);
            manager.CompleteAllActions();
        }

        using (var action = new RemoveAllOnDisposeAction(manager))
        {
            Assert.NotNull(manager.AddAction(action, target));
            ExpireTargetReference(manager, 0);
            manager.CancelAllActions();
        }
    }

    [Fact]
    public void Action_manager_update_skips_entries_without_target_references()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new TrackingAction(1.0f);

        Assert.NotNull(manager.AddAction(action, target));
        SetTargetReference(manager, 0, null);
        manager.Update(0.0f);

        SetTargetReference(manager, 0, new WeakReference<RedObject>(target));
        manager.CancelAllActions();
        Assert.Empty(GetTargets(manager));
    }

    [Fact]
    public void Action_manager_update_skips_an_entry_removed_by_another_action()
    {
        using var manager = new ActionManager(false);
        using var firstTarget = new Node();
        using var secondTarget = new Node();
        using var firstAction = new CancelOtherAction(manager, secondTarget);
        using var secondAction = new TrackingAction(2.0f);

        Assert.NotNull(manager.AddAction(firstAction, firstTarget));
        var secondState = Assert.IsType<TrackingState>(manager.AddAction(secondAction, secondTarget));

        manager.Update(0.0f);

        Assert.True(firstAction.State!.Updated);
        Assert.True(secondState.IsDisposed);
        Assert.Equal(1, manager.GetNumActions(firstTarget));

        manager.CancelAllActions();
    }

    [Fact]
    public void Action_manager_stops_processing_when_a_target_reference_expires()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var firstAction = new ExpireTargetAction(manager);
        using var secondAction = new TrackingAction(2.0f);

        Assert.NotNull(manager.AddAction(firstAction, target));
        Assert.NotNull(manager.AddAction(secondAction, target));

        manager.Update(0.0f);

        Assert.True(firstAction.State!.Updated);
        Assert.Empty(secondAction.State!.Updates);

        SetTargetReference(manager, 0, new WeakReference<RedObject>(target));
        manager.CancelAllActions();
    }

    [Fact]
    public void Action_ease_variants_start_their_inner_states()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var backOut = AddEase(manager, target, new EaseBackOut());
        using var bounceIn = AddEase(manager, target, new EaseBounceIn());
        using var sineIn = AddEase(manager, target, new EaseSineIn());
        using var exponentialIn = AddEase(manager, target, new EaseExponentialIn());
        using var elasticOut = AddEase(manager, target, new EaseElasticOut());
        using var bounceOut = Assert.IsType<EaseBounceOut>(bounceIn.Reverse());
        using var sineOut = Assert.IsType<EaseSineOut>(sineIn.Reverse());
        using var exponentialOut = Assert.IsType<EaseExponentialOut>(exponentialIn.Reverse());

        manager.Update(0.0f);

        Assert.Equal(5, manager.GetNumActions(target));
    }

    [Fact]
    public void Parallel_stops_all_inner_states_when_finished()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var first = new TrackingAction(1.0f);
        using var second = new TrackingAction(2.0f);
        using var parallel = new Parallel();
        parallel.AddAction(first);
        parallel.AddAction(second);

        Assert.NotNull(manager.AddAction(parallel, target));
        manager.Update(0.0f);
        manager.Update(2.0f);

        Assert.True(first.State!.Stopped);
        Assert.True(second.State!.Stopped);
    }

    [Fact]
    public void Repeat_exercises_final_iteration_and_stop_paths()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var inner = new TrackingAction(1.0f);
            using var repeat = new Repeat();
            repeat.SetInnerAction(inner);
            repeat.SetTimes(2);

            var state = Assert.IsAssignableFrom<FiniteTimeActionState>(manager.AddAction(repeat, target));
            state.Update(1.1f);

            Assert.True(state.IsDone);
            state.Stop();
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_forever_stops_its_current_inner_state()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var inner = new TrackingAction(1.0f);
            using var repeat = new RepeatForever();
            repeat.SetInnerAction(inner);

            var state = Assert.IsAssignableFrom<FiniteTimeActionState>(manager.AddAction(repeat, target));
            state.Stop();

            Assert.True(inner.State!.Stopped);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_exercises_zero_split_one_split_and_done_paths()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();

            using (var first = new ActionInstant())
            using (var repeated = new RepeatForever())
            using (var repeatedInner = new ActionInstant())
            using (var zeroSplit = new Sequence())
            {
                repeated.SetInnerAction(repeatedInner);
                zeroSplit.SetFirstAction(first);
                zeroSplit.SetSecondAction(repeated);

                var state = Assert.IsAssignableFrom<FiniteTimeActionState>(manager.AddAction(zeroSplit, target));
                state.Update(-0.1f);
            }

            using (var repeated = new RepeatForever())
            using (var repeatedInner = new ActionInstant())
            using (var second = new ActionInstant())
            using (var oneSplit = new Sequence())
            {
                repeated.SetInnerAction(repeatedInner);
                oneSplit.SetFirstAction(repeated);
                oneSplit.SetSecondAction(second);

                var state = Assert.IsAssignableFrom<FiniteTimeActionState>(manager.AddAction(oneSplit, target));
                state.Update(1.0f);
            }

            using (var first = new ActionInstant())
            using (var second = new ActionInstant())
            using (var sequence = new Sequence())
            {
                sequence.SetFirstAction(first);
                sequence.SetSecondAction(second);

                var state = Assert.IsAssignableFrom<FiniteTimeActionState>(manager.AddAction(sequence, target));
                state.Update(0.0f);
                state.Update(0.0f);
            }

            manager.CancelAllActions();
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Attribute_and_finite_time_states_handle_null_helpers()
    {
        using var action = new FiniteTimeAction();
        using var attributeState = new ExposedAttributeState(action);
        var value = new Variant(1.0f);
        try
        {
            attributeState.InvokeBaseUpdate(0.0f, ref value);
            attributeState.InvokeGet(ref value);

            Assert.True(value.IsEmpty);
        }
        finally
        {
            value.Dispose();
        }

        using var finiteState = new ExposedFiniteTimeState(action);
        Assert.Null(finiteState.InvokeStart(null, null));
    }

    [Fact]
    public void Rotate_around_handles_a_non_serializable_target()
    {
        using var manager = new ActionManager(false);
        using var target = new NonSerializableTarget();
        using var action = new RotateAround();
        action.SetDuration(1.0f);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(1.0f);

        Assert.Equal(0, manager.GetNumActions(target));
    }

    private static T AddEase<T>(ActionManager manager, RedObject target, T action)
        where T : ActionEase
    {
        var inner = new TrackingAction(1.0f);
        action.SetInnerAction(inner);
        Assert.NotNull(manager.AddAction(action, target));
        return action;
    }

    private static void AddExpiredEntry(ActionManager manager)
    {
        using var target = new OrphanTarget();
        using var action = new TrackingAction(1.0f);
        Assert.NotNull(manager.AddAction(action, target));
        ExpireTargetReference(manager, GetTargets(manager).Count - 1);
    }

    private static void CompleteExpiredPair(ActionManager manager)
    {
        ExerciseExpiredPair(manager, complete: true);
    }

    private static void CancelExpiredPair(ActionManager manager)
    {
        ExerciseExpiredPair(manager, complete: false);
    }

    private static void ExerciseExpiredPair(ActionManager manager, bool complete)
    {
        using var firstTarget = new OrphanTarget();
        using var secondTarget = new OrphanTarget();
        using var firstAction = new RemoveOtherOnDisposeAction(manager);
        using var secondAction = new TrackingAction(1.0f);

        Assert.NotNull(manager.AddAction(firstAction, firstTarget));
        var secondState = Assert.IsType<TrackingState>(manager.AddAction(secondAction, secondTarget));
        ExpireTargetReference(manager, 0);
        ExpireTargetReference(manager, 1);

        if (complete)
            manager.CompleteAllActions();
        else
            manager.CancelAllActions();

        secondState.Dispose();
    }

    private static IList GetTargets(ActionManager manager)
    {
        var field = typeof(ActionManager).GetField("_targets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IList>(field!.GetValue(manager));
    }

    private static void ExpireTargetReference(ActionManager manager, int index)
    {
        var element = GetTargets(manager)[index]!;
        var field = element.GetType().GetField("Target", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(field);
        var weakTarget = Assert.IsType<WeakReference<RedObject>>(field!.GetValue(element));
        weakTarget.SetTarget(null!);
    }

    private static void SetTargetReference(
        ActionManager manager, int index, WeakReference<RedObject>? targetReference)
    {
        var element = GetTargets(manager)[index]!;
        var field = element.GetType().GetField("Target", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(field);
        field!.SetValue(element, targetReference);
    }

    private sealed class TrackingAction : FiniteTimeAction
    {
        public TrackingAction(float duration)
        {
            SetDuration(duration);
        }

        public TrackingState? State { get; private set; }

        protected override ActionState StartAction(RedObject? target)
        {
            State = new TrackingState(this, target);
            return State;
        }
    }

    private sealed class TrackingState : FiniteTimeActionState
    {
        public TrackingState(FiniteTimeAction action, RedObject? target)
            : base(action, target)
        {
        }

        public List<float> Updates { get; } = [];

        public bool IsDisposed { get; private set; }

        public bool Stopped { get; private set; }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public override void Stop()
        {
            Stopped = true;
            base.Stop();
        }

        public override void Update(float time)
        {
            Updates.Add(time);
        }
    }

    private sealed class CancelOtherAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;
        private readonly RedObject _otherTarget;

        public CancelOtherAction(ActionManager manager, RedObject otherTarget)
        {
            _manager = manager;
            _otherTarget = otherTarget;
            SetDuration(2.0f);
        }

        public CancelOtherState? State { get; private set; }

        protected override ActionState StartAction(RedObject? target)
        {
            State = new CancelOtherState(this, target, _manager, _otherTarget);
            return State;
        }
    }

    private sealed class CancelOtherState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;
        private readonly RedObject _otherTarget;

        public CancelOtherState(
            FiniteTimeAction action, RedObject? target, ActionManager manager, RedObject otherTarget)
            : base(action, target)
        {
            _manager = manager;
            _otherTarget = otherTarget;
        }

        public bool Updated { get; private set; }

        public override void Update(float time)
        {
            if (Updated)
                return;
            Updated = true;
            _manager.CancelAllActionsFromTarget(_otherTarget);
        }
    }

    private sealed class ExpireTargetAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;

        public ExpireTargetAction(ActionManager manager)
        {
            _manager = manager;
            SetDuration(2.0f);
        }

        public ExpireTargetState? State { get; private set; }

        protected override ActionState StartAction(RedObject? target)
        {
            State = new ExpireTargetState(this, target, _manager);
            return State;
        }
    }

    private sealed class RemoveOtherOnDisposeAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;

        public RemoveOtherOnDisposeAction(ActionManager manager)
        {
            _manager = manager;
            SetDuration(1.0f);
        }

        protected override ActionState StartAction(RedObject? target)
        {
            return new RemoveOtherOnDisposeState(this, target, _manager);
        }
    }

    private sealed class RemoveOtherOnDisposeState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;
        private bool _removed;

        public RemoveOtherOnDisposeState(FiniteTimeAction action, RedObject? target, ActionManager manager)
            : base(action, target)
        {
            _manager = manager;
        }

        public override void Dispose()
        {
            if (!_removed)
            {
                _removed = true;
                var targets = GetTargets(_manager);
                if (targets.Count > 1)
                    targets.RemoveAt(1);
            }
            base.Dispose();
        }
    }

    private sealed class RemoveAllOnDisposeAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;

        public RemoveAllOnDisposeAction(ActionManager manager)
        {
            _manager = manager;
            SetDuration(1.0f);
        }

        protected override ActionState StartAction(RedObject? target) =>
            new RemoveAllOnDisposeState(this, target, _manager);
    }

    private sealed class RemoveAllOnDisposeState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;

        public RemoveAllOnDisposeState(FiniteTimeAction action, RedObject? target, ActionManager manager)
            : base(action, target)
        {
            _manager = manager;
        }

        public override void Dispose()
        {
            GetTargets(_manager).Clear();
            base.Dispose();
        }
    }

    private sealed class ExpireTargetState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;

        public ExpireTargetState(FiniteTimeAction action, RedObject? target, ActionManager manager)
            : base(action, target)
        {
            _manager = manager;
        }

        public bool Updated { get; private set; }

        public override void Update(float time)
        {
            if (Updated)
                return;
            Updated = true;
            ExpireTargetReference(_manager, 0);
        }
    }

    private sealed class ExposedAttributeState : AttributeActionState
    {
        public ExposedAttributeState(FiniteTimeAction action)
            : base(action, null, null)
        {
        }

        public void InvokeBaseUpdate(float time, ref Variant value)
        {
            base.Update(time, ref value);
        }

        public void InvokeGet(ref Variant value)
        {
            Get(ref value);
        }
    }

    private sealed class ExposedFiniteTimeState : FiniteTimeActionState
    {
        public ExposedFiniteTimeState(FiniteTimeAction action)
            : base(action, null)
        {
        }

        public FiniteTimeActionState? InvokeStart(FiniteTimeAction? action, RedObject? target)
        {
            return StartAction(action, target);
        }
    }

    private sealed class OrphanTarget : RedObject
    {
    }

    private sealed class NonSerializableTarget : RedObject
    {
    }
}
