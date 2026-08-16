// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Utility;

public sealed class FileSystemReflectionTests
{
    [Fact]
    public void Entry_comparison_tree_navigation_and_clone_match_value_behavior()
    {
        Assert.True(FileSystemEntry.ComparePathFilesFirst("A.txt", "A/Child.txt"));
        Assert.True(FileSystemEntry.ComparePathDirectoriesFirst("A/Child.txt", "A.txt"));

        var root = new FileSystemEntry { LocalName = "Root" };
        var child = new FileSystemEntry { LocalName = "Child", ResourceName = "Child" };
        root.Children.Add(child);
        root.FillParents();
        var visited = new List<string>();
        root.ForEach(entry => visited.Add(entry.LocalName));
        var clone = root.Clone();
        clone.Children[0].LocalName = "Changed";

        Assert.Equal(["Root", "Child"], visited);
        Assert.Same(child, root.FindChild("Child"));
        Assert.Same(root, child.Parent);
        Assert.Equal("Child", root.Children[0].LocalName);
    }

    [Fact]
    public void Reflection_merges_roots_builds_hierarchy_and_reports_added_files()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"RedBolt-FileSystemReflection-{Guid.NewGuid():N}");
        var firstRoot = Path.Combine(tempRoot, "First");
        var secondRoot = Path.Combine(tempRoot, "Second");
        Directory.CreateDirectory(Path.Combine(firstRoot, "Folder"));
        Directory.CreateDirectory(Path.Combine(secondRoot, "Folder"));
        File.WriteAllText(Path.Combine(firstRoot, "Shared.txt"), "first");
        File.WriteAllText(Path.Combine(secondRoot, "Shared.txt"), "second");
        File.WriteAllText(Path.Combine(firstRoot, "Folder", "One.txt"), "one");

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            var updated = new List<string>();
            var listUpdateCount = 0;
            using (var reflection = new FileSystemReflection(
                       [AddTrailingSeparator(firstRoot), AddTrailingSeparator(secondRoot)]))
            {
                reflection.OnResourceUpdated += (_, entry) => updated.Add(entry.ResourceName);
                reflection.OnListUpdated += _ => ++listUpdateCount;

                reflection.Update();

                var shared = Assert.IsType<FileSystemEntry>(reflection.FindEntry("Shared.txt"));
                var folder = Assert.IsType<FileSystemEntry>(reflection.FindEntry("Folder"));
                var nested = Assert.IsType<FileSystemEntry>(reflection.FindEntry("Folder/One.txt"));
                Assert.True(shared.IsFileAmbiguous);
                Assert.Equal(0, shared.DirectoryIndex);
                Assert.Same(reflection, reflection.Root.Owner);
                Assert.Same(folder, nested.Parent);
                Assert.Equal(1, listUpdateCount);
                Assert.Contains("Shared.txt", updated);
                Assert.Contains("Folder/One.txt", updated);
            }
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(tempRoot, true);
        }
    }

    private static string AddTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
}
