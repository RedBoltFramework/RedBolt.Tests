// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Reflection;

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

    [Fact]
    public void Friend_bridges_do_not_change_cpp_member_access()
    {
        AssertProtectedWithInternalBridge(typeof(ParticleGraphPin), "SetName",
            "SetNameFromParticleGraphNodeFriend");
        AssertProtectedWithInternalBridge(typeof(ParticleGraphPin), "SetValueType",
            "SetValueTypeFromParticleGraphNodeFriend");
        AssertProtectedWithInternalBridge(typeof(ParticleGraphNode), "SetGraph",
            "SetGraphFromParticleGraphFriend");
        AssertProtectedWithInternalBridge(typeof(ParticleGraphNode), "EvaluateOutputPinTypeCore",
            "EvaluateOutputPinTypeFromParticleGraphAttributeBuilderFriend");
        AssertProtectedWithInternalBridge(typeof(ParticleGraphLayerInstance), "OnSceneSet",
            "OnSceneSetFromParticleGraphEmitterFriend");
        AssertProtectedWithInternalBridge(typeof(ParticleGraphLayerInstance), "SetEmitter",
            "SetEmitterFromParticleGraphEmitterFriend");

        var pinFields = new[]
        {
            "_containerType", "_valueType", "_name", "_nameHash", "_sourceNode", "_sourcePin", "_flags",
            "_requestedValueType", "_attributeIndex", "_memory"
        };
        foreach (var fieldName in pinFields)
        {
            var field = typeof(ParticleGraphPin).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.True(field.IsPrivate, fieldName);
        }

        var builderAccess = typeof(ParticleGraphPin).GetNestedType(
            "ParticleGraphAttributeBuilderFriendAccess", BindingFlags.NonPublic);
        Assert.NotNull(builderAccess);
        foreach (var propertyName in new[] { "ContainerType", "Name", "SourceNode", "SourcePin", "RequestedValueType" })
            Assert.False(builderAccess.GetProperty(propertyName)!.CanWrite, propertyName);
        foreach (var propertyName in new[] { "ValueType", "AttributeIndex", "Memory" })
            Assert.True(builderAccess.GetProperty(propertyName)!.CanWrite, propertyName);
    }

    private static void AssertProtectedWithInternalBridge(Type type, string memberName, string bridgeName)
    {
        var member = type.GetMethod(memberName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(member);
        Assert.True(member.IsFamily, memberName);

        var bridge = type.GetMethod(bridgeName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(bridge);
        Assert.True(bridge.IsAssembly, bridgeName);
    }
}
