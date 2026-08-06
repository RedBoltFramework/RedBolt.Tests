namespace RedBolt.Tests;

using RedBolt.Network;
using RedBolt.Replica;

public sealed class NetworkTimeTests
{
    [Fact]
    public void Time_normalizes_and_preserves_delta_arithmetic()
    {
        var beforeFrame = new NetworkTime((NetworkFrame)10, -0.25f);
        var afterFrame = new NetworkTime((NetworkFrame)10, 2.25f);

        Assert.Equal((NetworkFrame)9, beforeFrame.Frame);
        Assert.Equal(0.75f, beforeFrame.Fraction);
        Assert.Equal((NetworkFrame)12, afterFrame.Frame);
        Assert.Equal(0.25f, afterFrame.Fraction);
        Assert.Equal(2.5, afterFrame - beforeFrame);
        Assert.Equal("#12:0.25", afterFrame.ToString());
        Assert.Equal(NetworkTime.FromDouble(12.25), afterFrame);
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
