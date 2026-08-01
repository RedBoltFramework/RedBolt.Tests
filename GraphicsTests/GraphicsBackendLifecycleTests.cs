namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.RenderAPI;

public sealed class GraphicsBackendLifecycleTests
{
    [Fact]
    public void Open_gl_fixture_exposes_backend_context_and_can_reinitialize_after_close()
    {
        if (!IsEnabled("REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE"))
            return;
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException(
                "REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE requires an X11 DISPLAY.");

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
}
