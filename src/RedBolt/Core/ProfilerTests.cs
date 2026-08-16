// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.CompilerServices;
using System.Threading;

using RedBolt.Core;

public class ProfilerTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] Allocate(int size) => new byte[size];

    [Fact]
    public void Profiler_constants_match_rbfx()
    {
        Assert.Equal(0xb26d19u, Profiler.ColorEvents);
        Assert.Equal(0x006b82u, Profiler.ColorResources);
    }

    [Fact]
    public void Profiler_sets_current_thread_name_when_unset()
    {
        string? threadName = null;

        var thread = new Thread(() =>
        {
            Profiler.SetProfilerThreadName("WorkerThread");
            threadName = Thread.CurrentThread.Name;
        });

        thread.Start();
        thread.Join();

        Assert.Equal("WorkerThread", threadName);
    }

    [Fact]
    public void Profiler_does_not_overwrite_existing_thread_name()
    {
        string? threadName = null;

        var thread = new Thread(() =>
        {
            Profiler.SetProfilerThreadName("InitialName");
            Profiler.SetProfilerThreadName("UpdatedName");
            threadName = Thread.CurrentThread.Name;
        });

        thread.Start();
        thread.Join();

        Assert.Equal("InitialName", threadName);
    }

    [Fact]
    public void Profiler_collects_hierarchical_time_and_allocations()
    {
        using var profiler = new Profiler { Enabled = true };

        profiler.BeginFrame();
        using (Profiler.Profile("Update"))
        {
            using (Profiler.Profile("LogicUpdate"))
                GC.KeepAlive(Allocate(4096));
        }
        using (Profiler.Profile("ApplyFrameLimit"))
            Thread.SpinWait(1000);
        profiler.EndFrame();

        var update = Assert.Single(profiler.RootBlock.Children, child => child.Name == "Update");
        var logicUpdate = Assert.Single(update.Children);
        Assert.Equal("LogicUpdate", logicUpdate.Name);
        Assert.Equal(1u, logicUpdate.FrameCount);
        Assert.True(logicUpdate.FrameTime >= 0);
        Assert.True(logicUpdate.FrameAllocatedBytes >= 4096);
        Assert.True(update.FrameAllocatedBytes >= logicUpdate.FrameAllocatedBytes);
        Assert.Equal(1ul, profiler.LastFrame.Number);
        Assert.InRange(profiler.LastFrame.ActiveTime, 0, profiler.LastFrame.Time);
        Assert.True(profiler.LastFrame.AllocatedBytes >= logicUpdate.FrameAllocatedBytes);
    }

    [Fact]
    public void Profiler_rolls_frame_interval_and_total_values()
    {
        using var profiler = new Profiler { Enabled = true };

        for (var i = 0; i < 2; ++i)
        {
            profiler.BeginFrame();
            using (Profiler.Profile("Block"))
                GC.KeepAlive(Allocate(128));
            profiler.EndFrame();
        }

        var block = Assert.Single(profiler.RootBlock.Children);
        Assert.Equal(1u, block.FrameCount);
        Assert.Equal(2ul, block.IntervalCount);
        Assert.Equal(2ul, block.TotalCount);
        Assert.Equal(2u, profiler.IntervalFrames);
        Assert.Contains("Frame B", profiler.PrintData());
        Assert.Contains("Block", profiler.PrintData(showTotal: true));

        profiler.BeginInterval();
        Assert.Equal(0ul, block.IntervalCount);
        Assert.Equal(0u, profiler.IntervalFrames);
        Assert.Equal(2ul, block.TotalCount);
    }

    [Fact]
    public void Disabled_and_worker_thread_scopes_are_ignored()
    {
        using var profiler = new Profiler();
        profiler.BeginFrame();
        using (Profiler.Profile("Disabled"))
            GC.KeepAlive(Allocate(64));
        profiler.EndFrame();
        Assert.Empty(profiler.RootBlock.Children);

        profiler.Enabled = true;
        var thread = new Thread(() =>
        {
            using (Profiler.Profile("Worker"))
                GC.KeepAlive(Allocate(64));
        });
        thread.Start();
        thread.Join();
        Assert.Empty(profiler.RootBlock.Children);
    }
}
