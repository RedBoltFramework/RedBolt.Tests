namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionResidualCoverageTests
{
    [Fact]
    public void Base_action_uses_the_empty_action_and_starts_a_noop_state()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var action = new ExposedBaseAction();
            using var target = new Node();

            Assert.Same(action, action.GetOrDefault(action));
            Assert.Same(manager.GetEmptyAction(), action.GetOrDefault(null));

            var state = action.Start(target);
            Assert.Same(action, state.Action);
            Assert.Same(target, state.Target);
            Assert.True(state.IsDone);
            state.Update(0.5f);
            state.Stop();
            Assert.Null(state.Target);

            using var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("action"))
                action.SerializeInBlock(output);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Blink_clamps_counts_and_round_trips_non_default_serialized_values()
    {
        using var source = new Blink();
        Assert.Equal(1, source.NumOfBlinks);
        source.SetNumOfBlinks(0);
        Assert.Equal(1, source.NumOfBlinks);
        source.SetNumOfBlinks(4);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            source.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new Blink();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        Assert.Equal(4, restored.NumOfBlinks);
    }

    [Fact]
    public void Parallel_can_grow_and_replace_indexed_actions()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var first = new DelayTime();
            using var second = new DelayTime();
            using var third = new DelayTime();
            using var parallel = new Parallel();

            Assert.Throws<ArgumentOutOfRangeException>(() => parallel.SetNumActions(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => parallel.SetAction(-1, first));
            parallel.SetNumActions(3);
            Assert.Equal(3, parallel.NumActions);
            Assert.Same(manager.GetEmptyAction(), parallel.GetAction(0));

            parallel.SetAction(0, first);
            parallel.SetAction(1, second);
            parallel.SetAction(4, third);

            Assert.Equal(5, parallel.NumActions);
            Assert.Same(first, parallel.GetAction(0));
            Assert.Same(second, parallel.GetAction(1));
            Assert.Same(third, parallel.GetAction(4));

            parallel.SetNumActions(2);
            Assert.Equal(2, parallel.NumActions);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    private sealed class ExposedBaseAction : BaseAction
    {
        public ActionState Start(RedObject? target) => base.StartAction(target);
    }
}
