namespace RedBolt.Tests;

using System.Collections;
using System.Globalization;

using RedBolt.Container;
using RedBolt.Math;

public sealed class ContainerResidualCoverageTests
{
    [Fact]
    public void Array_dimensions_and_array_overloads_cover_default_vector_and_interface_paths()
    {
        var defaultDimensions = new ArrayDimensions2D();
        Assert.Equal(Vector2i.Zero, defaultDimensions.GetSize());

        var dimensions = new ArrayDimensions2D(new Vector2i(2, 3));
        var dimensionsClone = Assert.IsType<ArrayDimensions2D>(((ICloneable)dimensions).Clone());
        Assert.Equal(new Vector2i(2, 3), dimensionsClone.GetSize());

        var defaultArray = new Array2D<int>();
        var byDimensions = new Array2D<int>(dimensions);
        var byDimensionsAndValue = new Array2D<int>(dimensions, 7);
        var bySize = new Array2D<int>(2, 3);
        var bySizeAndValue = new Array2D<int>(2, 3, 9);

        Assert.Empty(defaultArray.GetContainer());
        Assert.Equal(6u, byDimensions.GetCapacity());
        Assert.All(byDimensionsAndValue, value => Assert.Equal(7, value));
        Assert.All(bySize, value => Assert.Equal(0, value));
        Assert.All(bySizeAndValue, value => Assert.Equal(9, value));

        byDimensions.Reset(dimensions);
        byDimensions.Reset(dimensions, 11);
        Assert.All(byDimensions, value => Assert.Equal(11, value));

        var nongeneric = ((IEnumerable)byDimensions).GetEnumerator();
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(11, nongeneric.Current);

        defaultArray.Dispose();
        byDimensions.Dispose();
        byDimensionsAndValue.Dispose();
        bySize.Dispose();
        bySizeAndValue.Dispose();
    }

    [Fact]
    public void Keyframe_clone_interface_and_string_hash_order_operators_are_exercised()
    {
        using var keyFrames = new TestKeyFrameSet();
        keyFrames.AddKeyFrame(new TestKeyFrame { Time = 1.0f });
        using var clone = Assert.IsType<KeyFrameSet<TestKeyFrame>>(((ICloneable)keyFrames).Clone());
        Assert.Single(clone.KeyFrames);

        var lower = new StringHash(1u);
        var higher = new StringHash(2u);
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.False(higher < lower);
        Assert.False(lower > higher);
    }

    [Fact]
    public void CString_reports_failed_span_formatting()
    {
        using var value = new CString();
        Assert.Throws<FormatException>(() => value.FormatInPlace("value={0}", new AlwaysFailingSpanFormattable()));
    }

    [Fact]
    public void CString_ignores_null_interpolated_values()
    {
        using var value = new CString();
        CString? nullValue = null;
        value.FormatInPlace($"prefix-{nullValue}-suffix");
        Assert.Equal("prefix--suffix", value.ConvertToString());
    }

    [Fact]
    public void CString_formats_regular_values()
    {
        using var value = new CString();
        value.FormatInPlace("valid {0}", 42);
        Assert.Equal("valid 42", value.ConvertToString());
    }

    private sealed class TestKeyFrameSet : KeyFrameSet<TestKeyFrame>
    {
        public TestKeyFrameSet()
            : base(static frame => frame.Time)
        {
        }
    }

    private struct TestKeyFrame
    {
        public float Time;
    }

    private sealed class AlwaysFailingSpanFormattable : ISpanFormattable
    {
        private int _calls;

        public string ToString(string? format, IFormatProvider? formatProvider) => "unreachable";

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
            IFormatProvider? formatProvider)
        {
            if (++_calls <= 2)
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = 2;
            "ok".AsSpan().CopyTo(destination);
            return true;
        }
    }
}
