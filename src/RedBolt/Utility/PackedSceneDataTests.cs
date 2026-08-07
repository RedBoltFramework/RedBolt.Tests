namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PackedSceneDataTests
{
    [Fact]
    public void Default_data_is_empty()
    {
        var packed = new PackedSceneData();

        Assert.False(packed.HasSceneData);
        Assert.Equal(0u, packed.SceneData.Size);
    }

    [Fact]
    public void ToScene_restores_exact_captured_scene_and_temporary_objects()
    {
        using var sourceScene = new Scene();
        var sourceNode = sourceScene.CreateChild("Source", 100);
        sourceNode.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        var sourceCamera = sourceNode.CreateComponent<Camera>(200);
        sourceCamera.Fov = 67.5f;
        sourceNode.CreateTemporaryChild("Temporary", 101);
        var packed = PackedSceneData.FromScene(sourceScene);
        var clone = packed.Clone();
        sourceNode.SetName("Changed");
        sourceCamera.Fov = 30.0f;

        using var targetScene = new Scene();
        targetScene.CreateChild("Old", 300);
        clone.ToScene(targetScene);

        var restoredNode = Assert.IsType<Node>(targetScene.GetNode(100));
        var restoredCamera = Assert.IsType<Camera>(targetScene.GetComponent(200));
        Assert.True(packed.HasSceneData);
        Assert.Equal("Source", restoredNode.Name);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), restoredNode.Position);
        Assert.Equal(67.5f, restoredCamera.Fov);
        Assert.True(restoredNode.GetChild("Temporary")!.IsTemporary);
        Assert.Null(targetScene.GetNode(300));
    }

    [Fact]
    public void ToScene_can_discard_captured_ids()
    {
        using var sourceScene = new Scene();
        var sourceNode = sourceScene.CreateChild("Source", 100);
        sourceNode.CreateComponent<Camera>(200);
        var packed = PackedSceneData.FromScene(sourceScene);

        using var targetScene = new Scene();
        packed.ToScene(targetScene, PrefabLoadFlag.DiscardIds);

        var restoredNode = targetScene.GetChild("Source");
        var restoredCamera = restoredNode!.GetComponent<Camera>();
        Assert.NotEqual(100u, restoredNode.Id);
        Assert.NotEqual(200u, restoredCamera!.Id);
    }
}
