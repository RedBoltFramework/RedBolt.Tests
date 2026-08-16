// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionManagerCoverageGapTests
{
    [Fact]
    public void Action_manager_dispatches_updates_and_handles_empty_and_null_states()
    {
        using var manager = new ActionManager();
        using var target = new Node();

        manager.Update(0.0f);

        using var action = new TrackingAction(1.0f);
        var state = Assert.IsType<TrackingState>(manager.AddAction(action, target));

        var update = new CoreEvents.UpdateEvent { TimeStep = 0.0f };
        Context.Instance.SendEvent(ref update);
        update.TimeStep = 1.0f;
        Context.Instance.SendEvent(ref update);

        Assert.Equal([0.0f, 1.0f], state.Updates);
        Assert.True(state.IsDisposed);
        Assert.Equal(0, manager.GetNumActions(target));

        var empty = manager.GetEmptyAction();
        Assert.Same(empty, empty.Reverse());
        Assert.Equal(1.1920929E-07f, empty.Duration);

        using var emptyTarget = new Node();
        Assert.NotNull(manager.AddAction(empty, emptyTarget));
        manager.Update(0.0f);
        Assert.Equal(1, manager.GetNumActions(emptyTarget));
        manager.CancelAllActionsFromTarget(emptyTarget);
        Assert.Equal(0, manager.GetNumActions(emptyTarget));

        using var nullStateAction = new NullStateAction();
        Assert.Null(manager.AddAction(nullStateAction, target));
        Assert.Equal(1, manager.GetNumActions(target));
        manager.Update(0.0f);
        Assert.Equal(1, manager.GetNumActions(target));
        manager.CompleteAllActionsOnTarget(target);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Action_manager_bulk_operations_cover_empty_unknown_and_noncurrent_paths()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var otherTarget = new Node();

        manager.CompleteAllActions();
        manager.CancelAllActions();
        manager.CompleteAllActionsOnTarget(null);
        manager.CancelAllActionsFromTarget(null);

        using var first = new TrackingAction(2.0f);
        using var second = new TrackingAction(2.0f);
        var firstState = Assert.IsType<TrackingState>(manager.AddAction(first, target));
        var secondState = Assert.IsType<TrackingState>(manager.AddAction(second, target));

        manager.CompleteAllActionsOnTarget(otherTarget);
        Assert.Equal(2, manager.GetNumActions(target));

        manager.CompleteAllActions();

        Assert.Equal(0, manager.GetNumActions(target));
        Assert.Equal([0.0f, 1.0f], firstState.Updates);
        Assert.Equal([0.0f, 1.0f], secondState.Updates);
        Assert.True(firstState.IsDisposed);
        Assert.True(secondState.IsDisposed);

        manager.CancelAction(secondState);

        using var orphanAction = new FiniteTimeAction();
        using var orphanState = new FiniteTimeActionState(orphanAction, null);
        manager.CancelAction(orphanState);

        using var canceled = new TrackingAction(2.0f);
        var canceledState = Assert.IsType<TrackingState>(manager.AddAction(canceled, target));
        manager.CancelAllActions();

        Assert.Equal(0, manager.GetNumActions(target));
        Assert.Empty(canceledState.Updates);
        Assert.True(canceledState.IsDisposed);
        manager.CancelAllActions();
        manager.CompleteAllActions();
    }

    [Fact]
    public void Action_manager_updates_incomplete_actions_and_removes_them_explicitly()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new TrackingAction(2.0f);

        var state = Assert.IsType<TrackingState>(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.Equal([0.0f, 0.25f], state.Updates);
        Assert.Equal(1, manager.GetNumActions(target));
        Assert.False(state.IsDisposed);

        manager.CancelAction(state);

        Assert.Equal(0, manager.GetNumActions(target));
        Assert.True(state.IsDisposed);
    }

    [Fact]
    public void Action_manager_removes_expired_weak_targets_from_bulk_and_update_paths()
    {
        using var manager = new ActionManager(false);

        AddExpiredAction(manager);
        manager.CompleteAllActions();

        AddExpiredAction(manager);
        manager.CancelAllActions();

        AddExpiredAction(manager);
        manager.Update(0.0f);
        Assert.Equal(0, manager.GetNumActions(null));
    }

    [Fact]
    public void Action_builder_exercises_all_factory_overloads()
    {
        var move3 = Build<MoveBy>(builder => builder.MoveBy(
            2.0f, new Vector3f(1.0f, 2.0f, 3.0f), "Position"));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), move3.PositionDelta);

        var move2 = Build<MoveBy>(builder => builder.MoveBy(
            2.0f, new Vector2f(4.0f, 5.0f), "Position2D"));
        Assert.Equal(new Vector3f(4.0f, 5.0f, 0.0f), move2.PositionDelta);

        var quadratic3 = Build<MoveByQuadratic>(builder => builder.MoveByQuadratic(
            2.0f,
            new Vector3f(1.0f, 2.0f, 3.0f),
            new Vector3f(4.0f, 5.0f, 6.0f),
            "Quadratic3"));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), quadratic3.ControlDelta);
        Assert.Equal(new Vector3f(4.0f, 5.0f, 6.0f), quadratic3.PositionDelta);

        var quadratic2 = Build<MoveByQuadratic>(builder => builder.MoveByQuadratic(
            2.0f,
            new Vector2f(7.0f, 8.0f),
            new Vector2f(9.0f, 10.0f),
            "Quadratic2"));
        Assert.Equal(new Vector3f(7.0f, 8.0f, 0.0f), quadratic2.ControlDelta);
        Assert.Equal(new Vector3f(9.0f, 10.0f, 0.0f), quadratic2.PositionDelta);

        var jump3 = Build<JumpBy>(builder => builder.JumpBy(new Vector3f(1.0f, 2.0f, 3.0f), "Jump3"));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), jump3.PositionDelta);
        var jump2 = Build<JumpBy>(builder => builder.JumpBy(new Vector2f(4.0f, 5.0f), "Jump2"));
        Assert.Equal(new Vector3f(4.0f, 5.0f, 0.0f), jump2.PositionDelta);

        var scale3 = Build<ScaleBy>(builder => builder.ScaleBy(
            2.0f, new Vector3f(1.0f, 2.0f, 3.0f), "Scale3"));
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), scale3.ScaleDelta);
        var scale2 = Build<ScaleBy>(builder => builder.ScaleBy(
            2.0f, new Vector2f(4.0f, 5.0f), "Scale2"));
        Assert.Equal(new Vector3f(4.0f, 5.0f, 1.0f), scale2.ScaleDelta);

        var rotation = new Quaternion(15.0f, Vector3f.Up);
        var rotate = Build<RotateBy>(builder => builder.RotateBy(2.0f, rotation, "Rotation"));
        Assert.Equal(rotation, rotate.RotationDelta);

        var around = Build<RotateAround>(builder => builder.RotateAround(
            2.0f, new Vector3f(3.0f, 4.0f, 5.0f), rotation));
        Assert.Equal(new Vector3f(3.0f, 4.0f, 5.0f), around.Pivot);
        Assert.Equal(rotation, around.RotationDelta);

        Assert.Equal(AttributeActionConstants.IsVisibleAttribute, Build<Hide>(builder => builder.Hide()).AttributeName);
        Assert.Equal("Visible", Build<Show>(builder => builder.Show("Visible")).AttributeName);
        Assert.Equal(AttributeActionConstants.IsEnabledAttribute, Build<Enable>(builder => builder.Enable()).AttributeName);
        Assert.Equal("Enabled", Build<Disable>(builder => builder.Disable("Enabled")).AttributeName);

        var blink = Build<Blink>(builder => builder.Blink(2.0f, 3, "Blink"));
        Assert.Equal(3, blink.NumOfBlinks);
        Assert.Equal("Blink", blink.AttributeName);

        using var from = new Variant(1.0f);
        using var to = new Variant(2.0f);
        Assert.Equal("Value", Build<AttributeTo>(builder => builder.AttributeTo(2.0f, "Value", to)).AttributeName);
        Assert.Equal(
            "Value",
            Build<AttributeFromTo>(builder => builder.AttributeFromTo(2.0f, "Value", from, to)).AttributeName);
        Assert.Equal("ShaderValue", Build<ShaderParameterTo>(
            builder => builder.ShaderParameterTo(2.0f, "ShaderValue", to)).Name);
        Assert.Equal("ShaderValue", Build<ShaderParameterFromTo>(
            builder => builder.ShaderParameterFromTo(2.0f, "ShaderValue", from, to)).Name);

        var eventData = new BuilderEvent { Value = 17 };
        Assert.Equal(eventData, Build<SendEvent<BuilderEvent>>(builder => builder.SendEvent(eventData)).EventData);
        Assert.Equal(default, Build<SendEvent<BuilderEvent>>(builder => builder.SendEvent<BuilderEvent>()).EventData);

        using var receiver = new BuilderReceiver();
        var handler = new ActionCallHandlerImpl<BuilderReceiver>(receiver, static (_, _) => { }, "handler");
        var handlerAction = Build<CallFunc>(builder => builder.CallFunc(handler));
        Assert.Same(handler, handlerAction.CallHandler);

        var genericAction = Build<CallFunc>(builder => builder.CallFunc(
            receiver, static (_, _) => { }, "generic"));
        Assert.Equal("generic", genericAction.CallHandler!.UserData);
    }

    [Fact]
    public void Action_builder_wraps_actions_and_handles_null_inputs()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            var empty = new ActionBuilder();
            Assert.Same(empty, empty.Then(null));
            Assert.Same(empty, empty.Also(null));
            Assert.Null(empty.Build());

        using var first = new DelayTime();
        first.SetDuration(1.0f);
        using var second = new DelayTime();
        second.SetDuration(2.0f);

        var sequence = new ActionBuilder().Then(first).Then(second).Build();
        Assert.IsType<Sequence>(sequence);
        Assert.Same(first, Assert.IsType<Sequence>(sequence).FirstAction);
        Assert.Same(second, Assert.IsType<Sequence>(sequence).SecondAction);

        using var parallelFirst = new DelayTime();
        using var parallelSecond = new DelayTime();
        using var parallelThird = new DelayTime();
        var parallel = new ActionBuilder()
            .Also(parallelFirst)
            .Also(parallelSecond)
            .Also(parallelThird)
            .Build();
        Assert.Equal(3, Assert.IsType<Parallel>(parallel).NumActions);

        Assert.IsType<EaseBackIn>(Wrap(builder => builder.BackIn()));
        Assert.IsType<EaseBackOut>(Wrap(builder => builder.BackOut()));
        Assert.IsType<EaseBackInOut>(Wrap(builder => builder.BackInOut()));
        Assert.IsType<EaseBounceOut>(Wrap(builder => builder.BounceOut()));
        Assert.IsType<EaseBounceIn>(Wrap(builder => builder.BounceIn()));
        Assert.IsType<EaseBounceInOut>(Wrap(builder => builder.BounceInOut()));
        Assert.IsType<EaseSineOut>(Wrap(builder => builder.SineOut()));
        Assert.IsType<EaseSineIn>(Wrap(builder => builder.SineIn()));
        Assert.IsType<EaseSineInOut>(Wrap(builder => builder.SineInOut()));
        Assert.IsType<EaseExponentialOut>(Wrap(builder => builder.ExponentialOut()));
        Assert.IsType<EaseExponentialIn>(Wrap(builder => builder.ExponentialIn()));
        Assert.IsType<EaseExponentialInOut>(Wrap(builder => builder.ExponentialInOut()));

        var elasticIn = Assert.IsType<EaseElasticIn>(Wrap(builder => builder.ElasticIn(0.4f)));
        var elasticOut = Assert.IsType<EaseElasticOut>(Wrap(builder => builder.ElasticOut(0.5f)));
        var elasticInOut = Assert.IsType<EaseElasticInOut>(Wrap(builder => builder.ElasticInOut(0.6f)));
        Assert.Equal(0.4f, elasticIn.Period);
        Assert.Equal(0.5f, elasticOut.Period);
        Assert.Equal(0.6f, elasticInOut.Period);

        Assert.IsType<RemoveSelf>(new ActionBuilder().RemoveSelf().Build());
        Assert.Equal(3.0f, Assert.IsType<DelayTime>(new ActionBuilder().DelayTime(3.0f).Build()).Duration);

        var repeat = Assert.IsType<Repeat>(Wrap(builder => builder.Repeat(4)));
        Assert.Equal(4, repeat.Times);
            Assert.IsType<RepeatForever>(Wrap(builder => builder.RepeatForever()));
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Action_builder_runs_with_context_or_explicit_manager()
    {
        using var target = new Node();
        var builder = new ActionBuilder().DelayTime(1.0f);

        Assert.Null(builder.Run((ActionManager?)null, target));

        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            Assert.Null(builder.Run(manager, null));
            Assert.NotNull(builder.Run(target));
            manager.Update(0.0f);
            manager.Update(1.0f);
            Assert.Equal(0, manager.GetNumActions(target));
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    private static T Build<T>(Func<ActionBuilder, ActionBuilder> configure)
        where T : FiniteTimeAction
    {
        return Assert.IsType<T>(configure(new ActionBuilder()).Build());
    }

    private static FiniteTimeAction Wrap(Func<ActionBuilder, ActionBuilder> configure)
    {
        return configure(new ActionBuilder().DelayTime(1.0f)).Build()!;
    }

    private static void AddExpiredAction(ActionManager manager)
    {
        var target = new OrphanTarget();
        var action = new TrackingAction(1.0f);
        Assert.NotNull(manager.AddAction(action, target));

        var targetsField = typeof(ActionManager).GetField(
            "_targets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(targetsField);
        var targets = Assert.IsAssignableFrom<System.Collections.IList>(targetsField!.GetValue(manager));
        var element = targets[targets.Count - 1]!;
        var targetField = element.GetType().GetField(
            "Target", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(targetField);
        var weakTarget = Assert.IsType<WeakReference<RedObject>>(targetField!.GetValue(element));
        weakTarget.SetTarget(null!);
    }

    private sealed class OrphanTarget : RedObject
    {
    }

    private sealed class TrackingAction : FiniteTimeAction
    {
        public TrackingAction(float duration)
        {
            SetDuration(duration);
        }

        public TrackingState? LastState { get; private set; }

        protected override ActionState StartAction(RedObject? target)
        {
            LastState = new TrackingState(this, target);
            return LastState;
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

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public override void Update(float time)
        {
            Updates.Add(time);
        }
    }

    private sealed class NullStateAction : FiniteTimeAction
    {
        protected override ActionState StartAction(RedObject? target) => null!;
    }

    private sealed class BuilderReceiver : RedObject
    {
    }

    private struct BuilderEvent
    {
        public int Value;
    }
}
