// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PackedNodeComponentDataTests
{
    [Fact]
    public void Default_data_is_empty()
    {
        var packed = new PackedNodeComponentData();

        Assert.False(packed.HasNodes);
        Assert.Empty(packed.Nodes);
        Assert.False(packed.HasComponents);
        Assert.Empty(packed.Components);
        Assert.False(packed.HasNodesOrComponents);
    }

    [Fact]
    public void FromNodes_skips_null_nodes_and_preserves_order()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First", 100);
        var second = scene.CreateChild("Second", 101);

        var packed = PackedNodeComponentData.FromNodes(new Node?[] { second, null, first });
        var clone = packed.Clone();
        var target = scene.CreateChild("Target", 102);
        var firstCopy = clone.Nodes[0].SpawnCopy(target);
        var secondCopy = clone.Nodes[1].SpawnCopy(target);

        Assert.True(packed.HasNodes);
        Assert.Equal(2, packed.Nodes.Count);
        Assert.False(packed.HasComponents);
        Assert.True(packed.HasNodesOrComponents);
        Assert.Equal(101u, packed.Nodes[0].Id);
        Assert.Equal(100u, packed.Nodes[1].Id);
        Assert.Equal("Second", firstCopy.Name);
        Assert.Equal("First", secondCopy.Name);
    }

    [Fact]
    public void FromComponents_skips_null_components_and_preserves_order()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Source");
        var first = node.CreateComponent<Camera>();
        first.Fov = 60.0f;
        var second = node.CreateComponent<Camera>();
        second.Fov = 70.0f;

        var packed = PackedNodeComponentData.FromComponents(new Component?[] { second, null, first });
        var clone = packed.Clone();
        var target = scene.CreateChild("Target");
        var firstCopy = Assert.IsType<Camera>(clone.Components[0].SpawnCopy(target));
        var secondCopy = Assert.IsType<Camera>(clone.Components[1].SpawnCopy(target));

        Assert.False(packed.HasNodes);
        Assert.True(packed.HasComponents);
        Assert.Equal(2, packed.Components.Count);
        Assert.True(packed.HasNodesOrComponents);
        Assert.Equal(second.Id, packed.Components[0].Id);
        Assert.Equal(first.Id, packed.Components[1].Id);
        Assert.Equal(70.0f, firstCopy.Fov);
        Assert.Equal(60.0f, secondCopy.Fov);
    }
}
