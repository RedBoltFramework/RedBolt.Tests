// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Utility;

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

            var iconData = File.ReadAllBytes(Path.Combine(
                RbfxTestData.EditorData, "Fonts", "fa-solid-900.ttf"));
            ushort[] iconRanges = [0xf015, 0xf015, 0];
            var mergedFont = systemUI.AddFont(iconData, "Font Awesome", iconRanges, merge: true);
            Assert.NotNull(mergedFont);
            Assert.Equal(16.0f, mergedFont.LegacySize);
            Assert.Equal(initialSources + 2, GetFontSourceCount());
            Assert.True(IsFontAtlasBuilt());

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var baked = font.GetFontBaked(16.0f);
            using var icon = baked.FindGlyph(0xf015);
            Assert.Equal(1u, icon.SourceIdx);
            Assert.True(icon.AdvanceX > 0.0f);

            using (var io = ImGui.GetIO())
                io.FontDefault = font;
            _fixture.Context.SendEvent<InputEvents.InputEndEvent>();
            using (var drawList = ImGui.GetForegroundDrawList())
                drawList.AddText(new Vector2f(4.0f, 4.0f), 0xffffffff, Icons.HouseU8);
            _fixture.Context.SendEvent<GraphicsEvents.EndRenderingEvent>();
            using var drawData = ImGui.GetDrawData();
            Assert.True(drawData.TotalVtxCount >= 4);

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
