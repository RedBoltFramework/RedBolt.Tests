namespace RedBolt.Tests;

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
}
