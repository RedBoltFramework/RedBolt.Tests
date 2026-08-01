namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Jolt;

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
}
