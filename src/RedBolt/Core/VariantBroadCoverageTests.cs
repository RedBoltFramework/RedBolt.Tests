// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class VariantBroadCoverageTests
{
    [Fact]
    public void Variant_type_constructors_cover_default_objects_get_object_zero_hash_and_clone_paths()
    {
        foreach (var type in Enum.GetValues<VariantType>())
        {
            var value = new Variant(type);
            var boxed = value.GetObject();
            if (boxed is IDisposable disposable)
                disposable.Dispose();

            _ = value.IsZero();
            _ = value.ToString();
            _ = value.ToHash();
            using var clone = value.Clone();
            Assert.True(value.Equals(clone));
            value.Dispose();
        }

        using var nonEmptyResourceList = new Variant(new ResourceRefList("Texture", ["non-empty"]));
        Assert.False(nonEmptyResourceList.IsZero());

        foreach (var objectValue in new object?[]
        {
            new Vector2d(1, 2),
            new Vector3d(1, 2, 3),
            new Matrix3(1, 0, 0, 0, 1, 0, 0, 0, 1),
            new Matrix3x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0),
            Matrix4.Identity,
            new VariantVector { new(1) },
            new StringVector { "value" },
            new VariantMap { [new StringHash("value")] = new(1) },
            new StringVariantMap { ["value"] = new(1) },
            new VariantCurve(),
            new ResourceRef("Texture", "name"),
            new ResourceRefList("Texture", ["name"]),
            new VectorBuffer(new byte[] { 1, 2 }),
            new CString("value"),
            new IntPtr(1)
        })
        {
            using var converted = Variant.FromObject(objectValue);
            Assert.NotEqual(VariantType.None, converted.Type);
            (objectValue as IDisposable)?.Dispose();
        }

        using var empty = new Variant();
        Assert.True(empty.IsEmpty);
        Assert.Null(empty.GetOptional<int>());
        Assert.Equal(VariantType.None, Variant.GetTypeFromName("not-a-variant"));
        Assert.Equal(VariantType.Int, Variant.GetTypeFromName("Int"));
    }

    [Fact]
    public void Variant_mismatched_getters_and_generic_optional_paths_cover_fallbacks()
    {
        using var number = new Variant(7);
        Assert.True(number.GetBool());
        Assert.Equal(7, number.GetInt());
        Assert.Equal(7L, number.GetInt64());
        Assert.Equal(7.0f, number.GetFloat());
        Assert.Equal(7.0, number.GetDouble());
        Assert.Equal(7u, number.GetUInt());
        Assert.Equal(7ul, number.GetUInt64());
        Assert.Equal(7, number.Get<int>());
        Assert.Equal(7u, number.Get<uint>());
        Assert.Equal(7L, number.Get<long>());
        Assert.Equal(7ul, number.Get<ulong>());
        Assert.Equal(7.0f, number.Get<float>());
        Assert.Equal(7.0, number.Get<double>());
        Assert.Equal(7, number.GetOptional<int>());

        using var mismatch = new Variant("text");
        Assert.False(mismatch.GetBool());
        Assert.Equal(0, mismatch.GetInt());
        Assert.Equal(0L, mismatch.GetInt64());
        Assert.Equal(0.0f, mismatch.GetFloat());
        Assert.Equal(0.0, mismatch.GetDouble());
        Assert.Equal(0u, mismatch.GetUInt());
        Assert.Equal(0ul, mismatch.GetUInt64());
        Assert.Equal(Vector2f.Zero, mismatch.GetVector2());
        Assert.Equal(Vector2d.Zero, mismatch.GetDoubleVector2());
        Assert.Equal(Vector3f.Zero, mismatch.GetVector3());
        Assert.Equal(Vector3d.Zero, mismatch.GetDoubleVector3());
        Assert.Equal(Vector4f.Zero, mismatch.GetVector4());
        Assert.Equal(Quaternion.IDENTITY, mismatch.GetQuaternion());
        Assert.Equal(Color.White, mismatch.GetColor());
        Assert.Equal(Rect.Zero, mismatch.GetRect());
        Assert.Equal(IntRect.Zero, mismatch.GetIntRect());
        Assert.Equal(Vector2i.Zero, mismatch.GetIntVector2());
        Assert.Equal(Vector3i.Zero, mismatch.GetIntVector3());
        Assert.Equal(Matrix3.Identity, mismatch.GetMatrix3());
        Assert.Equal(Matrix3x4.Identity, mismatch.GetMatrix3x4());
        Assert.Equal(Matrix4.Identity, mismatch.GetMatrix4());
        Assert.Empty(mismatch.GetCString().ConvertToString());
        Assert.Empty(mismatch.GetBuffer());
        Assert.Equal(IntPtr.Zero, mismatch.GetVoidPtr());
        Assert.Equal(ResourceRef.Empty, mismatch.GetResourceRef());
        Assert.Equal(ResourceRefList.Empty, mismatch.GetResourceRefList());
        Assert.Empty(mismatch.GetVariantVector());
        Assert.Empty(mismatch.GetStringVector());
        Assert.Empty(mismatch.GetVariantMap());
        Assert.Empty(mismatch.GetStringVariantMap());
        Assert.Empty(mismatch.GetVariantCurve().KeyFrames);
    }

    [Fact]
    public void Variant_lerp_convert_and_parser_paths_cover_supported_and_fallback_types()
    {
        using var floatLeft = new Variant(2.0f);
        using var floatRight = new Variant(6.0f);
        using var floatLerp = floatLeft.Lerp(floatRight, 0.5f);
        Assert.Equal(4.0f, floatLerp.GetFloat());

        using var doubleLeft = new Variant(2.0);
        using var doubleRight = new Variant(6.0);
        using var doubleLerp = doubleLeft.Lerp(doubleRight, 0.5f);
        Assert.Equal(4.0, doubleLerp.GetDouble());

        using var intLeft = new Variant(2);
        using var intRight = new Variant(6);
        using var intLerp = intLeft.Lerp(intRight, 0.5f);
        Assert.Equal(4, intLerp.GetInt());

        using var longLeft = new Variant(2L);
        using var longRight = new Variant(6L);
        using var longLerp = longLeft.Lerp(longRight, 0.5f);
        Assert.Equal(4L, longLerp.GetInt64());

        using var vector2 = new Variant(Vector2f.Zero);
        using var vector2Other = new Variant(Vector2f.One);
        using var vector2Lerp = vector2.Lerp(vector2Other, 0.5f);
        Assert.Equal(new Vector2f(0.5f, 0.5f), vector2Lerp.GetVector2());

        using var vector3 = new Variant(Vector3f.Zero);
        using var vector3Other = new Variant(Vector3f.One);
        using var vector3Lerp = vector3.Lerp(vector3Other, 0.5f);
        Assert.Equal(new Vector3f(0.5f, 0.5f, 0.5f), vector3Lerp.GetVector3());

        using var doubleVector2 = new Variant(Vector2d.Zero);
        using var doubleVector2Other = new Variant(Vector2d.One);
        using var doubleVector2Lerp = doubleVector2.Lerp(doubleVector2Other, 0.5f);
        Assert.Equal(new Vector2d(0.5, 0.5), doubleVector2Lerp.GetDoubleVector2());

        using var doubleVector3 = new Variant(Vector3d.Zero);
        using var doubleVector3Other = new Variant(Vector3d.One);
        using var doubleVector3Lerp = doubleVector3.Lerp(doubleVector3Other, 0.5f);
        Assert.Equal(new Vector3d(0.5, 0.5, 0.5), doubleVector3Lerp.GetDoubleVector3());

        using var vector4 = new Variant(Vector4f.Zero);
        using var vector4Other = new Variant(Vector4f.One);
        using var vector4Lerp = vector4.Lerp(vector4Other, 0.5f);
        Assert.Equal(new Vector4f(0.5f, 0.5f, 0.5f, 0.5f), vector4Lerp.GetVector4());

        using var quaternion = new Variant(Quaternion.IDENTITY);
        using var quaternionLerp = quaternion.Lerp(quaternion, 0.5f);
        Assert.Equal(Quaternion.IDENTITY, quaternionLerp.GetQuaternion());

        using var color = new Variant(Color.Black);
        using var colorLerp = color.Lerp(new Variant(Color.White), 0.5f);
        Assert.Equal(new Color(0.5f, 0.5f, 0.5f, 1.0f), colorLerp.GetColor());

        using var intRect = new Variant(new IntRect(0, 0, 10, 10));
        using var intRectLerp = intRect.Lerp(new Variant(new IntRect(10, 10, 20, 20)), 0.5f);
        Assert.Equal(new IntRect(5, 5, 15, 15), intRectLerp.GetIntRect());

        using var intVector2 = new Variant(new Vector2i(0, 2));
        using var intVector2Lerp = intVector2.Lerp(new Variant(new Vector2i(10, 4)), 0.5f);
        Assert.Equal(new Vector2i(5, 3), intVector2Lerp.GetIntVector2());

        using var intVector3 = new Variant(Vector3i.Zero);
        using var intVector3Lerp = intVector3.Lerp(new Variant(new Vector3i(4, 6, 8)), 0.5f);
        Assert.Equal(new Vector3i(2, 3, 4), intVector3Lerp.GetIntVector3());

        using var unsupported = new Variant("left");
        using var unsupportedLerp = unsupported.Lerp(new Variant("right"), 0.5f);
        Assert.Equal("left", unsupportedLerp.GetString());

        using var same = intLeft.Convert(VariantType.Int);
        using var none = intLeft.Convert(VariantType.None);
        using var asString = intLeft.Convert(VariantType.String);
        using var fromString = new Variant("12").Convert(VariantType.Int);
        using var fallback = intLeft.Convert(VariantType.Vector3);
        Assert.Equal(2, same.GetInt());
        Assert.True(none.IsEmpty);
        Assert.Equal("2", asString.GetString());
        Assert.Equal(12, fromString.GetInt());
        Assert.True(fallback.IsEmpty);

        using var parser = new Variant();
        parser.FromString(VariantType.Bool, " y");
        Assert.True(parser.GetBool());
        parser.FromString(VariantType.Bool, " \t");
        Assert.False(parser.GetBool());
        parser.FromString(VariantType.Int, "bad");
        parser.FromString(VariantType.Int64, "12");
        parser.FromString(VariantType.Float, "bad");
        parser.FromString(VariantType.Double, "2.5");
        parser.FromString(VariantType.Buffer, "1 bad 3");
        parser.FromString(VariantType.Vector2, "1");
        parser.FromString(VariantType.Vector3, "1 2 3");
        parser.FromString(VariantType.DoubleVector2, "1 2");
        parser.FromString(VariantType.DoubleVector3, "1 2");
        parser.FromString(VariantType.Vector4, "1 2 3 4");
        parser.FromString(VariantType.Quaternion, "1 2");
        parser.FromString(VariantType.Quaternion, "1 2 3");
        parser.FromString(VariantType.Quaternion, "1 2 3 4");
        parser.FromString(VariantType.Color, "1 2");
        parser.FromString(VariantType.Color, "1 2 3");
        parser.FromString(VariantType.Color, "1 2 3 4");
        parser.FromString(VariantType.Rect, "1 2 3 4");
        parser.FromString(VariantType.IntRect, "1 2 3 4");
        parser.FromString(VariantType.Vector2i, "1 2");
        parser.FromString(VariantType.Vector3i, "1 2 3");
        parser.FromString(VariantType.Matrix3, "1 2 3");
        parser.FromString(VariantType.Matrix3x4, "1 2 3");
        parser.FromString(VariantType.Matrix4, "1 2 3");
        parser.FromString(VariantType.String, "text");
        parser.FromString(VariantType.CString, "text");
        parser.FromString(VariantType.VoidPtr, "ignored");
        parser.FromString(VariantType.ResourceRef, "Type;Name");
        parser.FromString(VariantType.ResourceRefList, "Type;One;Two");
        Assert.Equal(VariantType.ResourceRefList, parser.Type);
    }
}
