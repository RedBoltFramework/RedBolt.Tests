// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;

public sealed class ClockSynchronizerTests
{
    [Fact]
    public void Filtered_uint_rejects_high_outlier()
    {
        var filtered = new FilteredUint(4);
        Assert.False(filtered.IsInitialized);

        filtered.AddValue(100, false);
        filtered.AddValue(102, false);
        filtered.AddValue(104, false);
        filtered.AddValue(1000, false);
        filtered.Filter();

        Assert.True(filtered.IsInitialized);
        Assert.Equal(100u, filtered.MinValue);
        Assert.Equal(102u, filtered.AverageValue);
        Assert.Equal(104u, filtered.MaxValue);
        Assert.Equal(102u, filtered.StabilizedAverageMaxValue);
    }

    [Fact]
    public void Message_roundtrips_through_serializer()
    {
        var message = new ClockSynchronizerMessage
        {
            Magic = 123,
            Phase = ClockSynchronizerPhase.Pong,
            LocalSent = 1000,
            RemoteReceived = 2000,
            RemoteSent = 2001
        };
        using var buffer = new VectorBuffer();

        message.Save(buffer);
        buffer.Seek(0);
        var restored = new ClockSynchronizerMessage();
        restored.Load(buffer);

        Assert.Equal(message.Magic, restored.Magic);
        Assert.Equal(message.Phase, restored.Phase);
        Assert.Equal(message.LocalSent, restored.LocalSent);
        Assert.Equal(message.RemoteReceived, restored.RemoteReceived);
        Assert.Equal(message.RemoteSent, restored.RemoteSent);
        Assert.Equal("{magic=123, phase=2, ...}", restored.ToString());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    public void System_clock_is_synchronized_between_client_and_server(uint seed)
    {
        const uint minDelay = 250;
        const uint maxDelay = 350;
        const uint throttleDelay = 100;
        const float throttleChance = 0.2f;

        var random = new RandomEngine(seed);
        uint GetRandomDelay()
        {
            var isThrottled = random.GetBool(throttleChance);
            var baseDelay = random.GetUInt(minDelay, maxDelay);
            return baseDelay + (isThrottled ? throttleDelay : 0);
        }

        var simulator = new ClockSynchronizerSimulator(GetRandomDelay);
        simulator.Simulate(2000);

        Assert.InRange(simulator.PredictedServerToClientOffset[^1], 9900, 10100);
        Assert.InRange(simulator.PredictedClientToServerOffset[^1], -10100, -9900);
        Assert.InRange(simulator.PingOnClient[^1], 250u, 400u);
        Assert.InRange(simulator.PingOnServer[^1], 250u, 400u);

        simulator.Simulate(2000);
        simulator.Simulate(10000);

        Assert.InRange(simulator.PredictedServerToClientOffset[^1], 9950, 10050);
        Assert.InRange(simulator.PredictedClientToServerOffset[^1], -10050, -9950);
        Assert.True(simulator.PredictedServerToClientOffset.Max()
            - simulator.PredictedServerToClientOffset.Min() < 35);
        Assert.True(simulator.PredictedClientToServerOffset.Max()
            - simulator.PredictedClientToServerOffset.Min() < 35);
        Assert.InRange(simulator.PingOnServer.Min(), 250u, 400u);
        Assert.InRange(simulator.PingOnServer.Max(), 250u, 400u);
        Assert.InRange(simulator.PingOnClient.Min(), 250u, 400u);
        Assert.InRange(simulator.PingOnClient.Max(), 250u, 400u);
    }

    private sealed class ClockSynchronizerSimulator
    {
        public ClockSynchronizerSimulator(Func<uint> getRandomDelay)
        {
            _getRandomDelay = getRandomDelay;
            _serverSync = new ClockSynchronizer(250, 10000, 40, 10, () => _serverClock);
            _clientSync = new ClockSynchronizer(250, 10000, 40, 10, () => _clientClock);
        }

        public void Simulate(uint totalTime, uint timeStep = 10)
        {
            PredictedServerToClientOffset.Clear();
            PredictedClientToServerOffset.Clear();
            PingOnServer.Clear();
            PingOnClient.Clear();

            var numSteps = System.Math.Max(1u, totalTime / timeStep);
            for (var i = 0u; i < numSteps; ++i)
            {
                SimulateTimeStep(timeStep);
                if (_serverSync.IsReady)
                {
                    PredictedServerToClientOffset.Add(unchecked((int)_serverSync.LocalToRemote(0)));
                    PingOnClient.Add(_serverSync.Ping);
                }
                if (_clientSync.IsReady)
                {
                    PredictedClientToServerOffset.Add(unchecked((int)_clientSync.LocalToRemote(0)));
                    PingOnServer.Add(_clientSync.Ping);
                }
            }
        }

        public List<int> PredictedServerToClientOffset { get; } = [];
        public List<int> PredictedClientToServerOffset { get; } = [];
        public List<uint> PingOnServer { get; } = [];
        public List<uint> PingOnClient { get; } = [];

        private void SimulateTimeStep(uint timeStep)
        {
            ProcessAndRemove(_serverSync, _serverClock, _clientToServerMessages);
            ProcessAndRemove(_clientSync, _clientClock, _serverToClientMessages);

            _serverClock += timeStep;
            _clientClock += timeStep;

            while (_serverSync.PollMessage() is { } serverMessage)
                _serverToClientMessages.Add((_clientClock + _getRandomDelay(), serverMessage));

            while (_clientSync.PollMessage() is { } clientMessage)
                _clientToServerMessages.Add((_serverClock + _getRandomDelay(), clientMessage));
        }

        private static void ProcessAndRemove(ClockSynchronizer synchronizer, uint currentTime,
            List<(uint Time, ClockSynchronizerMessage Message)> queue)
        {
            for (var i = 0; i < queue.Count;)
            {
                if (queue[i].Time <= currentTime)
                {
                    synchronizer.ProcessMessage(queue[i].Message);
                    queue.RemoveAt(i);
                }
                else
                    ++i;
            }
        }

        private readonly Func<uint> _getRandomDelay;
        private uint _serverClock = 10000;
        private uint _clientClock = 20000;

        private readonly ClockSynchronizer _serverSync;
        private readonly ClockSynchronizer _clientSync;

        private readonly List<(uint Time, ClockSynchronizerMessage Message)> _serverToClientMessages = [];
        private readonly List<(uint Time, ClockSynchronizerMessage Message)> _clientToServerMessages = [];
    }
}
