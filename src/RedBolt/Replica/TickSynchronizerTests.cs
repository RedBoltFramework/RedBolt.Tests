// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Replica;
using RedBolt.Scene;

public sealed class TickSynchronizerTests
{
    [Fact]
    public void Different_clocks_are_synchronized_on_client()
    {
        var normal = CreateClientSynchronizer();
        Assert.Equal(0u, normal.Synchronize(0.0f));
        Assert.Equal(1u, normal.PendingFollowerTicks);
        Assert.Equal(0.0f, normal.FollowerAccumulatedTime);
        normal.Update(0.125f);
        Assert.Equal(0u, normal.PendingFollowerTicks);
        Assert.Equal(0.125f, normal.FollowerAccumulatedTime);
        normal.Update(0.25f);
        Assert.Equal(1u, normal.PendingFollowerTicks);
        Assert.Equal(0.125f, normal.FollowerAccumulatedTime);
        normal.Update(0.125f);
        Assert.Equal(0u, normal.PendingFollowerTicks);
        Assert.Equal(0.0f, normal.FollowerAccumulatedTime);

        var smallOvertime = CreateClientSynchronizer();
        Assert.Equal(0u, smallOvertime.Synchronize(0.125f));
        Assert.Equal(1u, smallOvertime.PendingFollowerTicks);
        Assert.Equal(0.125f, smallOvertime.FollowerAccumulatedTime);
        smallOvertime.Update(0.125f);
        Assert.Equal(1u, smallOvertime.PendingFollowerTicks);
        Assert.Equal(0.0f, smallOvertime.FollowerAccumulatedTime);
        smallOvertime.Update(0.25f);
        Assert.Equal(0u, smallOvertime.PendingFollowerTicks);
        Assert.Equal(0.0f, smallOvertime.FollowerAccumulatedTime);

        var bigOvertime = CreateClientSynchronizer();
        Assert.Equal(0u, bigOvertime.Synchronize(0.375f));
        Assert.Equal(2u, bigOvertime.PendingFollowerTicks);
        Assert.Equal(0.125f, bigOvertime.FollowerAccumulatedTime);
        bigOvertime.Update(0.125f);
        Assert.Equal(0u, bigOvertime.PendingFollowerTicks);
        Assert.Equal(0.0f, bigOvertime.FollowerAccumulatedTime);

        var debt = CreateClientSynchronizer();
        Assert.Equal(0u, debt.Synchronize(0.0f));
        Assert.Equal(1u, debt.PendingFollowerTicks);
        Assert.Equal(1u, debt.Synchronize(0.0f));
        Assert.Equal(2u, debt.PendingFollowerTicks);
    }

    [Fact]
    public void Different_clocks_are_synchronized_on_server()
    {
        var normal = CreateServerSynchronizer();
        Assert.Equal(0u, normal.Synchronize(0.0f));
        Assert.Equal(2u, normal.PendingFollowerTicks);
        normal.Update(0.125f);
        Assert.Equal(0u, normal.PendingFollowerTicks);
        normal.Update(0.25f);
        Assert.Equal(0u, normal.PendingFollowerTicks);
        normal.Update(0.125f);
        Assert.Equal(0u, normal.PendingFollowerTicks);

        var smallOvertime = CreateServerSynchronizer();
        Assert.Equal(0u, smallOvertime.Synchronize(0.125f));
        Assert.Equal(2u, smallOvertime.PendingFollowerTicks);
        smallOvertime.Update(0.125f);
        Assert.Equal(0u, smallOvertime.PendingFollowerTicks);
        smallOvertime.Update(0.25f);
        Assert.Equal(0u, smallOvertime.PendingFollowerTicks);

        var bigOvertime = CreateServerSynchronizer();
        Assert.Equal(0u, bigOvertime.Synchronize(0.375f));
        Assert.Equal(2u, bigOvertime.PendingFollowerTicks);
        bigOvertime.Update(0.125f);
        Assert.Equal(0u, bigOvertime.PendingFollowerTicks);

        var debt = CreateServerSynchronizer();
        Assert.Equal(0u, debt.Synchronize(0.0f));
        Assert.Equal(2u, debt.PendingFollowerTicks);
        Assert.Equal(0u, debt.Synchronize(0.0f));
        Assert.Equal(2u, debt.PendingFollowerTicks);
    }

    [Fact]
    public void Scene_updates_are_driven_by_synchronized_server_ticks()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var scene = new Scene();
            using var synchronizer = new SceneUpdateSynchronizer(scene,
                new SceneUpdateSynchronizerParams(IsServer: true, NetworkFrequency: 20));
            synchronizer.Synchronize(new NetworkFrame(1), 0.0f);
            var update = new CoreEvents.UpdateEvent { TimeStep = 0.01f };

            scene.SendEvent(ref update);

            Assert.InRange(scene.ElapsedTime, 0.04999f, 0.05001f);
            synchronizer.Update(0.01f);
            scene.SendEvent(ref update);
            Assert.InRange(scene.ElapsedTime, 0.04999f, 0.05001f);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private static TickSynchronizer CreateClientSynchronizer()
    {
        var synchronizer = new TickSynchronizer(2, false);
        synchronizer.SetFollowerFrequency(4);
        return synchronizer;
    }

    private static TickSynchronizer CreateServerSynchronizer()
    {
        var synchronizer = new TickSynchronizer(2, true);
        synchronizer.SetFollowerFrequency(4);
        return synchronizer;
    }
}
