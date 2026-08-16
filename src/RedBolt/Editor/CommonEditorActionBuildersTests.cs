// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CommonEditorActionBuildersTests
{
    [Theory]
    [InlineData(AttributeScopeHint.Attribute)]
    [InlineData(AttributeScopeHint.Serializable)]
    [InlineData(AttributeScopeHint.Node)]
    public void Create_node_builder_undoes_and_redoes_local_scope_creation(AttributeScopeHint scopeHint)
    {
        using var scene = new Scene();
        var builder = new CreateNodeActionBuilder(scene, scopeHint);
        var node = scene.CreateChild("Node", 100);
        var action = builder.Build(node);

        Assert.NotNull(action);
        action.Undo();
        Assert.Null(scene.GetNode(100));
        action.Redo();
        Assert.Equal("Node", Assert.IsType<Node>(scene.GetNode(100)).Name);
    }

    [Fact]
    public void Create_node_builder_undoes_and_redoes_scene_scope_creation()
    {
        using var scene = new Scene();
        var existing = scene.CreateChild("Existing", 100);
        var builder = new CreateNodeActionBuilder(scene, AttributeScopeHint.Scene);
        existing.SetName("Changed");
        scene.CreateChild("Created", 101);
        var action = builder.Build(scene.GetNode(101));

        Assert.NotNull(action);
        action.Undo();
        Assert.Equal("Existing", Assert.IsType<Node>(scene.GetNode(100)).Name);
        Assert.Null(scene.GetNode(101));
        action.Redo();
        Assert.Equal("Changed", Assert.IsType<Node>(scene.GetNode(100)).Name);
        Assert.Equal("Created", Assert.IsType<Node>(scene.GetNode(101)).Name);
    }

    [Fact]
    public void Create_node_builder_returns_no_action_for_unknown_scope()
    {
        using var scene = new Scene();
        var builder = new CreateNodeActionBuilder(scene, (AttributeScopeHint)999);
        var node = scene.CreateChild("Node");

        Assert.Null(builder.Build(node));
    }

    [Fact]
    public void Remove_node_builder_undoes_and_redoes_local_scope_removal()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var builder = new RemoveNodeActionBuilder(node);
        node.Remove();
        var action = builder.Build();

        Assert.NotNull(action);
        action.Undo();
        Assert.Equal("Node", Assert.IsType<Node>(scene.GetNode(100)).Name);
        action.Redo();
        Assert.Null(scene.GetNode(100));
    }

    [Fact]
    public void Remove_node_builder_undoes_and_redoes_scene_scope_removal()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        try
        {
            using var scene = new Scene();
            var existing = scene.CreateChild("Existing", 100);
            var node = scene.CreateChild("Node", 101);
            node.CreateComponent<Camera>(200);
            var builder = new RemoveNodeActionBuilder(node);
            existing.SetName("Changed");
            node.Remove();
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Existing", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal("Node", Assert.IsType<Node>(scene.GetNode(101)).Name);
            action.Redo();
            Assert.Equal("Changed", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Null(scene.GetNode(101));
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Theory]
    [InlineData(AttributeScopeHint.Attribute)]
    [InlineData(AttributeScopeHint.Serializable)]
    public void Create_component_builder_undoes_and_redoes_local_scope_creation(
        AttributeScopeHint scopeHint)
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(scopeHint);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Node", 100);
            var builder = new CreateComponentActionBuilder(node, reflection.TypeId);
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var action = builder.Build(camera);

            Assert.NotNull(action);
            action.Undo();
            Assert.Null(scene.GetComponent(200));
            action.Redo();
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Create_component_builder_undoes_and_redoes_node_scope_creation()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Node);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old", 100);
            var builder = new CreateComponentActionBuilder(node, reflection.TypeId);
            node.SetName("New");
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var action = builder.Build(camera);

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Old", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Null(scene.GetComponent(200));
            action.Redo();
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Create_component_builder_undoes_and_redoes_scene_scope_creation()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        try
        {
            using var scene = new Scene();
            var existing = scene.CreateChild("Existing", 100);
            var node = scene.CreateChild("Node", 101);
            var builder = new CreateComponentActionBuilder(node, reflection.TypeId);
            existing.SetName("Changed");
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var action = builder.Build(camera);

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Existing", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Null(scene.GetComponent(200));
            action.Redo();
            Assert.Equal("Changed", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Theory]
    [InlineData(AttributeScopeHint.Attribute)]
    [InlineData(AttributeScopeHint.Serializable)]
    public void Remove_component_builder_undoes_and_redoes_local_scope_removal(
        AttributeScopeHint scopeHint)
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(scopeHint);
        try
        {
            using var scene = new Scene();
            var camera = scene.CreateChild("Node", 100).CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var builder = new RemoveComponentActionBuilder(camera);
            camera.Remove();
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            action.Redo();
            Assert.Null(scene.GetComponent(200));
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Remove_component_builder_undoes_and_redoes_node_scope_removal()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Node);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old", 100);
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var builder = new RemoveComponentActionBuilder(camera);
            node.SetName("New");
            camera.Remove();
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Old", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            action.Redo();
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Null(scene.GetComponent(200));
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Remove_component_builder_undoes_and_redoes_scene_scope_removal()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        try
        {
            using var scene = new Scene();
            var existing = scene.CreateChild("Existing", 100);
            var camera = scene.CreateChild("Node", 101).CreateComponent<Camera>(200);
            camera.Fov = 70.0f;
            var builder = new RemoveComponentActionBuilder(camera);
            existing.SetName("Changed");
            camera.Remove();
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Existing", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            action.Redo();
            Assert.Equal("Changed", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Null(scene.GetComponent(200));
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Change_attribute_buffer_clone_is_independent()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var camera = node.CreateComponent<Camera>(200);
        camera.Fov = 70.0f;
        using var buffer = new ChangeAttributeBuffer();
        using (var oldValue = new Variant(10))
            buffer.OldValues.Add(oldValue);
        using (var newValue = new Variant(20))
            buffer.NewValues.Add(newValue);
        buffer.OldComponents.Add(new PackedComponentData(camera));
        buffer.NewComponents.Add(new PackedComponentData(camera));
        buffer.OldNodes.Add(new PackedNodeData(node));
        buffer.NewNodes.Add(new PackedNodeData(node));
        buffer.OldScene = PackedSceneData.FromScene(scene);
        buffer.NewScene = PackedSceneData.FromScene(scene);
        using var clone = buffer.Clone();

        using (var changedValue = new Variant(30))
            buffer.OldValues[0] = changedValue;
        buffer.OldComponents.Clear();
        buffer.NewComponents.Clear();
        buffer.OldNodes.Clear();
        buffer.NewNodes.Clear();
        buffer.OldScene.SceneData.Clear();
        buffer.NewScene.SceneData.Clear();

        Assert.Equal(10, clone.OldValues[0].GetInt());
        Assert.Equal(20, clone.NewValues[0].GetInt());
        Assert.Single(clone.OldComponents);
        Assert.Single(clone.NewComponents);
        Assert.Single(clone.OldNodes);
        Assert.Single(clone.NewNodes);
        Assert.True(clone.OldScene.HasSceneData);
        Assert.True(clone.NewScene.HasSceneData);
    }

    [Fact]
    public void Change_node_attributes_builder_keeps_other_attributes_local()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old", 100);
            node.CreateComponent<Camera>(200);
            using var buffer = new ChangeAttributeBuffer();
            IEnumerable<Node> nodes = new HashSet<Node> { node };
            var attribute = Context.Instance.GetAttribute<Node>("Name");
            var builder = new ChangeNodeAttributesActionBuilder(buffer, scene, nodes, attribute);
            node.SetName("New");
            scene.CreateChild("Unrelated", 101);
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Old", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal("Unrelated", Assert.IsType<Node>(scene.GetNode(101)).Name);
            action.Redo();
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal("Unrelated", Assert.IsType<Node>(scene.GetNode(101)).Name);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Change_node_attributes_builder_undoes_and_redoes_serializable_scope_change()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Serializable);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Node", 100);
            node.CreateComponent<Camera>(200);
            using var buffer = new ChangeAttributeBuffer();
            var attribute = Context.Instance.GetAttribute<Node>("Is Enabled");
            var builder = new ChangeNodeAttributesActionBuilder(buffer, scene, new[] { node }, attribute);
            node.SetEnabled(false);
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.True(Assert.IsType<Node>(scene.GetNode(100)).IsEnabled);
            action.Redo();
            Assert.False(Assert.IsType<Node>(scene.GetNode(100)).IsEnabled);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Change_node_attributes_builder_undoes_and_redoes_node_scope_change()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Node);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old Parent", 100);
            node.CreateComponent<Camera>(200);
            var child = node.CreateChild("Old Child", 101);
            using var buffer = new ChangeAttributeBuffer();
            var attribute = Context.Instance.GetAttribute<Node>("Is Enabled");
            var builder = new ChangeNodeAttributesActionBuilder(buffer, scene, new[] { node, child }, attribute);
            node.SetName("New Parent");
            child.SetName("New Child");
            node.SetEnabled(false);
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            var oldNode = Assert.IsType<Node>(scene.GetNode(100));
            Assert.Equal("Old Parent", oldNode.Name);
            Assert.True(oldNode.IsEnabled);
            Assert.Equal("Old Child", Assert.IsType<Node>(scene.GetNode(101)).Name);
            action.Redo();
            var newNode = Assert.IsType<Node>(scene.GetNode(100));
            Assert.Equal("New Parent", newNode.Name);
            Assert.False(newNode.IsEnabled);
            Assert.Equal("New Child", Assert.IsType<Node>(scene.GetNode(101)).Name);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Change_node_attributes_builder_undoes_and_redoes_scene_scope_change()
    {
        var reflection = Context.Instance.GetReflection<Camera>();
        var oldScopeHint = reflection.ScopeHint;
        reflection.SetScopeHint(AttributeScopeHint.Scene);
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Node", 100);
            node.CreateComponent<Camera>(200);
            var existing = scene.CreateChild("Old", 101);
            using var buffer = new ChangeAttributeBuffer();
            var attribute = Context.Instance.GetAttribute<Node>("Is Enabled");
            var builder = new ChangeNodeAttributesActionBuilder(buffer, scene, new[] { node }, attribute);
            node.SetEnabled(false);
            existing.SetName("New");
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.True(Assert.IsType<Node>(scene.GetNode(100)).IsEnabled);
            Assert.Equal("Old", Assert.IsType<Node>(scene.GetNode(101)).Name);
            action.Redo();
            Assert.False(Assert.IsType<Node>(scene.GetNode(100)).IsEnabled);
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(101)).Name);
        }
        finally
        {
            reflection.SetScopeHint(oldScopeHint);
        }
    }

    [Fact]
    public void Change_component_attributes_builder_undoes_and_redoes_attribute_scope_change()
    {
        var attribute = Context.Instance.GetAttribute<Camera>("FOV");
        var oldScopeHint = attribute.ScopeHint;
        attribute.ScopeHint = AttributeScopeHint.Attribute;
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old", 100);
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 60.0f;
            using var buffer = new ChangeAttributeBuffer();
            IEnumerable<Component> components = new HashSet<Component> { camera };
            var builder = new ChangeComponentAttributesActionBuilder(buffer, scene, components, attribute);
            camera.Fov = 70.0f;
            node.SetName("New");
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal(60.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(100)).Name);
            action.Redo();
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(100)).Name);
        }
        finally
        {
            attribute.ScopeHint = oldScopeHint;
        }
    }

    [Fact]
    public void Change_component_attributes_builder_undoes_and_redoes_serializable_scope_change()
    {
        var attribute = Context.Instance.GetAttribute<Camera>("FOV");
        var oldScopeHint = attribute.ScopeHint;
        attribute.ScopeHint = AttributeScopeHint.Serializable;
        try
        {
            using var scene = new Scene();
            var camera = scene.CreateChild("Node", 100).CreateComponent<Camera>(200);
            camera.Fov = 60.0f;
            camera.NearClip = 0.2f;
            using var buffer = new ChangeAttributeBuffer();
            var builder = new ChangeComponentAttributesActionBuilder(buffer, scene, new[] { camera }, attribute);
            camera.Fov = 70.0f;
            camera.NearClip = 0.3f;
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            var oldCamera = Assert.IsType<Camera>(scene.GetComponent(200));
            Assert.Equal(60.0f, oldCamera.Fov);
            Assert.Equal(0.2f, oldCamera.NearClip);
            action.Redo();
            var newCamera = Assert.IsType<Camera>(scene.GetComponent(200));
            Assert.Equal(70.0f, newCamera.Fov);
            Assert.Equal(0.3f, newCamera.NearClip);
        }
        finally
        {
            attribute.ScopeHint = oldScopeHint;
        }
    }

    [Fact]
    public void Change_component_attributes_builder_undoes_and_redoes_node_scope_change()
    {
        var attribute = Context.Instance.GetAttribute<Camera>("FOV");
        var oldScopeHint = attribute.ScopeHint;
        attribute.ScopeHint = AttributeScopeHint.Node;
        try
        {
            using var scene = new Scene();
            var node = scene.CreateChild("Old Parent", 100);
            var camera = node.CreateComponent<Camera>(200);
            camera.Fov = 60.0f;
            var child = node.CreateChild("Old Child", 101);
            var childCamera = child.CreateComponent<Camera>(201);
            childCamera.Fov = 61.0f;
            using var buffer = new ChangeAttributeBuffer();
            var builder = new ChangeComponentAttributesActionBuilder(
                buffer, scene, new[] { camera, childCamera }, attribute);
            node.SetName("New Parent");
            camera.Fov = 70.0f;
            child.SetName("New Child");
            childCamera.Fov = 71.0f;
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal("Old Parent", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(60.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("Old Child", Assert.IsType<Node>(scene.GetNode(101)).Name);
            Assert.Equal(61.0f, Assert.IsType<Camera>(scene.GetComponent(201)).Fov);
            action.Redo();
            Assert.Equal("New Parent", Assert.IsType<Node>(scene.GetNode(100)).Name);
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("New Child", Assert.IsType<Node>(scene.GetNode(101)).Name);
            Assert.Equal(71.0f, Assert.IsType<Camera>(scene.GetComponent(201)).Fov);
        }
        finally
        {
            attribute.ScopeHint = oldScopeHint;
        }
    }

    [Fact]
    public void Change_component_attributes_builder_undoes_and_redoes_scene_scope_change()
    {
        var attribute = Context.Instance.GetAttribute<Camera>("FOV");
        var oldScopeHint = attribute.ScopeHint;
        attribute.ScopeHint = AttributeScopeHint.Scene;
        try
        {
            using var scene = new Scene();
            var camera = scene.CreateChild("Node", 100).CreateComponent<Camera>(200);
            camera.Fov = 60.0f;
            var existing = scene.CreateChild("Old", 101);
            using var buffer = new ChangeAttributeBuffer();
            var builder = new ChangeComponentAttributesActionBuilder(buffer, scene, new[] { camera }, attribute);
            camera.Fov = 70.0f;
            existing.SetName("New");
            var action = builder.Build();

            Assert.NotNull(action);
            action.Undo();
            Assert.Equal(60.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("Old", Assert.IsType<Node>(scene.GetNode(101)).Name);
            action.Redo();
            Assert.Equal(70.0f, Assert.IsType<Camera>(scene.GetComponent(200)).Fov);
            Assert.Equal("New", Assert.IsType<Node>(scene.GetNode(101)).Name);
        }
        finally
        {
            attribute.ScopeHint = oldScopeHint;
        }
    }
}
