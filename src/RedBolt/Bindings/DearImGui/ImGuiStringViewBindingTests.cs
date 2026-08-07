namespace RedBolt.Tests;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Math;

public sealed class ImGuiStringViewBindingTests
{
    private static int _inputTextCallbackCount;

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

    [NativeBindingFact]
    public unsafe void Input_text_forwards_raw_callback_pointer()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;
        using var buffer = new CString("value");

        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            _inputTextCallbackCount = 0;
            ImGui.NewFrame();
            frameStarted = true;

            var open = true;
            ImGui.Begin("Input callback test"u8, ref open);
            windowStarted = true;
            ImGui.SetKeyboardFocusHere();

            var callback = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, int>)&CountInputTextCallback;
            ImGui.InputText(
                "Input"u8,
                buffer.Pointer,
                (nuint)buffer.Bytes.Length,
                ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackAlways,
                callback);
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Input callback test"u8, ref open);
            windowStarted = true;
            ImGui.InputText(
                "Input"u8,
                buffer.Pointer,
                (nuint)buffer.Bytes.Length,
                ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackAlways,
                callback);

            Assert.Equal(1, _inputTextCallbackCount);
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CountInputTextCallback(IntPtr data)
    {
        ++_inputTextCallbackCount;
        return 0;
    }
}
