// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Replica;

public sealed class NetworkSettingsTests
{
    [Fact]
    public void Settings_use_defaults_and_map_overrides()
    {
        using var settings = new VariantMap();

        Assert.Equal(30u, NetworkSettings.GetNetworkSetting(settings, NetworkSettings.UpdateFrequency).GetUInt());
        Assert.Equal(0.25f, NetworkSettings.GetNetworkSetting(settings, NetworkSettings.InterpolationLimit).GetFloat());

        NetworkSettings.SetNetworkSetting(settings, NetworkSettings.UpdateFrequency, new Variant(60u));
        Assert.Equal(60u, NetworkSettings.GetNetworkSetting(settings, NetworkSettings.UpdateFrequency).GetUInt());

        NetworkSettings.SetDefaultNetworkSetting(settings, NetworkSettings.UpdateFrequency);
        Assert.Equal(30u, NetworkSettings.GetNetworkSetting(settings, NetworkSettings.UpdateFrequency).GetUInt());
    }

    [Fact]
    public void Protocol_messages_roundtrip()
    {
        var configure = new MsgConfigure { Magic = 42 };
        NetworkSettings.SetNetworkSetting(configure.Settings, NetworkSettings.UpdateFrequency, new Variant(60u));
        using var configureBuffer = new VectorBuffer();
        configure.Save(configureBuffer);
        using var configureSource = new MemoryBuffer(configureBuffer);
        var restoredConfigure = new MsgConfigure();
        restoredConfigure.Load(configureSource);

        Assert.Equal(42u, restoredConfigure.Magic);
        Assert.Equal(60u, NetworkSettings.GetNetworkSetting(
            restoredConfigure.Settings, NetworkSettings.UpdateFrequency).GetUInt());
        Assert.Equal("{magic=42, settings...}", restoredConfigure.ToString());

        var synchronized = new MsgSynchronized { Magic = 7 };
        using var synchronizedBuffer = new VectorBuffer();
        synchronized.Save(synchronizedBuffer);
        using var synchronizedSource = new MemoryBuffer(synchronizedBuffer);
        var restoredSynchronized = new MsgSynchronized();
        restoredSynchronized.Load(synchronizedSource);
        Assert.Equal(7u, restoredSynchronized.Magic);
        Assert.Equal("{magic=7}", restoredSynchronized.ToString());

        var clock = new MsgSceneClock
        {
            LatestFrame = (NetworkFrame)1234567890123L,
            LatestFrameTime = 1234,
            InputDelay = 17
        };
        using var clockBuffer = new VectorBuffer();
        clock.Save(clockBuffer);
        using var clockSource = new MemoryBuffer(clockBuffer);
        var restoredClock = new MsgSceneClock();
        restoredClock.Load(clockSource);
        Assert.Equal(clock.LatestFrame, restoredClock.LatestFrame);
        Assert.Equal(clock.LatestFrameTime, restoredClock.LatestFrameTime);
        Assert.Equal(clock.InputDelay, restoredClock.InputDelay);
        Assert.Equal("{latestFrame=1234567890123 at 1234, inputDelay=17}", restoredClock.ToString());

        configure.Settings.Dispose();
        restoredConfigure.Settings.Dispose();
    }
}
