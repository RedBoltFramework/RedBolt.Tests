namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Platform;
using RedBolt.RenderAPI;

public sealed class PlatformCompositionTests
{
    [Fact]
    public void Linux_platform_exposes_OpenGL_and_Vulkan_backends()
    {
        var runtimeServices = Context.Instance.RuntimeServices;

        Assert.True(runtimeServices.Platform.SupportsFileLogging);
        Assert.IsType<OpenGLGraphicsBackend>(runtimeServices.GetGraphicsBackend(RenderBackend.OpenGL));
        Assert.IsType<VulkanGraphicsBackend>(runtimeServices.GetGraphicsBackend(RenderBackend.Vulkan));
        Assert.NotNull(runtimeServices.GetGraphicsApi<OpenGLGraphicsBackend>(RenderBackend.OpenGL));
        Assert.Null(runtimeServices.GetGraphicsApi<VulkanGraphicsBackend>(RenderBackend.OpenGL));
    }

    [Fact]
    public void Linux_platform_rejects_unavailable_backend()
    {
        var runtimeServices = Context.Instance.RuntimeServices;

        Assert.Throws<InvalidOperationException>(() =>
            runtimeServices.GetGraphicsBackend(RenderBackend.D3D11));
    }

    [Fact]
    public void Runtime_services_reject_duplicate_backends_and_preserve_backend_lookup_errors()
    {
        var backend = new FakeGraphicsBackend(RenderBackend.OpenGL);
        Assert.Throws<ArgumentException>(() => new RuntimeServices(
            new FakePlatformRuntime(), [backend, new FakeGraphicsBackend(RenderBackend.OpenGL)]));

        var services = new RuntimeServices(new FakePlatformRuntime(), [backend]);
        Assert.Same(backend, services.GetGraphicsBackend(RenderBackend.OpenGL));
        Assert.Null(services.GetGraphicsApi<OpenGLGraphicsBackend>(RenderBackend.OpenGL));
        Assert.Throws<InvalidOperationException>(() => services.GetGraphicsBackend(RenderBackend.Vulkan));
    }

    private sealed class FakePlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => true;

        public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

        public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) => default;
    }

    private sealed class FakeGraphicsBackend(RenderBackend backend) : IGraphicsBackend
    {
        public RenderBackend Backend => backend;

        public bool UsesOpenGLWindow => false;

        public void ConfigureWindow()
        {
        }

        public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
            WindowSettings windowSettings, SdlWindow window, IPlatformRuntime platform) =>
            throw new NotSupportedException();
    }
}
