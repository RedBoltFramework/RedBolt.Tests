namespace RedBolt.Tests;

using System.Reflection;

using RedBolt;
using RedBolt.Core;

public sealed class TimeTests
{
    [Fact]
    public void Static_helpers_return_valid_values()
    {
        var systemTime = Time.GetSystemTime();
        Time.Sleep(1);
        Assert.True(Time.GetSystemTime() - systemTime < 1000u);
        Assert.True(Time.GetTimeSinceEpoch() > 1_700_000_000u);
        Assert.False(string.IsNullOrWhiteSpace(Time.GetTimeStamp()));
    }

    [Fact]
    public void Frame_progress_matches_begin_and_end_lifecycle()
    {
        var context = new Context();
        Context.SetInstance(context);
        var time = new Time();

        Assert.False(time.IsFrameInProgress);
        time.BeginFrame(1.0f / 60.0f);
        Assert.True(time.IsFrameInProgress);
        time.EndFrame();
        Assert.False(time.IsFrameInProgress);
        time.Dispose();
    }

    [Fact]
    public void Sleep_splits_uint_durations_without_waiting_for_the_real_delay()
    {
        var sleep = typeof(Time).GetMethod(
            "Sleep", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, [typeof(uint), typeof(Action<int>)], modifiers: null)!;
        var calls = new List<int>();

        sleep.Invoke(null, [uint.MaxValue, (Action<int>)(milliseconds => calls.Add(milliseconds))]);

        Assert.Equal([int.MaxValue, int.MaxValue, 1], calls);
    }
}
