namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;
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
}
