// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using RedBolt.Core;
using RedBolt.UserCode;
using RedBolt.UserCode.Desktop;
using RedBolt.UserCode.TestPlugin;

public sealed class UserCodeBackendTests
{
    [Fact]
    public void Precompiled_script_backend_loads_once_and_detects_same_bundle()
    {
        using var backend = new PrecompiledScriptBackend();
        var input = new PrecompiledScriptBundle(typeof(UserCodeBackendTests).Assembly);
        using (var prepared = backend.Prepare(input))
        {
            Assert.True(prepared.Succeeded);
            Assert.False(prepared.Unchanged);
            Assert.Same(typeof(UserCodeBackendTests).Assembly, backend.Commit(prepared).Assembly);
        }

        using var unchanged = backend.Prepare(input);
        Assert.True(unchanged.Succeeded);
        Assert.True(unchanged.Unchanged);
    }

    [Fact]
    public void Reloadable_scripts_keep_last_good_bundle_and_skip_unchanged_content()
    {
        using var backend = new ReloadableScriptBackend();
        var valid = Bundle("public static class ScriptValue { public static int Get() => 7; }");
        using (var prepared = backend.Prepare(valid))
        {
            Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
            var loaded = backend.Commit(prepared);
            Assert.Equal(7, InvokeValue(loaded));
        }

        var current = backend.Current;
        using (var invalid = backend.Prepare(Bundle("public static class ScriptValue { this is invalid; }")))
        {
            Assert.False(invalid.Succeeded);
            Assert.Contains(invalid.Diagnostics, static diagnostic =>
                diagnostic.Severity == UserCodeDiagnosticSeverity.Error && diagnostic.Path == "Scripts/Main.cs");
            Assert.Same(current, backend.Current);
            Assert.Equal(7, InvokeValue(backend.Current!));
        }

        using var unchanged = backend.Prepare(valid);
        Assert.True(unchanged.Unchanged);
    }

    [Fact]
    public void Reloadable_scripts_replace_scoped_reflection_with_same_type_name()
    {
        using var context = new Context();
        using var backend = new ReloadableScriptBackend();

        using (var first = backend.Prepare(ReflectedBundle(1)))
        {
            Assert.True(first.Succeeded, Format(first.Diagnostics));
            Assert.Null(context.GetReflection("ReloadedObject"));
            var loaded = backend.Commit(first);
            loaded.Register(context);
            AssertReflectionValue(context, loaded, 1);
        }

        var old = backend.Current!;
        using var second = backend.Prepare(ReflectedBundle(2));
        Assert.True(second.Succeeded, Format(second.Diagnostics));

        old.Unregister();
        second.Candidate!.Register(context);
        backend.Commit(second);

        AssertReflectionValue(context, backend.Current!, 2);
    }

