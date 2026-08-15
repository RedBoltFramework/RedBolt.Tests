namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Audio;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>
/// Fixture that boots a headless RedBolt engine once per test collection.
/// Tests that need an initialized Engine should use [Collection(nameof(HeadlessEngineCollection))].
/// </summary>
public class HeadlessEngineFixture : IDisposable
{
    public HeadlessEngineFixture()
    {
        Engine = new Engine();
        EditorPlugins = new EditorPluginManager();
        Context.RegisterSubsystem(EditorPlugins);
        var log = Engine.GetSubsystem<Log>()!;
        log.SetQuiet(true);
        LogSink = new TestLogSink(log);
        Audio.RegisterAudioSubsystem(Engine.Context);
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests",
            Headless = true,
            LogQuiet = true,
            ResourcePaths = "CoreData;Data",
            ResourcePrefixPaths = RbfxTestData.Root,
            WindowMaximize = false,
            WindowWidth = 1280,
            WindowHeight = 720
        };
        Engine.Initialize(parameters);

        var model = new Model { Name = "Models/ProceduralBox" };
        model.SetNumGeometries(1);
        var geometry = new Geometry();
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 36, 0, 36);
        model.SetGeometry(0, 0, geometry);
        Resources.AddManualResource(model);
    }

    public Engine Engine { get; }

    public TestLogSink LogSink { get; }

    public EditorPluginManager EditorPlugins { get; }

    public Context Context => Engine.Context;

    public Time Time => Engine.GetSubsystem<Time>()!;

    public ResourceCache Resources => Engine.GetSubsystem<ResourceCache>()!;

    public Input Input => Engine.GetSubsystem<Input>()!;

    public Renderer Renderer => Engine.GetSubsystem<Renderer>()!;

    public Graphics Graphics => Engine.GetSubsystem<Graphics>()!;

    public RenderDevice RenderDevice => Engine.GetSubsystem<RenderDevice>()!;

    public RenderContext RenderContext => RenderDevice.GetRenderContext();

    public PipelineStateCache PipelineStateCache => Engine.GetSubsystem<PipelineStateCache>()!;

    public virtual void Dispose()
    {
        try
        {
            Context.RemoveSubsystem<EditorPluginManager>();
            EditorPlugins.Dispose();
            Engine.Shutdown();
        }
        finally
        {
            LogSink.Dispose();
        }
    }

}

internal static class TestGraphicsBackend
{
    public static RenderBackend Current
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("REDBOLT_TEST_GRAPHICS_API");
            if (string.IsNullOrWhiteSpace(value))
                return RenderBackend.OpenGL;

            return value.Trim().ToLowerInvariant() switch
            {
                "opengl" or "gl" => RenderBackend.OpenGL,
                "vulkan" or "vk" => RenderBackend.Vulkan,
                _ => throw new InvalidOperationException(
                    $"Unsupported REDBOLT_TEST_GRAPHICS_API '{value}'. Expected 'opengl' or 'vulkan'.")
            };
        }
    }
}

[CollectionDefinition(nameof(HeadlessEngineCollection), DisableParallelization = true)]
public sealed class HeadlessEngineCollection : ICollectionFixture<HeadlessEngineFixture>
{
}

public sealed class GraphicsEngineFixture : HeadlessEngineFixture
{
    private readonly EngineParameters _defaultGraphicsParameters = new()
    {
        ApplicationName = "RedBolt.Tests",
        RenderBackend = TestGraphicsBackend.Current,
        FullScreen = false,
        Borderless = false,
        MultiSample = 1,
        WindowMaximize = false,
        WindowHidden = true,
        WindowWidth = 64,
        WindowHeight = 64
    };

    public GraphicsEngineFixture() : this(true)
    {
    }

    internal GraphicsEngineFixture(bool initializeGraphics)
    {
        if (initializeGraphics)
            InitializeGraphics(_defaultGraphicsParameters);
    }

