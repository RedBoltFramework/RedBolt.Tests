// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Bindings.Jolt;

using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltSimdValueProjectionTests
{
    public JoltSimdValueProjectionTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);

        // Plane/AABox are heap-allocated natively; Jolt needs its allocator installed first.
        Jolt.RegisterDefaultAllocator();
    }

    [Fact]
    public void Vec4_round_trips_by_value_through_native_plane()
    {
        // Plane(Vec4) stores xyz as the normal and w as the constant.
        using var plane = new Plane(new Vector4f(0.0f, 1.0f, 0.0f, 2.5f));

        var normal = (BaseRealVector3<float>)plane.GetNormal();
        Assert.Equal(0.0f, normal.X);
        Assert.Equal(1.0f, normal.Y);
        Assert.Equal(0.0f, normal.Z);
        Assert.Equal(2.5f, plane.GetConstant());
    }

    [Fact]
    public void Vec4_return_value_is_read_component_wise()
    {
        using var color = new Color(255, 128, 0, 255);

        Vector4f value = color.ToVec4();

        Assert.Equal(1.0f, value.X, 1e-4f);
        Assert.Equal(128.0f / 255.0f, value.Y, 1e-4f);
        Assert.Equal(0.0f, value.Z, 1e-4f);
        Assert.Equal(1.0f, value.W, 1e-4f);
    }

    [Fact]
    public void UVec4_by_value_return_is_read_component_wise()
    {
        // A positive mass is required when any translation axis is allowed (JPH_ASSERT aborts in Debug).
        using var mass = new MassProperties { MMass = 1.0f };
        using var properties = new MotionProperties();
        properties.SetMassProperties(EAllowedDOFs.TranslationX | EAllowedDOFs.TranslationZ | EAllowedDOFs.RotationY, mass);

        // Allowed axes are all-ones, locked axes are zero; w compares 0 == 0 and is always all-ones.
        var linear = properties.GetLinearDOFsMask();
        Assert.Equal(uint.MaxValue, linear.X);
        Assert.Equal(0u, linear.Y);
        Assert.Equal(uint.MaxValue, linear.Z);
        Assert.Equal(uint.MaxValue, linear.W);

        var angular = properties.GetAngularDOFsMask();
        Assert.Equal(0u, angular.X);
        Assert.Equal(uint.MaxValue, angular.Y);
        Assert.Equal(0u, angular.Z);
        Assert.Equal(uint.MaxValue, angular.W);
    }

    [Fact]
    public void UVec4_by_value_and_by_reference_round_trip()
    {
        // Half floats 1, 2, 3, 4 (two packed per 32-bit lane, low half first) converted to a float
        // vector (by-value UVec4 in, Vec4 out).
        var halves = new JoltUInt4(0x40003C00u, 0x44004200u, 0u, 0u);
        Vector4f floats = Jolt.ToFloat(halves);
        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), floats);

        // Trues (all-ones lanes) are moved to the front of the identifiers (UVec4& in/out).
        var mask = new JoltUInt4(uint.MaxValue, 0u, uint.MaxValue, 0u);
        var identifiers = new JoltUInt4(10u, 11u, 12u, 13u);
        int trues = Jolt.CountAndSortTrues(mask, ref identifiers);
        Assert.Equal(2, trues);
        Assert.Equal(10u, identifiers.X);
        Assert.Equal(12u, identifiers.Y);
    }

    [Fact]
    public void DMat44_by_reference_reads_columns_and_translation()
    {
        // Distinct off-diagonal columns prove column order; translation values are exact in float.
        var matrix = new JoltDMatrix4(
            new Vector4f(0.0f, 2.0f, 0.0f, 0.0f),
            new Vector4f(3.0f, 0.0f, 0.0f, 0.0f),
            new Vector4f(0.0f, 0.0f, 4.0f, 0.0f),
            new BaseRealVector3<double>(100.5, -200.25, 0.125));
        var min = new BaseRealVector3<double>(-1.0, -1.0, -1.0);
        var max = new BaseRealVector3<double>(1.0, 1.0, 1.0);

        using var box = new AABox(in min, in max);
        using var transformed = box.Transformed(in matrix);

        var center = (BaseRealVector3<float>)transformed.GetCenter();
        Assert.Equal(100.5f, center.X);
        Assert.Equal(-200.25f, center.Y);
        Assert.Equal(0.125f, center.Z);

        var extent = (BaseRealVector3<float>)transformed.GetExtent();
        Assert.Equal(3.0f, extent.X);
        Assert.Equal(2.0f, extent.Y);
        Assert.Equal(4.0f, extent.Z);
    }

    [Fact]
    public void DMat44_translation_is_not_corrupted_by_double_transport()
    {
        // No bound API returns a DMat44 or exposes double results, so the double translation is
        // checked through the (float) transformed bounds: the center must be the float rounding
        // of the double value, not garbage from a mis-sized ABI copy.
        var translation = new BaseRealVector3<double>(1.0e10 + 0.25, -2.0e9 - 0.5, 0.125);
        var matrix = new JoltDMatrix4(
            new Vector4f(1.0f, 0.0f, 0.0f, 0.0f),
            new Vector4f(0.0f, 1.0f, 0.0f, 0.0f),
            new Vector4f(0.0f, 0.0f, 1.0f, 0.0f),
            translation);
        var half = new BaseRealVector3<double>(0.5, 0.5, 0.5);
        var negativeHalf = new BaseRealVector3<double>(-0.5, -0.5, -0.5);

        using var box = new AABox(in negativeHalf, in half);
        using var transformed = box.Transformed(in matrix);

        var center = (BaseRealVector3<float>)transformed.GetCenter();
        Assert.Equal((float)translation.X, center.X, 1024.0f);
        Assert.Equal((float)translation.Y, center.Y, 256.0f);
        Assert.Equal(0.125f, center.Z, 1e-3f);
    }

    [Fact]
    public void Const_Vec3_input_array_binds_as_read_only_span_with_minimum_length()
    {
        // A positive mass is required for a valid MotionProperties (a mass of 0 asserts and aborts).
        using var mass = new MassProperties { MMass = 1.0f };
        using var properties = new MotionProperties();
        properties.SetMassProperties(EAllowedDOFs.All, mass);

        // ResetSleepTestSpheres reads exactly three points and has no observable getter.
        BaseRealVector3<float>[] points =
        [
            new(0.0f, 0.0f, 0.0f),
            new(1.0f, 0.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f)
        ];
        properties.ResetSleepTestSpheres(points);

        // The wrapper rejects spans shorter than the three elements native code reads.
        Assert.Throws<ArgumentException>(() => properties.ResetSleepTestSpheres(points.AsSpan(0, 2)));
    }
}
