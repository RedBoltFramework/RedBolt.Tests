// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Net;
using System.Net.Sockets;

using RedBolt.Core;
using RedBolt.Container;
using RedBolt.Network;

public sealed class LANDiscoveryTests
{
    [Fact]
    public void Local_discovery_broadcasts_and_decodes_variant_beacon()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var receiver = new LANDiscoveryManager { BroadcastTimeMs = 0 };
            using var broadcaster = new LANDiscoveryManager { BroadcastTimeMs = 0 };
            using var probe = new DiscoveryProbe();
            using var beacon = new VariantMap
            {
                [new StringHash("Name")] = new Variant("Test server"),
                [new StringHash("Players")] = new Variant(3)
            };
            broadcaster.SetBroadcastData(beacon);
            var port = GetAvailableUdpPort();

            Assert.True(receiver.Start((ushort)port, LANDiscoveryMode.Local));
            Assert.True(broadcaster.Start((ushort)port, LANDiscoveryMode.Local));

            var update = new CoreEvents.UpdateEvent();
            Assert.True(SpinWait.SpinUntil(() =>
            {
                receiver.SendEvent(ref update);
                global::System.Threading.Thread.Sleep(5);
                return probe.Beacon is not null;
            }, TimeSpan.FromSeconds(3)));

            Assert.StartsWith("127.", probe.Address);
            Assert.True(probe.Port > 0);
            Assert.Equal("Test server",
                probe.Beacon![new StringHash("Name")].GetString());
            Assert.Equal(3, probe.Beacon[new StringHash("Players")].GetInt());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private static int GetAvailableUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class DiscoveryProbe : RedObject
    {
        public DiscoveryProbe()
        {
            SubscribeToEvent<NetworkEvents.NetworkHostDiscoveredEvent>(OnDiscovered);
        }

        public string Address { get; private set; } = string.Empty;
        public int Port { get; private set; }
        public VariantMap? Beacon { get; private set; }

        private void OnDiscovered(ref NetworkEvents.NetworkHostDiscoveredEvent eventData)
        {
            Address = eventData.Address;
            Port = eventData.Port;
            Beacon = eventData.Beacon;
        }
    }
}
