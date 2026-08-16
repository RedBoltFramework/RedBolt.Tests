// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Network;
using RedBolt.Replica;

public sealed class NetworkTimeTests
{
    [Fact]
    public void Network_time_is_updated_as_expected()
    {
        Assert.Equal(new NetworkTime((NetworkFrame)0, 0.0f), new NetworkTime());
        Assert.Equal(new NetworkTime((NetworkFrame)2, 0.25f), new NetworkTime((NetworkFrame)1, 1.25f));
        Assert.Equal(new NetworkTime((NetworkFrame)0, 0.75f), new NetworkTime((NetworkFrame)1, -0.25f));
        Assert.Equal("#2:0.25", new NetworkTime((NetworkFrame)2, 0.25f).ToString());

        Assert.Equal(new NetworkTime((NetworkFrame)2, 0.0f),
            new NetworkTime((NetworkFrame)1, 0.25f) + 0.75);
        Assert.Equal(new NetworkTime((NetworkFrame)0, 0.5f),
            new NetworkTime((NetworkFrame)1, 0.25f) - 0.75);
        Assert.Equal(new NetworkTime((NetworkFrame)34, 0.5f),
            new NetworkTime((NetworkFrame)10, 0.75f) + 23.75);
        Assert.Equal(new NetworkTime((NetworkFrame)(-14), 0.5f),
            new NetworkTime((NetworkFrame)10, 0.25f) - 23.75);
        Assert.Equal(new NetworkTime(), new NetworkTime((NetworkFrame)(-3), 0.25f) + 2.75);

        Assert.Equal(-0.75, new NetworkTime((NetworkFrame)1, 0.25f)
            - new NetworkTime((NetworkFrame)2, 0.0f));
        Assert.Equal(0.75, new NetworkTime((NetworkFrame)1, 0.25f)
            - new NetworkTime((NetworkFrame)0, 0.5f));
        Assert.Equal(-23.75, new NetworkTime((NetworkFrame)10, 0.75f)
            - new NetworkTime((NetworkFrame)34, 0.5f));
        Assert.Equal(23.75, new NetworkTime((NetworkFrame)10, 0.25f)
            - new NetworkTime((NetworkFrame)(-14), 0.5f));
        Assert.Equal(-2.75, new NetworkTime((NetworkFrame)(-3), 0.25f) - new NetworkTime());
    }

    [Fact]
    public void Soft_time_uses_normal_snap_and_dilation_paths()
    {
        var smooth = new SoftNetworkTime(10, 2.5f, 0.002f, 0.7f, 1.5f);

        smooth.Reset(new NetworkTime((NetworkFrame)0));
        Assert.Equal(0.1f, smooth.Update(0.1f, new NetworkTime((NetworkFrame)1)));
        Assert.Equal(new NetworkTime((NetworkFrame)1), smooth.Time);

        Assert.Equal(0.1f, smooth.Update(0.1f, new NetworkTime((NetworkFrame)100)));
        Assert.Equal(new NetworkTime((NetworkFrame)100), smooth.Time);

        smooth.Reset(new NetworkTime((NetworkFrame)0));
        Assert.InRange(smooth.Update(0.1f, new NetworkTime((NetworkFrame)2)), 0.14999f, 0.15001f);
        Assert.Equal(new NetworkTime((NetworkFrame)1, 0.5f), smooth.Time);
    }

    [Fact]
    public void Protocol_values_match_transport_flags()
    {
        Assert.Equal(0u, (uint)NetworkMessageId.ClockSync);
        Assert.Equal((ushort)3, (ushort)PacketType.ReliableOrdered);
        Assert.Equal(1024, NetworkProtocol.MaxNetworkPacketSize);
        Assert.Equal(0xFFFF, NetworkProtocol.MaxNetworkMessageSize);
        Assert.Equal(4, NetworkProtocol.NetworkMessageHeaderSize);
    }
}
