namespace RedBolt.Tests;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;

public sealed class FoundationTests
{
    [Fact]
    public void Hierarchy_browser_source_defaults_are_no_ops_and_owner_is_forwarded()
    {
        HierarchyBrowserSource source = new TestHierarchyBrowserSource();

        Assert.Null(source.GetOwnerTab());
        source.RenderContent();
        source.RenderContextMenuItems();
        source.RenderMenu();
        source.ApplyHotkeys(null);
    }

    private sealed class TestHierarchyBrowserSource : HierarchyBrowserSource
    {
        public EditorTab? GetOwnerTab() => null;
    }
}
