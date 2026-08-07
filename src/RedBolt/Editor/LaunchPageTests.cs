namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.SystemUI;

using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class LaunchPageTests
{
    private readonly GraphicsEngineFixture _fixture;

    public LaunchPageTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Page_adds_unused_names_removes_configurations_and_marks_project_unsaved()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.LaunchPageTests", Guid.NewGuid().ToString("N"));
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
        LaunchPage? page = null;
        var imguiContext = IntPtr.Zero;
        try
        {
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            using (var first = new LaunchConfiguration("Configuration 0", "Main.Plugin"))
                project.LaunchManager.AddConfiguration(first);
            using (var second = new LaunchConfiguration("Configuration 2", string.Empty))
                project.LaunchManager.AddConfiguration(second);

            page = new LaunchPage();
            project.SettingsManager.AddPage(page);
            Assert.Equal("Project.Launch", page.UniqueName);
            Assert.False(page.IsSerializable);
            Assert.Same(page, project.SettingsManager.FindPage(page.UniqueName));
            Assert.False(project.HasUnsavedChanges);
            page.SerializeInBlock(null!);

            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(800.0f, 800.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            RenderPageFrame(page, Activation.Add);
            RenderPageFrame(page, Activation.None);

            Assert.Equal(3, project.LaunchManager.Configurations.Count);
            Assert.Equal("Configuration 1", project.LaunchManager.Configurations[2].Name);
            Assert.True(project.HasUnsavedChanges);

            RenderPageFrame(page, Activation.RemoveSecond);
            RenderPageFrame(page, Activation.None);

            Assert.Equal(2, project.LaunchManager.Configurations.Count);
            Assert.Equal("Configuration 0", project.LaunchManager.Configurations[0].Name);
            Assert.Equal("Configuration 1", project.LaunchManager.Configurations[1].Name);
        }
        finally
        {
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
            page?.Dispose();
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

    private static void RenderPageFrame(LaunchPage page, Activation activation)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(700.0f, 700.0f));
            var open = true;
            ImGui.Begin("Launch page test"u8, ref open);
            windowStarted = true;

            page.RenderSettings();

            if (activation == Activation.Add)
                ImGui.ActivateItemByID(ImGui.GetID("\uf0fe Add new launch configuration"u8));
            else if (activation == Activation.RemoveSecond)
            {
                using var guard = new IdScopeGuard(1);
                ImGui.ActivateItemByID(ImGui.GetID("\uf2ed"u8));
            }
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private enum Activation
    {
        None,
        Add,
        RemoveSecond,
    }
}
