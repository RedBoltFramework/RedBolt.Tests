namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Project;

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
