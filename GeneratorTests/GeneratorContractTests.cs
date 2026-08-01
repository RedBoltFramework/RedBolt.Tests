namespace RedBolt.Tests;

using System.Reflection;

public sealed class GeneratorContractTests
{
    private static readonly Assembly GeneratorAssembly = Assembly.LoadFrom(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "tools", "RedBolt.Generator.Dependencies.dll")));

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
}
