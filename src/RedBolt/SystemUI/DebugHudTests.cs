// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class DebugHudTests
{
    private readonly GraphicsEngineFixture _fixture;

    public DebugHudTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Debug_hud_cycles_stats_renders_overlay_and_unsubscribes()
    {
        _fixture.ResetGraphics();
        DebugHud? hud = new DebugHud();
        var imguiContext = IntPtr.Zero;
        var frameStarted = false;
        try
        {
            Assert.True(_fixture.Context.HasSubscribedToEvent<GraphicsEvents.EndAllViewsRenderEvent>(hud));
            Assert.Equal(DebugHudMode.None, hud.GetMode());

            hud.CycleMode();
            Assert.Equal(DebugHudMode.Stats, hud.GetMode());
            hud.CycleMode();
            Assert.Equal(DebugHudMode.Mode, hud.GetMode());
            hud.CycleMode();
            Assert.Equal(DebugHudMode.All, hud.GetMode());
            hud.CycleMode();
            Assert.Equal(DebugHudMode.None, hud.GetMode());
            hud.ToggleAll();
            Assert.Equal(DebugHudMode.All, hud.GetMode());
            hud.ToggleAll();
            Assert.Equal(DebugHudMode.None, hud.GetMode());

            using var value = new Variant(42);
            hud.SetAppStats("Alpha", in value);
            hud.SetAppStats("Beta", "first");
            hud.SetAppStats("Beta", "second");
            Assert.True(hud.ResetAppStats("Alpha"));
            Assert.False(hud.ResetAppStats("Alpha"));
            hud.SetAppStats("Alpha", in value);

            imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;

            hud.SetMode(DebugHudMode.All);
            _fixture.Context.SendEvent<GraphicsEvents.EndAllViewsRenderEvent>();

            var window = ImGui.FindWindowByName("DebugHud"u8);
            Assert.False(window.IsNull);
            Assert.NotEqual(0, window.DrawList.VtxBufferSize);
            Assert.NotEqual(0u, (uint)(window.Flags & ImGuiWindowFlags.ImGuiWindowFlagsNoInputs));

            hud.ClearAppStats();
            Assert.False(hud.ResetAppStats("Beta"));

            hud.Dispose();
            Assert.False(_fixture.Context.HasSubscribedToEvent<GraphicsEvents.EndAllViewsRenderEvent>(hud));
            hud = null;
        }
        finally
        {
            hud?.Dispose();
            if (frameStarted)
                ImGui.EndFrame();
            if (imguiContext != IntPtr.Zero)
                ImGui.DestroyContext(imguiContext);
        }
    }
}
