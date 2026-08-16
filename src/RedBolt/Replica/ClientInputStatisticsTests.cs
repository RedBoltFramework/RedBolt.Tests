// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Replica;

public sealed class ClientInputStatisticsTests
{
    [Fact]
    public void Client_input_quality_is_evaluated()
    {
        var statistics = new ClientInputStatistics(10, 8);

        statistics.OnInputReceived((NetworkFrame)1001);
        statistics.OnInputReceived((NetworkFrame)1002);
        statistics.OnInputReceived((NetworkFrame)1004);
        statistics.OnInputReceived((NetworkFrame)1005);
        statistics.OnInputReceived((NetworkFrame)1007);
        statistics.OnInputReceived((NetworkFrame)1009);
        statistics.OnInputReceived((NetworkFrame)1010);
        Assert.Equal(1u, statistics.RecommendedBufferSize);

        statistics.OnInputReceived((NetworkFrame)1020);
        Assert.Equal(1u, statistics.RecommendedBufferSize);

        statistics.OnInputReceived((NetworkFrame)1023);
        statistics.OnInputReceived((NetworkFrame)1024);
        statistics.OnInputReceived((NetworkFrame)1026);
        statistics.OnInputReceived((NetworkFrame)1030);
        Assert.Equal(2u, statistics.RecommendedBufferSize);
    }
}
