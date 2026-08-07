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
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Texture2DInspectorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Texture2DInspectorTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_selection_creates_inspector_and_rendered_preview()
    {
        _fixture.ResetGraphics();
        var projectPath = Path.Combine(Path.GetTempPath(), "RedBoltTexture2DInspector-" + Guid.NewGuid().ToString("N"));
        var dataPath = Path.Combine(projectPath, "Data");
        var settingsPath = Path.Combine(projectPath, "Settings.json");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "Cache.json"), "{}");
        File.WriteAllText(Path.Combine(projectPath, "ui.ini"), string.Empty);
        File.WriteAllText(settingsPath, "{}");
        using (var image = new Image())
        {
            Assert.True(image.SetSize(8, 16, 4));
            image.Clear(new Color(0.25f, 0.5f, 0.75f, 1.0f));
            Assert.True(image.SavePNG(Path.Combine(dataPath, "Test.png")));
        }
        var preservedResources = _fixture.Resources.AllResources.Values.ToArray();

        Project? project = null;
        DearImGuiSubsystem? systemUI = null;
        Texture2DInspector? inspector = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);
            project = new Project(projectPath, settingsPath, ProjectFlag.ReadOnly | ProjectFlag.SingleProcess);
            project.AddAnalyzeFileCallback(static (descriptor, context) =>
            {
                if (descriptor.HasExtension(".png"))
                    descriptor.AddObjectType<Texture2D>();
            });

            inspector = new Texture2DInspector(project);
            var activationCount = 0;
            inspector.OnActivated += () => ++activationCount;
            using (var request = new InspectResourceRequest(["Test.png"]))
            {
                project.ProcessRequest(request);
                typeof(Project).GetMethod("ProcessPendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(project, null);
            }

            Assert.Equal(1, activationCount);
            var widget = Assert.IsType<Texture2DInspectorWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_inspector", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            var texture = Assert.IsType<Texture2D>(Assert.Single(widget.GetResources()));
            Assert.Equal("Test.png", texture.GetName());
            Assert.Equal(8, texture.Width);
            Assert.Equal(16, texture.Height);

            var preview = Assert.IsType<Texture2DWidget>(
                typeof(InspectorWithPreview)
                    .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(inspector));
            Assert.Same(texture, preview.GetTexture2D());

            var frameStarted = false;
            var windowStarted = false;
            var renderedSize = Vector2f.Zero;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(160.0f, 160.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(128.0f, 128.0f));
                var isOpen = true;
                ImGui.Begin("Texture2D Inspector"u8, ref isOpen);
                windowStarted = true;

                preview.RenderContent();
                renderedSize = ImGui.GetItemRectSize();
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }
            Assert.True(renderedSize.X > 0.0f);
            Assert.True(renderedSize.Y > 0.0f);

            inspector.Dispose();
            inspector = null;
            Assert.Empty(widget.GetResources());
            Assert.Null(preview.GetTexture2D());
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
