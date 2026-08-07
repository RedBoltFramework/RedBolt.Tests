namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PackedComponentDataTests
{
    [Fact]
    public void SpawnExact_restores_component_id_attributes_and_order()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        node.CreateComponent<Camera>(201);
        var source = node.CreateComponent<Camera>(202);
        source.Fov = 67.5f;
        node.CreateComponent<Camera>(203);
        var packed = new PackedComponentData(source);

        source.Remove();
        var restored = Assert.IsType<Camera>(packed.SpawnExact(scene));

        Assert.Equal(202u, restored.Id);
        Assert.Equal(67.5f, restored.Fov);
        Assert.Same(restored, node.Components[1]);
        Assert.Equal(restored.GetTypeNameHash(), packed.Type);
    }

    [Fact]
    public void SpawnCopy_restores_captured_state_and_resolves_id_conflict()
    {
        using var scene = new Scene();
        var sourceNode = scene.CreateChild("Source", 100);
        var source = sourceNode.CreateComponent<Camera>(200);
        source.Fov = 67.5f;
        var packed = new PackedComponentData(source);
        var clone = packed.Clone();
        source.Fov = 30.0f;
        var targetNode = scene.CreateChild("Target", 101);

        var restored = Assert.IsType<Camera>(clone.SpawnCopy(targetNode));

        Assert.NotEqual(source.Id, restored.Id);
        Assert.Same(targetNode, restored.Node);
        Assert.Equal(67.5f, restored.Fov);
    }

    [Fact]
    public void Update_restores_captured_component_attributes()
    {
        using var scene = new Scene();
        var source = scene.CreateChild("Source").CreateComponent<Camera>();
        source.Fov = 67.5f;
        var packed = new PackedComponentData(source);
        var target = scene.CreateChild("Target").CreateComponent<Camera>();
        target.Fov = 30.0f;

        packed.Update(target);

        Assert.Equal(67.5f, target.Fov);
    }

    [Fact]
    public void SpawnExact_fails_when_node_is_missing()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var source = node.CreateComponent<Camera>(200);
        var packed = new PackedComponentData(source);

        node.Remove();

        Assert.Null(packed.SpawnExact(scene));
    }

    [Fact]
    public void SpawnExact_fails_without_replacing_conflicting_component_id()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node", 100);
        var source = node.CreateComponent<Camera>(200);
        var packed = new PackedComponentData(source);
        source.Remove();
        var conflict = node.CreateComponent<Camera>(200);

        Assert.Null(packed.SpawnExact(scene));
        Assert.Same(conflict, scene.GetComponent(200));
        Assert.Single(node.Components);
    }

    [Fact]
    public void Default_data_cannot_spawn_unknown_component_type()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Node");
        var packed = new PackedComponentData();

        Assert.Null(packed.SpawnCopy(node));
    }
}
