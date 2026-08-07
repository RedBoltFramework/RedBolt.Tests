namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;

public sealed class ImGuiInternalBindingsTests
{
    [NativeBindingFact]
    public void Internal_item_rectangle_and_button_bindings_are_callable()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;
            var isOpen = true;
            ImGui.Begin("Internal Bindings"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;

            var minimum = ImGui.GetCursorScreenPos();
            var rectangle = new Rect(minimum, minimum + new Vector2f(32.0f, 16.0f));
            ImGui.ItemSize(in rectangle);
            Assert.True(ImGui.ItemAdd(in rectangle, ImGui.GetID("Rectangle"u8)));

            Assert.False(ImGui.ButtonEx(
                "Button"u8,
                new Vector2f(24.0f, 24.0f),
                (ImGuiButtonFlags)ImGuiButtonFlagsPrivate.ImGuiButtonFlagsPressedOnClick));
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(context);
        }
    }
}
