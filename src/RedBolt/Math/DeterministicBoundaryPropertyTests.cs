// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Math;

public sealed class DeterministicBoundaryPropertyTests
{
    [Fact]
    public void Variable_length_encoding_round_trips_integer_boundaries()
    {
        Span<byte> encoded = stackalloc byte[5];
        foreach (var value in new[] { 0u, 1u, 127u, 128u, 255u, 16384u, uint.MaxValue })
        {
            var length = MathDefs.EncodeVariableLength(value, encoded);
            var decoded = 0u;
            var offset = 0u;
            for (var index = 0u; index < length; ++index)
                Assert.Equal(index + 1 == length, MathDefs.DecodeVariableLength(ref decoded, ref offset, encoded[(int)index]));
            Assert.Equal(value, decoded);
        }

        Assert.Equal(0u, MathDefs.NextPowerOfTwo(0));
        Assert.Equal(0x80000000u, MathDefs.NextPowerOfTwo(0x80000000u));
        Assert.Equal(0u, MathDefs.NextPowerOfTwo(uint.MaxValue));
    }

    [Fact]
    public void Scalar_and_easing_endpoints_are_deterministic()
    {
        Assert.Equal(0.0f, MathDefs.Clamp(-1.0f, 0.0f, 1.0f));
        Assert.Equal(1.0f, MathDefs.Clamp(2.0f, 0.0f, 1.0f));
        Assert.Equal(0.0f, MathDefs.Clamp(-1.0f, 0.0f, -1.0f));
        Assert.Equal(0.0f, MathDefs.Fract(-2.0f));
        Assert.Equal(9, MathDefs.AbsMod(-1, 10));

        Func<float, float>[] endpointFunctions =
        [
            EaseMath.BackIn,
            EaseMath.BackOut,
            EaseMath.BackInOut,
            EaseMath.BounceIn,
            EaseMath.BounceOut,
            EaseMath.BounceInOut,
            EaseMath.SineIn,
            EaseMath.SineOut,
            EaseMath.SineInOut,
            EaseMath.ExponentialOut
        ];
        Assert.All(endpointFunctions, function =>
        {
            Assert.True(MathDefs.Equals(function(0.0f), 0.0f), function.Method.Name);
            Assert.True(MathDefs.Equals(function(1.0f), 1.0f), function.Method.Name);
        });
        Assert.Equal(0.0f, EaseMath.ExponentialIn(0.0f));
        Assert.InRange(EaseMath.ExponentialIn(1.0f), 0.999f, 1.0f);
        Assert.InRange(EaseMath.ExponentialInOut(0.0f), 0.0f, 0.001f);
        Assert.InRange(EaseMath.ExponentialInOut(1.0f), 0.999f, 1.0f);
        Assert.Equal(0.0f, EaseMath.ElasticIn(0.0f, 0.3f));
        Assert.Equal(1.0f, EaseMath.ElasticOut(1.0f, 0.3f));
        Assert.Equal(0.0f, EaseMath.ElasticInOut(0.0f, 0.0f));
        Assert.Equal(1.0f, EaseMath.ElasticInOut(1.0f, 0.0f));
    }

    [Fact]
    public void Range_and_wrapped_range_boundaries_preserve_invariants()
    {
        var invalid = new NumericRange<int>();
        Assert.False(invalid.IsValid());
        Assert.False(invalid.ContainsInclusive(0));

        var range = new NumericRange<int>(-2, 3);
        Assert.True(range.ContainsInclusive(-2));
        Assert.True(range.ContainsInclusive(3));
        Assert.False(range.ContainsExclusive(-2));
        Assert.True((range & new NumericRange<int>(3, 9)).Equals(new NumericRange<int>(3, 3)));
        Assert.Equal(new NumericRange<int>(-2, 9), range | new NumericRange<int>(3, 9));

        var wrapped = new WrappedScalar<int>(8, 0, 10);
        var update = wrapped.UpdateWrapped(5);
        Assert.Equal(3, wrapped.Value());
        Assert.True(update.ContainsInclusive(8));
        Assert.True(update.ContainsInclusive(3));

        var clamped = new WrappedScalar<int>(8, 0, 10);
        var clampedRange = clamped.UpdateClamped(5, returnOutOfBounds: true);
        Assert.Equal(10, clamped.Value());
        Assert.Equal(13, clampedRange.End());
        Assert.True(clampedRange.ContainsInclusive(10));
    }

    [Fact]
    public void Matrix_transform_inverse_property_holds_for_deterministic_cases()
    {
        var cases = new[]
        {
            new Transform(new Vector3f(1, -2, 3), new Quaternion(15, Vector3f.Up), new Vector3f(1, 2, 3)),
            new Transform(new Vector3f(-4, 5, 0.5f), new Quaternion(-70, Vector3f.Right), new Vector3f(2, 2, 2)),
            new Transform(new Vector3f(0, 0, 0), Quaternion.IDENTITY, Vector3f.One)
        };

        foreach (var transform in cases)
        {
            var matrix = transform.ToMatrix3x4();
            var inverse = matrix.Inverse;
            foreach (var point in new[] { Vector3f.Zero, Vector3f.One, new Vector3f(-2, 3, 4) })
            {
                var transformed = matrix * point;
                Assert.True((inverse * transformed).ApproximatelyEquivalent(point, 0.001f));
            }
        }
    }

    [Fact]
    public void Seeded_scalar_properties_hold_across_replayed_boundary_inputs()
    {
        var random = DeterministicTestSeed.Create(nameof(Seeded_scalar_properties_hold_across_replayed_boundary_inputs));
        for (var index = 0; index < 256; ++index)
        {
            var first = random.Next(-1000, 1001);
            var second = random.Next(-1000, 1001);
            if (first == second)
                ++second;

            var min = global::System.Math.Min(first, second);
            var max = global::System.Math.Max(first, second);
            var value = (float)(random.NextDouble() * (max - min + 2) + min - 1);
            var clamped = MathDefs.Clamp(value, min, max);
            var interpolation = (float)random.NextDouble();
            var lerped = MathDefs.Lerp(min, max, interpolation);

            Assert.InRange(clamped, min, max);
            Assert.InRange(lerped, min, max);
            Assert.InRange(MathDefs.InverseLerp(min, max, clamped), 0.0f, 1.0f);
        }
    }

    [Fact]
    public void Public_math_values_have_deterministic_native_compatible_layouts()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector2f>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector2d>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector3f>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector3d>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector2i>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector3i>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Vector4f>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Quaternion>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Color>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Transform>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<DoubleTransform>());

        Assert.Equal(8, Unsafe.SizeOf<Vector2f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector2d>());
        Assert.Equal(12, Unsafe.SizeOf<Vector3f>());
        Assert.Equal(24, Unsafe.SizeOf<Vector3d>());
        Assert.Equal(8, Unsafe.SizeOf<Vector2i>());
        Assert.Equal(12, Unsafe.SizeOf<Vector3i>());
        Assert.Equal(16, Unsafe.SizeOf<Vector4f>());
        Assert.Equal(16, Unsafe.SizeOf<Quaternion>());
        Assert.Equal(16, Marshal.SizeOf<Color>());
        Assert.Equal(40, Marshal.SizeOf<Transform>());
        Assert.Equal(56, Marshal.SizeOf<DoubleTransform>());
    }
}
