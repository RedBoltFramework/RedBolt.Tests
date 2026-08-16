// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PackedNodeDataTests
{
    [Fact]
    public void SpawnExact_restores_node_subtree_id_and_order()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent", 100);
        parent.CreateChild("Before", 101);
        var source = parent.CreateChild("Source", 102);
        source.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        source.CreateChild("Nested", 103);
        parent.CreateChild("After", 104);
        var packed = new PackedNodeData(source);

        source.Remove();
        var restored = packed.SpawnExact(scene);

        Assert.NotNull(restored);
        Assert.Equal(102u, restored.Id);
        Assert.Equal("Source", restored.Name);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), restored.Position);
        Assert.Equal("Nested", restored.GetChild(0)!.Name);
        Assert.Same(restored, parent.GetChild(1));
        Assert.Equal(AttributeScopeHint.Serializable, packed.EffectiveScopeHint);
    }

    [Fact]
    public void SpawnCopy_restores_captured_state_with_new_node_id()
    {
        using var scene = new Scene();
        var source = scene.CreateChild("Source", 100);
        source.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var packed = new PackedNodeData(source);
        var clone = packed.Clone();
        source.SetPosition(new Vector3f(4.0f, 5.0f, 6.0f));
        var parent = scene.CreateChild("Parent", 101);

        var restored = clone.SpawnCopy(parent);

        Assert.NotEqual(source.Id, restored.Id);
        Assert.Same(parent, restored.Parent);
        Assert.Equal("Source", restored.Name);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), restored.Position);
    }

    [Fact]
    public void SpawnExact_fails_when_parent_is_missing()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent", 100);
        var source = parent.CreateChild("Source", 101);
        var packed = new PackedNodeData(source);

        parent.Remove();

        Assert.Null(packed.SpawnExact(scene));
    }

    [Fact]
    public void SpawnExact_fails_without_replacing_conflicting_node_id()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent", 100);
        var source = parent.CreateChild("Source", 101);
        var packed = new PackedNodeData(source);
        source.Remove();
        var conflict = parent.CreateChild("Conflict", 101);

        Assert.Null(packed.SpawnExact(scene));
        Assert.Same(conflict, scene.GetNode(101));
        Assert.Single(parent.Children);
    }
}
