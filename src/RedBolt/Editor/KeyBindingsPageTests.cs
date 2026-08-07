namespace RedBolt.Tests;

using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Project;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Math;

using File = global::System.IO.File;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class KeyBindingsPageTests
{
    private readonly GraphicsEngineFixture _fixture;

    public KeyBindingsPageTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Page_is_registered_nonserializable_and_renders_press_and_hold_bindings()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.KeyBindingsPageTests", Guid.NewGuid().ToString("N"));
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
        KeyBindingsPage? page = null;
        var imguiContext = IntPtr.Zero;
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            page = new KeyBindingsPage();
            project.SettingsManager.AddPage(page);
            project.HotkeyManager.BindPassiveHotkey(
                new EditorHotkey("Editor.Press").Ctrl().Press(Key.P));
            project.HotkeyManager.BindPassiveHotkey(
                new EditorHotkey("Editor.Hold").Shift().Hold(Key.H));

            Assert.Equal("Editor.KeyBindings", page.UniqueName);
            Assert.False(page.IsSerializable);
            Assert.Same(page, project.SettingsManager.FindPage(page.UniqueName));
            page.SerializeInBlock(null!);

            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(800.0f, 600.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;
            var open = true;
            ImGui.Begin("Key bindings page test"u8, ref open);
            windowStarted = true;

            using var drawList = ImGui.GetWindowDrawList();
            var verticesBefore = drawList.VtxBufferSize;
            page.RenderSettings();
            Assert.True(drawList.VtxBufferSize > verticesBefore);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
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
}
