namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Scene;

public sealed class ReplicatedAnimationTests
{
    [Fact]
    public void Animation_is_synchronized_between_client_and_server()
    {
        using var fixture = new ReplicaTestContext();
        var resources = Context.Instance.GetSubsystem<ResourceCache>()!;
        var prefab = CreateTestPrefab();
        var animation1 = CreateTestAnimation("@Tests/ReplicatedAnimation/Animation1.ani",
            new Vector3f(1.0f, 0.0f, 0.0f));
        var animation2 = CreateTestAnimation("@Tests/ReplicatedAnimation/Animation2.ani",
            new Vector3f(0.0f, 0.0f, 2.0f));
        Assert.True(resources.AddManualResource(animation1));
        Assert.True(resources.AddManualResource(animation2));

        var quality = new ConnectionQuality(0.01f, 0.01f, 0.01f, 0, 0);
        using var serverScene = new Scene();
        using var clientScene = new Scene();

        var serverNode = NetworkSimulator.SpawnOnServer<BehaviorNetworkObject>(
            serverScene, prefab, "Node");
        var serverAnimationController = serverNode.GetComponent<AnimationController>()!;
        serverAnimationController.PlayNewExclusive(new AnimationParameters(animation1).Looped());

        using var simulator = new NetworkSimulator(serverScene);
        simulator.SimulateTime(5.0f);
        simulator.AddClient(clientScene, quality);
        simulator.SimulateTime(10.0f);

        var serverReplicator = serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!;
        var clientReplica = clientScene.GetComponent<ReplicationManager>()!.ClientReplica!;
        var clientDelay = (serverReplicator.ServerTime - clientReplica.ReplicaTime)
            / NetworkSimulator.FramesInSecond;

        var clientNode = clientScene.GetChild("Node")!;
        Assert.NotNull(clientNode);
        var clientAnimationController = clientNode.GetComponent<AnimationController>()!;
        var clientReplicatedAnimation = clientNode.GetComponent<ReplicatedAnimation>()!;
        Assert.NotNull(clientAnimationController);

        {
            var lookups = clientReplicatedAnimation.AnimationLookup;
            Assert.Single(lookups);
            Assert.Equal(animation1.Name, lookups[animation1.GetNameHash()]);

            Assert.Equal(1, clientAnimationController.GetNumAnimations());
            var serverParameters = serverAnimationController.GetAnimationParameters(0);
            var clientParameters = clientAnimationController.GetAnimationParameters(0);
            Assert.Same(animation1, clientParameters.GetAnimation());
            AssertClose(serverParameters.GetTime(), clientParameters.GetTime() + clientDelay);
        }

        serverAnimationController.PlayNewExclusive(new AnimationParameters(animation2).Looped());
        simulator.SimulateTime(1.0f);

        {
            var lookups = clientReplicatedAnimation.AnimationLookup;
            Assert.Equal(2, lookups.Count);
            Assert.Equal(animation1.Name, lookups[animation1.GetNameHash()]);
            Assert.Equal(animation2.Name, lookups[animation2.GetNameHash()]);

            Assert.Equal(1, clientAnimationController.GetNumAnimations());
            var serverParameters = serverAnimationController.GetAnimationParameters(0);
            var clientParameters = clientAnimationController.GetAnimationParameters(0);
            Assert.Same(animation2, clientParameters.GetAnimation());
            AssertClose(serverParameters.GetTime(), clientParameters.GetTime() + clientDelay);
        }

        serverAnimationController.PlayNew(new AnimationParameters(animation1).Looped().Additive());
        simulator.SimulateTime(1.0f);

        {
            var lookups = clientReplicatedAnimation.AnimationLookup;
            Assert.Equal(2, lookups.Count);
            Assert.Equal(animation1.Name, lookups[animation1.GetNameHash()]);
            Assert.Equal(animation2.Name, lookups[animation2.GetNameHash()]);

            Assert.Equal(2, clientAnimationController.GetNumAnimations());
            var serverParameters = serverAnimationController.GetAnimationParameters(0);
            var clientParameters = clientAnimationController.GetAnimationParameters(0);
            Assert.Same(animation2, clientParameters.GetAnimation());
            AssertClose(serverParameters.GetTime(), clientParameters.GetTime() + clientDelay);
            Assert.Equal(AnimationBlendMode.Lerp, clientParameters.BlendMode);

            serverParameters = serverAnimationController.GetAnimationParameters(1);
            clientParameters = clientAnimationController.GetAnimationParameters(1);
            Assert.Same(animation1, clientParameters.GetAnimation());
            AssertClose(serverParameters.GetTime(), clientParameters.GetTime() + clientDelay);
            Assert.Equal(AnimationBlendMode.Additive, clientParameters.BlendMode);
        }
    }

    private static PrefabResource CreateTestPrefab()
    {
        using var node = new Node();
        node.CreateComponent<AnimationController>();
        node.CreateComponent<ReplicatedAnimation>();
        return NetworkSimulator.ConvertNodeToPrefab(node,
            "@Tests/ReplicatedAnimation/Test.prefab");
    }

    private static Animation CreateTestAnimation(string name, Vector3f magnitude)
    {
        var animation = new Animation();
        animation.SetName(name);
        animation.SetLength(2.0f);
        var track = animation.CreateTrack(string.Empty);
        track.ChannelMask = AnimationChannel.Position;
        var origin = new Vector3f(0.0f, 1.0f, 0.0f);
        track.KeyFrames.Add(new AnimationKeyFrame(0.0f, origin));
        track.KeyFrames.Add(new AnimationKeyFrame(0.5f, origin - magnitude));
        track.KeyFrames.Add(new AnimationKeyFrame(1.5f, origin + magnitude));
        track.KeyFrames.Add(new AnimationKeyFrame(2.0f, origin));
        return animation;
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.InRange(actual, expected - 0.0001, expected + 0.0001);
}
