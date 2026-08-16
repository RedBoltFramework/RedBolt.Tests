// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests.MathTests;

using RedBolt;
using RedBolt.Math;

public sealed class MathBranchGapTests
{
    [Fact]
    public void MathDefs_boundaries_cover_smoothing_half_encoding_and_vector_choices()
    {
        Assert.Equal(7.0f, MathDefs.Smooth(1.0f, 7.0f, 0.0f, 0.0f));
        Assert.Equal(MathDefs.Smooth(1.0f, 7.0f, 0.5f), MathDefs.Smooth(1.0f, 7.0f, 2.0f, 1.0f));
        Assert.Equal(0.5f, MathDefs.AbsMod(-1.5f, 2.0f));
        Assert.Equal(1.5f, MathDefs.AbsMod(1.5f, 2.0f));
        Assert.Equal(1, MathDefs.AbsMod(-3, 2));
        Assert.Equal(0.0f, MathDefs.Average(Array.Empty<float>()));
        Assert.Equal(0.0, MathDefs.Average(Array.Empty<double>()));

        Assert.Equal((ushort)0x3c00, MathDefs.FloatToHalf(1.0f));
        Assert.Equal((ushort)0x0000, MathDefs.FloatToHalf(0.0f));
        Assert.Equal((ushort)0x7bff, MathDefs.FloatToHalf(float.MaxValue));
        Assert.Equal(1.0f, MathDefs.HalfToFloat(0x3c00));
        Assert.Equal(0.0f, MathDefs.HalfToFloat(0x0000));

        Span<byte> encoded = stackalloc byte[16];
        Assert.Equal(1u, MathDefs.EncodeVariableLength(0u, encoded));
        Assert.Equal(5u, MathDefs.EncodeVariableLength(uint.MaxValue, encoded));
        Assert.Equal(1u, MathDefs.EncodeVariableLength(0ul, encoded));
        Assert.Equal(10u, MathDefs.EncodeVariableLength(ulong.MaxValue, encoded));

        uint uintValue = 0;
        uint uintOffset = 0;
        Assert.False(MathDefs.DecodeVariableLength(ref uintValue, ref uintOffset, 0x80));
        Assert.True(MathDefs.DecodeVariableLength(ref uintValue, ref uintOffset, 0x01));

        ulong ulongValue = 0;
        uint ulongOffset = 0;
        Assert.False(MathDefs.DecodeVariableLength(ref ulongValue, ref ulongOffset, 0x80));
        Assert.True(MathDefs.DecodeVariableLength(ref ulongValue, ref ulongOffset, 0x01));

        Assert.Equal(new Vector4f(1.0f, 3.0f, 3.0f, 7.0f), MathDefs.VectorMin(
            new Vector4f(1.0f, 4.0f, 3.0f, 8.0f), new Vector4f(2.0f, 3.0f, 4.0f, 7.0f)));
        Assert.Equal(new Vector4f(2.0f, 4.0f, 4.0f, 8.0f), MathDefs.VectorMax(
            new Vector4f(1.0f, 4.0f, 3.0f, 8.0f), new Vector4f(2.0f, 3.0f, 4.0f, 7.0f)));
        Assert.Equal(Vector4f.Zero, MathDefs.VectorMin(Vector4f.Zero, Vector4f.One));
        Assert.Equal(Vector4f.One, MathDefs.VectorMax(Vector4f.Zero, Vector4f.One));
        Assert.Equal(Vector4f.Zero, MathDefs.VectorMin(Vector4f.One, Vector4f.Zero));
        Assert.Equal(Vector4f.One, MathDefs.VectorMax(Vector4f.One, Vector4f.Zero));
    }

