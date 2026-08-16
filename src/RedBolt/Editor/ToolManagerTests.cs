// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class ToolManagerTests
{
    [Fact]
    public void Serialized_tool_paths_and_found_state_are_loaded_without_rescanning()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        using var manager = new ToolManager();

        try
        {
            using var input = new JSONFile();
            Assert.True(input.FromString(
                "{\"BlenderFound\":true," +
                "\"BlenderPath\":\"/tools/blender\"," +
                "\"FBX2glTFFound\":false," +
                "\"FBX2glTFPath\":\"/tools/fbx2gltf\"}"));
            Assert.True(input.LoadObject("Tool", manager));

            Assert.True(manager.HasBlender);

            manager.Update();

            Assert.Equal("Editor.ExternalTools", manager.UniqueName);
            Assert.True(manager.IsSerializable);
            Assert.True(manager.HasBlender);
            Assert.Equal("/tools/blender", manager.Blender);
            Assert.False(manager.HasFBX2glTF);
            Assert.Equal("/tools/fbx2gltf", manager.FBX2glTF);

            using var output = new JSONFile();
            Assert.True(output.SaveObject("Tool", manager));
            Assert.Contains("/tools/blender", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("/tools/fbx2gltf", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void System_run_captures_output_and_reports_spawn_failure()
    {
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        string[] arguments = OperatingSystem.IsWindows()
            ? ["/c", "echo tool-output & exit /b 7"]
            : ["-c", "printf tool-output; exit 7"];

        var exitCode = ProcessUtils.SystemRun(executable, arguments, out var output);

        Assert.True(exitCode >= 0);
        Assert.Contains("tool-output", output, StringComparison.Ordinal);

        var missingExecutable = Path.Combine(Path.GetTempPath(), $"redbolt-missing-{Guid.NewGuid():N}");
        Assert.Equal(-1, ProcessUtils.SystemRun(missingExecutable, [], out output));
        Assert.Empty(output);
    }

    [NativeBindingFact]
    public void Settings_render_both_status_branches()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        using var manager = new ToolManager();
        using var input = new JSONFile();
        Assert.True(input.FromString(
            "{\"BlenderFound\":true," +
            "\"BlenderPath\":\"/tools/blender\"," +
            "\"FBX2glTFFound\":false," +
            "\"FBX2glTFPath\":\"/tools/100%/fbx2gltf\"}"));
        Assert.True(input.LoadObject("Tool", manager));
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
            ImGui.Begin("Tool manager"u8, ref open);
            windowStarted = true;

            manager.RenderSettings();

            Assert.True(open);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
            Context.SetInstance(previousContext);
        }
    }

    [NativeBindingFact]
    public void Url_text_is_underlined_and_uses_the_hand_cursor_when_hovered()
    {
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
            ImGui.Begin("URL"u8, ref open);
            windowStarted = true;
            Widgets.TextURL("Project page"u8, "https://example.com/"u8);
            var itemPosition = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("URL"u8, ref open);
            windowStarted = true;
            Widgets.TextURL("Project page"u8, "https://example.com/"u8);
            itemPosition = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddMousePosEvent(itemPosition.X, itemPosition.Y);
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("URL"u8, ref open);
            windowStarted = true;

            Widgets.TextURL("Project page"u8, "https://example.com/"u8);

            Assert.Equal(ImGuiMouseCursor.ImGuiMouseCursorHand, ImGui.GetMouseCursor());
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
        }
    }
}
