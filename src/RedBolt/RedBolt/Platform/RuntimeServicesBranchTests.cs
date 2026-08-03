namespace RedBolt.Tests;

using System;
using System.Reflection;

using RedBolt.Bindings.Sdl;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Platform;
using RedBolt.RenderAPI;

public sealed class RuntimeServicesBranchTests
{
    [Fact]
    public void Platform_and_graphics_api_lookups_distinguish_known_and_unknown_types()
    {
        var platform = new FirstPlatformRuntime();
        var backend = new FakeGraphicsBackend(RenderBackend.OpenGL);
        var services = new RuntimeServices(platform, [backend]);

        Assert.Same(platform, services.GetPlatformApi<FirstPlatformRuntime>());
        Assert.Null(services.GetPlatformApi<SecondPlatformRuntime>());
        Assert.Same(backend, services.GetGraphicsApi<FakeGraphicsBackend>(RenderBackend.OpenGL));
        Assert.Null(services.GetGraphicsApi<FakeGraphicsBackend>(RenderBackend.Vulkan));
    }

    [Fact]
    public void Default_rejects_activation_of_a_different_platform_runtime_type()
    {
        var defaultField = typeof(RuntimeServices).GetField(
            "<Default>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        var original = defaultField.GetValue(null);

        try
        {
            defaultField.SetValue(null, new RuntimeServices(new FirstPlatformRuntime(), []));

            var method = typeof(RuntimeServices).GetMethod(
                "SetDefault", BindingFlags.Static | BindingFlags.NonPublic)!;
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
                [new RuntimeServices(new SecondPlatformRuntime(), [])]));

            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }
        finally
        {
            defaultField.SetValue(null, original);
        }
    }

    private sealed class FirstPlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => true;

        public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

        public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) => default;
    }

    private sealed class SecondPlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => false;

        public bool UseHighDensityWindow => true;

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
