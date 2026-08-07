namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.IO;
using RedBolt.Resource;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class SettingsManagerTests
{
    [Fact]
    public void Pages_are_sorted_and_inserted_into_the_render_tree()
    {
        WithManager(manager =>
        {
            var firstCamera = new TestPage("Editor.Scene:Camera");
            var assets = new TestPage("Editor.Assets:Concurrency");
            var secondCamera = new TestPage("Editor.Scene:Camera");

            manager.AddPage(firstCamera);
            manager.AddPage(assets);
            manager.AddPage(secondCamera);

            Assert.Equal(["Editor.Assets:Concurrency", "Editor.Scene:Camera"], manager.SortedPages.Keys);
            Assert.Same(secondCamera, manager.FindPage("Editor.Scene:Camera"));
            Assert.Null(manager.FindPage("Missing"));

            var editorGroup = manager.PageTree.Children["Editor"];
            Assert.Same(assets, editorGroup.Children["Assets"].Pages["Concurrency"]);
            Assert.Same(firstCamera, editorGroup.Children["Scene"].Pages["Camera"]);
        });
    }

    [Fact]
    public void Serializable_pages_round_trip_and_missing_pages_keep_their_values()
    {
        WithManager(manager =>
        {
            var serialized = new TestPage("Editor.Scene:Camera") { Value = 42 };
            var transient = new TestPage("Editor.Transient") { Serializable = false, Value = 5 };
            manager.AddPage(serialized);
            manager.AddPage(transient);

            using var output = new JSONFile();
            Assert.True(output.SaveObject("Settings", manager));
            var json = output.ToString();

            var missing = new TestPage("Editor.Missing") { Value = 77 };
            manager.AddPage(missing);
            serialized.Value = 99;
            transient.Value = 6;

            using var input = new JSONFile();
            Assert.True(input.FromString(json));
            Assert.True(input.LoadObject("Settings", manager));

            Assert.Equal(42, serialized.Value);
            Assert.Equal(6, transient.Value);
            Assert.Equal(77, missing.Value);
            Assert.Equal(2, serialized.SerializeCount);
            Assert.Equal(0, transient.SerializeCount);
            Assert.Equal(0, missing.SerializeCount);
        });
    }

    [Fact]
    public void Simple_page_forwards_to_its_value_and_resets_it()
    {
        var page = new SimpleSettingsPage<TestSettings>();

        Assert.Equal(5, page.Values.Value);
        Assert.Equal("Editor.Test", page.UniqueName);
        Assert.True(page.IsSerializable);
        Assert.True(page.CanResetToDefault);

        page.RenderSettings();
        Assert.Equal(6, page.Values.Value);

        page.ResetToDefaults();
        Assert.Equal(5, page.Values.Value);
    }

    private static void WithManager(Action<SettingsManager> test)
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var manager = new SettingsManager();
            test(manager);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private sealed class TestPage : SettingsPage
    {
        public TestPage(string uniqueName)
        {
            UniqueName = uniqueName;
        }

        public override string UniqueName { get; }

        public override bool IsSerializable => Serializable;

        public bool Serializable { get; init; } = true;

        public int Value;

        public int SerializeCount { get; private set; }

        public override void SerializeInBlock(Archive archive)
        {
            ++SerializeCount;
            ArchiveSerialization.SerializeValue(archive, "Value", ref Value);
        }

        public override void RenderSettings()
        {
        }
    }

    private struct TestSettings : ISimpleSettingsPageValues
    {
        public TestSettings()
        {
            Value = 5;
        }

        public string UniqueName => "Editor.Test";

        public int Value;

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeValue(archive, "Value", ref Value);
        }

        public void RenderSettings()
        {
            ++Value;
        }
    }
}
