namespace RedBolt.Tests;

using RedBolt.Replica;
using RedBolt.Scene;

public sealed class TimeSynchronizationTests
{
    public static IEnumerable<object[]> Cases()
    {
        ConnectionQuality[] qualities =
        [
            new(0.08f, 0.12f, 0.20f, 0.02f, 0.02f),
            new(0.24f, 0.28f, 0.50f, 0.10f, 0.10f)
        ];
        for (var retry = 0u; retry < 5; ++retry)
        {
            foreach (var quality in qualities)
                yield return [retry, quality];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Time_is_synchronized_between_client_and_server(uint retry,
        ConnectionQuality quality)
    {
        using var fixture = new ReplicaTestContext();
        const uint fps = NetworkSimulator.FramesInSecond;
        const float frameErrorTolerance = 0.1f;
        const uint initialSyncTime = 10;
        const uint initialWaitTime = 30;
        const uint forwardSyncTime = 10;
        const uint forwardWaitTime = 30;
        const uint backwardSyncTime = 10;
        const uint backwardWaitTime = 30;

        var seed = retry;
        CombineHash(ref seed, BitConverter.SingleToUInt32Bits(quality.MinPing));
        CombineHash(ref seed, BitConverter.SingleToUInt32Bits(quality.MaxPing));
        CombineHash(ref seed, BitConverter.SingleToUInt32Bits(quality.SpikePing));

        using var serverScene = new Scene();
        using var clientScene = new Scene();
        using var simulator = new NetworkSimulator(serverScene, seed);
        simulator.AddClient(clientScene, quality);

        var serverReplicator = serverScene.GetComponent<ReplicationManager>()!.ServerReplicator!;

        simulator.SimulateTime(0.5f);
        Assert.Null(clientScene.GetComponent<ReplicationManager>()!.ClientReplica);

        simulator.SimulateTime(0.5f);
        simulator.SimulateTime(9.0f);

        var clientReplica = clientScene.GetComponent<ReplicationManager>()!.ClientReplica!;
        Assert.NotNull(clientReplica);
        var syncError = System.Math.Max(0.5f,
            (quality.MaxPing - quality.MinPing) * NetworkSimulator.FramesInSecond);
        var startTime = fps * 10;
        Assert.Equal(new NetworkFrame(startTime), serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime
            - new NetworkTime(new NetworkFrame(startTime))) < syncError);

        simulator.SimulateTime(initialSyncTime);
        Assert.Equal(new NetworkFrame(startTime + fps * initialSyncTime),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime
            - new NetworkTime(new NetworkFrame(startTime + fps * initialSyncTime)))
            < frameErrorTolerance);

        var syncFrame1 = clientReplica.LatestScaledInputTime.Frame;
        simulator.SimulateTime(initialWaitTime);
        Assert.Equal(new NetworkFrame(startTime + fps * (initialSyncTime + initialWaitTime)),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime - new NetworkTime(new NetworkFrame(
            startTime + fps * (initialSyncTime + initialWaitTime)))) < frameErrorTolerance);
        Assert.Equal(syncFrame1, clientReplica.LatestScaledInputTime.Frame);

        var bigTime = uint.MaxValue - fps * 30L;
        serverReplicator.SetCurrentFrame(new NetworkFrame(bigTime / 3));
        simulator.SimulateTime(5.0f);
        serverReplicator.SetCurrentFrame(new NetworkFrame(bigTime / 3 * 2));
        simulator.SimulateTime(5.0f);
        serverReplicator.SetCurrentFrame(new NetworkFrame(bigTime));
        simulator.SimulateTime(forwardSyncTime);

        Assert.Equal(new NetworkFrame(bigTime + fps * forwardSyncTime),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime - new NetworkTime(new NetworkFrame(
            bigTime + fps * forwardSyncTime))) < frameErrorTolerance);

        var syncFrame2 = clientReplica.LatestScaledInputTime.Frame;
        simulator.SimulateTime(forwardWaitTime);
        Assert.Equal(new NetworkFrame(bigTime + fps * (forwardSyncTime + forwardWaitTime)),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime - new NetworkTime(new NetworkFrame(
            bigTime + fps * (forwardSyncTime + forwardWaitTime)))) < frameErrorTolerance);
        Assert.Equal(syncFrame2, clientReplica.LatestScaledInputTime.Frame);

        var baseTime = bigTime + fps * (forwardSyncTime + forwardWaitTime);
        serverReplicator.SetCurrentFrame(new NetworkFrame(baseTime - fps));
        simulator.SimulateTime(backwardSyncTime + 1);
        Assert.Equal(new NetworkFrame(baseTime + fps * backwardSyncTime),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime - new NetworkTime(new NetworkFrame(
            baseTime + fps * backwardSyncTime))) < frameErrorTolerance);

        var syncFrame3 = clientReplica.LatestScaledInputTime.Frame;
        simulator.SimulateTime(backwardWaitTime);
        Assert.Equal(new NetworkFrame(baseTime + fps * (backwardSyncTime + backwardWaitTime)),
            serverReplicator.CurrentFrame);
        Assert.True(System.Math.Abs(clientReplica.ServerTime - new NetworkTime(new NetworkFrame(
            baseTime + fps * (backwardSyncTime + backwardWaitTime)))) < frameErrorTolerance);
        // Assert.Equal(syncFrame3, clientReplica.LatestScaledInputTime.Frame);
    }

    private static void CombineHash(ref uint seed, uint hash)
    {
        seed ^= hash + 0x9e3779b9u + (seed << 6) + (seed >> 2);
    }
}
