namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class ActionConditionalBranchTests
{
    [Fact]
    public void Builder_handles_null_actions_existing_parallel_and_missing_manager()
    {
        using var target = new Node();
        var empty = new ActionBuilder();

        Assert.Same(empty, empty.Then(null));
        Assert.Same(empty, empty.Also(null));
        Assert.Null(empty.Build());
        Assert.Null(empty.Run(null, target));

        using var first = new DelayTime();
        using var second = new DelayTime();
        using var third = new DelayTime();
        var combined = new ActionBuilder().Also(first).Also(second).Also(third).Build();

        var parallel = Assert.IsType<Parallel>(combined);
        Assert.Equal(3, parallel.NumActions);
        Assert.Same(first, parallel.GetAction(0));
        Assert.Same(second, parallel.GetAction(1));
        Assert.Same(third, parallel.GetAction(2));
    }

    [Fact]
    public void Builder_run_without_explicit_manager_uses_context_manager()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            var state = new ActionBuilder().DelayTime(1.0f).Run(target);

            Assert.NotNull(state);
            manager.Update(0.0f);
            manager.Update(1.0f);
            Assert.Equal(0, manager.GetNumActions(target));
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Action_manager_handles_completion_and_cancellation_during_current_update()
    {
        using var target = new Node();
        using var completingManager = new ActionManager(false);
        using var completingAction = new ManagerCallingAction(completingManager, complete: true);

        Assert.NotNull(completingManager.AddAction(completingAction, target));
        completingManager.Update(0.0f);
        Assert.Equal(0, completingManager.GetNumActions(target));

        using var cancellingManager = new ActionManager(false);
        using var cancellingAction = new ManagerCallingAction(cancellingManager, complete: false);

        Assert.NotNull(cancellingManager.AddAction(cancellingAction, target));
        cancellingManager.Update(0.0f);
        Assert.Equal(0, cancellingManager.GetNumActions(target));
    }

    [Fact]
    public void Action_manager_covers_no_handler_and_non_material_shader_target()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var call = new CallFunc();

        Assert.NotNull(manager.AddAction(call, target));
        manager.Update(0.0f);
        manager.Update(1.0f);
        Assert.Equal(0, manager.GetNumActions(target));
        manager.CancelAllActionsFromTarget(target);
        Assert.Equal(0, manager.GetNumActions(target));

        using var value = new Variant(3.0f);
        using var shaderAction = new ShaderParameterTo();
        shaderAction.SetDuration(1.0f);
        shaderAction.SetName("Value");
        shaderAction.SetTo(value);

        Assert.NotNull(manager.AddAction(shaderAction, target));
        manager.Update(0.0f);
        manager.Update(1.0f);
        manager.CancelAllActionsFromTarget(target);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Attribute_actions_fall_back_for_missing_invalid_and_non_serializable_targets()
    {
        Context.Instance.RegisterAssembly(typeof(ActionConditionalBranchTests).Assembly);
        using var manager = new ActionManager(false);

        using var missingTarget = new InvalidAttributeTarget();
        using var missing = new MoveBy();
        missing.SetAttributeName("Missing");
        missing.SetDuration(1.0f);
        Assert.NotNull(manager.AddAction(missing, missingTarget));

        using var invalidTarget = new InvalidAttributeTarget();
        using var invalid = new MoveBy();
        invalid.SetAttributeName("Value");
        invalid.SetDuration(1.0f);
        Assert.NotNull(manager.AddAction(invalid, invalidTarget));

        using var nonSerializableTarget = new PlainTarget();
        using var nonSerializable = new MoveBy();
        nonSerializable.SetAttributeName("Value");
        nonSerializable.SetDuration(1.0f);
        Assert.NotNull(manager.AddAction(nonSerializable, nonSerializableTarget));

        manager.Update(0.0f);

        Assert.Equal(0, manager.GetNumActions(missingTarget));
        Assert.Equal(0, manager.GetNumActions(invalidTarget));
        Assert.Equal(0, manager.GetNumActions(nonSerializableTarget));
        Assert.Equal(0.0f, invalidTarget.Value);
    }

    [Fact]
    public void Parallel_supports_shrinking_and_out_of_range_lookup()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var second = new DelayTime();
            using var parallel = new Parallel();
            parallel.AddAction(second);

            Assert.Same(second, parallel.GetAction(0));
            parallel.SetNumActions(0);
            Assert.Equal(0, parallel.NumActions);
            Assert.Same(manager.GetEmptyAction(), parallel.GetAction(4));
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_can_move_back_to_the_first_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var first = new RecordingAction(1.0f);
            using var second = new RecordingAction(1.0f);
            using var sequence = new Sequence();
            sequence.SetFirstAction(first);
            sequence.SetSecondAction(second);

            Assert.NotNull(manager.AddAction(sequence, target));
            manager.Update(0.0f);
            manager.Update(1.0f);
            manager.Update(-0.75f);

            Assert.Equal(1, manager.GetNumActions(target));
            Assert.Contains(0.0f, second.Updates);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Attribute_actions_serialize_optional_values()
    {
        using var from = new Variant(2.0f);
        using var to = new Variant(6.0f);
        using var sourceFromTo = new AttributeFromTo();
        sourceFromTo.SetDuration(2.0f);
        sourceFromTo.SetAttributeName("Value");
        sourceFromTo.SetFrom(from);
        sourceFromTo.SetTo(to);

        using var sourceTo = new AttributeTo();
        sourceTo.SetDuration(2.0f);
        sourceTo.SetAttributeName("Value");
        sourceTo.SetTo(to);

        using var sourceBlink = new AttributeBlink();
        sourceBlink.SetDuration(2.0f);
        sourceBlink.SetAttributeName("Value");
        sourceBlink.SetFrom(from);
        sourceBlink.SetTo(to);
        sourceBlink.SetNumOfBlinks(2);

        using var fromToOutput = new TestJSONOutputArchive();
        using (fromToOutput.OpenUnorderedBlock("action"))
            sourceFromTo.SerializeInBlock(fromToOutput);
        using var fromToInput = new TestJSONInputArchive(fromToOutput.ToJsonString());
        using var restoredFromTo = new AttributeFromTo();
        using (fromToInput.OpenUnorderedBlock("action"))
            restoredFromTo.SerializeInBlock(fromToInput);

        using var toOutput = new TestJSONOutputArchive();
        using (toOutput.OpenUnorderedBlock("action"))
            sourceTo.SerializeInBlock(toOutput);
        using var toInput = new TestJSONInputArchive(toOutput.ToJsonString());
        using var restoredTo = new AttributeTo();
        using (toInput.OpenUnorderedBlock("action"))
            restoredTo.SerializeInBlock(toInput);

        using var blinkOutput = new TestJSONOutputArchive();
        using (blinkOutput.OpenUnorderedBlock("action"))
            sourceBlink.SerializeInBlock(blinkOutput);
        using var blinkInput = new TestJSONInputArchive(blinkOutput.ToJsonString());
        using var restoredBlink = new AttributeBlink();
        using (blinkInput.OpenUnorderedBlock("action"))
            restoredBlink.SerializeInBlock(blinkInput);

        Assert.Equal(2.0f, restoredFromTo.From.GetFloat());
        Assert.Equal(6.0f, restoredFromTo.To.GetFloat());
        Assert.Equal(6.0f, restoredTo.To.GetFloat());
        Assert.Equal(2.0f, restoredBlink.From.GetFloat());
        Assert.Equal(6.0f, restoredBlink.To.GetFloat());
        Assert.Equal(1, restoredBlink.NumOfBlinks);
    }

    [Fact]
    public void Action_states_handle_null_targets_and_null_nested_actions()
    {
        using var action = new FiniteTimeAction();
        using var state = new FiniteTimeActionState(action, null);
        Assert.Null(state.Target);
        Assert.Null(state.OriginalTarget);

        using var nullAttributeState = new AttributeActionState(action, null, null);
        nullAttributeState.Update(0.0f);

        using var owner = new ExposedState(action, null);
        Assert.Null(owner.Start(null, null));

        using var removeSelf = new ExposedRemoveSelf();
        var removeState = removeSelf.Start(null);
        removeState.Update(0.0f);
        ((IDisposable)removeState).Dispose();

        using var nonNodeTarget = new BaseAction();
        using var manager = new ActionManager(false);
        Assert.NotNull(manager.AddAction(removeSelf, nonNodeTarget));
        manager.Update(0.0f);
        Assert.Equal(1, manager.GetNumActions(nonNodeTarget));
        manager.CancelAllActionsFromTarget(nonNodeTarget);
        Assert.Equal(0, manager.GetNumActions(nonNodeTarget));
    }

    private sealed class ManagerCallingAction : FiniteTimeAction
    {
        private readonly ActionManager _manager;
        private readonly bool _complete;

        public ManagerCallingAction(ActionManager manager, bool complete)
        {
            _manager = manager;
            _complete = complete;
            SetDuration(2.0f);
        }

        protected override ActionState StartAction(RedObject? target) =>
            new ManagerCallingState(this, target, _manager, _complete);
    }

    private sealed class ManagerCallingState : FiniteTimeActionState
    {
        private readonly ActionManager _manager;
        private readonly bool _complete;
        private bool _called;

        public ManagerCallingState(
            FiniteTimeAction action, RedObject? target, ActionManager manager, bool complete)
            : base(action, target)
        {
            _manager = manager;
            _complete = complete;
        }

        public override void Update(float time)
        {
            if (_called)
                return;

            _called = true;
            if (_complete)
                _manager.CompleteAllActionsOnTarget(Target);
            else
                _manager.CancelAllActionsFromTarget(Target);
        }
    }

    private sealed class RecordingAction : FiniteTimeAction
    {
        public RecordingAction(float duration)
        {
            SetDuration(duration);
        }

        public List<float> Updates { get; } = [];

        protected override ActionState StartAction(RedObject? target) => new RecordingState(this, target, Updates);
    }

    private sealed class RecordingState : FiniteTimeActionState
    {
        private readonly List<float> _updates;

        public RecordingState(FiniteTimeAction action, RedObject? target, List<float> updates)
            : base(action, target)
        {
            _updates = updates;
        }

        public override void Update(float time) => _updates.Add(time);
    }

    private sealed class ExposedState : ActionState, IDisposable
    {
        public ExposedState(BaseAction action, RedObject? target)
            : base(action, target)
        {
        }

        public ActionState? Start(BaseAction? action, RedObject? target) => StartAction(action, target);

        public void Dispose()
        {
        }
    }

    private sealed class ExposedRemoveSelf : RemoveSelf
    {
        public ActionState Start(RedObject? target) => StartAction(target);
    }

    private sealed class PlainTarget : RedObject
    {
    }

    [Reflected]
    private sealed partial class InvalidAttributeTarget : Serializable
    {
        [Serialized("Value")]
        public float Value { get; set; }
    }
}
