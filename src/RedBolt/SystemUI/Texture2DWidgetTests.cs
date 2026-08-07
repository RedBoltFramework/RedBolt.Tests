namespace RedBolt.Tests;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class Texture2DWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public Texture2DWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Texture_is_aspect_fitted_and_centered_in_square_content_area()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        using var texture = new Texture2D();
        Assert.True(texture.SetSize(8, 16, TextureFormat.TexFormatRgba8Unorm));
        var pixels = new byte[8 * 16 * 4];
        Array.Fill(pixels, (byte)255);
        Assert.True(texture.SetData(0, 0, 0, 8, 16, pixels));
        var widget = new Texture2DWidget(texture);
        try
        {
            Assert.Same(texture, widget.GetTexture2D());
            using (var emptyWidget = new Texture2DWidget(null))
            {
                Assert.Null(emptyWidget.GetTexture2D());
                emptyWidget.RenderContent();
            }

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(160.0f, 160.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(128.0f, 128.0f));
                var isOpen = true;
                ImGui.Begin("Texture2D Widget"u8, ref isOpen);
                windowStarted = true;

                var cursorScreenPosition = ImGui.GetCursorScreenPos();
                var region = ImGui.GetContentRegionAvail();
                var contentSize = new Vector2f(region.X, region.X);
                var previewSize = Widgets.FitContent(contentSize, new Vector2f(8.0f, 16.0f));
                widget.RenderContent();

                var itemMinimum = ImGui.GetItemRectMin();
                AssertClose(cursorScreenPosition.X + (contentSize.X - previewSize.X) * 0.5f, itemMinimum.X);
                AssertClose(cursorScreenPosition.Y, itemMinimum.Y);
                AssertClose(previewSize.X, ImGui.GetItemRectSize().X);
                AssertClose(previewSize.Y, ImGui.GetItemRectSize().Y);
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }

            widget.Dispose();
            Assert.Null(widget.GetTexture2D());
            Assert.Equal(8, texture.Width);
            Assert.Equal(16, texture.Height);
        }
        finally
        {
            widget.Dispose();
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
        }
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(MathF.Abs(expected - actual), 0.0f, 0.001f);
}
