// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class VariantCoverageTests
{
    [Fact]
    public void Variant_type_factory_covers_supported_storage_kinds()
    {
        foreach (var (type, expected) in SupportedTypes())
        {
            var value = new Variant(type);
            try
            {
                Assert.Equal(type, value.Type);
                Assert.Equal(Variant.GetTypeName(type), value.GetTypeName());
                Assert.Equal(type, Variant.GetTypeFromName(value.GetTypeName()));
                if (type != VariantType.None)
                    Assert.NotNull(value.GetObject());
                _ = value.GetHashCode();
                _ = value.ToString();
                _ = value.IsZero();
                _ = expected.IsZero();
                Assert.True(value.Equals(value));
                _ = value.GetRect();
                _ = value.GetIntRect();
                _ = value.GetIntVector2();
                _ = value.GetIntVector3();
                _ = value.GetMatrix3();
                _ = value.GetMatrix3x4();
                _ = value.GetMatrix4();
            }
            finally
            {
                value.Dispose();
            }

            expected.Dispose();
        }

        Assert.Equal(VariantType.None, new Variant(VariantType.Ptr).Type);
        Assert.Equal(VariantType.None, new Variant(VariantType.Custom).Type);
        Assert.Equal(VariantType.None, Variant.GetTypeFromName("missing"));
        Assert.Equal(Enum.GetValues<VariantType>().Length, Variant.GetTypeNameList().Count);
    }

    [Fact]
    public void Variant_lerp_covers_all_interpolatable_types()
    {
        foreach (var type in InterpolatableTypes())
        {
            var left = new Variant(type);
            var right = new Variant(type);
            var result = left.Lerp(right, 0.5f);

            Assert.Equal(type, result.Type);
            result.Dispose();
            left.Dispose();
            right.Dispose();
        }

        var unsupported = new Variant("left");
        using var lerpRight = new Variant("right");
        var clone = unsupported.Lerp(lerpRight, 0.5f);
        Assert.Equal(unsupported, clone);
        clone.Dispose();
        unsupported.Dispose();
    }

    [Fact]
    public void Variant_from_string_covers_scalar_vector_matrix_and_reference_parsers()
    {
        var values = new (VariantType Type, string Text)[]
        {
            (VariantType.Int, "7"),
            (VariantType.Int64, "8"),
            (VariantType.Bool, " yes"),
            (VariantType.Float, "0.5"),
            (VariantType.Double, "0.75"),
            (VariantType.Vector2, "1 2"),
            (VariantType.Vector3, "1 2 3"),
            (VariantType.DoubleVector2, "1 2"),
            (VariantType.DoubleVector3, "1 2 3"),
            (VariantType.Vector4, "1 2 3 4"),
            (VariantType.Quaternion, "1 2 3 4"),
            (VariantType.Color, "1 2 3 0.5"),
            (VariantType.Rect, "1 2 3 4"),
            (VariantType.IntRect, "1 2 3 4"),
            (VariantType.Vector2i, "1 2"),
            (VariantType.Vector3i, "1 2 3"),
            (VariantType.Matrix3, "1 2 3 4 5 6 7 8 9"),
            (VariantType.Matrix3x4, "1 2 3 4 5 6 7 8 9 10 11 12"),
            (VariantType.Matrix4, "1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16"),
            (VariantType.String, "text"),
            (VariantType.CString, "cstring"),
            (VariantType.Buffer, "1 2 255"),
            (VariantType.VoidPtr, "0x1234"),
            (VariantType.ResourceRef, "Texture;Textures/Test.xml"),
            (VariantType.ResourceRefList, "Texture;Textures/A.xml;Textures/B.xml")
        };

        foreach (var item in values)
        {
            var value = default(Variant);
            value.FromString(item.Type, item.Text);
            Assert.Equal(item.Type, value.Type);
            value.Dispose();
        }

        var shortValues = new (VariantType Type, string Text)[]
        {
            (VariantType.Vector2, "1"),
            (VariantType.Vector3, "1 2"),
            (VariantType.DoubleVector2, "1"),
            (VariantType.DoubleVector3, "1 2"),
            (VariantType.Vector4, "1 2 3"),
            (VariantType.Quaternion, "1 2"),
            (VariantType.Color, "1 2"),
            (VariantType.Rect, "1 2 3"),
            (VariantType.IntRect, "1 2 3"),
            (VariantType.Vector2i, "1"),
            (VariantType.Vector3i, "1 2"),
            (VariantType.Matrix3, "1"),
            (VariantType.Matrix3x4, "1"),
            (VariantType.Matrix4, "1"),
            (VariantType.Buffer, "")
        };

        foreach (var item in shortValues)
        {
            var value = default(Variant);
            value.FromString(item.Type, item.Text);
            Assert.Equal(item.Type, value.Type);
            value.Dispose();
        }
    }

    [Fact]
    public void Variant_type_utility_maps_every_supported_managed_type()
    {
        Assert.Equal(VariantType.Int, VariantTypeUtility.GetVariantType<int>());
        Assert.Equal(VariantType.Int, VariantTypeUtility.GetVariantType<uint>());
        Assert.Equal(VariantType.Int, VariantTypeUtility.GetVariantType<StringHash>());
        Assert.Equal(VariantType.Int64, VariantTypeUtility.GetVariantType<long>());
        Assert.Equal(VariantType.Int64, VariantTypeUtility.GetVariantType<ulong>());
        Assert.Equal(VariantType.Bool, VariantTypeUtility.GetVariantType<bool>());
        Assert.Equal(VariantType.Float, VariantTypeUtility.GetVariantType<float>());
        Assert.Equal(VariantType.Double, VariantTypeUtility.GetVariantType<double>());
        Assert.Equal(VariantType.Vector2, VariantTypeUtility.GetVariantType<Vector2f>());
        Assert.Equal(VariantType.DoubleVector2, VariantTypeUtility.GetVariantType<Vector2d>());
        Assert.Equal(VariantType.Vector3, VariantTypeUtility.GetVariantType<Vector3f>());
        Assert.Equal(VariantType.DoubleVector3, VariantTypeUtility.GetVariantType<Vector3d>());
        Assert.Equal(VariantType.Vector4, VariantTypeUtility.GetVariantType<Vector4f>());
        Assert.Equal(VariantType.Quaternion, VariantTypeUtility.GetVariantType<Quaternion>());
        Assert.Equal(VariantType.Color, VariantTypeUtility.GetVariantType<Color>());
        Assert.Equal(VariantType.String, VariantTypeUtility.GetVariantType<string>());
        Assert.Equal(VariantType.Buffer, VariantTypeUtility.GetVariantType<byte[]>());
        Assert.Equal(VariantType.ResourceRef, VariantTypeUtility.GetVariantType<ResourceRef>());
        Assert.Equal(VariantType.ResourceRefList, VariantTypeUtility.GetVariantType<ResourceRefList>());
        Assert.Equal(VariantType.VariantVector, VariantTypeUtility.GetVariantType<VariantVector>());
        Assert.Equal(VariantType.StringVector, VariantTypeUtility.GetVariantType<StringVector>());
        Assert.Equal(VariantType.VariantMap, VariantTypeUtility.GetVariantType<VariantMap>());
        Assert.Equal(VariantType.Rect, VariantTypeUtility.GetVariantType<Rect>());
        Assert.Equal(VariantType.IntRect, VariantTypeUtility.GetVariantType<IntRect>());
        Assert.Equal(VariantType.Vector2i, VariantTypeUtility.GetVariantType<Vector2i>());
        Assert.Equal(VariantType.Vector3i, VariantTypeUtility.GetVariantType<Vector3i>());
        Assert.Equal(VariantType.Matrix3, VariantTypeUtility.GetVariantType<Matrix3>());
        Assert.Equal(VariantType.Matrix3x4, VariantTypeUtility.GetVariantType<Matrix3x4>());
        Assert.Equal(VariantType.Matrix4, VariantTypeUtility.GetVariantType<Matrix4>());
        Assert.Equal(VariantType.VariantCurve, VariantTypeUtility.GetVariantType<VariantCurve>());
        Assert.Equal(VariantType.StringVariantMap, VariantTypeUtility.GetVariantType<StringVariantMap>());

        Assert.Equal(VariantType.None, VariantTypeUtility.GetVariantType(new object()));
        Assert.Equal(VariantType.None, VariantTypeUtility.GetVariantType(null));
        Assert.Equal(VariantType.CString, VariantTypeUtility.GetVariantType(new CString("value")));
        Assert.Equal(VariantType.VoidPtr, VariantTypeUtility.GetVariantType(IntPtr.Zero));
        Assert.Equal(VariantType.Buffer, VariantTypeUtility.GetVariantType(new VectorBuffer([1, 2])));

        foreach (var type in Enum.GetValues<VariantType>())
            Assert.NotNull(VariantTypeUtility.GetClrType(type));

        foreach (var type in InterpolatableTypes())
            Assert.True(VariantTypeUtility.IsInterpolatable(type));

        Assert.False(VariantTypeUtility.IsInterpolatable(VariantType.None));
        Assert.False(VariantTypeUtility.IsInterpolatable(VariantType.String));
        Assert.False(VariantTypeUtility.IsInterpolatable(VariantType.Matrix4));
    }

    [Fact]
    public void Variant_zero_and_conversion_paths_cover_owned_and_scalar_values()
    {
        foreach (var (type, expected) in SupportedTypes())
        {
            var value = new Variant(type);
            try
            {
                _ = value.GetBool();
                _ = value.GetInt();
                _ = value.GetInt64();
                _ = value.GetFloat();
                _ = value.GetDouble();
                _ = value.GetUInt();
                _ = value.GetUInt64();
                _ = value.GetOptional<int>();
                _ = value.GetObject();
                _ = value.GetHashCode();
                _ = value.GetRect();
                _ = value.GetIntRect();
                _ = value.GetIntVector2();
                _ = value.GetIntVector3();
                _ = value.GetMatrix3();
                _ = value.GetMatrix3x4();
                _ = value.GetMatrix4();
            }
            finally
            {
                value.Dispose();
            }

            expected.Dispose();
        }

        var custom = Variant.MakeCustomValue(0);
        Assert.True(custom.IsZero());
        Assert.True(custom.IsCustomType<int>());
        Assert.True(custom.TryGetCustom<int>(out var customValue));
        Assert.Equal(0, customValue);
        Assert.False(custom.TryGetCustom<string>(out _));
        custom.Dispose();

        var notCustom = new Variant(1);
        Assert.False(notCustom.TryGetCustom<int>(out _));
        Assert.False(notCustom.IsCustomType<int>());
        Assert.Same(CustomVariantValue.Empty, notCustom.GetCustomVariantValue());
        notCustom.Dispose();

        foreach (var (type, expected) in SupportedTypes())
        {
            var objectValue = expected.GetObject();
            var converted = Variant.FromObject(objectValue);
            Assert.Equal(type, converted.Type);
            converted.Dispose();
            expected.Dispose();
        }

        using var objectMap = new VariantMap
        {
            [new StringHash("one")] = new Variant(1)
        };
        var mapVariant = Variant.FromObject(objectMap.ToDictionary(
            static pair => pair.Key,
            static pair => (object?)pair.Value));
        Assert.Equal(VariantType.VariantMap, mapVariant.Type);
        mapVariant.Dispose();

        var stringVectorVariant = Variant.FromObject(new[] { "one", "two" });
        Assert.Equal(VariantType.StringVector, stringVectorVariant.Type);
        stringVectorVariant.Dispose();

        var numeric = new Variant(3.9f);
        var same = numeric.Convert(VariantType.Float);
        var none = numeric.Convert(VariantType.None);
        var text = numeric.Convert(VariantType.String);
        var integer = numeric.Convert(VariantType.Int);
        Assert.Equal(VariantType.Float, same.Type);
        Assert.True(none.IsEmpty);
        Assert.Equal("3.9", text.GetString());
        Assert.Equal(3, integer.GetInt());
        same.Dispose();
        none.Dispose();
        text.Dispose();
        integer.Dispose();
        numeric.Dispose();

        var parsed = new Variant("42");
        var parsedInt = parsed.Convert(VariantType.Int);
        Assert.Equal(42, parsedInt.GetInt());
        parsedInt.Dispose();
        parsed.Dispose();

        Assert.Equal(1, new Variant(1).Get<int>());
        Assert.Equal(1u, new Variant(1).Get<uint>());
        Assert.Equal(1L, new Variant(1L).Get<long>());
        Assert.Equal(1UL, new Variant(1L).Get<ulong>());
        Assert.Equal(1u, new Variant(1).Get<StringHash>().Value);
        Assert.True(new Variant(true).Get<bool>());
        Assert.Equal(1.0f, new Variant(1.0f).Get<float>());
        Assert.Equal(1.0, new Variant(1.0).Get<double>());
        Assert.Equal(Vector2f.Zero, new Variant(VariantType.Vector2).Get<Vector2f>());
        Assert.Equal(Vector2d.Zero, new Variant(VariantType.DoubleVector2).Get<Vector2d>());
        Assert.Equal(Vector3f.Zero, new Variant(VariantType.Vector3).Get<Vector3f>());
        Assert.Equal(Vector3d.Zero, new Variant(VariantType.DoubleVector3).Get<Vector3d>());
        Assert.Equal(Vector4f.Zero, new Variant(VariantType.Vector4).Get<Vector4f>());
        Assert.Equal(Quaternion.IDENTITY, new Variant(VariantType.Quaternion).Get<Quaternion>());
        Assert.Equal(Color.Black, new Variant(VariantType.Color).Get<Color>());
        Assert.Equal(string.Empty, new Variant(VariantType.String).Get<string>());
        Assert.Equal(CString.Empty, new Variant(VariantType.CString).Get<CString>());
        Assert.Empty(new Variant(VariantType.Buffer).Get<byte[]>());
        Assert.Equal(IntPtr.Zero, new Variant(VariantType.VoidPtr).Get<IntPtr>());
        Assert.Equal(ResourceRef.Empty, new Variant(VariantType.ResourceRef).Get<ResourceRef>());
        Assert.Equal(ResourceRefList.Empty, new Variant(VariantType.ResourceRefList).Get<ResourceRefList>());
        Assert.Equal(Matrix3.Zero, new Variant(VariantType.Matrix3).Get<Matrix3>());
        Assert.Equal(Matrix3x4.Zero, new Variant(VariantType.Matrix3x4).Get<Matrix3x4>());
        Assert.Equal(Matrix4.Zero, new Variant(VariantType.Matrix4).Get<Matrix4>());
        Assert.Equal(Rect.Zero, new Variant(VariantType.Rect).Get<Rect>());
        Assert.Equal(IntRect.Zero, new Variant(VariantType.IntRect).Get<IntRect>());
        Assert.Equal(Vector2i.Zero, new Variant(VariantType.Vector2i).Get<Vector2i>());
        Assert.Equal(Vector3i.Zero, new Variant(VariantType.Vector3i).Get<Vector3i>());
        Assert.Empty(new Variant(VariantType.VariantVector).Get<VariantVector>());
        Assert.Empty(new Variant(VariantType.StringVector).Get<StringVector>());
        Assert.Empty(new Variant(VariantType.VariantMap).Get<VariantMap>());
        Assert.Empty(new Variant(VariantType.StringVariantMap).Get<StringVariantMap>());
        Assert.Empty(new Variant(VariantType.VariantCurve).Get<VariantCurve>().KeyFrames);
    }

    private static IEnumerable<VariantType> InterpolatableTypes()
    {
        yield return VariantType.Float;
        yield return VariantType.Double;
        yield return VariantType.Vector2;
        yield return VariantType.DoubleVector2;
        yield return VariantType.Vector3;
        yield return VariantType.DoubleVector3;
        yield return VariantType.Vector4;
        yield return VariantType.Quaternion;
        yield return VariantType.Color;
        yield return VariantType.IntRect;
        yield return VariantType.Vector2i;
        yield return VariantType.Vector3i;
    }

    private static IEnumerable<(VariantType Type, Variant Expected)> SupportedTypes()
    {
        yield return (VariantType.None, default);
        yield return (VariantType.Int, new Variant(1));
        yield return (VariantType.Int64, new Variant(1L));
        yield return (VariantType.Bool, new Variant(true));
        yield return (VariantType.Float, new Variant(1.0f));
        yield return (VariantType.Double, new Variant(1.0));
        yield return (VariantType.Vector2, new Variant(Vector2f.One));
        yield return (VariantType.Vector3, new Variant(Vector3f.One));
        yield return (VariantType.DoubleVector2, new Variant(Vector2d.One));
        yield return (VariantType.DoubleVector3, new Variant(Vector3d.One));
        yield return (VariantType.Vector4, new Variant(Vector4f.One));
        yield return (VariantType.Quaternion, new Variant(Quaternion.IDENTITY));
        yield return (VariantType.Color, new Variant(Color.White));
        yield return (VariantType.String, new Variant("value"));
        yield return (VariantType.CString, new Variant(new CString("value")));
        yield return (VariantType.Buffer, new Variant([1, 2]));
        yield return (VariantType.VoidPtr, new Variant(new IntPtr(1)));
        yield return (VariantType.ResourceRef, new Variant(new ResourceRef("Texture", "value")));
        yield return (VariantType.ResourceRefList, new Variant(new ResourceRefList("Texture", ["value"])));
        yield return (VariantType.VariantVector, new Variant(new VariantVector { new(1) }));
        yield return (VariantType.VariantMap, new Variant(new VariantMap { [new StringHash("value")] = new(1) }));
        yield return (VariantType.IntRect, new Variant(new IntRect(1, 2, 3, 4)));
        yield return (VariantType.Vector2i, new Variant(new Vector2i(1, 2)));
        yield return (VariantType.Vector3i, new Variant(new Vector3i(1, 2, 3)));
        yield return (VariantType.Matrix3, new Variant(Matrix3.Identity));
        yield return (VariantType.Matrix3x4, new Variant(Matrix3x4.Identity));
        yield return (VariantType.Matrix4, new Variant(Matrix4.Identity));
        yield return (VariantType.StringVector, new Variant(new StringVector { "value" }));
        yield return (VariantType.Rect, new Variant(new Rect(1, 2, 3, 4)));
        yield return (VariantType.StringVariantMap, new Variant(new StringVariantMap { ["value"] = new(1) }));
        yield return (VariantType.VariantCurve, new Variant(new VariantCurve()));
    }
}
