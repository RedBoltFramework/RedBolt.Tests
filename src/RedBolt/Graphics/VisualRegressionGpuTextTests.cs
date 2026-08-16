// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.RmlUi;
using RedBolt.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionGpuTextTests
{
    [VisualFact]
    public void DearImGui_harfbuzz_gpu_text_and_white_pixel_primitives_render()
    {
        using var fixture = new VisualRegressionFixture(360, 144);
        using var systemUI = new DearImGuiSubsystem();
        fixture.EngineFixture.Context.RegisterSubsystem(systemUI);
        ReadOnlySpan<ushort> textRanges = [0x20, 0x52f, 0];
        ReadOnlySpan<ushort> iconRanges = [0xf000, 0xf8ff, 0];
        var defaultFont = systemUI.AddFont("Fonts/NotoSans-CondensedItalic.ttf", textRanges, 16.0f);
        Assert.NotNull(defaultFont);
        var fontRoot = Path.Combine(RbfxTestData.EditorData, "Fonts");
        Assert.NotNull(systemUI.AddFont(
            File.ReadAllBytes(Path.Combine(fontRoot, "fa-solid-900.ttf")),
            "Font Awesome", iconRanges, 16.0f, merge: true));
        using (var io = ImGui.GetIO())
            io.FontDefault = defaultFont;
        using var customTexture = new Texture2D();
        customTexture.SetNumLevels(1);
        Assert.True(customTexture.SetSize(2, 2, TextureFormat.TexFormatRgba8Unorm));
        Assert.True(customTexture.SetData(0, 0, 0, 2, 2,
        [
            0x20, 0xd0, 0x60, 0xff, 0x20, 0xd0, 0x60, 0xff,
            0x20, 0xd0, 0x60, 0xff, 0x20, 0xd0, 0x60, 0xff
        ]));
        using var customTextureReference = new ImTextureRef();
        customTextureReference.TexID = (ulong)(nuint)customTexture.GetRawTexture()!.GetHandles().ShaderResourceView!.Handle;

        try
        {
            var actual = fixture.CaptureBackBuffer(() =>
            {
                fixture.EngineFixture.Context.SendEvent<InputEvents.InputEndEvent>();
                ImGui.SetNextWindowPos(new Vector2f(0.0f, 0.0f));
                ImGui.SetNextWindowSize(new Vector2f(360.0f, 144.0f));
                var isOpen = true;
                ImGui.Begin("HarfBuzz GPU"u8, ref isOpen,
                    ImGuiWindowFlags.ImGuiWindowFlagsNoResize
                    | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                    | ImGuiWindowFlags.ImGuiWindowFlagsNoCollapse);
                ImGui.TextUnformatted("Software: right | Hardware: right"u8);
                ImGui.TextUnformatted("office affine | fi fl | \uf015 \uf013"u8);
                var cursor = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(
                    cursor, cursor + new Vector2f(120.0f, 12.0f), 0xffd06020);
                ImGui.Dummy(new Vector2f(120.0f, 12.0f));
                systemUI.ReferenceTexture(customTexture);
                ImGui.Image(customTextureReference, new Vector2f(120.0f, 12.0f),
                    new Vector2f(2.25f, 2.25f), new Vector2f(3.75f, 3.75f));
                ImGui.End();
                fixture.EngineFixture.Context.SendEvent<GraphicsEvents.EndRenderingEvent>();
            }, new Color(0.025f, 0.025f, 0.035f, 1.0f));

            fixture.AssertMatches(actual, "text/imgui-harfbuzz-gpu.png",
                channelTolerance: 8, maxDifferingPixelRatio: 0.01, maxAverageChannelError: 1.0);
        }
        finally
        {
            fixture.EngineFixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
        }
    }

    [VisualFact]
    public void RmlUI_harfbuzz_gpu_shaped_text_renders()
    {
        using var fixture = new VisualRegressionFixture(360, 144);
        using var providerModule =
            global::Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi");
        RmlUISubsystem.RegisterRmlUILibrary(fixture.EngineFixture.Context);
        using var ui = new RmlUISubsystem("visual-harfbuzz-gpu");
        fixture.EngineFixture.Context.RegisterSubsystem(ui);
        ui.SetRendering(false);
        Assert.True(ui.LoadFont("Fonts/Anonymous Pro.ttf", fallback: true));

        using var markup = new Std.String();
        using var source = new Std.String();
        markup.Assign("""
            <rml>
            <head><style>
            body { margin: 0px; padding: 12px; background-color: #111722; color: #f4f7ff;
                   font-family: "Anonymous Pro"; font-size: 18px; }
            div { display: block; }
            .accent { color: #70c8ff; font-size: 24px; }
            </style></head>
            <body>
              <div>Software: right | Hardware: right</div>
              <div>minimum iii | Ω Ж é</div>
              <div class="accent">Affine fi fl — GPU</div>
            </body>
            </rml>
            """);
        source.Assign("memory://visual-harfbuzz-gpu.rml");
        using var document = ui.GetRmlContext().LoadDocumentFromMemory(markup, source);
        Assert.NotNull(document);
        document.Show();

        try
        {
            var actual = fixture.CaptureBackBuffer(() =>
            {
                ui.Update(0.0f);
                ui.Render();
            }, new Color(0.025f, 0.025f, 0.035f, 1.0f));

            fixture.AssertMatches(actual, "text/rmlui-harfbuzz-gpu.png",
                channelTolerance: 8, maxDifferingPixelRatio: 0.01, maxAverageChannelError: 1.0);
        }
        finally
        {
            document.Close();
            fixture.EngineFixture.Context.RemoveSubsystem<RmlUISubsystem>();
        }
    }
}
