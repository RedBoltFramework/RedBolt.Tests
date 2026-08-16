// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EngineDeterministicBranchTests
{
    [Fact]
    public void Engine_command_line_rejects_unsupported_deterministic_values()
    {
        var invalidArguments = new Dictionary<string, string>
        {
            ["--log=verbose"] = "Unsupported log level",
            ["--tq=-1"] = "out of range",
            ["--tq=16"] = "out of range",
            ["--tf=unsupported"] = "Unsupported texture filter mode"
        };

        foreach (var (argument, message) in invalidArguments)
        {
            var parameters = new EngineParameters();
            var parser = new CommandLineParser("RedBolt.EngineDeterministicBranchTests");
            Engine.DefineParameters(parser, parameters);

            var result = parser.Parse([argument]);

            Assert.False(result.IsSuccess, argument);
            Assert.Contains(message, result.ErrorMessage);
        }
    }

    [Fact]
    public void Engine_parameter_binding_coerces_numeric_enum_and_boolean_values()
    {
        using var engine = new Engine();

        engine.SetParameter(EngineParameter.RenderBackend, (int)RenderBackend.Vulkan);
        engine.SetParameter(EngineParameter.TextureQuality, 2);
        engine.SetParameter(EngineParameter.FrameLimiter, 0);

        Assert.Equal(RenderBackend.Vulkan, engine.GetParameter(EngineParameter.RenderBackend));
        Assert.Equal(MaterialQuality.High, engine.GetParameter(EngineParameter.TextureQuality));
        Assert.False((bool)engine.GetParameter(EngineParameter.FrameLimiter)!);
    }

    [Fact]
    public void Uninitialized_engine_guards_update_render_and_profiler_calls()
    {
        using var engine = new Engine();

        engine.Context.GetSubsystem<Profiler>()!.DumpData();
        engine.Render();

        Assert.Throws<InvalidOperationException>(() => engine.Update());
    }

    [Fact]
    public void Engine_initialization_is_idempotent_after_first_success()
    {
        using var engine = new Engine();
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.EngineDeterministicBranchTests.Idempotent",
            ConfigName = string.Empty,
            Headless = true,
            LogName = "none://"
        };

        Assert.True(engine.Initialize(parameters));
        Assert.True(engine.Initialize(parameters));
    }

    [Fact]
    public void Application_state_returns_null_for_negative_and_unallocated_viewport_indices()
    {
        using var state = new ApplicationState();

        Assert.Null(state.GetViewport(-1));
        Assert.Null(state.GetViewport(0));
    }

    [Fact]
    public void State_manager_handles_partial_fade_steps_before_completing_transitions()
    {
        using var engine = new Engine();
        var manager = engine.GetSubsystem<StateManager>()!;
        manager.FadeInDuration = 1.0f;
        manager.FadeOutDuration = 1.0f;

        using var first = new TestState();
        using var second = new TestState();
        manager.EnqueueState(first);

        manager.Update(0.5f);
        Assert.Same(first, manager.State);
        manager.Update(0.5f);

        manager.EnqueueState(second);
        manager.Update(0.5f);
        Assert.Same(first, manager.State);
        manager.Update(0.5f);
        Assert.Same(second, manager.State);

        manager.Reset();
    }

    [Fact]
    public void Application_state_active_mouse_viewport_and_update_paths_are_deterministic()
    {
        using var engine = new Engine();
        Assert.True(engine.Initialize(new EngineParameters
        {
            ApplicationName = "RedBolt.EngineDeterministicBranchTests.ApplicationState",
            ConfigName = string.Empty,
            Headless = true,
            LogName = "none://"
        }));

        using var state = new TrackingState();
        state.SetNumViewports(1);
        state.SetViewport(0, null);
        state.SetMouseVisible(false);
        state.SetMouseGrabbed(true);
        state.SetMouseMode(MouseMode.Absolute);
        state.SetDefaultFogColor(new Color(0.1f, 0.2f, 0.3f));

        using var bundle = new StringVariantMap();
        state.Activate(bundle);
        state.Activate(bundle);
        state.SetMouseVisible(true);
        state.SetMouseGrabbed(false);
        state.SetMouseMode(MouseMode.Free);
        state.SetDefaultFogColor(new Color(0.4f, 0.5f, 0.6f));

        var update = new CoreEvents.UpdateEvent { TimeStep = 0.25f };
        Context.Instance.SendEvent(ref update);

        Assert.True(state.IsActive);
        Assert.Equal(1, state.UpdateCount);
        Assert.Equal(1, state.GetViewportForScene(null, 0) is null ? 1 : 0);

        state.Deactivate();
        state.Deactivate();
        Context.Instance.SendEvent(ref update);
        Assert.Equal(1, state.UpdateCount);
    }

    private sealed class TestState : ApplicationState
    {
    }

    private sealed class TrackingState : ApplicationState
    {
        public int UpdateCount { get; private set; }

        public override void Update(float timeStep) => ++UpdateCount;
    }
}
