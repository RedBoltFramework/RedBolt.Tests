namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;

public sealed class ImGuiStringViewBindingTests
{
    [NativeBindingFact]
    public void String_view_arguments_use_the_span_length()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;

        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            ImGui.NewFrame();
            frameStarted = true;

            var open = true;
            ImGui.Begin("String view test"u8, ref open);
            windowStarted = true;

            ReadOnlySpan<byte> id = "shared-id-extra"u8;
            Assert.NotEqual(ImGui.GetID(id[..9]), ImGui.GetID(id));
            ImGui.Text("100% formatted on C# side"u8);
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

    [NativeBindingFact]
    public void Popup_modal_accepts_the_native_null_open_pointer_default()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var popupStarted = false;

        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.OpenPopup("Modal"u8);

            popupStarted = ImGui.BeginPopupModal("Modal"u8);

            Assert.True(popupStarted);
        }
        finally
        {
            if (popupStarted)
                ImGui.EndPopup();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(context);
        }
    }
}
