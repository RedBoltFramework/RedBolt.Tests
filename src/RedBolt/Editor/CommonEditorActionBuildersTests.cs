namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Scene;

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
}
