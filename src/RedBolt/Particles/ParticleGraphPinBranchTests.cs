namespace RedBolt.Tests;

using System;

using RedBolt.Core;
using RedBolt.Particles;

public sealed class ParticleGraphPinBranchTests
{
    private sealed class MutablePin(ParticleGraphPinFlag flags, string name, VariantType type)
        : ParticleGraphPin(flags, name, type)
    {
        public bool ChangeName(string value) => SetName(value);

        public bool ChangeValueType(VariantType value) => SetValueType(value);

        public void ChangeIsInput(bool value) => SetIsInput(value);
    }

    [Fact]
    public void ICloneable_and_attribute_index_use_the_public_pin_state()
    {
        var pin = new ParticleGraphPin(ParticleGraphPinFlag.Input, "value");

        var clone = ((ICloneable)pin).Clone();

        Assert.IsType<ParticleGraphPin>(clone);
        Assert.Equal(0, pin.AttributeIndex);
        Assert.NotSame(pin, clone);
    }

    [Fact]
    public void Mutators_accept_unchanged_values_without_requiring_mutable_flags()
    {
        var pin = new MutablePin(ParticleGraphPinFlag.Input, "value", VariantType.Float);

        Assert.True(pin.ChangeName("value"));
        Assert.True(pin.ChangeValueType(VariantType.Float));
    }

    [Fact]
    public void SetIsInput_can_set_both_pin_directions()
    {
        var pin = new MutablePin(ParticleGraphPinFlag.Output, "value", VariantType.Float);

        pin.ChangeIsInput(true);
        Assert.True(pin.IsInput);

        pin.ChangeIsInput(false);
        Assert.False(pin.IsInput);
    }
}
