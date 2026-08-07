namespace RedBolt.Tests;

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
}
