// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsHeadlessLifecycleTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GraphicsHeadlessLifecycleTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Headless_graphics_keeps_window_operations_side_effect_free_and_close_is_idempotent()
    {
        var graphics = _fixture.Graphics;
        var originalTitle = graphics.GetWindowTitle();
        var originalPosition = graphics.GetWindowPosition();

        try
        {
            Assert.False(graphics.IsInitialized);
            graphics.SetWindowTitle("Headless title");
            graphics.SetWindowPosition(new Vector2i(17, 23));

            Assert.Equal("Headless title", graphics.GetWindowTitle());
            Assert.Equal(new Vector2i(17, 23), graphics.GetWindowPosition());
            Assert.False(graphics.SetScreenMode(new WindowSettings { Size = new Vector2i(64, 64) }));
            Assert.False(graphics.BeginFrame());
            Assert.Equal(Vector2i.Zero, graphics.GetRenderTargetDimensions());

            graphics.EndFrame();
            graphics.Clear(ClearTarget.Color | ClearTarget.Depth);
            graphics.ResetRenderTargets();
            graphics.Restore();
            graphics.Close();
            graphics.Close();

            Assert.False(graphics.IsInitialized);
            Assert.Null(graphics.GetNativeRenderDevice());
            Assert.Null(graphics.GetNativeSwapChain());
        }
        finally
        {
            graphics.SetWindowTitle(originalTitle);
            graphics.SetWindowPosition(originalPosition);
        }
    }

    [Fact]
    public void Headless_texture_validates_shape_but_does_not_require_a_gpu_for_cpu_state()
    {
        using var texture = new Texture2D { Name = "Textures/HeadlessLifecycle" };

        Assert.False(texture.SetSize(0, 4, TextureFormat.TexFormatR8Unorm));
        Assert.True(texture.SetSize(4, 2, TextureFormat.TexFormatR8Unorm));
        Assert.Equal(new Vector2i(4, 2), new Vector2i(texture.Width, texture.Height));
        Assert.True(texture.SetData(0, 0, 0, 4, 2, new byte[8]));
        Assert.False(texture.GetData(0, new byte[8]));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            texture.SetData(0, 3, 0, 2, 1, new byte[2]));
    }
}
