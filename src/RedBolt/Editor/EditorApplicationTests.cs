// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.IO;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor;
using RedBolt.Engine;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RenderAPI;

[Collection(nameof(ApplicationLifecycleCollection))]
public sealed class EditorApplicationTests
{
    [Fact]
    public void Setup_and_command_line_preserve_editor_defaults_and_project_argument()
    {
        using var application = new TestEditorApplication();
        var parameters = application.Configure();
        var parser = new CommandLineParser(parameters.ApplicationName);
        application.RegisterCommandLine(parser, parameters);

        var result = parser.Parse(
        [
            "--read-only",
            "--single-process",
            "--command",
            "Idle",
            "--exit",
            "Projects\\Sample",
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal("TestEditorApplication", parameters.WindowTitle);
        Assert.Equal("Editor | OpenGL", parameters.ApplicationName);
        Assert.False(parameters.Headless);
        Assert.False(parameters.FullScreen);
        Assert.False(parameters.Borderless);
        Assert.True(parameters.WindowResizable);
        Assert.True(parameters.WindowMaximize);
        Assert.Equal(0, parameters.WindowWidth);
        Assert.Equal(0, parameters.WindowHeight);
        Assert.Equal("{exeDir}/../core", parameters.PluginPaths);
        Assert.Equal("CoreData;EditorData", parameters.ResourcePaths);
        Assert.Equal(string.Empty, parameters.ResourceRootFile);
        Assert.False(parameters.EngineAutoLoadScripts);
        Assert.True(parameters.ReloadPlugins);
        Assert.True(parameters.Profile);
        Assert.NotEqual(0u, parameters.SystemUIFlags & (1u << 14));
        if (!string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.Ordinal))
        {
            Assert.NotEqual(0u,
                parameters.SystemUIFlags & (uint)ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable);
        }

        Assert.True(application.IsReadOnly);
        Assert.True(application.IsSingleProcess);
        Assert.Equal("Idle", application.Command);
        Assert.True(application.ExitAfterCommand);
        Assert.Equal("Projects\\Sample", application.PendingOpenProject);
        Assert.Contains("[project]", parser.GetUsage());

        application.OpenProject("Projects\\Sample");
        Assert.Equal("Projects/Sample/", application.PendingOpenProject);
    }

    [Fact]
    public void Serialization_round_trips_recent_projects_and_prunes_missing_directories()
    {
        using var application = new TestEditorApplication();
        var existingProject = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorApplicationTests", Guid.NewGuid().ToString("N"));
        var missingProject = existingProject + "-missing";
        Directory.CreateDirectory(existingProject);
        try
        {
            application.RecentProjects.Add(existingProject);
            application.RecentProjects.Add(missingProject);

            using var json = new JSONFile();
            Assert.True(json.SaveObject(application));

            application.RecentProjects.Clear();
            Assert.True(json.LoadObject(application));

            Assert.Equal([existingProject], application.RecentProjects);
        }
        finally
        {
            Directory.Delete(existingProject, true);
        }
    }

    [Fact]
    public void Dropped_project_file_project_directory_and_empty_directory_are_opened()
    {
        using var application = new TestEditorApplication();
        var rootPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorDropTests", Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(rootPath, "Project");
        var projectFileName = Path.Combine(projectPath, "Project.json");
        var emptyPath = Path.Combine(rootPath, "Empty");
        var nonProjectPath = Path.Combine(rootPath, "NonProject");
        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(emptyPath);
        Directory.CreateDirectory(nonProjectPath);
        File.WriteAllText(projectFileName, "{}");
        File.WriteAllText(Path.Combine(nonProjectPath, "readme.txt"), string.Empty);
        try
        {
            application.DropFile(projectFileName);
            Assert.Equal(projectPath.Replace('\\', '/') + "/", application.PendingOpenProject);

            application.DropFile(projectPath);
            Assert.Equal(projectPath.Replace('\\', '/') + "/", application.PendingOpenProject);

            application.DropFile(emptyPath);
            var emptyProjectPath = emptyPath.Replace('\\', '/') + "/";
            Assert.Equal(emptyProjectPath, application.PendingOpenProject);

            application.DropFile(nonProjectPath);
            Assert.Equal(emptyProjectPath, application.PendingOpenProject);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void Setup_detects_nearest_resource_prefix_and_command_line_can_override_it()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorPrefixTests", Guid.NewGuid().ToString("N"));
        var workingPath = Path.Combine(rootPath, "Nested", "Working");
        var originalWorkingDirectory = Environment.CurrentDirectory;
        Directory.CreateDirectory(Path.Combine(rootPath, "CoreData"));
        Directory.CreateDirectory(workingPath);
        try
        {
            using var application = new TestEditorApplication();
            Environment.CurrentDirectory = workingPath;
            var parameters = application.Configure();

            Assert.Equal(rootPath.Replace('\\', '/') + "/", parameters.ResourcePrefixPaths);

            var parser = new CommandLineParser(parameters.ApplicationName);
            Engine.DefineParameters(parser, parameters);
            application.RegisterCommandLine(parser, parameters);
            Assert.True(parser.Parse(["--prefix-paths", "ExplicitPrefix"]).IsSuccess);
            Assert.Equal("ExplicitPrefix", parameters.ResourcePrefixPaths);
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDirectory;
            Directory.Delete(rootPath, true);
        }
    }

    [GraphicsFact]
    public void Runtime_initializes_editor_ui_and_exits_cleanly_with_software_graphics()
    {
        var repositoryRoot = RbfxTestData.RepositoryRoot;
        var preferencesPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorApplicationRuntimeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(preferencesPath);
        try
        {
            using var application = new RuntimeEditorApplication(repositoryRoot, preferencesPath);

            var exitCode = application.Run([]);
            Assert.True(exitCode == 0, application.ErrorMessage);
            Assert.True(application.StartCalled, "StartCalled");
            Assert.True(application.StopCalled, "StopCalled");
            Assert.True(application.HasConsole, "HasConsole");
            Assert.True(application.HasDebugHud, "HasDebugHud");
            Assert.False(application.HasRmlUI, "HasRmlUI");
            Assert.True(application.HasSystemUI, "HasSystemUI");
            Assert.True(application.HasDefaultFont, "HasDefaultFont");
            Assert.True(application.HasProjectSettingsHandler, "HasProjectSettingsHandler");
            Assert.True(application.HasDocking, "HasDocking");
            Assert.True(application.ScalesFontsForDpi, "ScalesFontsForDpi");
            Assert.True(application.HasUsableRenderSize, "HasUsableRenderSize");
            Assert.Equal(3.0f, application.WindowRounding);
            Assert.Equal(new Vector2f(4.0f, 4.0f), application.ItemSpacing);
            Assert.False(application.HasWorkQueue, "HasWorkQueue");
            Assert.False(application.ReadUninitializedTextureWarning, "ReadUninitializedTextureWarning");
        }
        finally
        {
            Directory.Delete(preferencesPath, true);
        }
    }

    [GraphicsFact]
    public void Runtime_drop_closes_open_project_and_creates_project_in_empty_directory()
    {
        var repositoryRoot = RbfxTestData.RepositoryRoot;
        var rootPath = Path.Combine(
            Path.GetTempPath(), "RedBolt.EditorDropRuntimeTests", Guid.NewGuid().ToString("N"));
        var preferencesPath = Path.Combine(rootPath, "Preferences");
        var firstProjectPath = Path.Combine(rootPath, "First");
        var droppedProjectPath = Path.Combine(rootPath, "Dropped");
        Directory.CreateDirectory(preferencesPath);
        Directory.CreateDirectory(firstProjectPath);
        Directory.CreateDirectory(droppedProjectPath);
        File.WriteAllText(Path.Combine(firstProjectPath, "Project.json"), "{}");
        try
        {
            using var application = new RuntimeEditorApplication(repositoryRoot, preferencesPath, droppedProjectPath);

            var exitCode = application.Run([firstProjectPath]);
            Assert.True(exitCode == 0, application.ErrorMessage);
            Assert.True(application.DroppedProjectOpened);
            Assert.True(File.Exists(Path.Combine(droppedProjectPath, "Project.json")));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private sealed class TestEditorApplication : EditorApplication
    {
        public bool IsReadOnly => _readOnly;

        public bool IsSingleProcess => _singleProcess;

        public string Command => _command;

        public bool ExitAfterCommand => _exitAfterCommand;

        public string PendingOpenProject => _pendingOpenProject;

        public List<string> RecentProjects => _recentProjects;

        public EngineParameters Configure()
        {
            var parameters = new EngineParameters();
            Setup(parameters);
            return parameters;
        }

        public void RegisterCommandLine(CommandLineParser parser, EngineParameters parameters)
        {
            SetupCommandLine(parser, parameters);
        }

        public void DropFile(string fileName)
        {
            var eventData = new RedBolt.Input.InputEvents.DropFileEvent { FileName = fileName };
            OnDropFile(ref eventData);
        }
    }

    private sealed class RuntimeEditorApplication : EditorApplication
    {
        private readonly string _repositoryRoot;
        private readonly string _preferencesPath;
        private readonly string? _droppedProjectPath;
        private TestEventReceiver? _dropObserver;
        private TestEventReceiver? _logObserver;
        private bool _dropScheduled;

        public RuntimeEditorApplication(string repositoryRoot, string preferencesPath, string? droppedProjectPath = null)
        {
            _repositoryRoot = repositoryRoot;
            _preferencesPath = preferencesPath;
            _droppedProjectPath = droppedProjectPath;
        }

        public bool StartCalled { get; private set; }

        public bool StopCalled { get; private set; }

        public bool HasConsole { get; private set; }

        public bool HasDebugHud { get; private set; }

        public bool HasRmlUI { get; private set; }

        public bool HasSystemUI { get; private set; }

        public bool HasDefaultFont { get; private set; }

        public bool HasProjectSettingsHandler { get; private set; }

        public bool HasDocking { get; private set; }

        public bool ScalesFontsForDpi { get; private set; }

        public bool HasUsableRenderSize { get; private set; }

        public bool DroppedProjectOpened { get; private set; }

        public bool ReadUninitializedTextureWarning { get; private set; }

        public float WindowRounding { get; private set; }

        public Vector2f ItemSpacing { get; private set; }

        public bool HasWorkQueue => Context.GetSubsystem<WorkQueue>() is not null;

        protected override void Setup(EngineParameters parameters)
        {
            base.Setup(parameters);
            parameters.ApplicationPreferencesDir = _preferencesPath + Path.DirectorySeparatorChar;
            parameters.LogName = string.Empty;
            parameters.LogQuiet = true;
            parameters.RenderBackend = RenderBackend.OpenGL;
            parameters.ResourcePrefixPaths = Path.GetFullPath(Path.Combine(
                _repositoryRoot, "build", "bin",
                RbfxTestData.BuildConfiguration, RbfxTestData.TargetPlatform));
            parameters.TimeOut = _droppedProjectPath is null ? 0 : 10;
            parameters.WindowHidden = true;
            parameters.WindowMaximize = false;
        }

        protected override void Start()
        {
            base.Start();
            _logObserver = new TestEventReceiver();
            _logObserver.SubscribeToEvent<RedBolt.IO.IOEvents.LogMessage>(ObserveLogMessage);
            StartCalled = true;
            HasConsole = Context.GetSubsystem<RedBolt.SystemUI.Console>() is not null;
            HasDebugHud = Context.GetSubsystem<RedBolt.SystemUI.DebugHud>() is not null;
            HasRmlUI = Context.GetSubsystem<RedBolt.RmlUI.RmlUI>() is not null;
            HasSystemUI = Context.GetSubsystem<RedBolt.DearImGui.DearImGui>() is not null;
            var graphics = GetSubsystem<global::RedBolt.Graphics.Graphics>();
            HasUsableRenderSize = graphics.Width > 1 && graphics.Height > 1;

            using (var io = ImGui.GetIO())
            {
                HasDefaultFont = !io.FontDefault.IsNull;
                HasDocking = (io.ConfigFlags & ImGuiConfigFlags.ImGuiConfigFlagsDockingEnable) != 0;
                ScalesFontsForDpi = io.ConfigDpiScaleFonts;
            }
            using (var style = ImGui.GetStyle())
            {
                WindowRounding = style.WindowRounding;
                ItemSpacing = style.ItemSpacing;
            }
            using (var handler = ImGui.FindSettingsHandler("Project"u8))
                HasProjectSettingsHandler = !handler.IsNull;

            if (_droppedProjectPath is null)
                SendEvent<RedBolt.Input.InputEvents.ExitRequestedEvent>();
            else
            {
                _dropObserver = new TestEventReceiver();
                _dropObserver.SubscribeToEvent<CoreEvents.EndFrameEvent>(HandleDropProject);
            }
        }

        private void HandleDropProject(ref CoreEvents.EndFrameEvent _)
        {
            if (_project is null)
                return;

            if (!_dropScheduled)
            {
                _dropScheduled = true;
                _project.OnInitialized += DropProject;
            }
            else if (string.Equals(
                _project.ProjectPath, _droppedProjectPath!.Replace('\\', '/') + "/", StringComparison.Ordinal))
            {
                DroppedProjectOpened = true;
                SendEvent<RedBolt.Input.InputEvents.ExitRequestedEvent>();
            }
        }

        private void DropProject(global::RedBolt.Editor.Project.Project _)
        {
            var eventData = new RedBolt.Input.InputEvents.DropFileEvent { FileName = _droppedProjectPath! };
            SendEvent(ref eventData);
        }

        private void ObserveLogMessage(ref RedBolt.IO.IOEvents.LogMessage eventData)
        {
            ReadUninitializedTextureWarning |= eventData.Message.Contains(
                "RawTexture::Read is ignored for uninitialized texture", StringComparison.Ordinal);
        }

        protected override void Stop()
        {
            _dropObserver?.Dispose();
            base.Stop();
            _logObserver?.Dispose();
            StopCalled = true;
        }
    }
}
