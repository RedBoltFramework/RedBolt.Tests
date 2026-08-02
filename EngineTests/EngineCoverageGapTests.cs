namespace RedBolt.Tests;

using System.Threading;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public EngineCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Config_file_covers_definition_priority_empty_and_changed_value_paths()
    {
        using var config = new ConfigFile();
        using var priorityDefault = new Variant(1);
        using var fixedDefault = new Variant("fixed");
        using var optionalDefault = new Variant();

        config.DefineVariable("Priority", priorityDefault).CommandLinePriority();
        config.DefineVariable("Fixed", fixedDefault);
        config.DefineVariable("Optional", optionalDefault).SetOptional(VariantType.Int);

        using var definitions = new StringVariantMap
        {
            ["Priority"] = new Variant(2),
            ["Fixed"] = new Variant("replacement"),
            ["Additional"] = new Variant(3)
        };
        config.DefineVariables(definitions);

        using var priorityUpdate = new StringVariantMap
        {
            ["Priority"] = new Variant(4),
            ["Fixed"] = new Variant("ignored"),
            ["Unknown"] = new Variant(5)
        };
        config.UpdatePriorityVariables(priorityUpdate);

        Assert.Equal(4, config.GetVariable("Priority").GetInt());
        Assert.Equal("replacement", config.GetVariable("Fixed").GetString());
        Assert.Equal(3, config.GetVariable("Additional").GetInt());
        Assert.True(config.GetVariable("Optional").IsEmpty);
        Assert.Null(config.GetVariableDefinition("Unknown"));
        Assert.False(config.HasVariable("Priority"));

        using var currentPriority = new Variant(6);
        using var currentFixed = new Variant("replacement");
        using var currentAdditional = new Variant(7);
        config.SetVariable("Priority", currentPriority);
        config.SetVariable("Fixed", currentFixed);
        config.SetVariable("Additional", currentAdditional);

        Assert.True(config.HasVariable("Priority"));
        Assert.Equal(6, config.GetVariable("Priority").GetInt());
        Assert.Equal("replacement", config.GetVariable("Fixed").GetString());
        Assert.True(config.GetVariable("Missing").IsEmpty);

        using var changed = config.GetChangedVariables(ApplicationFlavor.Empty);
        Assert.Equal(2, changed.Count);
        Assert.Equal(6, changed["Priority"].GetInt());
        Assert.Equal(7, changed["Additional"].GetInt());
        Assert.DoesNotContain("Fixed", changed.Keys);

        config.SetVariable("Priority", default);
        Assert.False(config.HasVariable("Priority"));
        Assert.Equal(4, config.GetVariable("Priority").GetInt());

        Assert.Throws<ArgumentException>(() => config.DefineVariable(" ", new Variant(1)));
        Assert.Throws<ArgumentException>(() => config.SetVariable(" ", new Variant(1)));
        Assert.Throws<ArgumentException>(() => config.HasVariable(" "));
        Assert.Throws<ArgumentException>(() => config.GetVariableDefinition(" "));
    }

    [Fact]
    public void Config_file_prefers_specific_flavors_and_ignores_nonmatching_entries()
    {
        using var config = new ConfigFile();
        config.VariablesPerFlavor.Add(CreateFlavor(string.Empty, 1));
        config.VariablesPerFlavor.Add(CreateFlavor("platform=desktop", 2));
        config.VariablesPerFlavor.Add(CreateFlavor("platform=desktop,linux;quality=high", 3));
        config.VariablesPerFlavor.Add(CreateFlavor("platform=windows", 4));
        config.VariablesPerFlavor.Add(new ConfigFile.ConfigFlavor
        {
            Flavor = new ApplicationFlavorPattern(),
            Variables = null!
        });

        var flavor = new ApplicationFlavor("platform=desktop,linux;quality=high");
        using var selected = config.GetFlavorVariables(flavor);

        Assert.Equal(3, selected["Value"].GetInt());

        using var clone = config.VariablesPerFlavor[2].Clone();
        clone.Variables["Value"] = new Variant(9);
        Assert.Equal(3, config.VariablesPerFlavor[2].Variables["Value"].GetInt());
        clone.Dispose();
    }

    [Fact]
    public void Config_file_loads_flavor_edges_and_rejects_invalid_overrides_from_memory()
    {
        var fileSystem = _fixture.Context.GetSubsystem<VirtualFileSystem>()!;
        using var mount = new MountedExternalMemory("engine-coverage");
        fileSystem.Mount(mount);
        mount.LinkMemory("defaults.json", """
            {
              "Default": [
                42,
                {
                  "Flavor": [
                    7,
                    { "key": "", "tags": ["ignored"] },
                    { "key": "platform", "tags": ["desktop", "linux", ""] },
                    { "key": "quality", "tags": "not-an-array" }
                  ],
                  "Variables": [
                    7,
                    { "key": "" },
                    { "key": "Value", "type": "Int", "value": 11 }
                  ]
                },
                {
                  "Flavor": [ { "key": "platform", "tags": ["desktop"] } ],
                  "Variables": [ { "key": "Value", "type": "Int", "value": 12 } ]
                }
              ]
            }
            """);
        mount.LinkMemory("overrides.json", """
            {
              "Unknown": { "type": "Int", "value": 1 },
              "Value": { "type": "Int", "value": 13 },
              "WrongType": { "type": "String", "value": "ignored" },
              "Fixed": { "type": "Int", "value": 14 },
              "Ignored": 7
            }
            """);
        mount.LinkMemory("empty-overrides.json", "[]");

        try
        {
            using var config = new ConfigFile();
            using var valueDefault = new Variant(0);
            using var wrongTypeDefault = new Variant(2);
            using var fixedDefault = new Variant(3);
            config.DefineVariable("Value", valueDefault).Overridable();
            config.DefineVariable("WrongType", wrongTypeDefault).Overridable();
            config.DefineVariable("Fixed", fixedDefault);

            var flavor = new ApplicationFlavor("platform=desktop,linux");
            Assert.True(config.LoadDefaults("engine-coverage://defaults.json", flavor));
        Assert.Equal(12, config.GetVariable("Value").GetInt());

            Assert.True(config.LoadOverrides("engine-coverage://overrides.json"));
            Assert.Equal(13, config.GetVariable("Value").GetInt());
            Assert.Equal(2, config.GetVariable("WrongType").GetInt());
            Assert.Equal(3, config.GetVariable("Fixed").GetInt());
            Assert.True(config.LoadOverrides("engine-coverage://empty-overrides.json"));
            Assert.Equal(13, config.GetVariable("Value").GetInt());
            Assert.False(config.SaveOverrides("engine-coverage://defaults.json", flavor));
        }
        finally
        {
            fileSystem.Unmount(mount);
        }
    }

    [Fact]
    public void Application_flavor_matches_wildcards_missing_tags_and_extra_components()
    {
        var exact = new ApplicationFlavor("platform=desktop,linux;quality=high");
        Assert.Equal(0u, exact.Matches(new ApplicationFlavorPattern("platform=desktop,linux;quality=high")));
        Assert.Equal(2u, exact.Matches(new ApplicationFlavorPattern("platform=*;quality=high")));
        Assert.Null(exact.Matches(new ApplicationFlavorPattern("platform=desktop,windows")));
        Assert.Null(exact.Matches(new ApplicationFlavorPattern("missing=value")));
        Assert.NotNull(exact.Matches(new ApplicationFlavorPattern("missing=*")));

        var wildcard = new ApplicationFlavor("platform=*;quality=high");
        Assert.Equal(2u, wildcard.Matches(new ApplicationFlavorPattern("platform=desktop,linux;quality=high")));

        var differentWildcard = new ApplicationFlavor(new ApplicationFlavorMap
        {
            ["*"] = new ApplicationFlavorComponent { "desktop" }
        });
        Assert.Equal(1u, differentWildcard.Matches(new ApplicationFlavorPattern()));

        var parsed = new ApplicationFlavorPattern("platform=desktop,linux;platform=linux;invalid;*=*");
        Assert.Equal("desktop,linux", string.Join(',', parsed.Components["platform"].OrderBy(static tag => tag)));
        Assert.Contains("*", parsed.Components["*"]);
    }

    [Fact]
    public void Engine_command_line_covers_full_parameter_registration_and_aliases()
    {
        var parameters = new EngineParameters();
        var parser = new CommandLineParser("RedBolt.EngineCoverage");
        Engine.DefineParameters(parser, parameters);

        var result = parser.Parse(
        [
            "--headless", "--validate-shaders", "--nolimit", "--landscape", "--portrait",
            "--nosound", "--noip", "--speakermode=5", "--nothreads", "--vsync", "--windowed",
            "--full-screen", "--borderless", "--resizable", "--quiet", "--log=trace",
            "--log-file=engine.log", "--no-log-rotate-on-open", "--log-rotate-by-size",
            "--log-rotate-max-files=7", "--log-rotate-max-size=4096", "--width=123",
            "--height=456", "--monitor=2", "--hz=60", "--multisample=4", "--sound-buffer=50",
            "--mix-rate=48000", "--prefix-paths=prefix-a;prefix-b", "--resource-paths=Data;CoreData",
            "--resource-packages=packages", "--autoload-paths=Autoload", "--config-name=Config.json",
            "--tq=0", "--tf=nearest-anisotropic", "--af=0", "--touch", "--timeout=9",
            "--plugins=One;Two", "--main=One", "--log-shader-sources", "--discard-shader-cache",
            "--no-save-shader-cache", "--xr", "--profile", "--no-profile", "--vulkan",
            "--app-name=Coverage", "--organization=RedBolt", "--preferences-dir=/tmp/prefs",
            "--window-title=Coverage Window", "--window-icon=icon.png", "--maximize",
            "--window-x=10", "--window-y=20", "--resource-root-file=Root.ini",
            "--package-cache-dir=cache", "--shader-cache-dir=shader-cache", "--pso-cache=pso.bin",
            "--shader-policy=Optimize", "--gpu-debug", "--adapter-id=3", "--reload-plugins",
            "--imgui-flags=9", "--tweak-d3d12={}", "--tweak-vulkan={}",
            "--engine-auto-load-scripts", "--no-load-fonts", "--screenshot-on-exit=shot.png",
            "--screenshot-parameters=3,2"
        ]);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(parameters.Headless);
        Assert.True(parameters.ValidateShaders);
        Assert.False(parameters.FrameLimiter);
        Assert.Contains("LandscapeLeft", parameters.Orientations);
        Assert.Contains("Portrait", parameters.Orientations);
        Assert.False(parameters.Sound);
        Assert.False(parameters.SoundInterpolation);
        Assert.Equal(5, parameters.SoundMode);
        Assert.False(parameters.WorkerThreads);
        Assert.True(parameters.VSync);
        Assert.True(parameters.FullScreen);
        Assert.True(parameters.Borderless);
        Assert.True(parameters.WindowResizable);
        Assert.True(parameters.LogQuiet);
        Assert.Equal(LogLevel.Trace, parameters.LogLevel);
        Assert.Equal("engine.log", parameters.LogName);
        Assert.False(parameters.LogRotateOnOpen);
        Assert.True(parameters.LogRotateBySize);
        Assert.Equal(7u, parameters.LogMaxFiles);
        Assert.Equal(4096u, parameters.LogMaxSize);
        Assert.Equal(123, parameters.WindowWidth);
        Assert.Equal(456, parameters.WindowHeight);
        Assert.Equal(2, parameters.Monitor);
        Assert.Equal(60, parameters.RefreshRate);
        Assert.Equal(4, parameters.MultiSample);
        Assert.Equal(50, parameters.SoundBuffer);
        Assert.Equal(48000, parameters.SoundMixRate);
        Assert.Equal("prefix-a;prefix-b", parameters.ResourcePrefixPaths);
        Assert.Equal(MaterialQuality.Low, parameters.TextureQuality);
        Assert.Equal(TextureFilterMode.Anisotropic, parameters.TextureFilterMode);
        Assert.Equal(1, parameters.TextureAnisotropy);
        Assert.True(parameters.TouchEmulation);
        Assert.Equal(9, parameters.TimeOut);
        Assert.Equal("One;Two", parameters.Plugins);
        Assert.Equal("One", parameters.MainPlugin);
        Assert.True(parameters.ShaderLogSources);
        Assert.True(parameters.DiscardShaderCache);
        Assert.False(parameters.SaveShaderCache);
        Assert.True(parameters.XR);
        Assert.False(parameters.Profile);
        Assert.Equal(RenderBackend.Vulkan, parameters.RenderBackend);
        Assert.Equal("Coverage", parameters.ApplicationName);
        Assert.Equal("RedBolt", parameters.OrganizationName);
        Assert.Equal("/tmp/prefs", parameters.ApplicationPreferencesDir);
        Assert.Equal("Coverage Window", parameters.WindowTitle);
        Assert.Equal("icon.png", parameters.WindowIcon);
        Assert.True(parameters.WindowMaximize);
        Assert.Equal(10, parameters.WindowPositionX);
        Assert.Equal(20, parameters.WindowPositionY);
        Assert.Equal("Root.ini", parameters.ResourceRootFile);
        Assert.Equal("cache", parameters.PackageCacheDir);
        Assert.Equal("shader-cache", parameters.ShaderCacheDir);
        Assert.Equal("pso.bin", parameters.PsoCache);
        Assert.Equal("Optimize", parameters.ShaderPolicy);
        Assert.True(parameters.GPUDebug);
        Assert.Equal(3u, parameters.RenderAdapterId);
        Assert.True(parameters.ReloadPlugins);
        Assert.Equal(9u, parameters.DearImGuiFlags);
        Assert.Equal("{}", parameters.TweakD3D12);
        Assert.Equal("{}", parameters.TweakVulkan);
        Assert.True(parameters.EngineAutoLoadScripts);
        Assert.False(parameters.LoadFonts);
        Assert.Equal("shot.png", parameters.ScreenshotOnExit);
        Assert.Equal(3, parameters.ScreenshotCount);
        Assert.Equal(2, parameters.ScreenshotFrameDelay);
    }

    [Theory]
    [InlineData("trace", LogLevel.Trace)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData(" info ", LogLevel.Info)]
    [InlineData("Warn", LogLevel.Warning)]
    [InlineData("ERROR", LogLevel.Error)]
    public void Engine_command_line_parses_all_log_levels(string value, LogLevel expected)
    {
        var parameters = new EngineParameters();
        var parser = new CommandLineParser("RedBolt.EngineCoverage");
        Engine.DefineParameters(parser, parameters);

        var result = parser.Parse(["--log", value]);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expected, parameters.LogLevel);
    }

    [Theory]
    [InlineData("nearest", TextureFilterMode.Nearest)]
    [InlineData("point", TextureFilterMode.Nearest)]
    [InlineData("bilinear", TextureFilterMode.Bilinear)]
    [InlineData("linear", TextureFilterMode.Bilinear)]
    [InlineData("trilinear", TextureFilterMode.Trilinear)]
    [InlineData("anisotropic", TextureFilterMode.Anisotropic)]
    [InlineData("nearest_anisotropic", TextureFilterMode.NearestAnisotropic)]
    [InlineData("default", TextureFilterMode.Default)]
    public void Engine_command_line_parses_all_texture_filter_modes(string value, TextureFilterMode expected)
    {
        var parameters = new EngineParameters();
        var parser = new CommandLineParser("RedBolt.EngineCoverage");
        Engine.DefineParameters(parser, parameters);

        var result = parser.Parse(["--tf=" + value]);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expected, parameters.TextureFilterMode);
    }

    [Theory]
    [InlineData("", "Invalid screenshot parameters")]
    [InlineData(",2", "Invalid screenshot parameters")]
    [InlineData("2", "Invalid screenshot parameters")]
    [InlineData("2,1,0", "Invalid screenshot parameters")]
    [InlineData("0,1", "must be positive")]
    [InlineData("2,-1", "cannot be negative")]
    public void Engine_command_line_rejects_invalid_screenshot_parameters(string value, string message)
    {
        var parameters = new EngineParameters();
        var parser = new CommandLineParser("RedBolt.EngineCoverage");
        Engine.DefineParameters(parser, parameters);

        var result = parser.Parse(["--screenshot-parameters=" + value]);

        Assert.False(result.IsSuccess);
        Assert.Contains(message, result.ErrorMessage);
    }

    [Fact]
    public void Engine_parameter_binding_coerces_scalars_enums_nullable_values_and_custom_values()
    {
        using var engine = new Engine();

        engine.SetParameter(EngineParameter.Monitor, "3");
        engine.SetParameter(EngineParameter.LogMaxFiles, "7");
        engine.SetParameter(EngineParameter.RenderAdapterId, "9");
        engine.SetParameter(EngineParameter.RenderBackend, "OpenGL");
        engine.SetParameter(EngineParameter.TextureFilterMode, "Bilinear");
        engine.SetParameter(EngineParameter.TextureQuality, "Low");
        engine.SetParameter(EngineParameter.ExternalWindow, 17);
        engine.SetParameter(EngineParameter.Headless, "true");
        engine.SetParameter(EngineParameter.TextureAnisotropy, "8");
        engine.SetParameter(EngineParameter.TouchEmulation, true);
        engine.SetParameter(EngineParameter.ApplicationName, 123);
        engine.SetParameter(EngineParameter.WindowPositionX, null);
        engine.SetParameter("CustomValue", 42);

        Assert.Equal(3, engine.GetParameter(EngineParameter.Monitor));
        Assert.Equal(7u, engine.GetParameter(EngineParameter.LogMaxFiles));
        Assert.Equal(9u, engine.GetParameter(EngineParameter.RenderAdapterId));
        Assert.Equal(RenderBackend.OpenGL, engine.GetParameter(EngineParameter.RenderBackend));
        Assert.Equal(TextureFilterMode.Bilinear, engine.GetParameter(EngineParameter.TextureFilterMode));
        Assert.Equal(MaterialQuality.Low, engine.GetParameter(EngineParameter.TextureQuality));
        Assert.Equal((nint)17, engine.GetParameter(EngineParameter.ExternalWindow));
        Assert.True((bool)engine.GetParameter(EngineParameter.Headless)!);
        Assert.Equal(8, engine.GetParameter(EngineParameter.TextureAnisotropy));
        Assert.True((bool)engine.GetParameter(EngineParameter.TouchEmulation)!);
        Assert.Equal("123", engine.GetParameter(EngineParameter.ApplicationName));
        Assert.Null(engine.GetParameter(EngineParameter.WindowPositionX));
        Assert.True(engine.HasParameter("CustomValue"));
        Assert.Equal(42, engine.GetParameter("CustomValue"));
        Assert.True(engine.HasParameter(EngineParameter.WindowWidth));
        Assert.False(engine.HasParameter("MissingParameter"));
        Assert.Null(engine.GetParameter("MissingParameter"));

        Assert.Throws<InvalidCastException>(() => engine.SetParameter(EngineParameter.Monitor, null));
        Assert.Throws<ArgumentException>(() => engine.SetParameter(" ", 1));
    }

    [Fact]
    public void Engine_frame_limit_covers_defaults_inactive_fallback_and_smoothing_paths()
    {
        using var uninitialized = new Engine();
        uninitialized.ApplyFrameLimit();
        Assert.False(uninitialized.IsInitialized);

        using var engine = new Engine();
        Assert.True(engine.Initialize(new EngineParameters
        {
            ApplicationName = "RedBolt.EngineCoverage.FrameLimit",
            ConfigName = string.Empty,
            FrameLimiter = false,
            Headless = true,
            LogName = "none://"
        }));

        Assert.True(engine.IsHeadless);
        Assert.Equal(0, engine.MaxFps);

        engine.SetMinFps(-1);
        engine.SetMaxFps(-1);
        engine.SetMaxInactiveFps(-1);
        engine.SetTimeStepSmoothing(0);
        engine.SetNextTimeStep(-1.0f);
        Assert.Equal(0, engine.MinFps);
        Assert.Equal(0, engine.MaxFps);
        Assert.Equal(0, engine.MaxInactiveFps);
        Assert.Equal(1, engine.TimeStepSmoothing);
        Assert.Equal(0.0f, engine.GetNextTimeStep());
        engine.ApplyFrameLimit();

        engine.SetMaxFps(1_000_000);
        engine.SetMaxInactiveFps(1_000_000);
        engine.SetMinFps(1_000_000);
        engine.SetTimeStepSmoothing(2);
        engine.ApplyFrameLimit();
        engine.ApplyFrameLimit();
        engine.ApplyFrameLimit();

        Assert.Equal(2, engine.TimeStepSmoothing);
        Assert.True(engine.GetNextTimeStep() >= 0.0f);
    }

    [Fact]
    public void Application_state_applies_active_viewport_mouse_and_fog_settings()
    {
        using var engine = new Engine();
        Assert.True(engine.Initialize(new EngineParameters
        {
            ApplicationName = "RedBolt.EngineCoverage.State",
            ConfigName = string.Empty,
            Headless = true,
            LogName = "none://"
        }));

        using var scene = new Scene();
        using var viewport = new Viewport(scene, null);
        using var state = new ProbeState();
        using var bundle = new StringVariantMap();

        state.SetMouseVisible(false);
        state.SetMouseGrabbed(true);
        state.SetMouseMode(MouseMode.Wrap);
        state.SetNumViewports(2);
        state.SetViewport(1, viewport);
        state.SetDefaultFogColor(new Color(0.2f, 0.3f, 0.4f));

        Assert.False(state.IsActive);
        Assert.Same(viewport, state.GetViewportForScene(scene, 0));
        Assert.Null(state.GetViewportForScene(scene, 1));

        state.Activate(bundle);
        state.Activate(bundle);
        state.SetMouseVisible(true);
        state.SetMouseGrabbed(false);
        state.SetMouseMode(MouseMode.Free);
        state.SetNumViewports(1);
        state.SetViewport(0, viewport);
        state.SetDefaultFogColor(new Color(0.5f, 0.6f, 0.7f));

        Assert.True(state.IsActive);
        Assert.True(state.IsMouseVisible);
        Assert.False(state.IsMouseGrabbed);
        Assert.Equal(MouseMode.Free, state.MouseMode);
        Assert.Same(viewport, state.GetViewport(0));
        Assert.Equal(new Color(0.5f, 0.6f, 0.7f), state.DefaultFogColor);

        state.Deactivate();
        state.Deactivate();
        Assert.False(state.IsActive);
    }

    [Fact]
    public void State_manager_covers_wait_to_exit_cached_type_and_creation_fallbacks()
    {
        using var engine = new Engine();
        Assert.True(engine.Initialize(new EngineParameters
        {
            ApplicationName = "RedBolt.EngineCoverage.StateManager",
            ConfigName = string.Empty,
            Headless = true,
            LogName = "none://"
        }));
        var manager = engine.GetSubsystem<StateManager>()!;
        manager.Reset();
        manager.FadeInDuration = 1.0f;
        manager.FadeOutDuration = 1.0f;

        using var first = new BlockingState { AllowLeave = false };
        using var second = new BlockingState();
        manager.EnqueueState(first);
        manager.EnqueueState(second);
        Assert.Equal(second.GetTypeNameHash(), manager.TargetState);

        manager.Update(1.0f);
        Assert.Same(first, manager.State);
        Assert.False(first.TransitionWasAllowed);

        first.AllowLeave = true;
        manager.Update(1.0f);
        manager.Update(1.0f);
        Assert.Same(second, manager.State);
        Assert.True(second.IsActive);
        manager.Reset();
        Assert.Null(manager.State);
        Assert.Equal(StringHash.Empty, manager.TargetState);

        using var cached = new BlockingState();
        manager.CacheState(cached);
        Assert.Same(cached, manager.GetCachedState(cached.GetTypeNameHash()));
        Assert.Null(manager.GetCachedState(new StringHash("MissingState")));
        manager.EnqueueState(cached.GetTypeNameHash());
        manager.Update(1.0f);
        manager.Update(1.0f);
        Assert.Same(cached, manager.State);
        manager.Reset();

        manager.EnqueueState(new StringHash("MissingApplicationState"));
        manager.Update(1.0f);
        Assert.Null(manager.State);
        Assert.Equal(StringHash.Empty, manager.TargetState);

        using var rejected = new BlockingState();
        using var bundle = new StringVariantMap();
        var worker = new Thread(() => manager.EnqueueState(rejected, bundle));
        worker.Start();
        worker.Join();
        Assert.Null(manager.State);
        manager.EnqueueState((ApplicationState?)null, bundle);
        Assert.Null(manager.State);
        manager.Reset();
    }

    [GraphicsFact]
    public void Engine_uses_default_graphics_tweak_values_when_json_is_invalid()
    {
        foreach (var shaderPolicy in new[] { "Translate", "Optimize", "Verbatim", "UnsupportedPolicy" })
        {
            using var engine = new Engine();
            Assert.True(engine.Initialize(new EngineParameters
            {
                ApplicationName = "RedBolt.EngineCoverage.GraphicsFallback",
                ConfigName = string.Empty,
                Headless = false,
                RenderBackend = RenderBackend.OpenGL,
                TweakD3D12 = "[",
                TweakVulkan = "{",
                ShaderPolicy = shaderPolicy,
                WindowHidden = true,
                WindowWidth = 64,
                WindowHeight = 64,
                LogName = "none://"
            }));

            Assert.False(engine.IsHeadless);
        }
    }

    private static ConfigFile.ConfigFlavor CreateFlavor(string pattern, int value)
    {
        return new ConfigFile.ConfigFlavor
        {
            Flavor = new ApplicationFlavorPattern(pattern),
            Variables = new StringVariantMap
            {
                ["Value"] = new Variant(value)
            }
        };
    }

    private sealed class ProbeState : ApplicationState
    {
    }

    private sealed class BlockingState : ApplicationState
    {
        public bool AllowLeave { get; set; } = true;

        public bool TransitionWasAllowed { get; private set; }

        public override bool CanLeaveState() => AllowLeave;

        public override void TransitionStarted()
        {
            TransitionWasAllowed = true;
        }
    }
}
