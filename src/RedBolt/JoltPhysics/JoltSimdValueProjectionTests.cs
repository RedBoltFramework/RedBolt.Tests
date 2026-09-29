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

        // Plane/DMat44 are heap-allocated natively; Jolt needs its allocator installed first.
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
    public void DVec3_round_trips_with_double_precision_through_native_matrix()
    {
        // Values that are not representable in float, to prove no narrowing happens.
        var translation = new BaseRealVector3<double>(1.0e10 + 0.25, -2.0e9 - 0.5, 0.125);

        using var matrix = DMat44.STranslation(in translation);
        var roundTripped = matrix.GetTranslation();

        Assert.Equal(translation.X, roundTripped.X);
        Assert.Equal(translation.Y, roundTripped.Y);
        Assert.Equal(translation.Z, roundTripped.Z);
    }

    [Fact]
    public void DVec3_by_reference_parameter_updates_native_matrix()
    {
        using var matrix = new DMat44();
        var translation = new BaseRealVector3<double>(3.0e12 + 0.5, 4.0, -5.0);

        matrix.SetTranslation(in translation);

        var roundTripped = matrix.GetTranslation();
        Assert.Equal(translation.X, roundTripped.X);
        Assert.Equal(translation.Y, roundTripped.Y);
        Assert.Equal(translation.Z, roundTripped.Z);
    }
}
