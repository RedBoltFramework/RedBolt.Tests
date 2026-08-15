namespace RedBolt.Tests;

using RedBolt.IO;
using RedBolt.Plugins;
using RedBolt.RmlUI.Editor;
using RedBolt.UserCode.Desktop;
using File = global::System.IO.File;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PluginManagerTests
{
    public PluginManagerTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Static_plugins_follow_ordered_lifecycle()
    {
        ProbeApplication.Calls.Clear();
        using var manager = new PluginManager();
        Assert.True(manager.AddStaticPlugin("First", () => new ProbeApplication("First")));
        Assert.True(manager.AddStaticPlugin("Second", () => new ProbeApplication("Second")));
        manager.SetPluginsLoaded(["First", "Second"]);
        manager.Commit();

        manager.StartApplication();
        manager.Commit();
        manager.StopApplication();
        manager.Commit();

        Assert.Equal([
            "First.Load", "Second.Load",
            "First.Start", "Second.Start",
            "Second.Stop", "First.Stop"
        ], ProbeApplication.Calls);
    }

    [Fact]
    public void Catalog_reads_RmlUI_companion_metadata_without_loading_it()
    {
        var assemblyPath = typeof(RmlUIEditorPlugin).Assembly.Location;
        var catalog = new ManagedPluginCatalog([Path.GetDirectoryName(assemblyPath)!]);

        var descriptor = Assert.Single(catalog.Scan(),
            static candidate => candidate.Name == "RedBolt.RmlUI.Editor");

        Assert.Equal(PluginReloadPolicy.ProcessResident, descriptor.ReloadPolicy);
        Assert.Equal(Path.GetFullPath(assemblyPath), descriptor.AssemblyPath);
    }

    [NativeBindingFact]
    public void RmlUI_companion_loads_and_unloads_through_standard_plugin_manager()
    {
        var assemblyPath = typeof(RmlUIEditorPlugin).Assembly.Location;
        using var manager = new PluginManager();
        new ManagedPluginCatalog([Path.GetDirectoryName(assemblyPath)!]).Populate(manager);
        Assert.Contains("RedBolt.RmlUI.Editor", manager.EnumerateAvailablePlugins());

        manager.SetPluginsLoaded(["RedBolt.RmlUI.Editor"]);
        manager.Commit();
        Assert.NotNull(manager.GetPluginApplication("RedBolt.RmlUI.Editor", true));
        Assert.NotNull(RedBolt.Core.Context.Instance.GetSubsystem<RedBolt.RmlUI.RmlUI>());

        manager.SetPluginsLoaded([]);
        manager.Commit();
        Assert.Null(RedBolt.Core.Context.Instance.GetSubsystem<RedBolt.RmlUI.RmlUI>());
    }

    [Fact]
    public void Collectible_plugin_reloads_when_its_assembly_changes()
    {
        var sourcePath = typeof(RedBolt.UserCode.TestPlugin.TestPluginApplication).Assembly.Location;
        var tempDirectory = Directory.CreateTempSubdirectory("RedBoltPluginReload");
        try
        {
            var pluginPath = Path.Combine(tempDirectory.FullName, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, pluginPath);
            using var manager = new PluginManager();
            new ManagedPluginCatalog([tempDirectory.FullName]).Populate(manager);
            manager.SetPluginsLoaded(["RedBolt.Test.Reloadable"]);
            manager.Commit();
            var first = manager.GetPluginApplication("RedBolt.Test.Reloadable", true);
            Assert.NotNull(first);

            File.SetLastWriteTimeUtc(pluginPath, DateTime.UtcNow.AddSeconds(2));
            manager.Reload();
            manager.Commit();

            var second = manager.GetPluginApplication("RedBolt.Test.Reloadable", true);
            Assert.NotNull(second);
            Assert.NotSame(first, second);

            File.Copy(typeof(RmlUIEditorPlugin).Assembly.Location, pluginPath, true);
            File.SetLastWriteTimeUtc(pluginPath, DateTime.UtcNow.AddSeconds(4));
            manager.Reload();
            manager.Commit();

            Assert.Same(second, manager.GetPluginApplication("RedBolt.Test.Reloadable", true));
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    private sealed class ProbeApplication(string name) : PluginApplication
    {
        public static List<string> Calls { get; } = [];

        protected override void Load() => Calls.Add($"{name}.Load");

        protected override void Unload() => Calls.Add($"{name}.Unload");

        protected override void Start(bool isMain) => Calls.Add($"{name}.Start");

        protected override void Stop() => Calls.Add($"{name}.Stop");

        protected override void Suspend(Archive archive) { }
    }
}
