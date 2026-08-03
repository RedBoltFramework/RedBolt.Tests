namespace RedBolt.Tests;

using RedBolt.Engine;
using RedBolt.Platform;

[Collection(nameof(ApplicationLifecycleCollection))]
public sealed class PlatformLifecycleTests
{
    [Fact]
    public void Headless_application_runs_start_update_stop_in_order_and_applies_configuration()
    {
        using var application = new LifecycleApplication();

        Assert.Equal(0, application.Run([]));
        Assert.Equal(["setup", "start", "update", "stop"], application.Events);
        Assert.Equal(320, application.ObservedWidth);
        Assert.Equal(200, application.ObservedHeight);
        Assert.True(application.ObservedHeadless);
    }

    [Fact]
    public void Update_exception_sets_failure_exit_and_still_runs_stop()
    {
        using var application = new FailingUpdateApplication();

        Assert.Equal(Application.FailureExitCode, application.Run([]));
        Assert.Contains("update failed", application.ErrorMessage);
        Assert.True(application.StopCalled);
    }

    [Fact]
    public void Startup_exception_sets_failure_exit_without_running_the_main_loop()
    {
        using var application = new FailingStartApplication();

        Assert.Equal(Application.FailureExitCode, application.Run([]));
        Assert.Contains("start failed", application.ErrorMessage);
        Assert.False(application.UpdateCalled);
        Assert.False(application.StopCalled);
    }

    private sealed class LifecycleApplication : Application
    {
        public List<string> Events { get; } = [];

        public int ObservedWidth { get; private set; }

        public int ObservedHeight { get; private set; }

        public bool ObservedHeadless { get; private set; }

        protected override void Setup(EngineParameters parameters)
        {
            Events.Add("setup");
            parameters.ApplicationName = "RedBolt.Tests.PlatformLifecycle";
            parameters.EngineCliParameters = false;
            parameters.Headless = true;
            parameters.WindowWidth = 320;
            parameters.WindowHeight = 200;
        }

        protected override void Start()
        {
            Events.Add("start");
            ObservedWidth = Engine.Parameters.WindowWidth;
            ObservedHeight = Engine.Parameters.WindowHeight;
            ObservedHeadless = Engine.Parameters.Headless;
        }

        protected override void Update(float timeStep)
        {
            Events.Add("update");
            Exit();
        }

        protected override void Stop()
        {
            Events.Add("stop");
        }
    }

    private sealed class FailingUpdateApplication : Application
    {
        public bool StopCalled { get; private set; }

        protected override void Setup(EngineParameters parameters)
        {
            parameters.ApplicationName = "RedBolt.Tests.PlatformFailure";
            parameters.EngineCliParameters = false;
            parameters.Headless = true;
        }

        protected override void Update(float timeStep)
        {
            throw new InvalidOperationException("update failed");
        }

        protected override void Stop()
        {
            StopCalled = true;
        }
    }

    private sealed class FailingStartApplication : Application
    {
        public bool UpdateCalled { get; private set; }

        public bool StopCalled { get; private set; }

        protected override void Setup(EngineParameters parameters)
        {
            parameters.ApplicationName = "RedBolt.Tests.PlatformStartFailure";
            parameters.EngineCliParameters = false;
            parameters.Headless = true;
        }

        protected override void Start()
        {
            throw new InvalidOperationException("start failed");
        }

        protected override void Update(float timeStep)
        {
            UpdateCalled = true;
        }

        protected override void Stop()
        {
            StopCalled = true;
        }
    }
}

[CollectionDefinition(nameof(ApplicationLifecycleCollection), DisableParallelization = true)]
public sealed class ApplicationLifecycleCollection
{
}
