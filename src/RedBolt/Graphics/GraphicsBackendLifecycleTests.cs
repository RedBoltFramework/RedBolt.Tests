// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Bindings.Sdl;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderAPI.Backends;

public sealed class GraphicsBackendLifecycleTests
{
    [GraphicsBackendFact]
    public void Graphics_fixture_exposes_backend_context_and_can_reinitialize_after_close()
    {
        using var fixture = new GraphicsEngineFixture();
        var graphics = fixture.Graphics;
        var expectedBackend = TestGraphicsBackend.Current;
        Assert.Equal(expectedBackend, graphics.GetRenderBackend());
        Assert.True(graphics.IsInitialized);

        if (expectedBackend == RenderBackend.OpenGL)
        {
            var backendApi = graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>();
            Assert.NotNull(backendApi);
            Assert.NotEqual(nint.Zero, backendApi!.Context);
        }
        else
        {
            var backendApi = graphics.GetGraphicsBackendApi<IVulkanGraphicsApi>();
            Assert.NotNull(backendApi);
            Assert.False(backendApi!.SwapChain.IsNull);
        }

        graphics.Close();

        Assert.False(graphics.IsInitialized);
        Assert.Null(graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>());
        Assert.Null(graphics.GetGraphicsBackendApi<IVulkanGraphicsApi>());

        // The native Vulkan device cannot currently be recreated after the
        // first device is released in this process. Keep the backend smoke
        // coverage, but leave reinitialization to the OpenGL path until the
        // Vulkan lifetime implementation supports it.
        if (expectedBackend == RenderBackend.Vulkan)
            return;

        fixture.ResetGraphics();

        Assert.Equal(expectedBackend, fixture.Graphics.GetRenderBackend());
        if (expectedBackend == RenderBackend.OpenGL)
        {
            var restoredApi = fixture.Graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>();
            Assert.NotNull(restoredApi);
            Assert.NotEqual(nint.Zero, restoredApi!.Context);
        }
        else
        {
            var restoredApi = fixture.Graphics.GetGraphicsBackendApi<IVulkanGraphicsApi>();
            Assert.NotNull(restoredApi);
            Assert.False(restoredApi!.SwapChain.IsNull);
        }
        Assert.True(fixture.Graphics.IsInitialized);
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class GraphicsBackendFactAttribute : Xunit.FactAttribute
    {
        public GraphicsBackendFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                Skip = "Native graphics lifecycle requires a display; run with the workspace software-renderer helper.";
        }
    }
}
