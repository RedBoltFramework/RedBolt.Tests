namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Bindings.Sdl;
using RedBolt.Graphics;
using RedBolt.RenderAPI;

public sealed class GraphicsBackendLifecycleTests
{
    [GraphicsBackendFact]
    public void Open_gl_fixture_exposes_backend_context_and_can_reinitialize_after_close()
    {
        using var fixture = new GraphicsEngineFixture();
        var graphics = fixture.Graphics;
        var backendApi = graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>();

        Assert.NotNull(backendApi);
        Assert.NotEqual(nint.Zero, backendApi!.Context);
        Assert.Equal(RenderBackend.OpenGL, graphics.GetRenderBackend());
        Assert.True(graphics.IsInitialized);

        graphics.Close();

        Assert.False(graphics.IsInitialized);
        Assert.Null(graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>());

        fixture.ResetGraphics();

        var restoredApi = fixture.Graphics.GetGraphicsBackendApi<IOpenGLGraphicsApi>();
        Assert.NotNull(restoredApi);
        Assert.NotEqual(nint.Zero, restoredApi!.Context);
        Assert.True(fixture.Graphics.IsInitialized);
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class GraphicsBackendFactAttribute : Xunit.FactAttribute
    {
        public GraphicsBackendFactAttribute()
        {
            if (!IsEnabled("REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE"))
            {
                Skip = "Set REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE=1 to enable the native graphics lifecycle gate.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                Skip = "Native graphics lifecycle requires a display; use the software-renderer harness.";
        }
    }
}
