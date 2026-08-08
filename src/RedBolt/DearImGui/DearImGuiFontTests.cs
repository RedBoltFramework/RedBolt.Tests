namespace RedBolt.Tests;

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

    private static bool IsFontAtlasBuilt()
    {
        using var io = ImGui.GetIO();
        using var atlas = io.Fonts;
        return atlas.TexIsBuilt;
    }
}
