namespace RedBolt.Tests;

using System.Text;

using RedBolt.IO;
using RedBolt.Resource;

public sealed class ResourceBoundaryTests
{
    [Fact]
    public void Resource_load_runs_begin_and_end_and_always_restores_done_state()
    {
        var success = new TrackingResource(beginResult: true, endResult: true);
        using var source = new MemoryBuffer(Array.Empty<byte>());

        Assert.True(success.Load(source));
        Assert.True(success.BeginCalled);
        Assert.True(success.EndCalled);
        Assert.Equal(AsyncLoadState.Done, success.AsyncLoadState);

        var failure = new TrackingResource(beginResult: true, endResult: false);
        Assert.False(failure.Load(source));
        Assert.True(failure.EndCalled);
        Assert.Equal(AsyncLoadState.Done, failure.AsyncLoadState);
    }

    [Fact]
    public void JSONValue_array_mutations_are_owned_and_clones_are_independent()
    {
        using var value = new JSONValue(JSONValueType.Array);
        value.Push(new JSONValue("first"));
        value.Insert(0, new JSONValue("zero"));
        value.Resize(3);
        value[2].Set("last");

        using var clone = (JSONValue)value.Clone();
        value.Erase(0);
        value.Pop();

        Assert.Equal(1, value.Size());
        Assert.Equal("first", value[0].GetString());
        Assert.Equal(3, clone.Size());
        Assert.Equal("zero", clone[0].GetString());
        Assert.Equal("last", clone[2].GetString());

        value.Dispose();
        value.Dispose();
        Assert.True(value.IsNull);
    }

    [Fact]
    public void JSONFile_rejects_empty_and_malformed_input_without_replacing_root()
    {
        using var file = new JSONFile();
        Assert.True(file.FromString("{\"keep\":7}"));
        var original = file.ToString(string.Empty);

        Assert.False(file.FromString(string.Empty));
        Assert.Equal(original, file.ToString(string.Empty));
        Assert.False(file.FromString("[1,"));
        Assert.Equal(original, file.ToString(string.Empty));

        var utf8WithTerminator = Encoding.UTF8.GetBytes("{\"value\":\"ž\"}\0ignored");
        using var buffer = new MemoryBuffer(utf8WithTerminator);
        Assert.True(file.Load(buffer));
        Assert.Equal("ž", file.Root.Get("value").GetString());
    }

    [Fact]
    public void PListValue_wrong_type_and_malformed_geometry_return_defaults()
    {
        using var value = new PListValue("not geometry");
        Assert.Equal(0, value.Int);
        Assert.False(value.Bool);
        Assert.Equal(0.0f, value.Float);
        Assert.Equal(default, value.IntRect);
        Assert.Equal(default, value.IntVector2);
        Assert.Equal(default, value.IntVector3);

        value.SetValueVector([new PListValue(1), new PListValue("two")]);
        using var clone = (PListValue)value.Clone();
        value.ValueVector[0].SetInt(9);

        Assert.Equal(1, clone.ValueVector[0].Int);
        value.Dispose();
        value.Dispose();
        Assert.Equal(PListValueType.None, value.Type);
    }

    private sealed class TrackingResource(bool beginResult, bool endResult) : Resource
    {
        public bool BeginCalled { get; private set; }
        public bool EndCalled { get; private set; }

        public override bool BeginLoad(Deserializer source)
        {
            BeginCalled = true;
            return beginResult;
        }

        public override bool EndLoad()
        {
            EndCalled = true;
            return endResult;
        }
    }
}
