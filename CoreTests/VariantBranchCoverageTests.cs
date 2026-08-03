namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;

public sealed class VariantBranchCoverageTests
{
    [Fact]
    public void Variant_container_assignment_failures_release_the_cloned_value()
    {
        using var value = new Variant(1);

        using var map = new VariantMap();
        var mapSetter = typeof(VariantMap).GetMethod(
            "SetValue", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(StringHash), typeof(Variant), typeof(bool)], modifiers: null)!;
        var mapException = Assert.Throws<TargetInvocationException>(() => mapSetter.Invoke(
            map, [new StringHash("missing"), value, true]));
        Assert.IsType<InvalidOperationException>(mapException.InnerException);
        Assert.Empty(map);

        using var vector = new VariantVector();
        var vectorAdder = typeof(VariantVector).GetMethod(
            "AddValue", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(Variant), typeof(bool)], modifiers: null)!;
        var vectorException = Assert.Throws<TargetInvocationException>(() => vectorAdder.Invoke(
            vector, [value, true]));
        Assert.IsType<InvalidOperationException>(vectorException.InnerException);
        Assert.Empty(vector);

        using var stringMap = new StringVariantMap();
        var stringMapSetter = typeof(StringVariantMap).GetMethod(
            "SetValue", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(string), typeof(Variant), typeof(bool)], modifiers: null)!;
        var stringMapException = Assert.Throws<TargetInvocationException>(() => stringMapSetter.Invoke(
            stringMap, ["missing", value, true]));
        Assert.IsType<InvalidOperationException>(stringMapException.InnerException);
        Assert.Empty(stringMap);
    }

    [Fact]
    public void Variant_custom_object_and_generic_get_paths_cover_cloneable_and_fallback_values()
    {
        using var cloneable = Variant.MakeCustomValue(new CloneableValue(7));
        var customValue = cloneable.Get<CloneableValue>();
        Assert.Equal(7, customValue.Value);
        Assert.True(cloneable.IsCustomType<CloneableValue>());
        Assert.IsType<CloneableValue>(cloneable.GetObject());

        using var plain = Variant.MakeCustomValue(new PlainValue(9));
        Assert.Same(plain.Get<PlainValue>(), plain.GetObject());
        Assert.False(plain.TryGetCustom<CloneableValue>(out _));

        Assert.Throws<ArgumentException>(() => new Variant(1).Get<DateTime>());
        Assert.Equal(new StringHash(1), new Variant(1).GetStringHash());
        Assert.Equal(StringHash.Empty, new Variant("not a hash").GetStringHash());
    }

    [Fact]
    public void Variant_object_conversion_covers_enum_variant_collection_and_custom_branches()
    {
        using var enumValue = Variant.FromObject(TestEnum.Value);
        Assert.Equal(VariantType.Int, enumValue.Type);
        Assert.Equal(1, enumValue.GetInt());

        using var original = new Variant(3);
        using var cloned = Variant.FromObject(original);
        Assert.Equal(original, cloned);
        Assert.NotSame(original.GetObject(), cloned.GetObject());

        using var map = Variant.FromObject(new Dictionary<StringHash, object?>
        {
            [new StringHash("number")] = 4
        });
        Assert.Equal(VariantType.VariantMap, map.Type);
        Assert.Equal(4, map.GetVariantMap()[new StringHash("number")].GetInt());

        using var strings = Variant.FromObject(new[] { "one", "two" });
        Assert.Equal(VariantType.StringVector, strings.Type);
        Assert.Equal(new[] { "one", "two" }, strings.GetStringVector());

        using var customValue = Variant.FromObject(new CustomVariantValue(11));
        Assert.Equal(VariantType.Custom, customValue.Type);
        Assert.Equal(11, customValue.GetCustomVariantValue().Value);
    }

    [Fact]
    public void Variant_conversion_equality_clone_and_parser_edges_cover_uncommon_branches()
    {
        using var number = new Variant(3);
        using var boolean = number.Convert(VariantType.Bool);
        using var floating = number.Convert(VariantType.Float);
        using var doubleValue = number.Convert(VariantType.Double);
        using var longValue = number.Convert(VariantType.Int64);
        Assert.True(boolean.GetBool());
        Assert.Equal(3.0f, floating.GetFloat());
        Assert.Equal(3.0, doubleValue.GetDouble());
        Assert.Equal(3L, longValue.GetInt64());

        using var quaternion = new Variant(VariantType.Quaternion);
        quaternion.FromString(VariantType.Quaternion, "1 2 3");
        Assert.Equal(new Quaternion(1, 2, 3), quaternion.GetQuaternion());

        using var custom = Variant.MakeCustomValue(new PlainValue(4));
        using var customClone = custom.Clone();
        Assert.True(custom.Equals((object)customClone));
        Assert.False(custom.Equals((object?)null));
        Assert.False(custom.Equals("custom"));

        using var leftVector = new Variant(new VariantVector { new(1) });
        using var rightVector = new Variant(new VariantVector { new(2) });
        Assert.False(leftVector.Equals(rightVector));
        using var leftMap = new Variant(new VariantMap { [new StringHash("key")] = new(1) });
        using var rightMap = new Variant(new VariantMap { [new StringHash("key")] = new(2) });
        Assert.False(leftMap.Equals(rightMap));
    }

    private enum TestEnum
    {
        None,
        Value
    }

    private sealed class CloneableValue(int value) : ICloneable
    {
        public int Value { get; } = value;

        public object Clone() => new CloneableValue(Value);
    }

    private sealed record PlainValue(int Value);
}
