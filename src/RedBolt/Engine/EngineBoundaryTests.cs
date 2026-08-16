// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineBoundaryTests
{
    private readonly HeadlessEngineFixture _fixture;

    public EngineBoundaryTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Config_variable_definition_clone_is_independent_and_preserves_flags()
    {
        using var source = new Variant(new byte[] { 1, 2 });
        using var definition = new ConfigVariableDefinition();
        definition.SetDefault(source).Overridable().CommandLinePriority();

        using var clone = definition.Clone();
        clone.DefaultValue.GetBuffer()[0] = 9;

        Assert.True(clone.IsOverridable);
        Assert.True(clone.HasCommandLinePriority);
        Assert.Equal(1, definition.DefaultValue.GetBuffer()[0]);
        Assert.Equal(definition.Type, clone.Type);
    }

    [Fact]
    public void Config_file_rejects_missing_and_malformed_defaults_without_mutating_values()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        using var mount = new MountedExternalMemory("engine-boundary");
        fileSystem.Mount(mount);
        mount.LinkMemory("missing-default.json", "{}");
        mount.LinkMemory("malformed-default.json", "{not json");

        try
        {
            using var config = new ConfigFile();
            config.DefineVariable("Value", new Variant(17));

            Assert.False(config.LoadDefaults("engine-boundary://not-present.json", ApplicationFlavor.Empty));
            Assert.False(config.LoadDefaults("engine-boundary://malformed-default.json", ApplicationFlavor.Empty));
            Assert.Equal(17, config.GetVariable("Value").GetInt());
        }
        finally
        {
            fileSystem.Unmount(mount);
        }
    }

    [Fact]
    public void Null_state_enqueue_is_ignored_and_does_not_start_a_transition()
    {
        using var engine = new Engine();
        var manager = engine.GetSubsystem<StateManager>();
        Assert.NotNull(manager);

        manager!.Reset();
        manager.EnqueueState((ApplicationState?)null);

        Assert.Null(manager.State);
        Assert.Equal(StringHash.Empty, manager.TargetState);
    }

    [Fact]
    public void Application_flavor_empty_and_universal_values_are_clone_safe()
    {
        var empty = ApplicationFlavor.Empty;
        var universal = ApplicationFlavor.Universal;
        empty.Components["unexpected"] = new ApplicationFlavorComponent { "tag" };
        universal.Components["*"].Add("unexpected");

        Assert.True(ApplicationFlavor.Empty.Components.Count == 0);
        Assert.DoesNotContain("unexpected", ApplicationFlavor.Universal.Components["*"]);
        Assert.Equal(0u, ApplicationFlavor.Empty.Matches(new ApplicationFlavorPattern()));
    }
}
