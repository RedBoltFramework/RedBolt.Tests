namespace RedBolt.Tests;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class IdScopeGuardTests
{
    [NativeBindingFact]
    public void Id_scope_guard_pushes_and_pops_each_supported_id_type()
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
            ImGui.Begin("ID Scope Guard"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;

            var outerId = ImGui.GetID("Item"u8);

            using (new IdScopeGuard("Span"u8))
                Assert.NotEqual(outerId, ImGui.GetID("Item"u8));
            Assert.Equal(outerId, ImGui.GetID("Item"u8));

            using (new IdScopeGuard(new IntPtr(42)))
                Assert.NotEqual(outerId, ImGui.GetID("Item"u8));
            Assert.Equal(outerId, ImGui.GetID("Item"u8));

            using (new IdScopeGuard(42))
                Assert.NotEqual(outerId, ImGui.GetID("Item"u8));
            Assert.Equal(outerId, ImGui.GetID("Item"u8));
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
