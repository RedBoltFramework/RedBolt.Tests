// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ModelInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ModelInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Model_selection_creates_inspector_and_rendered_scene_preview()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltModelInspector-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        using (var destination = new RedBolt.IO.VectorBuffer())
        {
            Assert.True(_fixture.Resources.Get<Model>("Models/Box.mdl").Save(destination));
            File.WriteAllBytes(Path.Combine(dataPath, "Test.mdl"), destination.GetBuffer());
        }
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        ModelInspector? inspector = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".mdl"))
                    descriptor.AddObjectType<Model>();
            });

            inspector = new ModelInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            using (var request = new InspectResourceRequest(["Test.mdl"]))
            {
                project.ProcessRequest(request);
                typeof(Project).GetMethod("ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(project, null);
            }

            Assert.Equal(1, activationCount);
            var widget = Assert.IsType<ModelInspectorWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var model = Assert.IsType<Model>(Assert.Single(widget.GetResources()));
            Assert.Equal("Test.mdl", model.GetName());
            Assert.False(widget.CanSave());

            var preview = Assert.IsType<SceneWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var scene = Assert.IsType<RedBolt.Scene.Scene>(preview.GetScene());
            var staticModel = Assert.IsType<StaticModel>(scene.GetChild("Model")!.GetComponent<StaticModel>());
            Assert.Same(model, staticModel.Model);
            var bounds = model.GetBoundingBox();
            Assert.Equal(
                bounds.Center + bounds.Size.Length * Vector3f.One,
                preview.GetCamera()!.Node.GetPosition());

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(128.0f, 128.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(96.0f, 96.0f));
                var isOpen = true;
                ImGui.Begin("Model Inspector"u8, ref isOpen);
                windowStarted = true;

                preview.RenderContent();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }
            Assert.True(preview.GetRenderer()!.Texture.Width > 0);
            Assert.True(preview.GetRenderer()!.Texture.Height > 0);

            inspector.Dispose();
            inspector = null;
            Assert.Empty(widget.GetResources());
            Assert.Null(preview.GetScene());
        }
        finally
        {
            inspector?.Dispose();
            if (project is not null)
            {
                project.Destroy();
                _fixture.Context.RemoveSubsystem<Project>();
                project.Dispose();
            }
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
            foreach (var resource in preservedResources)
                _fixture.Resources.AddManualResource(resource);
            Directory.Delete(projectPath, true);
        }
    }
}
