namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionRenderApiTests
{
    [VisualFact]
    public void Render_context_clear_writes_the_bound_native_texture()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        using var target = CreateRenderTarget(128, 96, "RenderApi.ContextClear");
        var context = fixture.EngineFixture.RenderContext;

        context.SetRenderTargets(default, [RenderTargetView.Texture(target)]);
        context.SetFullViewport();
        context.ClearRenderTarget(0, new Color(0.12f, 0.34f, 0.68f, 1.0f));
        context.ResetRenderTargets();

        fixture.AssertMatches(
            fixture.ReadTextureImage(target),
            "render-api/context-clear.png");
    }

    [VisualFact]
    public void Draw_command_queue_renders_parameterized_scissor_regions()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        using var target = CreateRenderTarget(128, 96, "RenderApi.DrawQueue");
        using var manager = TestRenderPipelineInterface.CreateRenderBufferManager();
        var context = fixture.EngineFixture.RenderContext;

        context.SetRenderTargets(default, [RenderTargetView.Texture(target)]);
        context.SetFullViewport();
        context.ClearRenderTarget(0, new Color(0.025f, 0.025f, 0.035f, 1.0f));

        const string shaderName = "v2/X_ClearFramebuffer";
        var pipelineDescription = new GraphicsPipelineStateDesc
        {
            DebugName = "Visual regression scissor quad",
            ColorWriteEnabled = true,
            BlendMode = BlendMode.Replace,
            ScissorTestEnabled = true,
            VertexShader = fixture.Graphics.GetShader(ShaderType.VS, shaderName, string.Empty),
            PixelShader = fixture.Graphics.GetShader(ShaderType.PS, shaderName, string.Empty)
        };
        var pipelineId = manager.CreateQuadPipelineState(pipelineDescription);
        var pipelineState = manager.GetQuadPipelineState(pipelineId);
        Assert.NotNull(pipelineState);
        var quad = fixture.Renderer.GetQuadGeometry();
        Assert.NotNull(quad);
        var queue = fixture.EngineFixture.RenderDevice.GetDefaultQueue();

        queue.Reset();
        queue.SetPipelineState(pipelineState);
        DrawQuad(queue, quad, new IntRect(0, 0, 64, 96), new Vector4f(0.9f, 0.18f, 0.08f, 1.0f));
        DrawQuad(queue, quad, new IntRect(64, 0, 128, 96), new Vector4f(0.08f, 0.38f, 0.92f, 1.0f));
        context.Execute(queue);
        context.ResetRenderTargets();

        fixture.AssertMatches(
            fixture.ReadTextureImage(target),
            "render-api/draw-command-queue.png");
    }

    [VisualFact]
    public void Raw_texture_upload_and_render_pipeline_copy_preserve_pixels()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        using var source = CreatePatternTexture();
        using var target = CreateRenderTarget(128, 96, "RenderApi.CopyTarget");
        using var manager = TestRenderPipelineInterface.CreateRenderBufferManager();
        var context = fixture.EngineFixture.RenderContext;

        context.SetRenderTargets(default, [RenderTargetView.Texture(target)]);
        context.SetFullViewport();
        manager.DrawTexture(
            "Visual regression raw texture copy",
            source.GetRawTexture(),
            ColorSpaceTransition.None);
        context.ResetRenderTargets();

        fixture.AssertMatches(
            fixture.ReadTextureImage(target),
            "render-api/raw-texture-copy.png");
    }

    private static Texture2D CreateRenderTarget(int width, int height, string name)
    {
        var texture = new Texture2D { Name = name };
        Assert.True(texture.SetSize(width, height, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        return texture;
    }

    private static Texture2D CreatePatternTexture()
    {
        using var image = new Image();
        Assert.True(image.SetSize(8, 8, 4));
        var pixels = new byte[8 * 8 * 4];
        for (var y = 0; y < 8; ++y)
        {
            for (var x = 0; x < 8; ++x)
            {
                var index = (y * 8 + x) * 4;
                var top = y < 4;
                var left = x < 4;
                pixels[index] = top ? (byte)(left ? 240 : 30) : (byte)(left ? 30 : 220);
                pixels[index + 1] = top ? (byte)(left ? 40 : 210) : (byte)(left ? 180 : 60);
                pixels[index + 2] = top ? (byte)(left ? 70 : 40) : (byte)(left ? 220 : 230);
                pixels[index + 3] = byte.MaxValue;
            }
        }

        image.SetData(pixels);
        var texture = new Texture2D { Name = "RenderApi.PatternSource" };
        texture.SetNumLevels(1);
        Assert.True(texture.SetData(image));
        texture.SetFilterMode(TextureFilterMode.Nearest);
        return texture;
    }

    private static void DrawQuad(
        DrawCommandQueue queue, Geometry quad, IntRect scissor, Vector4f color)
    {
        queue.SetScissorRect(scissor);
        Assert.True(queue.BeginShaderParameterGroup(
            ShaderParameterGroup.Custom,
            differentFromPrevious: true));
        queue.AddShaderParameter(new StringHash("Color"), color);
        queue.AddShaderParameter(new StringHash("Depth"), 0.5f);
        queue.CommitShaderParameterGroup(ShaderParameterGroup.Custom);
        queue.CommitShaderResources();
        GraphicsUtils.SetBuffersFromGeometry(queue, quad);
        queue.DrawIndexed(checked((int)quad.GetIndexStart()), checked((int)quad.GetIndexCount()));
    }
}
