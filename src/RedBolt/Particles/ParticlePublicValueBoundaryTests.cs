// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Particles;

public sealed class ParticlePublicValueBoundaryTests
{
    [Fact]
    public void Public_particle_value_types_have_stable_layouts_and_defaults()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(EmitFrom)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ParticleGraphContainerType)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ParticleGraphPinFlag)));
        Assert.Equal(8, Unsafe.SizeOf<ParticleGraphSpan>());
        Assert.Equal(8, Unsafe.SizeOf<ParticleGraphPinRef>());
        Assert.Equal(0, Marshal.OffsetOf<ParticleGraphSpan>(nameof(ParticleGraphSpan.Offset)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<ParticleGraphSpan>(nameof(ParticleGraphSpan.Size)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<ParticleGraphPinRef>(nameof(ParticleGraphPinRef.Type)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<ParticleGraphPinRef>(nameof(ParticleGraphPinRef.Index)).ToInt32());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ParticleGraphSpan>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ParticleGraphPinRef>());

        Assert.Equal(new ParticleGraphSpan(0, 0), new ParticleGraphSpan());
        Assert.Equal(new ParticleGraphPinRef(ParticleGraphContainerType.Auto, 0), new ParticleGraphPinRef());
    }

    [Fact]
    public void Span_boundaries_are_empty_or_bounded_without_invalid_access()
    {
        var buffer = new byte[sizeof(int) * 2];
        Assert.Empty(new ParticleGraphSpan(0, 0).MakeSpan<int>(buffer).ToArray());
        Assert.Empty(new ParticleGraphSpan(-1, 0).MakeSpan<int>(buffer).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParticleGraphSpan(buffer.Length + 1, sizeof(int)).MakeSpan<int>(buffer));
        Assert.Empty(new ParticleGraphSpan(0, sizeof(int) - 1).MakeSpan<int>(buffer).ToArray());
    }

    [Fact]
    public void Attribute_and_buffer_layouts_are_deterministic_at_zero_and_changed_capacity()
    {
        var attributes = new ParticleGraphAttributeLayout();
        attributes.Reset(5, 0);
        var empty = attributes.GetOrAddAttribute("empty", VariantType.Int);
        Assert.Equal(0, empty);
        Assert.Equal(0, attributes.GetSpan(empty).Size);

        attributes.Reset(10, 2);
        var first = attributes.GetOrAddAttribute("value", VariantType.Int);
        var conflicting = attributes.GetOrAddAttribute("value", VariantType.Vector3);
        Assert.Equal(0, first);
        Assert.Equal(1, conflicting);
        var firstSpan = attributes.GetSpan(first);
        var conflictingSpan = attributes.GetSpan(conflicting);
        Assert.Equal(10, firstSpan.Offset);
        Assert.Equal(VariantTypeUtility.GetVariantTypeSize(VariantType.Int) * 2, firstSpan.Size);
        Assert.Equal(firstSpan.Offset + firstSpan.Size, conflictingSpan.Offset);
        Assert.Equal(VariantTypeUtility.GetVariantTypeSize(VariantType.Vector3) * 2, conflictingSpan.Size);
        Assert.Equal(nameof(VariantType.Int), attributes.GetType(first).ToString());

        var buffers = new ParticleGraphBufferLayout();
        buffers.Reset(0);
        var zero = buffers.Allocate(ParticleGraphContainerType.Span, VariantType.Vector3);
        Assert.Equal(0, buffers[zero].Size);
        Assert.Equal(0, buffers.RequiredMemory);
        buffers.Reset(3);
        var span = buffers.Allocate(ParticleGraphContainerType.Span, VariantType.Float);
        var scalar = buffers.Allocate(ParticleGraphContainerType.Scalar, VariantType.Int);
        Assert.Equal(sizeof(float) * 3, buffers[span].Size);
        Assert.Equal(sizeof(float) * 3, buffers[scalar].Offset);
        Assert.Equal(sizeof(float) * 3 + sizeof(int), buffers.RequiredMemory);
    }

    [Fact]
    public void Pin_connection_boundaries_keep_output_unconnected_and_replace_input_source()
    {
        var output = new ParticleGraphPin(ParticleGraphPinFlag.Output, "output");
        Assert.False(output.SetSource(0, 0));
        Assert.False(output.Connected);

        var input = new ParticleGraphPin(ParticleGraphPinFlag.Input, "input");
        Assert.True(input.SetSource(1, 2));
        Assert.True(input.SetSource(3, 4));
        Assert.Equal(3, input.ConnectedNodeIndex);
        Assert.Equal(4, input.ConnectedPinIndex);

        var clone = input.Clone();
        Assert.Equal(input.ConnectedNodeIndex, clone.ConnectedNodeIndex);
        Assert.Equal(input.ConnectedPinIndex, clone.ConnectedPinIndex);
    }
}
