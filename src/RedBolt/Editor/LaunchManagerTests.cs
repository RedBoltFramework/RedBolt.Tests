// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Project;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LaunchManagerTests
{
    [Fact]
    public void Add_configuration_copies_value_and_find_returns_stored_configuration()
    {
        using var manager = new LaunchManager();
        using var source = new LaunchConfiguration("Play", "MainPlugin");
        using (var value = new Variant(true))
            source.EngineParameters.Add("Headless", value);
        manager.AddConfiguration(source);

        source.Name = "Changed";
        source.MainPlugin = string.Empty;
        using (var value = new Variant(false))
            source.EngineParameters["Headless"] = value;

        var stored = Assert.IsType<LaunchConfiguration>(manager.FindConfiguration("Play"));
        Assert.Same(manager.Configurations[0], stored);
        Assert.Equal("MainPlugin", stored.MainPlugin);
        Assert.True(stored.EngineParameters["Headless"].GetBool());
        Assert.True(manager.HasConfiguration("Play"));
        Assert.False(manager.HasConfiguration("Missing"));

        manager.MutableConfigurations[0].Name = "Renamed";
        Assert.Null(manager.FindConfiguration("Play"));
        Assert.Same(stored, manager.FindConfiguration("Renamed"));
    }

    [Fact]
    public void Sorted_configurations_are_unique_and_remove_configuration_erases_index()
    {
        using var manager = new LaunchManager();
        using (var configuration = new LaunchConfiguration("B", string.Empty))
            manager.AddConfiguration(configuration);
        using (var configuration = new LaunchConfiguration("A", string.Empty))
            manager.AddConfiguration(configuration);
        using (var configuration = new LaunchConfiguration("B", string.Empty))
            manager.AddConfiguration(configuration);

        Assert.Equal(new[] { "A", "B" }, manager.GetSortedConfigurations());

        manager.RemoveConfiguration(0);
        Assert.Equal(2, manager.Configurations.Count);
        Assert.Equal("A", manager.Configurations[0].Name);
        Assert.Equal("B", manager.Configurations[1].Name);
    }

    [Fact]
    public void Configurations_archive_roundtrip_and_missing_block_reset_values()
    {
        using var source = new LaunchManager();
        using (var first = new LaunchConfiguration("Play", "MainPlugin"))
        {
            using var value = new Variant(true);
            first.EngineParameters.Add("Headless", value);
            source.AddConfiguration(first);
        }
        using (var second = new LaunchConfiguration("Edit", string.Empty))
            source.AddConfiguration(second);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("Manager"))
            source.SerializeInBlock(output);

        using var destination = new LaunchManager();
        using (var obsolete = new LaunchConfiguration("Obsolete", string.Empty))
            destination.AddConfiguration(obsolete);
        using var input = new TestJSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("Manager"))
            destination.SerializeInBlock(input);

        Assert.Equal(2, destination.Configurations.Count);
        Assert.Equal("Play", destination.Configurations[0].Name);
        Assert.Equal("MainPlugin", destination.Configurations[0].MainPlugin);
        Assert.True(destination.Configurations[0].EngineParameters["Headless"].GetBool());
        Assert.Equal("Edit", destination.Configurations[1].Name);

        using var missingInput = new TestJSONInputArchive("{\"Manager\":{}}");
        using (missingInput.OpenUnorderedBlock("Manager"))
            destination.SerializeInBlock(missingInput);
        Assert.Empty(destination.Configurations);
    }
}
