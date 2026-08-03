namespace RedBolt.Tests;

using System.Runtime.CompilerServices;

using RedBolt.Container;
using RedBolt.Math;

public sealed class ContainerCoverageGapTests
{
    [Fact]
    public void Array2d_optional_access_and_wrapped_clone_paths_are_stable()
    {
        using var array = new Array2D<int>(2, 2, 7);
        array[new Vector2i(1, 1)] = 9;

        ref var present = ref array.GetOptional(new Vector2i(1, 1));
        Assert.Equal(9, present);

        ref var missing = ref array.GetOptional(new Vector2i(-1, 0));
        Assert.True(Unsafe.IsNullRef(ref missing));
        Assert.Equal(9, array.GetWrapped(new Vector2i(-1, -1)));
        Assert.Equal(9, array.GetClamped(new Vector2i(10, 10)));

        var clone = Assert.IsType<Array2D<int>>(array.Clone());
        using (clone)
            Assert.Equal(array.GetContainer(), clone.GetContainer());
    }

    [Fact]
    public void String_hash_object_equality_and_keyframe_boundaries_are_covered()
    {
        var hash = new StringHash("container-gap");
        Assert.True(hash.Equals((object)hash));
        Assert.False(hash.Equals((object)"container-gap"));

        var keyFrames = new FloatKeyFrameSet();
        keyFrames.AddKeyFrame(10.0f);
        keyFrames.AddKeyFrame(0.0f);

        uint index = 99;
        Assert.True(keyFrames.GetKeyFrameIndex(5.0f, ref index));
        Assert.Equal(0u, index);

        keyFrames.GetKeyFrames(5.0f, 10.0f, isLooped: false, ref index,
            out var nextIndex, out var blendFactor);
        Assert.Equal(1u, nextIndex);
        Assert.Equal(0.5f, blendFactor);

        index = 1;
        keyFrames.GetKeyFrames(10.0f, 10.0f, isLooped: true, ref index,
            out nextIndex, out blendFactor);
        Assert.Equal(0u, nextIndex);
        Assert.Equal(1.0f, blendFactor);

        var clone = keyFrames.Clone();
        Assert.Equal(keyFrames.KeyFrames, clone.KeyFrames);
        keyFrames.Dispose();
        clone.Dispose();
    }

    [Fact]
    public void CString_formatting_and_equality_cover_edge_cases()
    {
        using var argument = new CString("native");
        using var formatted = CString.Format(
            "{0}|{1}|{2}|{3,4}|{3,-4}|{4:X2}|{5}",
            new object?[] { "text", argument, '!', 7, 255, null });
        Assert.Equal("text|native|!|7|7|FF|", formatted.ConvertToString());

        using var text = new CString("é");
        Assert.False(text.Equals((CString?)null));
        Assert.True(text.Equals((string?)"é"));
        Assert.False(text.Equals((string?)null));
        Assert.True(text.Equals((object)text));
        Assert.True(text.Equals((object)"é"));
        Assert.False(text.Equals((object)7));
        Assert.True(text.Equals(new byte[] { 0xc3, 0xa9 }));
        Assert.True((CString?)null == null);
        Assert.True(text != null);

        using var malformed = new CString("\ud800");
        Assert.True(malformed.Equals("\ud800"));

        using var longText = new CString(new string('x', 32));
        Assert.NotEqual(IntPtr.Zero, longText.Pointer);
        using var copy = Assert.IsType<CString>(longText.Clone());
        Assert.Equal(longText, copy);

        text.FormatInPlace("{{{0}}} {1,4}", new object?[] { "ok", 3 });
        Assert.Equal("{ok} 3", text.ConvertToString());
        text.FormatInPlace(string.Empty, Array.Empty<object?>());
        Assert.Empty(text.ConvertToString());

        Assert.Throws<FormatException>(() => text.FormatInPlace("{x}", Array.Empty<object?>()));
        Assert.Throws<FormatException>(() => text.FormatInPlace("{0", new object?[] { "x" }));
        Assert.Throws<FormatException>(() => CString.Format("{0}", new object?[] { new object() }));
    }

    private sealed class FloatKeyFrameSet : KeyFrameSet<float>
    {
        public FloatKeyFrameSet()
            : base(static value => value)
        {
        }
    }
}
