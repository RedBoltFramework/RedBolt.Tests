namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Replica;
using RedBolt.Scene;

public sealed class TrackedAnimatedModelTests
{
    [Fact]
    public void TrackedAnimatedModel_tracks_bones_on_server()
    {
        using var fixture = new ReplicaTestContext();
        using var model = CreateTestAnimatedModel();
        using var serverScene = new Scene();

        var node = serverScene.CreateChild("Node");
        node.CreateComponent<BehaviorNetworkObject>();
        var animatedModel = node.CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model);
        var trackedAnimatedModel = node.CreateComponent<TrackedAnimatedModel>();

        var quad1 = node.GetChild("Quad 1", true)!;
        var quad2 = node.GetChild("Quad 2", true)!;
        Assert.NotNull(quad1);
        Assert.NotNull(quad2);

        using var animator = new BoneAnimator(serverScene, quad1, quad2);
        using var simulator = new NetworkSimulator(serverScene);
        var serverReplicator = serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!;

        simulator.SimulateTime(10.0f);
        var serverTime = serverReplicator.ServerTime;
        var quad1Position = quad1.GetWorldPosition();
        var quad1Rotation = quad1.GetWorldRotation();
        var quad2Position = quad2.GetWorldPosition();
        var quad2Rotation = quad2.GetWorldRotation();

        simulator.SimulateTime(2.0f);
        Assert.Equal(ToVector3d(quad1Position),
            trackedAnimatedModel.SampleTemporalBonePosition(serverTime, 1));
        Assert.Equal(ToVector3d(quad2Position),
            trackedAnimatedModel.SampleTemporalBonePosition(serverTime, 2));
        Assert.Equal(quad1Rotation,
            trackedAnimatedModel.SampleTemporalBoneRotation(serverTime, 1));
        Assert.Equal(quad2Rotation,
            trackedAnimatedModel.SampleTemporalBoneRotation(serverTime, 2));
    }

    private static Model CreateTestAnimatedModel()
    {
        var model = new Model { Name = "@/TrackedAnimatedModel/TestModel.mdl" };
        using var skeleton = new Skeleton();
        skeleton.SetNumBones(3);
        var bones = skeleton.GetModifiableBones();
        ConfigureBone(bones[0], "Root", 0);
        ConfigureBone(bones[1], "Quad 1", 0);
        ConfigureBone(bones[2], "Quad 2", 0);
        skeleton.SetRootBoneIndex(0);
        skeleton.UpdateBoneOrder();
        model.SetSkeleton(skeleton);
        return model;
    }

    private static void ConfigureBone(Bone bone, string name, int parentIndex)
    {
        bone.Name = name;
        bone.NameHash = new StringHash(name);
        bone.ParentIndex = parentIndex;
    }

    private static Vector3d ToVector3d(Vector3f value) => new(value.X, value.Y, value.Z);

    private sealed class BoneAnimator : RedObject
    {
        public BoneAnimator(Scene scene, Node quad1, Node quad2)
        {
            _quad1 = quad1;
            _quad2 = quad2;
            SubscribeToEvent<SceneEvents.SceneUpdateEvent>(scene, HandleSceneUpdate);
        }

        private void HandleSceneUpdate(ref SceneEvents.SceneUpdateEvent eventData)
        {
            _quad1.Translate(eventData.TimeStep * Vector3f.Left, TransformSpace.Parent);
            _quad1.Rotate(new Quaternion(eventData.TimeStep, Vector3f.Up), TransformSpace.Parent);
            _quad2.Translate(eventData.TimeStep * Vector3f.Forward, TransformSpace.Parent);
        }

        private readonly Node _quad1;
        private readonly Node _quad2;
    }
}
