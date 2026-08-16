// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

public sealed class SceneSelectionTests
{
    [Fact]
    public void Packed_selection_roundtrips_clones_compares_and_clears()
    {
        var packed = new PackedSceneSelection
        {
            NodeIds = [9, 3],
            ComponentIds = [12, 4],
            ActiveNodeOrSceneId = 9,
            ActiveNodeId = 3,
            ActiveComponentId = 12,
        };
        var clone = packed.Clone();
        Assert.Equal(packed, clone);
        Assert.True(packed == clone);
        clone.NodeIds[0] = 100;
        Assert.NotEqual(packed, clone);

        using var jsonFile = new JSONFile();
        Assert.True(jsonFile.SaveObject("selection", packed));
        using var restoredFile = new JSONFile();
        Assert.True(restoredFile.FromString(jsonFile.ToString()));
        var restored = new PackedSceneSelection();
        Assert.True(restoredFile.LoadObject("selection", restored));
        Assert.Equal(packed, restored);

        restored.Clear();
        Assert.Empty(restored.NodeIds);
        Assert.Empty(restored.ComponentIds);
        Assert.Equal(0u, restored.ActiveNodeOrSceneId);
        Assert.Equal(0u, restored.ActiveNodeId);
        Assert.Equal(0u, restored.ActiveComponentId);
    }

    [Fact]
    public void Selection_tracks_active_effective_and_packed_scene_objects()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.RegisterAssembly(typeof(Scene).Assembly);

            using var scene = new Scene();
            var node = scene.CreateChild("Alpha");
            var camera = node.CreateComponent<Camera>();

            var selection = new SceneSelection();
            var changeCount = 0;
            selection.OnChanged += changedSelection =>
            {
                Assert.Same(selection, changedSelection);
                ++changeCount;
            };

            Assert.Equal(1u, selection.Revision);
            Assert.True(selection.IsEmpty);

            selection.SetSelected(scene, true);
            selection.SetSelected(camera, true);
            Assert.False(selection.IsEmpty);
            Assert.True(selection.IsSelected(scene));
            Assert.True(selection.IsSelected(camera));
            Assert.True(selection.IsSelected((RedObject)camera));
            Assert.True(selection.IsSelected(node, true));
            Assert.Same(node, selection.ActiveNodeOrScene);
            Assert.Same(node, selection.ActiveNode);
            Assert.Same(camera, selection.ActiveObject);
            Assert.Equal("Scene, Camera", selection.GetSummary(scene));

            selection.SetSelected(node, true, false);
            Assert.Equal("Scene, Node 'Alpha', Camera", selection.GetSummary(scene));

            var packed = selection.Pack();
            var expectedNodeIds = new[] { scene.Id, node.Id };
            Array.Sort(expectedNodeIds);
            Assert.Equal(expectedNodeIds, packed.NodeIds);
            Assert.Equal([camera.Id], packed.ComponentIds);
            Assert.Equal(node.Id, packed.ActiveNodeOrSceneId);
            Assert.Equal(node.Id, packed.ActiveNodeId);
            Assert.Equal(camera.Id, packed.ActiveComponentId);

            selection.Clear();
            Assert.True(selection.IsEmpty);
            selection.Load(scene, packed);
            Assert.True(selection.IsSelected(scene));
            Assert.True(selection.IsSelected(node));
            Assert.True(selection.IsSelected(camera));
            Assert.False(selection.IsSelected((RedObject)camera));
            Assert.Same(node, selection.ActiveNodeOrScene);
            Assert.Same(node, selection.ActiveNode);
            Assert.Null(selection.ActiveObject);

            selection.Clear();
            selection.SetSelected(camera, true);
            var changesBeforeConversion = changeCount;
            selection.ConvertToNodes();
            Assert.True(selection.IsSelected(node));
            Assert.False(selection.IsSelected(camera));
            Assert.True(selection.IsSelected((RedObject)camera));
            Assert.Equal(changesBeforeConversion + 2, changeCount);
            Assert.True(selection.Revision > 1);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
