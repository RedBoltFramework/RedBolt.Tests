namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Project;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class EditorTabTypesTests
{
    [Fact]
    public void Editor_tab_flags_combine_and_test_independently()
    {
        var flags = EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault;

        Assert.NotEqual(EditorTabFlag.None, flags & EditorTabFlag.NoContentPadding);
        Assert.NotEqual(EditorTabFlag.None, flags & EditorTabFlag.OpenByDefault);
        Assert.Equal(EditorTabFlag.None, flags & EditorTabFlag.FocusOnStart);
    }

    [Fact]
    public void Editor_configurable_dispatches_ini_contract_to_implementation()
    {
        EditorConfigurable configurable = new TestEditorConfigurable();

        configurable.WriteIniSettings(null!);
        configurable.ReadIniSettings("Value=42");

        var implementation = Assert.IsType<TestEditorConfigurable>(configurable);
        Assert.True(implementation.WasWritten);
        Assert.Equal("Value=42", implementation.Line);
        Assert.Equal("Test", configurable.IniEntry);
    }

    [NativeBindingFact]
    public void Separator_helper_adds_only_once_until_reset()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;

        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            ImGui.NewFrame();
            frameStarted = true;

            var open = true;
            ImGui.Begin("Separator helper test"u8, ref open);
            windowStarted = true;

            var helper = new SeparatorHelper();
            using var drawList = ImGui.GetWindowDrawList();
            var before = drawList.VtxBufferSize;

            helper.Add();
            var afterFirst = drawList.VtxBufferSize;
            helper.Add();
            var afterSecond = drawList.VtxBufferSize;
            helper.Reset();
            helper.Add();
            var afterReset = drawList.VtxBufferSize;

            Assert.True(afterFirst > before);
            Assert.Equal(afterFirst, afterSecond);
            Assert.True(afterReset > afterSecond);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(context);
        }
    }

    private sealed class TestEditorConfigurable : EditorConfigurable
    {
        public bool WasWritten { get; private set; }

        public string Line { get; private set; } = string.Empty;

        public override string IniEntry => "Test";

        public override void WriteIniSettings(ImGuiTextBuffer output)
        {
            WasWritten = true;
        }

        public override void ReadIniSettings(string line)
        {
            Line = line;
        }
    }
}
