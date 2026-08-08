namespace RedBolt.Tests;

using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class DearImGuiFontTests
{
    private readonly GraphicsEngineFixture _fixture;

    public DearImGuiFontTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [GraphicsFact]
    public void Resource_fonts_preserve_size_name_ranges_and_merge_behavior()
    {
        _fixture.ResetGraphics();
        DearImGuiSubsystem? systemUI = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);

            Assert.Equal("HarfBuzz GPU", GetFontLoaderName());
            var initialSources = GetFontSourceCount();

            ushort[] ranges = [0x20, 0x7e, 0];
            var font = systemUI.AddFont("Fonts/BlueHighway.ttf", ranges, 16.0f);
            Assert.NotNull(font);
            Assert.Equal(16.0f, font.LegacySize);
            Assert.Contains("BlueHighway (16.00)", Encoding.UTF8.GetString(font.GetDebugName()));
            Assert.Equal(initialSources + 1, GetFontSourceCount());

            var mergedFont = systemUI.AddFont("Fonts/BlueHighway.ttf", ranges, merge: true);
            Assert.NotNull(mergedFont);
            Assert.Equal(16.0f, mergedFont.LegacySize);
            Assert.Equal(initialSources + 2, GetFontSourceCount());
            Assert.True(IsFontAtlasBuilt());

            using var baked = font.GetFontBaked(16.0f);
            using var glyph = baked.FindGlyph('W');
            using var io = ImGui.GetIO();
            using var atlas = io.Fonts;
            Assert.Equal(-1, glyph.PackId);
            Assert.True(MathF.Floor(glyph.U0) >= 2.0f);
            Assert.True(MathF.Floor(glyph.V0) >= 2.0f);
            Assert.Equal(MathF.Floor(glyph.U0), MathF.Floor(glyph.U1));
            Assert.Equal(MathF.Floor(glyph.V0), MathF.Floor(glyph.V1));
            Assert.InRange(glyph.U0 - MathF.Floor(glyph.U0), 0.125f, 0.875f);
            Assert.InRange(glyph.V0 - MathF.Floor(glyph.V0), 0.125f, 0.875f);
            Assert.InRange(atlas.TexUvWhitePixel.X, 0.0f, 1.0f);
            Assert.InRange(atlas.TexUvWhitePixel.Y, 0.0f, 1.0f);

            Assert.Null(systemUI.AddFont("Fonts/DoesNotExist.ttf", ranges));
            Assert.Equal(initialSources + 2, GetFontSourceCount());
        }
        finally
        {
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
        }
    }

    private static int GetFontSourceCount()
    {
        using var io = ImGui.GetIO();
        using var atlas = io.Fonts;
        return atlas.SourcesSize;
    }

    private static string? GetFontLoaderName()
    {
        using var io = ImGui.GetIO();
        using var atlas = io.Fonts;
        return Marshal.PtrToStringUTF8(atlas.FontLoaderName);
    }

    private static bool IsFontAtlasBuilt()
    {
        using var io = ImGui.GetIO();
        using var atlas = io.Fonts;
        return atlas.TexIsBuilt;
    }
}
