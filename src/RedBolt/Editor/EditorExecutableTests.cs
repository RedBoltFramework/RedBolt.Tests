namespace RedBolt.Tests;

using System.Diagnostics;

[Collection(nameof(ApplicationLifecycleCollection))]
public sealed class EditorExecutableTests
{
    [Fact]
    public void Built_editor_reports_command_line_usage_and_stages_editor_data()
    {
        var result = RunEditor(["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: Editor | OpenGL", result.StandardOutput);
        Assert.Contains("--read-only", result.StandardOutput);
        Assert.Contains("[project]", result.StandardOutput);

        var editorData = Path.Combine(Path.GetDirectoryName(EditorExecutable)!, "EditorData");
        Assert.True(Directory.Exists(editorData));
        Assert.True(File.Exists(Path.Combine(editorData, "Fonts", "NotoSans-Regular.ttf")));
        Assert.True(File.Exists(Path.Combine(editorData, "Textures", "Editor", "EditorIcons.png")));
        Assert.Equal(53, Directory.EnumerateFiles(editorData, "*", SearchOption.AllDirectories).Count());
    }

    [GraphicsFact]
    public void Built_editor_renders_and_exits_with_software_graphics()
    {
        var preferencesPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorExecutableTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(preferencesPath);
        try
        {
            var result = RunEditor(
            [
                "--timeout", "1",
                "--preferences-dir", preferencesPath,
                "--quiet",
                "--no-profile",
            ]);

            Assert.True(result.ExitCode == 0, result.StandardError);
            Assert.Contains("llvmpipe", result.StandardOutput);
        }
        finally
        {
            Directory.Delete(preferencesPath, true);
        }
    }

    private static ProcessResult RunEditor(IReadOnlyList<string> arguments)
    {
        Assert.True(File.Exists(EditorExecutable), $"Editor executable was not built at '{EditorExecutable}'.");
        var startInfo = new ProcessStartInfo(EditorExecutable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Editor executable did not exit within 30 seconds.");
        }

        Task.WaitAll(standardOutput, standardError);
        return new ProcessResult(process.ExitCode, standardOutput.Result, standardError.Result);
    }

    private static string EditorExecutable { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", OperatingSystem.IsWindows() ? "RedBolt.Editor.exe" : "RedBolt.Editor"));

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
