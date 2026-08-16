// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class UndoManagerTests
{
    [Fact]
    public void Actions_in_same_frame_are_undone_and_redone_as_group()
    {
        WithManager(manager =>
        {
            var trace = new List<string>();
            var first = new TraceAction("first", trace);
            var second = new TraceAction("second", trace);

            Assert.Equal(0ul, manager.PushAction(first));
            Assert.Equal(0ul, manager.PushAction(second));
            Assert.True(manager.CanUndo());
            Assert.False(manager.CanRedo());

            Assert.True(manager.Undo());
            Assert.Equal(["undo:second", "undo:first"], trace);
            Assert.False(manager.CanUndo());
            Assert.True(manager.CanRedo());

            Assert.True(manager.Redo());
            Assert.Equal(["undo:second", "undo:first", "redo:first", "redo:second"], trace);
            Assert.True(manager.CanUndo());
            Assert.False(manager.CanRedo());
        });
    }

    [Fact]
    public void New_frame_separates_action_groups()
    {
        WithManager(manager =>
        {
            var trace = new List<string>();
            var first = new TraceAction("first", trace);
            var second = new TraceAction("second", trace);

            Assert.Equal(0ul, manager.PushAction(first));
            manager.NewFrame();
            Assert.Equal(1ul, manager.PushAction(second));

            Assert.True(manager.Undo());
            Assert.Equal(["undo:second"], trace);
            Assert.True(manager.Undo());
            Assert.Equal(["undo:second", "undo:first"], trace);
        });
    }

    [Fact]
    public void Top_action_merges_with_next_action()
    {
        WithManager(manager =>
        {
            var value = new MutableValue { Value = 3 };
            var first = new DeltaAction(value, 1);
            var second = new DeltaAction(value, 2);

            manager.PushAction(first);
            manager.PushAction(second);

            Assert.Equal(0ul, first.PushedFrame);
            Assert.Equal(0ul, second.PushedFrame);
            Assert.True(manager.Undo());
            Assert.Equal(0, value.Value);
            Assert.True(manager.Redo());
            Assert.Equal(3, value.Value);
        });
    }

    [Fact]
    public void Transparent_action_preserves_redo_and_remove_on_undo_drops_action()
    {
        WithManager(manager =>
        {
            var trace = new List<string>();
            manager.PushAction(new TraceAction("normal", trace));
            Assert.True(manager.Undo());

            manager.PushAction(new TraceAction("transparent", trace)
            {
                Transparent = true,
                RemoveAfterUndo = true,
            });

            Assert.True(manager.CanRedo());
            Assert.True(manager.Undo());
            Assert.False(manager.CanUndo());
            Assert.True(manager.CanRedo());
        });
    }

    [Fact]
    public void Incomplete_action_is_forced_complete_before_next_action()
    {
        WithManager(manager =>
        {
            var incomplete = new IncompleteAction();
            manager.PushAction(incomplete);

            Assert.False(incomplete.IsComplete());
            manager.PushAction(new TraceAction("next", []));

            Assert.True(incomplete.IsComplete());
            Assert.Equal([true], incomplete.CompletionRequests);
        });
    }

    [Fact]
    public void Wrapper_forwards_behavior_and_only_merges_with_same_wrapper_type()
    {
        var first = new MergeProbeAction();
        var second = new MergeProbeAction();
        var wrapper = new TestWrapper(first);

        wrapper.OnPushed(42);
        wrapper.Complete(true);
        wrapper.Redo();
        wrapper.Undo();

        Assert.Equal(42ul, first.PushedFrame);
        Assert.Equal([true], first.CompletionRequests);
        Assert.Equal(1, first.RedoCount);
        Assert.Equal(1, first.UndoCount);
        Assert.True(wrapper.MergeWith(new TestWrapper(second)));
        Assert.Same(second, first.MergedWith);
        Assert.False(wrapper.MergeWith(new OtherWrapper(second)));
    }

    private static void WithManager(Action<UndoManager> test)
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var manager = new UndoManager();
            test(manager);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private sealed class TraceAction : EditorAction
    {
        private readonly string _name;
        private readonly List<string> _trace;

        public TraceAction(string name, List<string> trace)
        {
            _name = name;
            _trace = trace;
        }

        public bool Transparent { get; init; }

        public bool RemoveAfterUndo { get; init; }

        public override bool IsTransparent() => Transparent;

        public override bool RemoveOnUndo() => RemoveAfterUndo;

        public override void Redo() => _trace.Add($"redo:{_name}");

        public override void Undo() => _trace.Add($"undo:{_name}");
    }

    private sealed class MutableValue
    {
        public int Value { get; set; }
    }

    private sealed class DeltaAction : EditorAction
    {
        private readonly MutableValue _value;
        private int _delta;

        public DeltaAction(MutableValue value, int delta)
        {
            _value = value;
            _delta = delta;
        }

        public ulong PushedFrame { get; private set; }

        public override void OnPushed(ulong frame) => PushedFrame = frame;

        public override void Redo() => _value.Value += _delta;

        public override void Undo() => _value.Value -= _delta;

        public override bool MergeWith(EditorAction other)
        {
            if (other is not DeltaAction deltaAction)
                return false;

            _delta += deltaAction._delta;
            return true;
        }
    }

    private sealed class IncompleteAction : EditorAction
    {
        private bool _isComplete;

        public List<bool> CompletionRequests { get; } = [];

        public override bool IsComplete() => _isComplete;

        public override void Complete(bool force)
        {
            CompletionRequests.Add(force);
            _isComplete = force;
        }

        public override void Redo()
        {
        }

        public override void Undo()
        {
        }
    }

    private sealed class MergeProbeAction : EditorAction
    {
        public ulong PushedFrame { get; private set; }

        public List<bool> CompletionRequests { get; } = [];

        public int RedoCount { get; private set; }

        public int UndoCount { get; private set; }

        public EditorAction? MergedWith { get; private set; }

        public override void OnPushed(ulong frame) => PushedFrame = frame;

        public override void Complete(bool force) => CompletionRequests.Add(force);

        public override void Redo() => ++RedoCount;

        public override void Undo() => ++UndoCount;

        public override bool MergeWith(EditorAction other)
        {
            MergedWith = other;
            return true;
        }
    }

    private sealed class TestWrapper : BaseEditorActionWrapper
    {
        public TestWrapper(EditorAction action)
            : base(action)
        {
        }
    }

    private sealed class OtherWrapper : BaseEditorActionWrapper
    {
        public OtherWrapper(EditorAction action)
            : base(action)
        {
        }
    }
}
