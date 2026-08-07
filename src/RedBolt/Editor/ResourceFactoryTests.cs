namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Utility;

public sealed class ResourceFactoryTests
{
    [Fact]
    public void Factory_exposes_defaults_and_orders_by_group_then_title()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var beta = new TestResourceFactory(1, "Beta");
            using var alpha = new TestResourceFactory(1, "Alpha");
            using var earlierGroup = new TestResourceFactory(0, "Zulu");
            var factories = new List<ResourceFactory> { beta, alpha, earlierGroup };
            factories.Sort(static (lhs, rhs) =>
                ResourceFactory.Compare(lhs, rhs) ? -1 : ResourceFactory.Compare(rhs, lhs) ? 1 : 0);

            Assert.Equal([earlierGroup, alpha, beta], factories);
            Assert.True(alpha.IsEnabled(new FileSystemEntry()));
            Assert.Equal(1, alpha.Group);
            Assert.Equal("Alpha", alpha.Title);
            alpha.DiscardAndClose();
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private sealed class TestResourceFactory(int group, string title) : ResourceFactory(group, title)
    {
        public override void Open(string baseFilePath, string baseResourcePath)
        {
        }

        public override void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit)
        {
        }

        public override void CommitAndClose()
        {
        }
    }
}