    [Fact]
    public void Reloadable_script_context_is_collectible_after_teardown()
    {
        var reference = CreateAndUnloadGeneration();
        for (var iteration = 0; reference.IsAlive && iteration < 10; ++iteration)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive);
    }

    [Fact]
    public void Script_bundle_validation_rejects_non_script_and_duplicate_paths()
    {
        using var backend = new ReloadableScriptBackend();
        var input = new SourceScriptBundle("InvalidPaths",
        [
            new ScriptSource("Scripts/One.txt", ""),
            new ScriptSource("Scripts/Two.cs", ""),
            new ScriptSource("Scripts/Two.cs", ""),
        ]);

        using var prepared = backend.Prepare(input);
        Assert.False(prepared.Succeeded);
        Assert.Equal(2, prepared.Diagnostics.Count);
    }

    [Fact]
    public void Desktop_assembly_backend_loads_without_locking_and_shares_RedBolt()
    {
        using var backend = new ReloadableAssemblyBackend();
        var path = typeof(ReloadableScriptBackend).Assembly.Location;
        using var prepared = backend.Prepare(new DesktopAssemblyInput(path));
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        Assert.True(prepared.Candidate!.IsCollectible);
        Assert.NotSame(typeof(ReloadableScriptBackend).Assembly, prepared.Candidate.Assembly);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
        }

        backend.Commit(prepared);
        backend.Unload();
    }

    [Fact]
    public void Desktop_assembly_backend_resolves_private_managed_and_shared_native_dependencies()
    {
        using var backend = new ReloadableAssemblyBackend();
        var dependencyPath = typeof(RedBolt.UserCode.TestDependency.Dependency).Assembly.Location;
        using var prepared = backend.Prepare(new DesktopAssemblyInput(
            typeof(PluginProbe).Assembly.Location,
            sharedNativeLibraries: ["libc"],
            dependencyPaths: [dependencyPath]));
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        var loaded = backend.Commit(prepared);
        var probe = loaded.Assembly.GetType("RedBolt.UserCode.TestPlugin.PluginProbe", true)!;

        Assert.Equal("Private dependency", probe.GetMethod("GetDependencyValue")!.Invoke(null, null));
        if (OperatingSystem.IsLinux())
            Assert.True((int)probe.GetMethod("GetProcessId")!.Invoke(null, null)! > 0);
    }

    [Fact]
    public void Desktop_assembly_registration_includes_reflected_private_dependencies()
    {
        using var backend = new ReloadableAssemblyBackend();
        var dependencyPath = typeof(RedBolt.UserCode.TestDependency.Dependency).Assembly.Location;
        using var prepared = backend.Prepare(new DesktopAssemblyInput(
            typeof(PluginProbe).Assembly.Location,
            dependencyPaths: [dependencyPath]));
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        var loaded = backend.Commit(prepared);
        using var context = new Context();

        loaded.Register(context);

        var reflection = context.GetReflection(nameof(RedBolt.UserCode.TestDependency.ReflectedDependency));
        Assert.NotNull(reflection);
        Assert.NotSame(typeof(RedBolt.UserCode.TestDependency.ReflectedDependency).Assembly,
            reflection.ManagedType.Assembly);
        Assert.Equal("RedBolt.UserCode.TestDependency", reflection.ManagedType.Assembly.GetName().Name);

        loaded.Unregister();
        Assert.Null(context.GetReflection(nameof(RedBolt.UserCode.TestDependency.ReflectedDependency)));
    }

    [Fact]
    public void Desktop_assembly_backend_rejects_incompatible_shared_dependency()
    {
        var dependencyAssembly = typeof(RedBolt.UserCode.TestDependency.Dependency).Assembly;
        var name = new AssemblyName(dependencyAssembly.GetName().Name!)
        {
            Version = new Version(0, 0, 0, 0),
        };
        var incompatible = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        using var backend = new ReloadableAssemblyBackend();
        using var prepared = backend.Prepare(new DesktopAssemblyInput(
            typeof(PluginProbe).Assembly.Location, [incompatible]));
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        var loaded = backend.Commit(prepared);
        var probe = loaded.Assembly.GetType("RedBolt.UserCode.TestPlugin.PluginProbe", true)!;
        var exception = Assert.Throws<TargetInvocationException>(
            () => probe.GetMethod("GetDependencyValue")!.Invoke(null, null));
        Assert.IsType<FileLoadException>(exception.InnerException);
    }

    [Fact]
    public void Desktop_load_only_backends_reject_replacement()
    {
        using var assemblies = new DesktopLoadOnlyAssemblyBackend();
        var path = typeof(ReloadableScriptBackend).Assembly.Location;
        using (var first = assemblies.Prepare(new DesktopAssemblyInput(path)))
        {
            Assert.True(first.Succeeded, Format(first.Diagnostics));
            Assert.False(assemblies.Commit(first).IsCollectible);
        }
        using var second = assemblies.Prepare(new DesktopAssemblyInput(path));
        Assert.False(second.Succeeded);

        using var scripts = new DesktopLoadOnlyScriptBackend();
        using (var first = scripts.Prepare(Bundle("public static class Once { }", "LoadOnlyScripts")))
        {
            Assert.True(first.Succeeded, Format(first.Diagnostics));
            scripts.Commit(first);
        }
        using var replacement = scripts.Prepare(Bundle("public static class Twice { }", "LoadOnlyScripts"));
        Assert.False(replacement.Succeeded);
    }

    [Fact]
    public void Load_only_assembly_backend_validates_input_and_registration_lifetime()
    {
        using var backend = new LoadOnlyAssemblyBackend();
        using (var wrong = backend.Prepare(new UnknownAssemblyInput()))
            Assert.False(wrong.Succeeded);

        using var prepared = backend.Prepare(new PrecompiledAssemblyInput(typeof(UserCodeBackendTests).Assembly));
        var loaded = backend.Commit(prepared);
        Assert.Null(loaded.CollectionReference);

        using var context = new Context();
        loaded.Register(context);
        Assert.Throws<InvalidOperationException>(() => loaded.Register(context));
        loaded.Unregister();
        loaded.Unregister();

        using var alreadyLoaded = backend.Prepare(new PrecompiledAssemblyInput(typeof(UserCodeBackendTests).Assembly));
        Assert.False(alreadyLoaded.Succeeded);
        Assert.Throws<InvalidOperationException>(() => backend.Commit(prepared));

        backend.Unload();
        loaded.Dispose();
    }

    [Fact]
    public void Script_backends_validate_input_and_empty_unchanged_commit()
    {
        using (var precompiled = new PrecompiledScriptBackend())
        {
            using var wrong = precompiled.Prepare(new UnknownScriptInput());
            Assert.False(wrong.Succeeded);
            using var unchanged = new ScriptPreparationForTests(true);
            Assert.Throws<InvalidOperationException>(() => precompiled.Commit(unchanged.Value));
            precompiled.Unload();
        }

        using var desktop = new ReloadableScriptBackend();
        using (var wrong = desktop.Prepare(new UnknownScriptInput()))
            Assert.False(wrong.Succeeded);
        using var unchangedDesktop = new ScriptPreparationForTests(true);
        Assert.Throws<InvalidOperationException>(() => desktop.Commit(unchangedDesktop.Value));
    }

    [Fact]
    public void Desktop_inputs_report_missing_and_invalid_assemblies()
    {
        using var backend = new ReloadableAssemblyBackend();
        using (var wrong = backend.Prepare(new PrecompiledAssemblyInput(typeof(UserCodeBackendTests).Assembly)))
            Assert.False(wrong.Succeeded);

        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.dll");
        using (var missing = backend.Prepare(new DesktopAssemblyInput(missingPath)))
            Assert.False(missing.Succeeded);

        var tempDirectory = Directory.CreateTempSubdirectory("RedBoltUserCode");
        try
        {
            var invalidPath = Path.Combine(tempDirectory.FullName, "Invalid.dll");
            File.WriteAllText(invalidPath, "not an assembly");
            using var invalid = backend.Prepare(new DesktopAssemblyInput(invalidPath));
            Assert.False(invalid.Succeeded);

            var noSymbolsPath = Path.Combine(tempDirectory.FullName, "NoSymbols.dll");
            File.Copy(typeof(ReloadableScriptBackend).Assembly.Location, noSymbolsPath);
            using var noSymbols = backend.Prepare(new DesktopAssemblyInput(noSymbolsPath));
            Assert.True(noSymbols.Succeeded, Format(noSymbols.Diagnostics));
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Desktop_script_bundle_uses_explicit_references_and_reports_warnings()
    {
        using var backend = new ReloadableScriptBackend();
        var redBoltPath = typeof(Context).Assembly.Location;
        var bundle = new SourceScriptBundle(
            "ReferencedScripts",
            [new ScriptSource("Scripts/Warning.cs", "public static class Warning { public static void Run() { int unused; } }")],
            [typeof(UserCodeBackendTests).Assembly],
            [redBoltPath]);

        using var prepared = backend.Prepare(bundle);
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        Assert.Contains(prepared.Diagnostics, static diagnostic =>
            diagnostic.Severity == UserCodeDiagnosticSeverity.Warning);
        backend.Commit(prepared);

        using var missingReference = backend.Prepare(new SourceScriptBundle(
            "MissingReference", [new ScriptSource("Scripts/Main.cs", "")],
            referencePaths: [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")]));
        Assert.False(missingReference.Succeeded);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndUnloadGeneration()
    {
        using var backend = new ReloadableScriptBackend();
        using var prepared = backend.Prepare(Bundle("public static class TemporaryScript { }", "CollectibleScripts"));
        Assert.True(prepared.Succeeded, Format(prepared.Diagnostics));
        var loaded = backend.Commit(prepared);
        var reference = loaded.CollectionReference!;
        backend.Unload();
        return reference;
    }

    private static SourceScriptBundle Bundle(string source, string assemblyName = "TestScripts") =>
        new(assemblyName, [new ScriptSource("Scripts/Main.cs", source)]);

    private static SourceScriptBundle ReflectedBundle(int value) => Bundle(
        $$"""
        using RedBolt.Core;

        [Reflected("Scripts")]
        public sealed partial class ReloadedObject : RedObject
        {
            public int Value => {{value}};
        }
        """,
        "ReflectedScripts");

    private static int InvokeValue(UserCodeAssembly loaded)
    {
        var type = loaded.Assembly.GetType("ScriptValue", true)!;
        return (int)type.GetMethod("Get")!.Invoke(null, null)!;
    }

    private static void AssertReflectionValue(Context context, UserCodeAssembly loaded, int expected)
    {
        var reflection = context.GetReflection("ReloadedObject");
        Assert.NotNull(reflection);
        Assert.Same(loaded.Assembly, reflection.ManagedType.Assembly);
        using var instance = reflection.CreateObject();
        Assert.Equal(expected, reflection.ManagedType.GetProperty("Value")!.GetValue(instance));
    }

    private static string Format(IReadOnlyList<UserCodeDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(static diagnostic =>
            $"{diagnostic.Path}:{diagnostic.Line}:{diagnostic.Column}: {diagnostic.Message}"));

    private sealed record UnknownAssemblyInput : AssemblyLoadInput;

    private sealed record UnknownScriptInput : ScriptBundleInput;

    private sealed class ScriptPreparationForTests : IDisposable
    {
        public ScriptPreparationForTests(bool unchanged)
        {
            var constructor = typeof(ScriptPreparation).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic).Single();
            Value = (ScriptPreparation)constructor.Invoke([null, unchanged, string.Empty, Array.Empty<UserCodeDiagnostic>()]);
        }

        public ScriptPreparation Value { get; }

        public void Dispose() => Value.Dispose();
    }
}
