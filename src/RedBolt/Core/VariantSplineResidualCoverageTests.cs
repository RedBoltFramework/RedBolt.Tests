// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class VariantSplineResidualCoverageTests
{
    [Fact]
    public void Variant_fallback_switches_cover_custom_values_object_inputs_and_unsupported_sizes()
    {
        using var custom = Variant.MakeCustomValue(new CustomVariantValue(3));
        Assert.False(custom.IsZero());
        Assert.IsType<CustomVariantValue>(custom.GetObject());
        Assert.True(custom.Equals(custom));

        foreach (var input in new object[]
        {
            true,
            1,
            1u,
            1L,
            1ul,
            1.0f,
            1.0,
            new StringHash("hash"),
            new Vector2f(1, 2),
            new Vector2d(1, 2),
            new Vector3f(1, 2, 3),
            new Vector3d(1, 2, 3),
            new Vector4f(1, 2, 3, 4),
            Quaternion.IDENTITY,
            Color.White,
            new Rect(1, 2, 3, 4),
            new IntRect(1, 2, 3, 4),
            new Vector2i(1, 2),
            new Vector3i(1, 2, 3),
            new byte[] { 1 },
            new IntPtr(1),
            new ResourceRef("Texture", "name"),
            new ResourceRefList("Texture", ["name"]),
            Matrix3.Identity,
            Matrix3x4.Identity,
            Matrix4.Identity,
            new VariantVector { new(1) },
            new StringVector { "value" },
            new VariantMap { [new StringHash("key")] = new(1) },
            new StringVariantMap { ["key"] = new(1) },
            new VariantCurve(),
            new CString("value")
        })
        {
            using var converted = Variant.FromObject(input);
            Assert.NotEqual(VariantType.None, converted.Type);
        }

        foreach (var unsupported in new[]
        {
            VariantType.VoidPtr,
            VariantType.Ptr,
            VariantType.Custom,
            VariantType.VariantCurve,
            VariantType.CString
        })
            Assert.Throws<ArgumentOutOfRangeException>(() => VariantTypeUtility.GetVariantTypeSize(unsupported));
    }

    [Fact]
    public void Variant_maps_cover_duplicate_insertions_and_missing_removals()
    {
        using var value = new Variant(7);
        using var map = new VariantMap();
        var hash = new StringHash("key");

        Assert.True(map.TryAdd(hash, value));
        Assert.False(map.TryAdd(hash, value));
        Assert.True(map.Remove(hash, out var removed));
        removed.Dispose();
        Assert.False(map.Remove(hash, out removed));
        removed.Dispose();

        using var stringMap = new StringVariantMap();
        Assert.True(stringMap.TryAdd("key", value));
        Assert.False(stringMap.TryAdd("key", value));
        Assert.True(stringMap.Remove("key", out removed));
        removed.Dispose();
        Assert.False(stringMap.Remove("key", out removed));
        removed.Dispose();
    }

    [Fact]
    public void Spline_catmull_rom_tuple_patterns_cover_partial_type_matches()
    {
        var method = typeof(Spline).GetMethod(
            "CalculateCatmullRom", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(object));

        var values = new object[]
        {
            1.0f,
            new Vector2f(1, 2),
            new Vector3f(1, 2, 3),
            new Vector4f(1, 2, 3, 4),
            Color.White,
            1.0
        };

        foreach (var value in values)
        {
            var other = new object();
            Invoke(method, value, other, value, value);
            Invoke(method, value, value, other, value);
            Invoke(method, value, value, value, other);
        }
    }

    private static void Invoke(MethodInfo method, object first, object second, object third, object fourth)
    {
        var result = (Variant)method.Invoke(null, [first, second, third, fourth, 0.5f, 0.25f, 0.125f])!;
        Assert.Equal(VariantType.None, result.Type);
        result.Dispose();
    }
}
