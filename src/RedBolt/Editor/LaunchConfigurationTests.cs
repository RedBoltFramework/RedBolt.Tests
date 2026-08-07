namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Project;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LaunchConfigurationTests
{
    [Fact]
    public void Default_configuration_serializes_as_empty_and_missing_values_reset_to_defaults()
    {
        using var source = new LaunchConfiguration();
        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("Configuration"))
            source.SerializeInBlock(output);

        var json = output.ToJsonString();
        Assert.DoesNotContain("Name", json);
        Assert.DoesNotContain("MainPlugin", json);
        Assert.DoesNotContain("EngineParameters", json);

        using var destination = new LaunchConfiguration("Old", "OldPlugin");
        using (var value = new Variant(true))
            destination.EngineParameters.Add("Headless", value);
        using var input = new TestJSONInputArchive(json);
        using (input.OpenUnorderedBlock("Configuration"))
            destination.SerializeInBlock(input);

        Assert.Empty(destination.Name);
        Assert.Empty(destination.MainPlugin);
        Assert.Empty(destination.EngineParameters);
    }

    [Fact]
    public void Configuration_clone_and_archive_roundtrip_preserve_independent_values()
    {
        using var source = new LaunchConfiguration("Play", "MainPlugin");
        using (var value = new Variant(true))
            source.EngineParameters.Add("Headless", value);
        var sourceHash = source.ToHash();
        using var clone = source.Clone();

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("Configuration"))
            source.SerializeInBlock(output);
        using var loaded = new LaunchConfiguration();
        using var input = new TestJSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("Configuration"))
            loaded.SerializeInBlock(input);

        Assert.Equal("Play", loaded.Name);
        Assert.Equal("MainPlugin", loaded.MainPlugin);
        Assert.True(loaded.EngineParameters["Headless"].GetBool());
        Assert.Equal(sourceHash, loaded.ToHash());

        source.Name = "Changed";
        source.MainPlugin = string.Empty;
        using (var value = new Variant(false))
            source.EngineParameters["Headless"] = value;

        Assert.Equal("Play", clone.Name);
        Assert.Equal("MainPlugin", clone.MainPlugin);
        Assert.True(clone.EngineParameters["Headless"].GetBool());
        Assert.Equal(sourceHash, clone.ToHash());
        Assert.NotEqual(sourceHash, source.ToHash());
    }
}
