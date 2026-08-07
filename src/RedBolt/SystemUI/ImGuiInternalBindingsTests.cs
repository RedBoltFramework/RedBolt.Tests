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

            var floatValue = 2.5f;
            Assert.False(ImGui.DragFloat("Float"u8, ref floatValue));
            Assert.Equal(2.5f, floatValue);

            Span<float> vectorValue = [1.0f, 2.0f, 3.0f, 4.0f];
            Assert.False(ImGui.DragFloat4("Vector"u8, vectorValue));
            Assert.Equal([1.0f, 2.0f, 3.0f, 4.0f], vectorValue.ToArray());

            Assert.False(ImGui.ColorEdit4("Color"u8, vectorValue));
            Assert.Equal([1.0f, 2.0f, 3.0f, 4.0f], vectorValue.ToArray());

            var intValue = 5;
            Assert.False(ImGui.DragInt("Int"u8, ref intValue));
            Assert.Equal(5, intValue);

            Span<int> intVectorValue = [6, 7, 8];
            Assert.False(ImGui.DragInt3("Int Vector"u8, intVectorValue));
            Assert.Equal([6, 7, 8], intVectorValue.ToArray());
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
