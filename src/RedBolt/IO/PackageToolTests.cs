namespace RedBolt.Tests;

using System.Diagnostics;
using System.Text;

using RedBolt.IO;

public sealed class PackageToolTests
{
    [Fact]
    public void Invalid_archive_is_rejected_without_entries()
    {
        using var scope = new TestArtifactScope(nameof(Invalid_archive_is_rejected_without_entries));
        var packagePath = scope.Write("invalid.pak", "not-a-package"u8);

        using var package = new PackageFile();
        Assert.False(package.Open(packagePath));
        Assert.Empty(package.GetEntries());
        Assert.Equal(0u, package.GetNumFiles());
    }

    [Fact]
    public void Missing_input_and_output_parent_return_diagnostics_and_nonzero_exit()
    {
        using var scope = new TestArtifactScope(nameof(Missing_input_and_output_parent_return_diagnostics_and_nonzero_exit));
        var missingInput = Path.Combine(scope.DirectoryPath, "missing");
        var missingInputResult = RunPackageToolProcess([missingInput, Path.Combine(scope.DirectoryPath, "missing.pak"), "-q"]);
        Assert.NotEqual(0, missingInputResult.ExitCode);
        Assert.Contains("DirectoryNotFoundException", missingInputResult.StandardError);

        var sourceDirectory = Path.Combine(scope.DirectoryPath, "source");
        Directory.CreateDirectory(sourceDirectory);
        System.IO.File.WriteAllText(Path.Combine(sourceDirectory, "file.txt"), "content");
        var missingParent = Path.Combine(scope.DirectoryPath, "absent", "output.pak");
        var outputResult = RunPackageToolProcess([sourceDirectory, missingParent, "-q"]);
        Assert.Equal(1, outputResult.ExitCode);
        Assert.Contains("Could not open output file", outputResult.StandardError);
    }

    [Fact]
    public void Existing_unchanged_output_is_reported_up_to_date_without_rewriting()
    {
        using var scope = new TestArtifactScope(nameof(Existing_unchanged_output_is_reported_up_to_date_without_rewriting));
        var sourceDirectory = Path.Combine(scope.DirectoryPath, "source");
        var packagePath = Path.Combine(scope.DirectoryPath, "output.pak");
        Directory.CreateDirectory(sourceDirectory);
        System.IO.File.WriteAllText(Path.Combine(sourceDirectory, "file.txt"), "stable content");

        var first = RunPackageToolProcess([sourceDirectory, packagePath, "-q"]);
        Assert.Equal(0, first.ExitCode);
        var originalBytes = System.IO.File.ReadAllBytes(packagePath);

        var second = RunPackageToolProcess([sourceDirectory, packagePath, "-q"]);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("up to date", second.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(originalBytes, System.IO.File.ReadAllBytes(packagePath));
    }

    [Fact]
    public void Invalid_option_reports_usage_error_and_nonzero_exit()
    {
        using var scope = new TestArtifactScope(nameof(Invalid_option_reports_usage_error_and_nonzero_exit));
        var result = RunPackageToolProcess(["-x", Path.Combine(scope.DirectoryPath, "unknown.pak")]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unrecognized output option", result.StandardError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackageTool_writes_package_readable_by_PackageFile(bool compress)
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(), $"redbolt-package-tool-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(temporaryDirectory, "Data");
        var packagePath = Path.Combine(temporaryDirectory, "Data.pak");
        var firstContent = Encoding.UTF8.GetBytes("first package entry");
        var secondContent = Encoding.UTF8.GetBytes(new string('x', 65536));

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Folder"));
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".hidden"));
            System.IO.File.WriteAllBytes(Path.Combine(sourceDirectory, "First.txt"), firstContent);
            System.IO.File.WriteAllBytes(Path.Combine(sourceDirectory, "Folder", "Second.bin"), secondContent);
            System.IO.File.WriteAllText(Path.Combine(sourceDirectory, "Ignored.rule"), "ignored");
            System.IO.File.WriteAllText(Path.Combine(sourceDirectory, ".hidden", "Ignored.txt"), "ignored");

            var arguments = new List<string> { sourceDirectory, packagePath, "Prefix", "-q" };
            if (compress)
                arguments.Add("-c");
            RunPackageTool(arguments);

            using var package = new PackageFile(packagePath);
            Assert.Equal(compress, package.IsCompressed());
            Assert.Equal(2u, package.GetNumFiles());
            Assert.True(package.Exists("Prefix/First.txt"));
            Assert.True(package.Exists("Prefix/Folder/Second.bin"));
            Assert.False(package.Exists("Prefix/Ignored.rule"));
            Assert.False(package.Exists("Prefix/.hidden/Ignored.txt"));
            AssertPackageEntry(package, "Prefix/First.txt", firstContent);
            AssertPackageEntry(package, "Prefix/Folder/Second.bin", secondContent);

            RunPackageTool(["-i", packagePath]);
            RunPackageTool(["-l", packagePath]);
            if (compress)
                RunPackageTool(["-L", packagePath]);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }
    }

    private static void RunPackageTool(IEnumerable<string> arguments)
    {
        var result = RunPackageToolProcess(arguments);
        Assert.True(
            result.ExitCode == 0,
            $"PackageTool exited with code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
    }

    private static PackageToolResult RunPackageToolProcess(IEnumerable<string> arguments)
    {
        var testOutputName = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var pivotParts = testOutputName.Split('-', 4);
        var packageToolPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", $"tools-{pivotParts[1]}-{pivotParts[2]}",
                "RedBolt.PackageTool.dll"));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add(packageToolPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new PackageToolResult(process.ExitCode, standardOutput, standardError);
    }

    private readonly record struct PackageToolResult(int ExitCode, string StandardOutput, string StandardError);

    private static void AssertPackageEntry(PackageFile package, string name, byte[] expected)
    {
        using var file = package.OpenFile(new FileIdentifier(string.Empty, name), FileMode.Read);
        Assert.NotNull(file);
        var actual = new byte[expected.Length];
        Assert.Equal((uint)actual.Length, file!.Read(actual));
        Assert.Equal(expected, actual);
    }
}