    public void InitializeGraphics(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.RenderBackend ??= TestGraphicsBackend.Current;
        parameters.WindowHidden = true;
        if (Graphics.IsInitialized)
        {
            Graphics.SetMode(parameters.WindowWidth, parameters.WindowHeight,
                parameters.FullScreen, parameters.Borderless, parameters.WindowResizable,
                false, parameters.VSync, false,
                parameters.MultiSample, parameters.Monitor, parameters.RefreshRate);
            return;
        }
        if (Input.IsInitialized)
            Input.Shutdown();

        Input.Initialize(parameters);
        var deviceSettings = new RenderDeviceSettings
        {
            Backend = RenderAPIUtils.SelectRenderBackend(parameters.RenderBackend),
            ExternalWindowHandle = parameters.ExternalWindow,
            GpuDebug = parameters.GPUDebug,
            AdapterId = parameters.RenderAdapterId.HasValue
                ? checked((int)parameters.RenderAdapterId.Value)
                : null
        };
        var windowSettings = new WindowSettings
        {
            Mode = parameters.FullScreen
                ? WindowMode.Fullscreen
                : parameters.Borderless
                    ? WindowMode.Borderless
                    : WindowMode.Windowed,
            Size = new Vector2i(parameters.WindowWidth, parameters.WindowHeight),
            Title = parameters.WindowTitle,
            Resizable = parameters.WindowResizable,
            Monitor = parameters.Monitor,
            VSync = parameters.VSync,
            RefreshRate = parameters.RefreshRate,
            MultiSample = parameters.MultiSample,
            SRgb = true
        };
        var requestedShaderPolicy = parameters.ShaderPolicy switch
        {
            nameof(ShaderTranslationPolicy.Translate) => ShaderTranslationPolicy.Translate,
            nameof(ShaderTranslationPolicy.Optimize) => ShaderTranslationPolicy.Optimize,
            nameof(ShaderTranslationPolicy.Verbatim) => ShaderTranslationPolicy.Verbatim,
            _ => (ShaderTranslationPolicy?)null
        };
        Graphics.Configure(new GraphicsSettings
        {
            RenderDevice = deviceSettings,
            ShaderTranslationPolicy = RenderAPIUtils.SelectShaderTranslationPolicy(
                deviceSettings.Backend, requestedShaderPolicy),
            ShaderCacheDir = FileIdentifier.FromUri(parameters.ShaderCacheDir),
            LogShaderSources = parameters.ShaderLogSources,
            ValidateShaders = parameters.ValidateShaders,
            DiscardShaderCache = parameters.DiscardShaderCache,
            CacheShaders = parameters.SaveShaderCache
        });
        Graphics.Initialize(parameters, deviceSettings, windowSettings, Input);
        Graphics.InitializePipelineStateCache(FileIdentifier.FromUri(parameters.PsoCache));
    }

    public void ResetGraphics()
    {
        if (!Graphics.IsInitialized)
            InitializeGraphics(_defaultGraphicsParameters);
        else
            Graphics.SetMode(_defaultGraphicsParameters.WindowWidth, _defaultGraphicsParameters.WindowHeight,
                _defaultGraphicsParameters.FullScreen, _defaultGraphicsParameters.Borderless,
                _defaultGraphicsParameters.WindowResizable, false, _defaultGraphicsParameters.VSync, false,
                _defaultGraphicsParameters.MultiSample, _defaultGraphicsParameters.Monitor,
                _defaultGraphicsParameters.RefreshRate);
    }

    public override void Dispose()
    {
        if (Graphics.IsInitialized)
        {
            RenderDevice.InvalidateDeviceState();
            Graphics.Close();
        }
        if (Input.IsInitialized)
            Input.Shutdown();
        base.Dispose();
    }
}

[CollectionDefinition(nameof(GraphicsEngineCollection), DisableParallelization = true)]
public sealed class GraphicsEngineCollection : ICollectionFixture<GraphicsEngineFixture>
{
}

[CollectionDefinition(nameof(GraphicsLifecycleCollection), DisableParallelization = true)]
public sealed class GraphicsLifecycleCollection
{
}
