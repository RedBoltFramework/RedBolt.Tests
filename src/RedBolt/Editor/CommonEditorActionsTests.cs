namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

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

    [Fact]
    public void Change_node_attributes_action_undoes_and_redoes_values_for_all_nodes()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("New First");
        var second = scene.CreateChild("New Second");
        using var oldValues = new VariantVector { new("Old First"), new("Old Second") };
        using var newValues = new VariantVector { new("New First"), new("New Second") };
        var action = new ChangeNodeAttributesAction(
            scene, "Name", new[] { first, second }, oldValues, newValues);
        oldValues.Clear();
        newValues.Clear();

        action.Undo();
        Assert.Equal("Old First", first.Name);
        Assert.Equal("Old Second", second.Name);

        action.Redo();
        Assert.Equal("New First", first.Name);
        Assert.Equal("New Second", second.Name);
    }

    [Fact]
    public void Change_node_attributes_action_requires_every_node_to_exist()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First");
        var second = scene.CreateChild("Second");
        using var oldValues = new VariantVector { new("Old First"), new("Old Second") };
        using var newValues = new VariantVector { new("New First"), new("New Second") };
        var action = new ChangeNodeAttributesAction(
            scene, "Name", new[] { first, second }, oldValues, newValues);

        Assert.True(action.CanUndoRedo());
        second.Remove();
        Assert.False(action.CanUndoRedo());

        action.Undo();
        Assert.Equal("Old First", first.Name);
    }

    [Fact]
    public void Change_node_attributes_action_merges_latest_values_for_same_nodes()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Newest");
        using var oldValues = new VariantVector { new("Old") };
        using var intermediateValues = new VariantVector { new("Intermediate") };
        using var newestValues = new VariantVector { new("Newest") };
        var action = new ChangeNodeAttributesAction(
            scene, "Name", new[] { node }, oldValues, intermediateValues);
        var other = new ChangeNodeAttributesAction(
            scene, "Name", new[] { node }, intermediateValues, newestValues);

        Assert.True(action.MergeWith(other));
        newestValues.Clear();
        action.Undo();
        Assert.Equal("Old", node.Name);
        action.Redo();
        Assert.Equal("Newest", node.Name);
    }

    [Fact]
    public void Change_node_attributes_action_rejects_other_type_scene_or_node_order()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var first = scene.CreateChild("First");
        var second = scene.CreateChild("Second");
        var otherNode = otherScene.CreateChild("Other");
        using var values = new VariantVector { new("Value"), new("Value") };
        using var oneValue = new VariantVector { new("Value") };
        var action = new ChangeNodeAttributesAction(
            scene, "Name", new[] { first, second }, values, values);
        var reordered = new ChangeNodeAttributesAction(
            scene, "Name", new[] { second, first }, values, values);
        var differentScene = new ChangeNodeAttributesAction(
            otherScene, "Name", new[] { otherNode }, oneValue, oneValue);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(reordered));
        Assert.False(action.MergeWith(differentScene));
    }

    [Fact]
    public void Change_component_attributes_action_undoes_and_redoes_values_for_all_components()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        var second = node.CreateComponent<Camera>();
        first.Fov = 60.0f;
        second.Fov = 70.0f;
        using var oldValues = new VariantVector { new(40.0f), new(50.0f) };
        using var newValues = new VariantVector { new(60.0f), new(70.0f) };
        var action = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { first, second }, oldValues, newValues);
        oldValues.Clear();
        newValues.Clear();

        action.Undo();
        Assert.Equal(40.0f, first.Fov);
        Assert.Equal(50.0f, second.Fov);

        action.Redo();
        Assert.Equal(60.0f, first.Fov);
        Assert.Equal(70.0f, second.Fov);
    }

    [Fact]
    public void Change_component_attributes_action_requires_every_component_to_exist()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        var second = node.CreateComponent<Camera>();
        using var oldValues = new VariantVector { new(40.0f), new(50.0f) };
        using var newValues = new VariantVector { new(60.0f), new(70.0f) };
        var action = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { first, second }, oldValues, newValues);

        Assert.True(action.CanUndoRedo());
        second.Remove();
        Assert.False(action.CanUndoRedo());

        action.Undo();
        Assert.Equal(40.0f, first.Fov);
    }

    [Fact]
    public void Change_component_attributes_action_merges_latest_values_for_same_components()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node").CreateComponent<Camera>();
        using var oldValues = new VariantVector { new(40.0f) };
        using var intermediateValues = new VariantVector { new(50.0f) };
        using var newestValues = new VariantVector { new(60.0f) };
        var action = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { camera }, oldValues, intermediateValues);
        var other = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { camera }, intermediateValues, newestValues);

        Assert.True(action.MergeWith(other));
        newestValues.Clear();
        action.Undo();
        Assert.Equal(40.0f, camera.Fov);
        action.Redo();
        Assert.Equal(60.0f, camera.Fov);
    }

    [Fact]
    public void Change_component_attributes_action_rejects_other_type_scene_or_component_order()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        var second = node.CreateComponent<Camera>();
        var otherCamera = otherScene.CreateChild("Other").CreateComponent<Camera>();
        using var values = new VariantVector { new(50.0f), new(50.0f) };
        using var oneValue = new VariantVector { new(50.0f) };
        var action = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { first, second }, values, values);
        var reordered = new ChangeComponentAttributesAction(
            scene, "FOV", new Component[] { second, first }, values, values);
        var differentScene = new ChangeComponentAttributesAction(
            otherScene, "FOV", new Component[] { otherCamera }, oneValue, oneValue);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(reordered));
        Assert.False(action.MergeWith(differentScene));
    }

    [Fact]
    public void Reorder_node_action_redoes_and_undoes_child_order()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First");
        var second = scene.CreateChild("Second");
        var third = scene.CreateChild("Third");
        var action = new ReorderNodeAction(third, oldIndex: 2, newIndex: 0);

        action.Redo();
        Assert.Same(third, scene.GetChild(0));
        Assert.Same(first, scene.GetChild(1));
        Assert.Same(second, scene.GetChild(2));

        action.Undo();
        Assert.Same(first, scene.GetChild(0));
        Assert.Same(second, scene.GetChild(1));
        Assert.Same(third, scene.GetChild(2));
    }

    [Fact]
    public void Reorder_node_action_merges_latest_index_for_same_node_and_parent()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First");
        scene.CreateChild("Second");
        var third = scene.CreateChild("Third");
        var action = new ReorderNodeAction(third, oldIndex: 2, newIndex: 1);
        var other = new ReorderNodeAction(third, oldIndex: 1, newIndex: 0);

        Assert.True(action.MergeWith(other));
        action.Redo();
        Assert.Same(third, scene.GetChild(0));
        action.Undo();
        Assert.Same(first, scene.GetChild(0));
        Assert.Same(third, scene.GetChild(2));
    }

    [Fact]
    public void Reorder_node_action_rejects_other_type_scene_node_or_parent()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var parent = scene.CreateChild("Parent");
        var node = parent.CreateChild("Node");
        var sibling = parent.CreateChild("Sibling");
        var otherParent = scene.CreateChild("Other Parent");
        var otherParentNode = otherParent.CreateChild("Node");
        var externalNode = otherScene.CreateChild("Node");
        var action = new ReorderNodeAction(node, 0, 1);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(new ReorderNodeAction(sibling, 1, 0)));
        Assert.False(action.MergeWith(new ReorderNodeAction(otherParentNode, 0, 0)));
        Assert.False(action.MergeWith(new ReorderNodeAction(externalNode, 0, 0)));
    }

    [Fact]
    public void Reorder_node_action_reports_missing_parent_or_node()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent", 100);
        var node = parent.CreateChild("Node", 101);
        var action = new ReorderNodeAction(node, 0, 0);

        Assert.True(action.CanUndoRedo());
        node.Remove();
        Assert.False(action.CanUndoRedo());
        var exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("node with id 101", exception.Message);

        node = parent.CreateChild("Node", 101);
        action = new ReorderNodeAction(node, 0, 0);
        parent.Remove();
        exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("parent node with id 100", exception.Message);
    }

    [Fact]
    public void Reorder_component_action_redoes_and_undoes_component_order()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        var second = node.CreateComponent<Camera>();
        var third = node.CreateComponent<Camera>();
        var action = new ReorderComponentAction(third, oldIndex: 2, newIndex: 0);

        action.Redo();
        Assert.Same(third, node.Components[0]);
        Assert.Same(first, node.Components[1]);
        Assert.Same(second, node.Components[2]);

        action.Undo();
        Assert.Same(first, node.Components[0]);
        Assert.Same(second, node.Components[1]);
        Assert.Same(third, node.Components[2]);
    }

    [Fact]
    public void Reorder_component_action_merges_latest_index_for_same_component_and_node()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        node.CreateComponent<Camera>();
        var third = node.CreateComponent<Camera>();
        var action = new ReorderComponentAction(third, oldIndex: 2, newIndex: 1);
        var other = new ReorderComponentAction(third, oldIndex: 1, newIndex: 0);

        Assert.True(action.MergeWith(other));
        action.Redo();
        Assert.Same(third, node.Components[0]);
        action.Undo();
        Assert.Same(first, node.Components[0]);
        Assert.Same(third, node.Components[2]);
    }

    [Fact]
    public void Reorder_component_action_rejects_other_type_scene_component_or_node()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var node = scene.CreateChild("Node");
        var component = node.CreateComponent<Camera>();
        var sibling = node.CreateComponent<Camera>();
        var otherNode = scene.CreateChild("Other Node");
        var otherNodeComponent = otherNode.CreateComponent<Camera>();
        var externalComponent = otherScene.CreateChild("Node").CreateComponent<Camera>();
        var action = new ReorderComponentAction(component, 0, 1);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(new ReorderComponentAction(sibling, 1, 0)));
        Assert.False(action.MergeWith(new ReorderComponentAction(otherNodeComponent, 0, 0)));
        Assert.False(action.MergeWith(new ReorderComponentAction(externalComponent, 0, 0)));
    }

    [Fact]
    public void Reorder_component_action_reports_missing_node_or_component()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var component = node.CreateComponent<Camera>(200);
        var action = new ReorderComponentAction(component, 0, 0);

        Assert.True(action.CanUndoRedo());
        component.Remove();
        Assert.False(action.CanUndoRedo());
        var exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("component with id 200", exception.Message);

        component = node.CreateComponent<Camera>(200);
        action = new ReorderComponentAction(component, 0, 0);
        node.Remove();
        exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("node with id 100", exception.Message);
    }

    [Fact]
    public void Reparent_node_action_redoes_and_restores_parent_and_index()
    {
        using var scene = new Scene();
        var oldParent = scene.CreateChild("Old Parent", 100);
        var before = oldParent.CreateChild("Before", 101);
        var node = oldParent.CreateChild("Node", 102);
        var after = oldParent.CreateChild("After", 103);
        var newParent = scene.CreateChild("New Parent", 104);
        var action = new ReparentNodeAction(node, newParent);

        action.Redo();
        Assert.Same(newParent, node.Parent);

        action.Undo();
        Assert.Same(oldParent, node.Parent);
        Assert.Same(before, oldParent.Children[0]);
        Assert.Same(node, oldParent.Children[1]);
        Assert.Same(after, oldParent.Children[2]);
    }

    [Fact]
    public void Reparent_node_action_merges_latest_parent_for_same_node()
    {
        using var scene = new Scene();
        var oldParent = scene.CreateChild("Old Parent", 100);
        var newParent = scene.CreateChild("New Parent", 101);
        var latestParent = scene.CreateChild("Latest Parent", 102);
        var node = oldParent.CreateChild("Node", 103);
        var action = new ReparentNodeAction(node, newParent);
        node.SetParent(newParent);
        var other = new ReparentNodeAction(node, latestParent);

        Assert.True(action.MergeWith(other));
        action.Undo();
        Assert.Same(oldParent, node.Parent);
        action.Redo();
        Assert.Same(latestParent, node.Parent);
    }

    [Fact]
    public void Reparent_node_action_rejects_other_type_scene_or_node()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var oldParent = scene.CreateChild("Old Parent");
        var newParent = scene.CreateChild("New Parent");
        var node = oldParent.CreateChild("Node");
        var sibling = oldParent.CreateChild("Sibling");
        var externalParent = otherScene.CreateChild("Old Parent");
        var externalNewParent = otherScene.CreateChild("New Parent");
        var externalNode = externalParent.CreateChild("Node");
        var action = new ReparentNodeAction(node, newParent);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(new ReparentNodeAction(sibling, newParent)));
        Assert.False(action.MergeWith(new ReparentNodeAction(externalNode, externalNewParent)));
    }

    [Fact]
    public void Reparent_node_action_reports_missing_node_or_parent()
    {
        using var scene = new Scene();
        var oldParent = scene.CreateChild("Old Parent", 100);
        var newParent = scene.CreateChild("New Parent", 101);
        var node = oldParent.CreateChild("Node", 102);
        var action = new ReparentNodeAction(node, newParent);

        Assert.True(action.CanUndoRedo());
        node.Remove();
        Assert.False(action.CanUndoRedo());
        var exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("node with id 102", exception.Message);

        node = oldParent.CreateChild("Node", 102);
        action = new ReparentNodeAction(node, newParent);
        newParent.Remove();
        Assert.False(action.CanUndoRedo());
        exception = Assert.Throws<UndoException>(() => action.Redo());
        Assert.Contains("parent node with id 101", exception.Message);
    }

    [Fact]
    public void Change_component_action_redoes_and_undoes_component_snapshot()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node", 100).CreateComponent<Camera>(200);
        camera.Fov = 30.0f;
        var oldData = new PackedComponentData(camera);
        camera.Fov = 70.0f;
        var action = new ChangeComponentAction(scene, oldData, camera);
        camera.Fov = 90.0f;

        action.Undo();
        Assert.Equal(30.0f, camera.Fov);
        action.Redo();
        Assert.Equal(70.0f, camera.Fov);
    }

    [Fact]
    public void Change_component_action_accepts_packed_new_snapshot()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node", 100).CreateComponent<Camera>(200);
        camera.Fov = 25.0f;
        var oldData = new PackedComponentData(camera);
        camera.Fov = 65.0f;
        var newData = new PackedComponentData(camera);
        var action = new ChangeComponentAction(scene, oldData, newData);
        camera.Fov = 90.0f;

        action.Undo();
        Assert.Equal(25.0f, camera.Fov);
        action.Redo();
        Assert.Equal(65.0f, camera.Fov);
    }

    [Fact]
    public void Change_component_action_merges_latest_snapshot_in_same_scene()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var first = node.CreateComponent<Camera>();
        first.Fov = 20.0f;
        var firstOldData = new PackedComponentData(first);
        first.Fov = 50.0f;
        var action = new ChangeComponentAction(scene, firstOldData, first);
        var second = node.CreateComponent<Camera>();
        second.Fov = 75.0f;
        var other = new ChangeComponentAction(scene, new PackedComponentData(second), second);

        Assert.True(action.MergeWith(other));
        second.Fov = 90.0f;
        var latest = new ChangeComponentAction(scene, new PackedComponentData(second), second);
        Assert.True(other.MergeWith(latest));
        action.Redo();
        Assert.Equal(75.0f, first.Fov);
        action.Undo();
        Assert.Equal(20.0f, first.Fov);
    }

    [Fact]
    public void Change_component_action_rejects_other_type_or_scene()
    {
        using var scene = new Scene();
        using var otherScene = new Scene();
        var camera = scene.CreateChild("Node").CreateComponent<Camera>();
        var otherCamera = otherScene.CreateChild("Node").CreateComponent<Camera>();
        var action = new ChangeComponentAction(scene, new PackedComponentData(camera), camera);
        var other = new ChangeComponentAction(
            otherScene, new PackedComponentData(otherCamera), otherCamera);

        Assert.False(action.MergeWith(new EmptyEditorAction()));
        Assert.False(action.MergeWith(other));
    }

    [Fact]
    public void Change_component_action_requires_scene_and_original_component()
    {
        using var scene = new Scene();
        var camera = scene.CreateChild("Node").CreateComponent<Camera>();
        var data = new PackedComponentData(camera);

        Assert.False(new ChangeComponentAction(null, data, camera).CanUndoRedo());

        var action = new ChangeComponentAction(scene, data, camera);
        Assert.True(action.CanUndoRedo());
        camera.Remove();
        Assert.False(action.CanUndoRedo());
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
