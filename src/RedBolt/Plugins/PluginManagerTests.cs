// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.IO;
using RedBolt.Glow.Editor;
using RedBolt.IK;
using RedBolt.IK.Editor;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.Plugins;
using RedBolt.Resource;
using RedBolt.Utility;
using RedBolt.RmlUI.Editor;
using RedBolt.PhysicsJolt;
using RedBolt.UserCode.Desktop;
using File = global::System.IO.File;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PluginManagerTests
{
    public PluginManagerTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
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

        var first = Assert.IsType<ProbeApplication>(manager.GetPluginApplication("First", true));
        Assert.Equal(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Resources")),
            first.GetPluginPath("Resources"));
        Assert.Throws<ArgumentException>(() => first.GetPluginPath(Path.GetPathRoot(AppContext.BaseDirectory)!));

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
        Assert.True(descriptor.AutoLoad);
        Assert.Equal(Path.GetFullPath(assemblyPath), descriptor.AssemblyPath);
    }

    [Fact]
    public void Catalog_expands_executable_directory_in_plugin_paths()
    {
        var sourcePath = typeof(RedBolt.UserCode.TestPlugin.TestPluginApplication).Assembly.Location;
        var directoryName = "plugin-path-" + Guid.NewGuid().ToString("N");
        var pluginDirectory = Path.Combine(AppContext.BaseDirectory, directoryName);
        Directory.CreateDirectory(pluginDirectory);
        try
        {
            var pluginPath = Path.Combine(pluginDirectory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, pluginPath);
            var catalog = new ManagedPluginCatalog([$"{{exeDir}}/{directoryName}"]);

            var descriptor = Assert.Single(catalog.Scan());

            Assert.Equal("RedBolt.Test.Reloadable", descriptor.Name);
            Assert.Equal(Path.GetFullPath(pluginPath), descriptor.AssemblyPath);
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    [Fact]
    public void Catalog_discovers_reflected_module_without_plugin_entry_point()
    {
        var sourcePath = typeof(Physics).Assembly.Location;
        var tempDirectory = Directory.CreateTempSubdirectory("RedBoltModuleCatalog");
        try
        {
            var modulePath = Path.Combine(tempDirectory.FullName, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, modulePath);
            var catalog = new ManagedPluginCatalog([tempDirectory.FullName]);

            var descriptor = Assert.Single(catalog.Scan());

            Assert.Equal("RedBolt.PhysicsJolt", descriptor.Name);
            Assert.Equal(PluginReloadPolicy.ProcessResident, descriptor.ReloadPolicy);
            Assert.True(descriptor.AutoLoad);
            Assert.True(descriptor.IsImplicitModule);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [NativeBindingFact]
    public void RmlUI_companion_auto_loads_and_unloads_through_standard_plugin_manager()
    {
        var assemblyPath = typeof(RmlUIEditorPlugin).Assembly.Location;
        var editorPlugins = RedBolt.Core.Context.Instance.GetSubsystem<EditorPluginManager>()!;
        using (var manager = new PluginManager())
        {
            new ManagedPluginCatalog([Path.GetDirectoryName(assemblyPath)!]).Populate(manager);
            Assert.Contains("RedBolt.RmlUI.Editor", manager.EnumerateAvailablePlugins());
            Assert.True(manager.IsPluginAutoLoaded("RedBolt.RmlUI.Editor"));

            manager.Commit();
            Assert.Empty(manager.LoadedPlugins);
            Assert.NotNull(manager.GetPluginApplication("RedBolt.RmlUI.Editor", true));
            Assert.NotNull(RedBolt.Core.Context.Instance.GetSubsystem<RedBolt.RmlUI.RmlUI>());
            Assert.Contains(editorPlugins.Plugins, static plugin => plugin.Name == "RmlUI.ReloadFonts");
            Assert.Contains(editorPlugins.Plugins, static plugin => plugin.Name == "RmlUI.GameView");
        }

        Assert.Null(RedBolt.Core.Context.Instance.GetSubsystem<RedBolt.RmlUI.RmlUI>());
        Assert.DoesNotContain(editorPlugins.Plugins, static plugin => plugin.Name == "RmlUI.ReloadFonts");
        Assert.DoesNotContain(editorPlugins.Plugins, static plugin => plugin.Name == "RmlUI.GameView");
    }

    [Fact]
    public void IK_companion_auto_loads_runtime_and_retargeting_reflection()
    {
        var assemblyPath = typeof(IKEditorPlugin).Assembly.Location;
        var editorPlugins = RedBolt.Core.Context.Instance.GetSubsystem<EditorPluginManager>()!;
        using (var manager = new PluginManager())
        {
            var catalog = new ManagedPluginCatalog([Path.GetDirectoryName(assemblyPath)!]);
            var descriptor = Assert.Single(catalog.Scan(),
                static candidate => candidate.Name == "RedBolt.IK.Editor");
            Assert.True(descriptor.AutoLoad);
            Assert.Equal(PluginReloadPolicy.ProcessResident, descriptor.ReloadPolicy);

            catalog.Populate(manager);
            manager.Commit();

            Assert.Empty(manager.LoadedPlugins);
            Assert.NotNull(manager.GetPluginApplication("RedBolt.IK.Editor", true));
            Assert.Contains(editorPlugins.Plugins,
                static plugin => plugin.Name == "IK.DefaultRetargetTransformer");
            using var solver = Assert.IsType<IKSolver>(
                RedBolt.Core.Context.Instance.CreateObject(nameof(IKSolver)));
            using var transformer = Assert.IsType<RedBolt.Utility.RetargetAnimationsTransformer>(
                RedBolt.Core.Context.Instance.CreateObject(nameof(RedBolt.Utility.RetargetAnimationsTransformer)));
        }

        Assert.DoesNotContain(editorPlugins.Plugins,
            static plugin => plugin.Name == "IK.DefaultRetargetTransformer");
    }

    [Fact]
    public void Glow_companion_auto_loads_declarative_editor_extension()
    {
        var assemblyPath = typeof(GlowEditorPlugin).Assembly.Location;
        var editorPlugins = RedBolt.Core.Context.Instance.GetSubsystem<EditorPluginManager>()!;
        using (var manager = new PluginManager())
        {
            new ManagedPluginCatalog([Path.GetDirectoryName(assemblyPath)!]).Populate(manager);
            manager.Commit();

            var application = Assert.IsAssignableFrom<PluginApplication>(
                manager.GetPluginApplication("RedBolt.Glow.Editor", true));
            Assert.Equal(Path.GetFullPath(Path.GetDirectoryName(assemblyPath)!), application.PluginDirectory);
            Assert.Contains(editorPlugins.Plugins,
                static plugin => plugin.Name == "Glow.DefaultLightmapUVTransformer");
        }

        Assert.DoesNotContain(editorPlugins.Plugins,
            static plugin => plugin.Name == "Glow.DefaultLightmapUVTransformer");
    }

    [Fact]
    public void IK_companion_contributes_retargeting_to_default_asset_pipeline()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltIKPlugin-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        using (var pipeline = new AssetPipeline())
            Assert.True(pipeline.SaveFile(new FileIdentifier(Path.Combine(dataPath, "Default.assetpipeline"))));

        var engine = _fixture.Context.GetSubsystem<Engine>()!;
        var previousPluginPaths = engine.Parameters.PluginPaths;
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();
        Project? project = null;
        try
        {
            // The application directory is always scanned for plugins; adding it again would report the same
            // plugin twice, which happens when the libraries sit beside the tests (the coverage output).
            var companionDirectory = Path.GetFullPath(Path.GetDirectoryName(typeof(IKEditorPlugin).Assembly.Location)!);
            var applicationDirectory = Path.GetFullPath(AppContext.BaseDirectory);
            engine.Parameters.PluginPaths = string.Equals(
                Path.TrimEndingDirectorySeparator(companionDirectory),
                Path.TrimEndingDirectorySeparator(applicationDirectory), StringComparison.Ordinal)
                ? string.Empty
                : companionDirectory;
            project = new Project(projectPath, settingsPath, ProjectFlag.SingleProcess);

            using var pipeline = new AssetPipeline();
            Assert.True(pipeline.LoadFile(new FileIdentifier(Path.Combine(dataPath, "Default.assetpipeline"))));
            Assert.Contains(pipeline.GetTransformers(),
                static transformer => transformer is RedBolt.Utility.RetargetAnimationsTransformer);
            Assert.Empty(project.PluginManager.LoadedPlugins);
        }
        finally
        {
            engine.Parameters.PluginPaths = previousPluginPaths;
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            Directory.Delete(projectPath, true);
        }
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

    private readonly HeadlessEngineFixture _fixture;
}
