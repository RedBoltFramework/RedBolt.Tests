// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Utility;

using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ConcurrentAssetProcessingTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ConcurrentAssetProcessingTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Single_process_command_round_trips_an_empty_asset_batch()
    {
        WithProject(ProjectFlag.ReadOnly | ProjectFlag.SingleProcess, (project, projectPath) =>
        {
            ConcurrentAssetProcessing.Register(project);
            Assert.Null(project.SettingsManager.FindPage("Editor.Assets:Concurrency"));

            var pipePath = Path.Combine(projectPath, "Pipe");
            Directory.CreateDirectory(pipePath);
            var inputPath = Path.Combine(pipePath, "input.json");
            var outputPath = Path.Combine(pipePath, "output.json");
            List<AssetTransformerInput> input = [];
            using (var inputFile = new JSONFile())
            {
                Assert.True(inputFile.SaveObjectCallback(archive =>
                    ArchiveSerialization.SerializeVector(
                        archive, "input", input, UnexpectedInputElement)));
                Assert.True(inputFile.SaveFile(new FileIdentifier(inputPath)));
            }

            var processCommand = typeof(Project).GetMethod(
                "ProcessCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
            processCommand.Invoke(project, ["ProcessAsset Pipe/input.json Pipe/output.json", false]);

            Assert.True(File.Exists(outputPath));
            List<AssetTransformerOutput?> output = [];
            using var outputFile = new JSONFile();
            Assert.True(outputFile.LoadFile(new FileIdentifier(outputPath)));
            Assert.True(outputFile.LoadObjectCallback(archive =>
                ArchiveSerialization.SerializeVector(
                    archive, "output", output, UnexpectedOutputElement)));
            Assert.Empty(output);
        });
    }

    [NativeBindingFact]
    public void Concurrent_mode_registers_settings_and_applies_loaded_concurrency()
    {
        WithProject(ProjectFlag.ReadOnly, (project, projectPath) =>
        {
            ConcurrentAssetProcessing.Register(project);

            var page = Assert.IsAssignableFrom<RedBolt.Editor.Core.SettingsPage>(
                project.SettingsManager.FindPage("Editor.Assets:Concurrency"));
            Assert.Equal(4, GetMaxConcurrentRequests(project.AssetManager));

            var settingsPath = Path.Combine(projectPath, "Settings.json");
            File.WriteAllText(settingsPath,
                "{\"Editor.Assets:Concurrency\":{\"MaxConcurrency\":7}}");
            project.SettingsManager.LoadFile(settingsPath);
            Assert.Equal(7, GetMaxConcurrentRequests(project.AssetManager));

            var imguiContext = ImGui.CreateContext();
            var frameStarted = false;
            var windowStarted = false;
            try
            {
                ImGui.SetCurrentContext(imguiContext);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(640.0f, 480.0f);
                io.DeltaTime = 1.0f / 60.0f;
                io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                ImGui.NewFrame();
                frameStarted = true;
                var open = true;
                ImGui.Begin("Concurrent asset settings test"u8, ref open);
                windowStarted = true;

                using var drawList = ImGui.GetWindowDrawList();
                var verticesBefore = drawList.VtxBufferSize;
                page.RenderSettings();
                Assert.True(drawList.VtxBufferSize > verticesBefore);
                Assert.Equal(7, GetMaxConcurrentRequests(project.AssetManager));
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(imguiContext);
            }

            page.Dispose();
        });
    }

    private void WithProject(ProjectFlag flags, Action<Project, string> test)
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.ConcurrentAssetProcessingTests", Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var editorDataPath = Path.Combine(projectPath, "EditorData");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(editorDataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        var virtualFileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        var editorDataMount = virtualFileSystem.MountDir(editorDataPath);
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        try
        {
            project = new Project(projectPath, settingsPath, flags);
            test(project, projectPath);
        }
        finally
        {
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            virtualFileSystem.Unmount(editorDataMount);
            Directory.Delete(projectPath, true);
        }
    }

    private static int GetMaxConcurrentRequests(AssetManager assetManager)
    {
        return (int)typeof(AssetManager).GetField(
            "_maxConcurrentRequests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(assetManager)!;
    }

    private static void UnexpectedInputElement(
        Archive archive, string name, ref AssetTransformerInput value)
    {
        throw new InvalidOperationException("Empty input vector unexpectedly contained an element.");
    }

    private static void UnexpectedOutputElement(
        Archive archive, string name, ref AssetTransformerOutput? value)
    {
        throw new InvalidOperationException("Empty output vector unexpectedly contained an element.");
    }
}
