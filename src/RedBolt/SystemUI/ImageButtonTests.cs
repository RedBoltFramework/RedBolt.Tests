namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ImageButtonTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ImageButtonTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Image_button_references_texture_and_preserves_requested_size()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        using var texture = new Texture2D();
        using var textureReference = new ImTextureRef();
        try
        {
            Assert.True(texture.SetSize(8, 4, TextureFormat.TexFormatRgba8Unorm));

            var frameStarted = false;
            var windowStarted = false;
            try
            {
                using var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(160.0f, 120.0f);
                io.DeltaTime = 1.0f / 60.0f;
                ImGui.NewFrame();
                frameStarted = true;
                var isOpen = true;
                ImGui.Begin("Image Button"u8, ref isOpen);
                windowStarted = true;

                using var style = ImGui.GetStyle();
                Assert.False(Widgets.ImageButton(texture, textureReference, new Vector2f(48.0f, 24.0f)));
                Assert.NotEqual(0UL, (ulong)textureReference.TexID);
                Assert.Equal(new Vector2f(
                    48.0f + style.FramePadding.X * 2.0f,
                    24.0f + style.FramePadding.Y * 2.0f), ImGui.GetItemRectSize());
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }
        }
        finally
        {
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
        }
    }
}
