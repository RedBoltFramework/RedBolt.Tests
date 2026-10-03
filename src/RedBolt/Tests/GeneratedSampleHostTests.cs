// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Runs the monorepo sample host generator in check mode so the suite fails when a
/// checked-in generated host differs from the generator output.
/// </summary>
public sealed class GeneratedSampleHostTests
{
    private static readonly TimeSpan GeneratorTimeout = TimeSpan.FromMinutes(5);

    [MonorepoFact]
    public async Task Generated_sample_hosts_match_generator_output()
    {
        var result = await RunGenerateSamplesCheckAsync();

        Assert.True(
            result.ExitCode == 0,
            $"generate-samples --check exited with code {result.ExitCode}. " +
            $"Run ./monorepo.cs generate-samples to regenerate the hosts.{Environment.NewLine}" +
            $"{result.StandardError}{result.StandardOutput}");
    }

    private static async Task<GeneratorResult> RunGenerateSamplesCheckAsync()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RbfxTestData.RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[] { "run", "--file", "monorepo.cs", "--", "generate-samples", "--check" })
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(GeneratorTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail(
                $"generate-samples --check did not finish within {GeneratorTimeout}.{Environment.NewLine}" +
                $"{await standardError}{await standardOutput}");
        }

        return new GeneratorResult(process.ExitCode, await standardOutput, await standardError);
    }

    private readonly record struct GeneratorResult(int ExitCode, string StandardOutput, string StandardError);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class MonorepoFactAttribute : Xunit.FactAttribute
{
    public MonorepoFactAttribute()
    {
        if (!System.IO.File.Exists(Path.Combine(RbfxTestData.RepositoryRoot, "monorepo.cs")))
            Skip = "monorepo.cs is not in the workspace root; the generated sample hosts are only checked in the monorepo.";
    }
}
