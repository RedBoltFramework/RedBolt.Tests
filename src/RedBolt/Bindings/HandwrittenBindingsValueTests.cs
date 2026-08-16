// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Jolt;
using RedBolt.PhysicsJolt;

public sealed class HandwrittenBindingsValueTests
{
    [Fact]
    public void Blend_factors_and_jolt_result_have_stable_abi_layout()
    {
        Assert.Equal(16, Marshal.SizeOf<BlendFactors>());
        var one = BlendFactors.One;
        Assert.Equal(1.0f, one.Red);
        Assert.Equal(1.0f, one.Green);
        Assert.Equal(1.0f, one.Blue);
        Assert.Equal(1.0f, one.Alpha);

        Assert.Equal(12, Marshal.SizeOf<JoltRayCastResult>());
        Assert.Equal((IntPtr)0, Marshal.OffsetOf<JoltRayCastResult>(nameof(JoltRayCastResult.BodyId)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<JoltRayCastResult>(nameof(JoltRayCastResult.Fraction)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<JoltRayCastResult>(nameof(JoltRayCastResult.SubShapeId)));

        var mixed = new BlendFactors(0.25f, 0.5f, 0.75f, 1.0f);
        Assert.Equal(0.25f, mixed.Red);
        Assert.Equal(0.5f, mixed.Green);
        Assert.Equal(0.75f, mixed.Blue);
        Assert.Equal(1.0f, mixed.Alpha);
    }

    [Fact]
    public void Jolt_result_is_a_value_copy_with_expected_defaults()
    {
        var result = new JoltRayCastResult();
        Assert.Equal(0u, result.BodyId);
        Assert.Equal(0.0f, result.Fraction);
        Assert.Equal(0u, result.SubShapeId);

        var copy = result;
        copy.BodyId = 42;
        copy.Fraction = 0.5f;
        Assert.Equal(0u, result.BodyId);
        Assert.Equal(0.0f, result.Fraction);
    }

    [Fact]
    public void Physics_library_registration_rejects_a_missing_context()
    {
        Assert.Throws<ArgumentNullException>(() => Physics.RegisterPhysicsLibrary(null!));
    }
}
