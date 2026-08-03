namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;

public sealed class VariantDeepCoverageTests
{
    [Fact]
    public void Variant_map_covers_owned_replacement_try_add_remove_and_dictionary_interfaces()
    {
        var key = new StringHash("value");
        using var source = new Variant(1);
        using var map = new VariantMap();

        map.Add(key, source);
        Assert.False(map.TryAdd(key, new Variant(2)));
        map[key] = new Variant(3);
        Assert.Equal(3, map[key].GetInt());
        Assert.False(map.Remove(new StringHash("missing")));
        Assert.True(map.Remove(key, out var removed));
        Assert.Equal(3, removed.GetInt());
        removed.Dispose();
        Assert.False(map.Remove(key, out removed));

        IDictionary<StringHash, Variant> dictionary = map;
        dictionary[key] = source;
        Assert.Equal(1, dictionary[key].GetInt());
        var pairs = (ICollection<KeyValuePair<StringHash, Variant>>)map;
        Assert.True(pairs.Remove(new KeyValuePair<StringHash, Variant>(key, new Variant(1))));
        Assert.False(pairs.Remove(new KeyValuePair<StringHash, Variant>(key, new Variant(2))));

        map.Add(key, source);
        map.Clear();
        Assert.Empty(map);
    }

    [Fact]
    public void Variant_vector_covers_index_insert_remove_range_predicate_and_interface_paths()
    {
        using var vector = new VariantVector();
        using var one = new Variant(1);
        using var two = new Variant(2);
        vector.Add(one);
        vector.Insert(0, two);
        vector.InsertRange(1, [new Variant(3), new Variant(4)]);
        Assert.Equal([2, 3, 4, 1], vector.Select(static value => value.GetInt()).ToArray());

        vector[0] = new Variant(8);
        Assert.False(vector.Remove(new Variant(99)));
        Assert.True(vector.Remove(new Variant(3)));
        Assert.Equal(2, vector.RemoveAll(static value => value.GetInt() % 2 == 0));

        IList<Variant> list = vector;
        list.Add(new Variant(5));
        list.Clear();
        Assert.Empty(vector);

        vector.Add(new Variant(6));
        vector.RemoveRange(0, 1);
        Assert.Empty(vector);
    }

    [Fact]
    public void String_variant_map_covers_owned_replacement_try_add_remove_and_interfaces()
    {
        using var value = new Variant("one");
        using var map = new StringVariantMap(new Dictionary<string, Variant>());

        map.Add("key", value);
        Assert.False(map.TryAdd("key", new Variant("duplicate")));
        map["key"] = new Variant("replacement");
        Assert.Equal("replacement", map["key"].GetString());
        Assert.False(map.Remove("missing"));
        Assert.True(map.Remove("key", out var removed));
        Assert.Equal("replacement", removed.GetString());
        removed.Dispose();

        IDictionary<string, Variant> dictionary = map;
        dictionary["key"] = value;
        var pairs = (ICollection<KeyValuePair<string, Variant>>)map;
        Assert.True(pairs.Remove(new KeyValuePair<string, Variant>("key", value)));
        Assert.False(pairs.Remove(new KeyValuePair<string, Variant>("key", value)));

        map.Add("a", value);
        map.Clear();
        Assert.Empty(map);
    }

    [Fact]
    public void Variant_parsing_and_conversion_cover_malformed_scalar_and_unsupported_paths()
    {
        var value = default(Variant);
        value.FromString(VariantType.Int, "not-an-int");
        Assert.Equal(0, value.GetInt());
        value.FromString(VariantType.Bool, "no");
        Assert.False(value.GetBool());
        value.FromString("missing", "value");
        Assert.True(value.IsEmpty);
        value.Dispose();

        using var stringValue = new Variant("bad");
        var unsupported = stringValue.Convert(VariantType.Matrix4);
        Assert.Equal(VariantType.Matrix4, unsupported.Type);
        unsupported.Dispose();

        Assert.Null(new Variant().GetOptional<int>());
        Assert.Equal(7, new Variant(7).GetOptional<int>());
        Assert.NotEqual(new Variant(1), new Variant(2));
        Assert.False(new Variant(1).Equals("1"));
    }
}
