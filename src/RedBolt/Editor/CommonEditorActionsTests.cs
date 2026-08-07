namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
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

    [Fact]
    public void Create_node_action_undoes_creation_and_redoes_it_exactly()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent", 100);
        var node = parent.CreateChild("Node", 101);
        var action = new CreateRemoveNodeAction(node, removed: false);

        action.Undo();
        Assert.Null(scene.GetNode(101));

        action.Redo();
        var restored = scene.GetNode(101);
        Assert.NotNull(restored);
        Assert.Equal("Node", restored.Name);
        Assert.Same(parent, restored.Parent);
    }

    [Fact]
    public void Remove_node_action_redoes_removal_and_undoes_it_exactly()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var action = new CreateRemoveNodeAction(node, removed: true);

        action.Redo();
        Assert.Null(scene.GetNode(100));

        action.Undo();
        Assert.Equal("Node", scene.GetNode(100)!.Name);
    }

    [Fact]
    public void Create_remove_node_action_detects_desynchronized_state()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var action = new CreateRemoveNodeAction(node, removed: false);

        Assert.Throws<UndoException>(() => action.Redo());
        action.Undo();
        Assert.Throws<UndoException>(() => action.Undo());
    }

    [Fact]
    public void Create_remove_node_action_is_disabled_without_scene()
    {
        using var node = new Node();
        var action = new CreateRemoveNodeAction(node, removed: false);

        Assert.False(action.CanUndoRedo());
        action.Redo();
        action.Undo();
    }

    [Fact]
    public void Create_component_action_undoes_creation_and_redoes_it_exactly()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var camera = node.CreateComponent<Camera>(200);
        camera.Fov = 67.5f;
        var action = new CreateRemoveComponentAction(camera, removed: false);

        action.Undo();
        Assert.Null(scene.GetComponent(200));

        action.Redo();
        var restored = Assert.IsType<Camera>(scene.GetComponent(200));
        Assert.Equal(67.5f, restored.Fov);
        Assert.Same(node, restored.Node);
    }

    [Fact]
    public void Remove_component_action_redoes_removal_and_undoes_it_exactly()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node").CreateComponent<Camera>(200);
        camera.Fov = 67.5f;
        var action = new CreateRemoveComponentAction(camera, removed: true);

        action.Redo();
        Assert.Null(scene.GetComponent(200));

        action.Undo();
        Assert.Equal(67.5f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
    }

    [Fact]
    public void Create_remove_component_action_detects_desynchronized_state()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node").CreateComponent<Camera>(200);
        var action = new CreateRemoveComponentAction(camera, removed: false);

        Assert.Throws<UndoException>(() => action.Redo());
        action.Undo();
        Assert.Throws<UndoException>(() => action.Undo());
    }

    [Fact]
    public void Create_remove_component_action_is_disabled_without_scene()
    {
        using var node = new Node();
        var camera = new Camera();
        node.AddComponent(camera);
        var action = new CreateRemoveComponentAction(camera, removed: false);

        Assert.False(action.CanUndoRedo());
        action.Redo();
        action.Undo();
    }

    [Fact]
    public void Change_node_transform_action_undoes_and_redoes_transform()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var oldTransform = node.GetTransform();
        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var action = new ChangeNodeTransformAction(node, oldTransform);
        node.SetPosition(new Vector3f(9.0f, 9.0f, 9.0f));

        action.Undo();
        Assert.Equal(Vector3f.Zero, node.Position);

        action.Redo();
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), node.Position);
    }

    [Fact]
    public void Change_node_transform_action_merges_latest_transform_for_same_node()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var oldTransform = node.GetTransform();
        node.SetPosition(new Vector3f(1.0f, 0.0f, 0.0f));
        var action = new ChangeNodeTransformAction(node, oldTransform);
        var intermediateTransform = node.GetTransform();
        node.SetPosition(new Vector3f(2.0f, 0.0f, 0.0f));
        var other = new ChangeNodeTransformAction(node, intermediateTransform);

        Assert.True(action.MergeWith(other));
        action.Undo();
        Assert.Equal(Vector3f.Zero, node.Position);
        action.Redo();
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), node.Position);
    }

    [Fact]
    public void Change_node_transform_action_merges_additional_nodes_in_same_scene()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First");
        var second = scene.CreateChild("Second");
        var firstOld = first.GetTransform();
        var secondOld = second.GetTransform();
        first.SetPosition(new Vector3f(1.0f, 0.0f, 0.0f));
        second.SetPosition(new Vector3f(2.0f, 0.0f, 0.0f));
        var action = new ChangeNodeTransformAction(first, firstOld);
        var other = new ChangeNodeTransformAction(second, secondOld);

        Assert.True(action.MergeWith(other));
        action.Undo();
        Assert.Equal(Vector3f.Zero, first.Position);
        Assert.Equal(Vector3f.Zero, second.Position);
        action.Redo();
        Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f), first.Position);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), second.Position);
    }

    [Fact]
    public void Change_node_transform_action_rejects_other_type_or_scene_and_detects_missing_node()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var node = scene.CreateChild("Node");
        var action = new ChangeNodeTransformAction(node, node.GetTransform());
        var otherNode = otherScene.CreateChild("Other");
        var other = new ChangeNodeTransformAction(otherNode, otherNode.GetTransform());

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(other));
        Assert.True(action.CanUndoRedo());

        node.Remove();
        Assert.Throws<UndoException>(() => action.Undo());
        Assert.Throws<UndoException>(() => action.Redo());
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
