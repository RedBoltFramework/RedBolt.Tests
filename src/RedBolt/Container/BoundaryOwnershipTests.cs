// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;

public sealed class BoundaryOwnershipTests
{
    [Theory]
    [InlineData("}")]
    [InlineData("{")]
    [InlineData("{0")]
    [InlineData("{-1}")]
    [InlineData("{2}")]
    public void CString_rejects_malformed_or_out_of_range_placeholders(string format)
    {
        using var value = new CString();

        Assert.Throws<FormatException>(() => value.FormatInPlace(format, 1));
    }

    [Fact]
    public void Keyframes_sort_and_resolve_before_between_and_after_times()
    {
        using var frames = new TestKeyFrameSet();
        frames.AddKeyFrame(new Frame(3.0f, 30));
        frames.AddKeyFrame(new Frame(1.0f, 10));
        frames.AddKeyFrame(new Frame(2.0f, 20));

        Assert.Equal([1.0f, 2.0f, 3.0f], frames.KeyFrames.Select(frame => frame.Time));

        uint index = 0;
        Assert.True(frames.GetKeyFrameIndex(-1.0f, ref index));
        Assert.Equal(0u, index);
        Assert.True(frames.GetKeyFrameIndex(2.5f, ref index));
        Assert.Equal(1u, index);
        Assert.True(frames.GetKeyFrameIndex(99.0f, ref index));
        Assert.Equal(2u, index);

        frames.RemoveAllKeyFrames();
        Assert.True(frames.IsEmpty);
        Assert.False(frames.GetKeyFrameIndex(1.0f, ref index));
    }

    private sealed class TestKeyFrameSet : KeyFrameSet<Frame>
    {
        public TestKeyFrameSet()
            : base(static frame => frame.Time)
        {
        }
    }

    private readonly record struct Frame(float Time, int Value);
}
