// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ConfigFileCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ConfigFileCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Config_file_archive_input_loads_valid_and_empty_flavors()
    {
        const string json = """
            {
              "Default": [
                {
                  "Flavor": [
                    { "key": "platform", "tags": ["desktop", "linux"] },
                    { "key": "quality", "tags": ["high"] }
                  ],
                  "Variables": [
                    { "key": "Width", "type": "Int", "value": 1280 },
                    { "key": "Label", "type": "String", "value": "headless" }
                  ]
                },
                { "Flavor": [], "Variables": [] }
              ]
            }
            """;

        using var config = new ConfigFile();
        using var input = new TestJSONInputArchive(json);
        using (input.OpenUnorderedBlock("config"))
            config.SerializeInBlock(input);

        Assert.Equal(2, config.VariablesPerFlavor.Count);
        Assert.Equal(["desktop", "linux"],
            config.VariablesPerFlavor[0].Flavor.Components["platform"].OrderBy(static tag => tag));
        Assert.Equal(1280, config.VariablesPerFlavor[0].Variables["Width"].GetInt());
        Assert.Equal("headless", config.VariablesPerFlavor[0].Variables["Label"].GetString());
        Assert.Empty(config.VariablesPerFlavor[1].Flavor.Components);
        Assert.Empty(config.VariablesPerFlavor[1].Variables);
    }

    [Fact]
    public void Config_flavor_archive_input_reinitializes_null_maps_and_skips_empty_keys()
    {
        const string json = """
            {
              "Flavor": [
                { "key": "", "tags": ["ignored"] },
                { "key": "platform", "tags": ["desktop", "", "linux"] }
              ],
              "Variables": [
                { "key": "", "type": "Int", "value": 1 },
                { "key": "Count", "type": "Int", "value": 9 }
              ]
            }
            """;

        var flavor = default(ConfigFile.ConfigFlavor);
        using (var input = new TestJSONInputArchive(json))
        using (input.OpenUnorderedBlock("flavor"))
            flavor.SerializeInBlock(input);

        Assert.Single(flavor.Flavor.Components!);
        Assert.Equal(["", "desktop", "linux"],
            flavor.Flavor.Components!["platform"].OrderBy(static tag => tag));
        Assert.Single(flavor.Variables!);
        Assert.Equal(9, flavor.Variables!["Count"].GetInt());

        flavor.Dispose();
    }

    [Fact]
    public void Config_file_archive_input_replaces_old_flavors_and_ignores_empty_entries()
    {
        using var config = new ConfigFile();
        config.VariablesPerFlavor.Add(CreateFlavor("old=value", "Old", new Variant(1)));

        const string json = """
            {
              "Default": [
                {
                  "Flavor": [
                    { "key": "platform", "tags": ["desktop"] },
                    { "key": "", "tags": ["ignored"] }
                  ],
                  "Variables": [
                    { "key": "", "type": "String", "value": "ignored" },
                    { "key": "Name", "type": "String", "value": "loaded" }
                  ]
                },
                { "Flavor": [], "Variables": [] }
              ]
            }
            """;

        using var input = new TestJSONInputArchive(json);
        using (input.OpenUnorderedBlock("config"))
            config.SerializeInBlock(input);

        Assert.Equal(2, config.VariablesPerFlavor.Count);
        Assert.DoesNotContain("old=value", config.VariablesPerFlavor[0].Flavor.Components.Keys);
        Assert.Equal("loaded", config.VariablesPerFlavor[0].Variables["Name"].GetString());
        Assert.Single(config.VariablesPerFlavor[0].Flavor.Components);
        Assert.Empty(config.VariablesPerFlavor[1].Variables);
    }

    [Fact]
    public void Config_file_archive_output_round_trips_flavors_and_variables()
    {
        using var config = new ConfigFile();
        config.VariablesPerFlavor.Add(new ConfigFile.ConfigFlavor
        {
            Flavor = new ApplicationFlavorPattern("platform=desktop;quality=high"),
            Variables = new StringVariantMap
            {
                ["Value"] = new Variant(17),
                ["Name"] = new Variant("output")
            }
        });

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("config"))
            config.SerializeInBlock(output);

        using var restored = new ConfigFile();
        using var input = new TestJSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("config"))
            restored.SerializeInBlock(input);

        Assert.Single(restored.VariablesPerFlavor);
        Assert.Equal(["desktop"], restored.VariablesPerFlavor[0].Flavor.Components["platform"]);
        Assert.Equal(["high"], restored.VariablesPerFlavor[0].Flavor.Components["quality"]);
        Assert.Equal(17, restored.VariablesPerFlavor[0].Variables["Value"].GetInt());
        Assert.Equal("output", restored.VariablesPerFlavor[0].Variables["Name"].GetString());
    }

    [Fact]
    public void Config_flavor_clone_and_default_dispose_cover_owned_and_uninitialized_maps()
    {
        var source = new ConfigFile.ConfigFlavor
        {
            Flavor = new ApplicationFlavorPattern("platform=desktop"),
            Variables = new StringVariantMap { ["Value"] = new Variant(3) }
        };
        var clone = source.Clone();
        var boxedClone = ((ICloneable)source).Clone();

        Assert.NotSame(source.Variables, clone.Variables);
        Assert.Equal(3, clone.Variables["Value"].GetInt());
        Assert.IsType<ConfigFile.ConfigFlavor>(boxedClone);
        clone.Variables["Value"].Dispose();
        source.Dispose();
        clone.Dispose();

        var uninitialized = default(ConfigFile.ConfigFlavor);
        using (var output = new TestJSONOutputArchive())
        using (output.OpenUnorderedBlock("flavor"))
            uninitialized.SerializeInBlock(output);
        uninitialized.Dispose();
    }

    [Fact]
    public void Config_file_load_defaults_covers_invalid_shapes_and_valid_flavor_variables()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var mount = new MountedExternalMemory("config-gap-defaults");
        fileSystem.Mount(mount);
        mount.LinkMemory("malformed.json", "{not json");
        mount.LinkMemory("wrong-root.json", "[]");
        mount.LinkMemory("missing-default.json", "{}");
        mount.LinkMemory("valid.json", """
            {
              "Default": [
                { "Flavor": 7, "Variables": 7 },
                {
                  "Flavor": [
                    7,
                    { "tags": [null] },
                    { "key": "", "tags": ["ignored"] },
                    { "key": "platform", "tags": ["desktop", "", "linux"] },
                    { "key": "quality", "tags": "not-an-array" }
                  ],
                  "Variables": [
                    7,
                    { "type": "Int", "value": 2 },
                    { "key": "", "type": "String", "value": "ignored" },
                    { "key": "Value", "type": "Int", "value": 17 }
                  ]
                },
                { "Flavor": [], "Variables": [] }
              ]
            }
            """);

        try
        {
            using var config = new ConfigFile();
            config.VariablesPerFlavor.Add(CreateFlavor("old=value", "Old", new Variant(99)));

            Assert.False(config.LoadDefaults("config-gap-defaults://missing.json", ApplicationFlavor.Empty));
            Assert.False(config.LoadDefaults("config-gap-defaults://malformed.json", ApplicationFlavor.Empty));
            Assert.False(config.LoadDefaults("config-gap-defaults://wrong-root.json", ApplicationFlavor.Empty));
            Assert.False(config.LoadDefaults("config-gap-defaults://missing-default.json", ApplicationFlavor.Empty));
            Assert.Single(config.VariablesPerFlavor);

            Assert.True(config.LoadDefaults(
                "config-gap-defaults://valid.json",
                ApplicationFlavor.Platform));

            Assert.Equal(3, config.VariablesPerFlavor.Count);
            using var matchingFlavorVariables = config.GetFlavorVariables(
                new ApplicationFlavor("platform=desktop,linux;quality=high"));
            Assert.Equal(17, matchingFlavorVariables["Value"].GetInt());
            Assert.Equal(["desktop", "linux"],
                config.VariablesPerFlavor[1].Flavor.Components["platform"].OrderBy(static tag => tag));
            Assert.Contains("quality", config.VariablesPerFlavor[1].Flavor.Components.Keys);
            Assert.Empty(config.VariablesPerFlavor[1].Flavor.Components["quality"]);
            Assert.DoesNotContain("", config.VariablesPerFlavor[1].Variables.Keys);
        }
        finally
        {
            fileSystem.Unmount(mount);
        }
    }

    [Fact]
    public void Config_file_load_overrides_covers_missing_invalid_and_rejected_values()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var mount = new MountedExternalMemory("config-gap-overrides");
        fileSystem.Mount(mount);
        mount.LinkMemory("malformed.json", "{not json");
        mount.LinkMemory("object.json", """
            {
              "Value": { "type": "Int", "value": 8 },
              "Fixed": { "type": "Int", "value": 9 },
              "WrongType": { "type": "String", "value": "wrong" },
              "Unknown": { "type": "Int", "value": 10 },
              "Ignored": 7
            }
            """);
        mount.LinkMemory("array.json", "[]");

        try
        {
            using var config = new ConfigFile();
            config.DefineVariable("Value", new Variant(1)).Overridable();
            config.DefineVariable("Fixed", new Variant(2));
            config.DefineVariable("WrongType", new Variant(3)).Overridable();

            Assert.False(config.LoadOverrides("config-gap-overrides://missing.json"));
            Assert.False(config.LoadOverrides("config-gap-overrides://malformed.json"));
            Assert.True(config.LoadOverrides("config-gap-overrides://object.json"));
            Assert.Equal(8, config.GetVariable("Value").GetInt());
            Assert.Equal(2, config.GetVariable("Fixed").GetInt());
            Assert.Equal(3, config.GetVariable("WrongType").GetInt());
            Assert.True(config.LoadOverrides("config-gap-overrides://array.json"));
            Assert.Equal(8, config.GetVariable("Value").GetInt());
        }
        finally
        {
            fileSystem.Unmount(mount);
        }
    }

    [Fact]
    public void Engine_config_and_parameter_edges_work_without_graphics()
    {
        using (var uninitialized = new Engine())
        {
            uninitialized.LoadConfigFiles();
            uninitialized.SaveConfigFile();

            Assert.Throws<ArgumentNullException>(() => Engine.DefineParameters(null!, new EngineParameters()));
            Assert.Throws<ArgumentNullException>(() =>
                Engine.DefineParameters(new CommandLineParser("ConfigFileCoverageGapTests"), null!));
            Assert.Throws<ArgumentException>(() => uninitialized.SetParameter(" ", 1));
            Assert.Throws<ArgumentException>(() => uninitialized.GetParameter(" "));
        }

        var preferencesDirectory = Path.Combine(
            Path.GetTempPath(), $"redbolt-config-gap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(preferencesDirectory);
        try
        {
            using var engine = new Engine();
            Assert.True(engine.Initialize(new EngineParameters
            {
                ApplicationName = "RedBolt.Tests.ConfigFileCoverageGap",
                ApplicationPreferencesDir = preferencesDirectory + Path.DirectorySeparatorChar,
                ConfigName = "EngineConfig.json",
                Headless = true,
                LogName = "none://"
            }));

            engine.SetParameter(EngineParameter.FullScreen, true);
            engine.SetParameter(EngineParameter.RenderAdapterId, null);
            engine.SetParameter(EngineParameter.FrameLimiter, 0);
            engine.SetParameter("CustomNull", null);

            Assert.True(engine.HasParameter(EngineParameter.FullScreen));
            Assert.Null(engine.GetParameter(EngineParameter.RenderAdapterId));
            Assert.False((bool)engine.GetParameter(EngineParameter.FrameLimiter)!);
            Assert.True(engine.HasParameter("CustomNull"));
            Assert.Null(engine.GetParameter("CustomNull"));
            Assert.False(engine.HasParameter("NotAParameter"));
            Assert.Null(engine.GetParameter("NotAParameter"));
            Assert.Throws<InvalidCastException>(() =>
                engine.SetParameter(EngineParameter.WindowWidth, null));

            engine.SaveConfigFile();
            var configPath = Path.Combine(preferencesDirectory, "EngineConfig.json");
            Assert.True(System.IO.File.Exists(configPath));
            Assert.Contains("FullScreen", System.IO.File.ReadAllText(configPath), StringComparison.Ordinal);

            engine.SetParameter(EngineParameter.FullScreen, false);
            engine.LoadConfigFiles();
            Assert.True((bool)engine.GetParameter(EngineParameter.FullScreen)!);
        }
        finally
        {
            if (Directory.Exists(preferencesDirectory))
                Directory.Delete(preferencesDirectory, recursive: true);
        }
    }

    [Fact]
    public void Engine_command_line_rejects_invalid_config_and_parameter_values()
    {
        var parser = new CommandLineParser("RedBolt.ConfigFileCoverageGapTests");
        var parameters = new EngineParameters();
        Engine.DefineParameters(parser, parameters);

        var invalidConfig = parser.Parse(["--config-name"]);
        Assert.False(invalidConfig.IsSuccess);

        parser = new CommandLineParser("RedBolt.ConfigFileCoverageGapTests");
        Engine.DefineParameters(parser, parameters);
        var invalidNumber = parser.Parse(["--monitor=not-a-number"]);
        Assert.False(invalidNumber.IsSuccess);

        parser = new CommandLineParser("RedBolt.ConfigFileCoverageGapTests");
        Engine.DefineParameters(parser, parameters);
        var validAliases = parser.Parse(["--cn=Config.json", "--af=0", "--windowed", "--opengl"]);
        Assert.True(validAliases.IsSuccess, validAliases.ErrorMessage);
        Assert.Equal("Config.json", parameters.ConfigName);
        Assert.Equal(TextureFilterMode.Anisotropic, parameters.TextureFilterMode);
        Assert.Equal(1, parameters.TextureAnisotropy);
        Assert.False(parameters.Borderless);
    }

    private static ConfigFile.ConfigFlavor CreateFlavor(string pattern, string name, Variant value)
    {
        return new ConfigFile.ConfigFlavor
        {
            Flavor = new ApplicationFlavorPattern(pattern),
            Variables = new StringVariantMap
            {
                [name] = value
            }
        };
    }
}
