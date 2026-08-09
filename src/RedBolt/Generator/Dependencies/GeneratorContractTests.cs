namespace RedBolt.Tests;

using System.Reflection;

public sealed class GeneratorContractTests
{
    private static readonly object ProgramLock = new();
    private static readonly Assembly GeneratorAssembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(static assembly => assembly.GetName().Name == "RedBolt.Generator.Dependencies")
        ?? Assembly.LoadFrom(RbfxTestData.GetAppFile(
            "tools", "RedBolt.Generator.Dependencies.dll"));

    [Fact]
    public void Generator_options_normalize_paths_and_keep_repeated_options()
    {
        var optionsType = GeneratorAssembly.GetType("RedBolt.Generator.Dependencies.GeneratorOptions")!;
        var parse = optionsType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var args = new[]
        {
            "--surface", "SDL", "--header", "headers/main.hpp", "--include-name", "main.hpp",
            "--output-dir", "out", "--file-stem", "Bindings", "--metadata", "meta.json",
            "--log", "generator.log", "--sharpcpp-root", "sharp-cpp", "--redbolt-root", "redbolt",
            "--native-library-name", "native", "--compiler", "clang++",
            "--include", "include-a", "--include", "include-b", "--define", "ONE=1",
            "--define", "TWO", "--owned-header", "owned.hpp"
        };

        var options = parse.Invoke(null, [args]);
        Assert.NotNull(options);
        Assert.Equal("SDL", optionsType.GetProperty("Surface")!.GetValue(options));
        Assert.Equal(Path.GetFullPath("headers/main.hpp"), optionsType.GetProperty("HeaderPath")!.GetValue(options));
        Assert.Equal(
            [Path.GetFullPath("include-a"), Path.GetFullPath("include-b")],
            ((IReadOnlyList<string>)optionsType.GetProperty("IncludeDirectories")!.GetValue(options)!).ToArray());
        Assert.Equal(["ONE=1", "TWO"], ((IReadOnlyList<string>)optionsType.GetProperty("Defines")!.GetValue(options)!).ToArray());
    }

    [Fact]
    public void Generator_options_report_missing_required_values()
    {
        var optionsType = GeneratorAssembly.GetType("RedBolt.Generator.Dependencies.GeneratorOptions")!;
        var parse = optionsType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;

        var exception = Assert.Throws<TargetInvocationException>(() => parse.Invoke(null, [Array.Empty<string>()]));
        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("--surface", exception.InnerException!.Message);
    }

    [Fact]
    public void Compiler_toolchain_discovery_finds_standard_headers()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var discoveryType = GeneratorAssembly.GetType("RedBolt.Generator.Dependencies.CompilerToolchainDiscovery")!;
        var discover = discoveryType.GetMethod("Discover", BindingFlags.Public | BindingFlags.Static)!;
        var toolchainInfo = discover.Invoke(null, [Environment.GetEnvironmentVariable("CXX") ?? "c++"])!;
        var includeDirectories = (IReadOnlyList<string>)toolchainInfo.GetType()
            .GetProperty("IncludeDirectories")!.GetValue(toolchainInfo)!;

        Assert.Contains(includeDirectories,
            static includeDirectory => File.Exists(Path.Combine(includeDirectory, "stddef.h")));
    }

    [Fact]
    public void Generator_program_reports_unknown_surface_and_prints_usage()
    {
        using var artifacts = new TestArtifactScope(nameof(Generator_program_reports_unknown_surface_and_prints_usage));
        var result = InvokeProgram(
            [
                "--surface", "not-a-surface",
                "--header", Path.Combine(artifacts.DirectoryPath, "missing.hpp"),
                "--include-name", "missing.hpp",
                "--output-dir", Path.Combine(artifacts.DirectoryPath, "output"),
                "--file-stem", "Bindings",
                "--metadata", Path.Combine(artifacts.DirectoryPath, "metadata.json"),
                "--log", Path.Combine(artifacts.DirectoryPath, "generator.log"),
                "--sharpcpp-root", artifacts.DirectoryPath,
                "--redbolt-root", artifacts.DirectoryPath,
                "--native-library-name", "missing",
                "--compiler", Path.Combine(artifacts.DirectoryPath, "missing-compiler")
            ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown binding surface 'not-a-surface'", result.Error);
        Assert.Contains("Usage: RedBolt.Generator.Dependencies", result.Error);
        Assert.False(File.Exists(Path.Combine(artifacts.DirectoryPath, "generator.log")));
    }

    [Fact]
    public void Generator_program_creates_and_closes_log_on_each_supported_surface_error()
    {
        var surfaces = new[]
        {
            "sdl", "diligent", "dearimgui", "stb", "lz4", "harfbuzz", "rmlui",
            "webp", "xatlas", "glslang", "spirvcross", "spirvtools", "shadercompiler", "jolt", "embree"
        };

        foreach (var surface in surfaces)
        {
            using var artifacts = new TestArtifactScope($"{nameof(Generator_program_creates_and_closes_log_on_each_supported_surface_error)}-{surface}");
            var logPath = Path.Combine(artifacts.DirectoryPath, "logs", "generator.log");
            var result = InvokeProgram(
                [
                    "--surface", surface.ToUpperInvariant(),
                    "--header", Path.Combine(artifacts.DirectoryPath, "missing.hpp"),
                    "--include-name", "missing.hpp",
                    "--output-dir", Path.Combine(artifacts.DirectoryPath, "output"),
                    "--file-stem", "Bindings",
                    "--metadata", Path.Combine(artifacts.DirectoryPath, "metadata.json"),
                    "--log", logPath,
                    "--sharpcpp-root", artifacts.DirectoryPath,
                    "--redbolt-root", artifacts.DirectoryPath,
                    "--native-library-name", "missing",
                    "--compiler", Path.Combine(artifacts.DirectoryPath, "missing-compiler")
                ]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Usage: RedBolt.Generator.Dependencies", result.Error);
            Assert.True(File.Exists(logPath), $"No log was created for {surface}.");
            Assert.NotEmpty(File.ReadAllText(logPath));

            File.Delete(logPath);
        }
    }

    private static (int ExitCode, string Output, string Error) InvokeProgram(string[] args)
    {
        lock (ProgramLock)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var previousOutput = Console.Out;
            var previousError = Console.Error;
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                var exitCode = (int)GeneratorAssembly.EntryPoint!.Invoke(null, [args])!;
                return (exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(previousOutput);
                Console.SetError(previousError);
            }
        }
    }
}
