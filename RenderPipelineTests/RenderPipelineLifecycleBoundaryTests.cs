namespace RedBolt.Tests;

using RedBolt.RenderPipeline;
using RedBolt.Resource;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineLifecycleBoundaryTests
{
    [Fact]
    public void Disposing_render_pipeline_clears_owned_state_and_reload_callbacks()
    {
        var pipeline = new RenderPipeline();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/LifecycleBoundary.renderpath");

        var settingsChanged = 0;
        var renderPathChanged = 0;
        var parametersChanged = 0;
        pipeline.SettingsChanged += (_, _) => ++settingsChanged;
        pipeline.RenderPathChanged += (_, _) => ++renderPathChanged;
        pipeline.ParametersChanged += _ => ++parametersChanged;
        pipeline.SetRenderPath(renderPath);
        pipeline.SetRenderPasses([("First", true)]);

        Assert.Equal(1, renderPathChanged);
        Assert.Equal(1, parametersChanged);

        pipeline.Dispose();
        renderPath.SendEvent<ResourceEvents.ReloadFinished>();

        Assert.Null(pipeline.GetRenderPath());
        Assert.Empty(pipeline.GetRenderPasses());
        Assert.Empty(pipeline.GetRenderPathParameters());
        Assert.Empty(pipeline.GetFrameShaderParameters());

        pipeline.SetSettings(new RenderPipelineSettings());
        pipeline.SetRenderPassEnabled("AfterDispose", true);

        Assert.Equal(0, settingsChanged);
        Assert.Equal(1, renderPathChanged);
        Assert.Equal(1, parametersChanged);
    }
}