    [Fact]
    public void Color_boundaries_cover_all_hue_sectors_clipping_and_object_equality()
    {
        var clipped = new Color(2.0f, -1.0f, 0.5f, 2.0f);
        clipped.Bounds(out var min, out var max, clipped: true);
        Assert.Equal(0.0f, min);
        Assert.Equal(1.0f, max);
        clipped.Clip(clipAlpha: true);
        Assert.Equal(new Color(1.0f, 0.0f, 0.5f, 1.0f), clipped);

        var negativeRedSector = new Color(1.0f, 0.2f, 0.8f);
        var positiveRedSector = new Color(1.0f, 0.8f, 0.2f);
        Assert.InRange(negativeRedSector.Hue(), 0.0f, 1.0f);
        Assert.InRange(positiveRedSector.Hue(), 0.0f, 1.0f);
        Assert.InRange(new Color(0.2f, 1.0f, 0.5f).Hue(), 0.0f, 1.0f);
        Assert.InRange(new Color(0.2f, 0.5f, 1.0f).Hue(), 0.0f, 1.0f);
        Assert.Equal(0.0f, Color.Black.SaturationHSV());
        Assert.True(Color.Red.SaturationHSV() > 0.0f);

        var inverted = new Color(0.2f, 0.4f, 0.6f, 0.25f);
        inverted.Invert(invertAlpha: true);
        Assert.True(inverted.Equals(new Color(0.8f, 0.6f, 0.4f, 0.75f), 0.000001f));
        Assert.True(inverted.Equals((object)inverted));
        Assert.False(inverted.Equals((object?)null));
        Assert.False(inverted.Equals(new object()));
    }

    [Fact]
    public void Plane_and_sphere_degeneracies_cover_parallel_and_undefined_paths()
    {
        var plane = new Plane(Vector3f.Up, Vector3f.Zero);
        var parallel = plane.Intersect(plane);
        Assert.Equal(Vector3f.Zero, parallel.Direction);

        var singular = plane.Intersect(
            new Plane(Vector3f.Up, new Vector3f(0.0f, 1.0f, 0.0f)),
            new Plane(Vector3f.Up, new Vector3f(0.0f, 2.0f, 0.0f)));
        Assert.Equal(Vector3f.Zero, singular);

        var empty = new Sphere();
        empty.Merge(new Sphere(Vector3f.Right, 2.0f));
        Assert.Equal(Vector3f.Right, empty.Center);
        Assert.Equal(2.0f, empty.Radius);
        Assert.True(empty.GetPoint(MathDefs.HalfPi, 0.0f).ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 0.0f)));
        Assert.True(empty.Equals((object)empty));
        Assert.False(empty.Equals((object?)null));
    }

    [Fact]
    public void Integer_and_real_vector_generic_edges_cover_object_and_lexicographic_paths()
    {
        var int2 = new Vector2i(2, -3);
        Assert.False(int2.Equals((object?)null));
        Assert.False(int2.Equals(new object()));
        Assert.Equal(new Vector2i(1, -4), Vector2i.Min(int2, new Vector2i(1, -4)));
        Assert.Equal(new Vector2i(2, -3), Vector2i.Max(int2, new Vector2i(1, -4)));

        var int3 = new Vector3i(1, 2, 3);
        Assert.True(int3 < new Vector3i(1, 3, 0));
        Assert.True(int3 <= new Vector3i(1, 2, 3));
        Assert.True(int3 >= new Vector3i(1, 2, 3));
        Assert.False(int3.Equals((object?)null));
        Assert.Equal(new Vector3i(1, 2, 2), Vector3i.Min(int3, new Vector3i(2, 3, 2)));
        Assert.Equal(new Vector3i(2, 3, 3), Vector3i.Max(int3, new Vector3i(2, 3, 2)));

        Assert.False(Vector2f.One.Equals((object?)null));
        Assert.False(Vector3f.One.Equals((object?)null));
        Assert.False(Vector4f.One.Equals((object?)null));
    }

    [Fact]
    public void Wrapped_scalars_cover_all_boundary_modes_and_equality_operands()
    {
        var range = new WrappedScalarRange<float>(2.0f, 8.0f, 0.0f, 10.0f, 0);
        Assert.True(range.ContainsInclusive(2.0f));
        Assert.False(range.ContainsExclusive(2.0f));
        Assert.False(range.ContainsExcludingBegin(2.0f));
        Assert.False(range.ContainsExcludingEnd(8.0f));
        Assert.True(range.Equals((object)range));
        Assert.False(range.Equals((object?)null));

        var scalar = new WrappedScalar<float>(5.0f, 0.0f, 10.0f);
        var outOfBounds = scalar.UpdateClamped(10.0f, returnOutOfBounds: true);
        Assert.Equal(15.0f, outOfBounds.End());
        Assert.False(scalar.Equals((object?)null));
        Assert.False(scalar.Equals(new object()));
    }
}
