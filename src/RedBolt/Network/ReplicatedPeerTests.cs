// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Network;

public sealed class ReplicatedPeerTests
{
    [Fact]
    public void Default_settings_match_upstream()
    {
        var settings = new ReplicatedPeerSettings();

        Assert.Equal(250u, settings.PingIntervalMs);
        Assert.Equal(10000u, settings.MaxPingMs);
        Assert.Equal(40u, settings.ClockBufferSize);
        Assert.Equal(10u, settings.PingBufferSize);
        Assert.Null(settings.GetTimestamp);
    }

    [Fact]
    public void Peer_without_replication_manager_synchronizes_clock()
    {
        using var fixture = new ReplicaTestContext();
        var network = fixture.Context.GetSubsystem<RedBolt.Network.Network>()!;
        var now = 1000u;
        var settings = new ReplicatedPeerSettings { GetTimestamp = () => now };
        using var connectionA = new InMemoryConnection();
        using var connectionB = new InMemoryConnection();
        connectionA.Peer = connectionB;
        connectionB.Peer = connectionA;
        connectionA.ConnectNow();
        connectionB.ConnectNow();
        using var peerA = new ReplicatedPeer(connectionA, settings);
        using var peerB = new ReplicatedPeer(connectionB, settings);
        Assert.Null(peerA.ReplicationManager);
        Assert.Null(peerB.ReplicationManager);

        // The first update sends a ping from each peer.
        var postUpdate = new CoreEvents.PostUpdateEvent { TimeStep = 0.01f };
        network.SendEvent(ref postUpdate);
        Assert.Equal(1, connectionA.FlushOutgoing());
        Assert.Equal(1, connectionB.FlushOutgoing());
        Assert.Equal(0u, peerA.LocalTimeOfLatestRoundtrip);

        // The second update answers the pings with pongs. The pongs complete the roundtrips.
        now = 1010;
        network.SendEvent(ref postUpdate);
        Assert.Equal(1, connectionA.FlushOutgoing());
        Assert.Equal(1, connectionB.FlushOutgoing());
        Assert.Equal(1010u, peerA.LocalTimeOfLatestRoundtrip);
        Assert.Equal(1010u, peerB.LocalTimeOfLatestRoundtrip);
    }

    private sealed class InMemoryConnection : NetworkConnection
    {
        public InMemoryConnection? Peer { get; set; }

        public override bool Connect(URL url) => false;

        public override void Disconnect()
        {
            _state = NetworkConnectionState.Disconnected;
            HandleDisconnected();
        }

        public override bool SendData(ReadOnlySpan<byte> data,
            PacketType type = PacketType.ReliableOrdered)
        {
            _packets.Add(data.ToArray());
            return true;
        }

        public void ConnectNow()
        {
            _state = NetworkConnectionState.Connected;
            HandleConnected();
        }

        public int FlushOutgoing()
        {
            var packets = _packets.ToArray();
            _packets.Clear();
            foreach (var packet in packets)
                Peer!.HandleDataReceived(packet);
            return packets.Length;
        }

        private readonly List<byte[]> _packets = [];
    }
}
