namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Editor.Core;

public sealed class CommonEditorActionsTests
{
    [Fact]
    public void Empty_action_is_removed_on_undo_and_changes_nothing()
    {
        var action = new EmptyEditorAction();

        action.Redo();
        action.Undo();

        Assert.True(action.RemoveOnUndo());
        Assert.True(action.CanRedo());
        Assert.True(action.CanUndo());
    }

    [Fact]
    public void Composite_action_redoes_forward_and_undoes_reverse()
    {
        var calls = new List<string>();
        var action = new CompositeEditorAction();
        action.AddAction(new RecordingEditorAction("First", calls));
        action.EmplaceAction(new RecordingEditorAction("Second", calls));

        action.Redo();
        action.Undo();

        Assert.Equal(new[] { "Redo First", "Redo Second", "Undo Second", "Undo First" }, calls);
    }

    [Fact]
    public void Composite_action_requires_all_children_to_allow_undo_and_redo()
    {
        var calls = new List<string>();
        var action = new CompositeEditorAction();
        action.AddAction(new RecordingEditorAction("First", calls));
        action.AddAction(new RecordingEditorAction("Second", calls, canRedo: false));

        Assert.False(action.CanRedo());
        Assert.True(action.CanUndo());

        action = new CompositeEditorAction();
        action.AddAction(new RecordingEditorAction("First", calls));
        action.AddAction(new RecordingEditorAction("Second", calls, canUndo: false));

        Assert.True(action.CanRedo());
        Assert.False(action.CanUndo());
    }

    private sealed class RecordingEditorAction : EditorAction
    {
        private readonly string _name;
        private readonly List<string> _calls;
        private readonly bool _canRedo;
        private readonly bool _canUndo;

        public RecordingEditorAction(
            string name, List<string> calls, bool canRedo = true, bool canUndo = true)
        {
            _name = name;
            _calls = calls;
            _canRedo = canRedo;
            _canUndo = canUndo;
        }

        public override bool CanRedo() => _canRedo;

        public override void Redo() => _calls.Add($"Redo {_name}");

        public override bool CanUndo() => _canUndo;

        public override void Undo() => _calls.Add($"Undo {_name}");
    }
}
