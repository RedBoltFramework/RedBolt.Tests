// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class VariantUtilityCoverageGapTests
{
    [Fact]
    public void Object_variant_type_mapping_covers_every_runtime_value_kind()
    {
        using var variant = new Variant(7);
        using var vector = new VariantVector { new(1) };
        using var map = new VariantMap { [new StringHash("key")] = new(1) };
        using var stringMap = new StringVariantMap { ["key"] = new(1) };
        using var curve = new VariantCurve();
        using var cstring = new CString("text");

        var values = new (object? Value, VariantType Type)[]
        {
            (null, VariantType.None),
            (variant, VariantType.Int),
            (true, VariantType.Bool),
            (1, VariantType.Int),
            (1u, VariantType.Int),
            (1L, VariantType.Int64),
            (1UL, VariantType.Int64),
            (1.0f, VariantType.Float),
            (1.0, VariantType.Double),
            (new StringHash("hash"), VariantType.Int),
            (Vector2f.One, VariantType.Vector2),
            (Vector2d.One, VariantType.DoubleVector2),
            (Vector3f.One, VariantType.Vector3),
            (Vector3d.One, VariantType.DoubleVector3),
            (Vector4f.One, VariantType.Vector4),
            (Quaternion.IDENTITY, VariantType.Quaternion),
            (Color.White, VariantType.Color),
            (new Rect(1, 2, 3, 4), VariantType.Rect),
            (new IntRect(1, 2, 3, 4), VariantType.IntRect),
            (Vector2i.One, VariantType.Vector2i),
            (Vector3i.One, VariantType.Vector3i),
            (new byte[] { 1, 2 }, VariantType.Buffer),
            (new VectorBuffer([1, 2]), VariantType.Buffer),
            (IntPtr.Zero, VariantType.VoidPtr),
            (new ResourceRef("Texture", "name"), VariantType.ResourceRef),
            (new ResourceRefList("Texture", ["name"]), VariantType.ResourceRefList),
            (Matrix3.Identity, VariantType.Matrix3),
            (Matrix3x4.Identity, VariantType.Matrix3x4),
            (Matrix4.Identity, VariantType.Matrix4),
            (vector, VariantType.VariantVector),
            (new StringVector { "text" }, VariantType.StringVector),
            (map, VariantType.VariantMap),
            (stringMap, VariantType.StringVariantMap),
            (curve, VariantType.VariantCurve),
            ("text", VariantType.String),
            (cstring, VariantType.CString),
            (new object(), VariantType.None)
        };

        foreach (var (value, type) in values)
            Assert.Equal(type, VariantTypeUtility.GetVariantType(value));
    }

    [Fact]
    public void Variant_utility_covers_default_sizes_and_variant_json_overloads()
    {
        using var value = new Variant(42);
        object boxedVariant = value;

        Assert.Equal("42", VariantTypeUtility.Serialize(boxedVariant, VariantType.Int));
        Assert.Equal(42, Assert.IsType<int>(VariantTypeUtility.Deserialize("42", VariantType.Int)));
        using var fromJson = VariantTypeUtility.DeserializeVariant("42", VariantType.Int);
        Assert.Equal(42, fromJson.GetInt());

        Assert.Equal("\"\"", VariantTypeUtility.Serialize(null, VariantType.CString));
        using var emptyCString = Assert.IsType<CString>(VariantTypeUtility.Deserialize("null", VariantType.CString));
        Assert.Empty(emptyCString.ConvertToString());
    }

    [Fact]
    public void Generic_variant_type_mapping_rejects_unrepresented_clr_types()
    {
        Assert.Throws<NotSupportedException>(() => VariantTypeUtility.GetVariantType<DateTime>());
        Assert.Throws<NotSupportedException>(() => VariantTypeUtility.GetVariantType<CString>());
    }
}
