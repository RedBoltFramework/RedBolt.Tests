// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Xunit;

using RedBolt.IO;

public sealed class MultiFileWatcherTests
{
    [Fact]
    public void MultiFileWatcher_is_an_extensible_noncopyable_class()
    {
        Assert.True(typeof(MultiFileWatcher).IsClass);
        Assert.False(typeof(MultiFileWatcher).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(MultiFileWatcher)));
    }

    [Fact]
    public void MultiFileWatcher_forwards_delay_and_changes()
    {
        var firstDirectory = CreateTempDirectory();
        var secondDirectory = CreateTempDirectory();
        using var watcher = new MultiFileWatcher();

        try
        {
            watcher.SetDelay(0);
            Assert.Equal(0.0f, watcher.GetDelay());
            Assert.True(watcher.StartWatching(firstDirectory, false));
            Assert.True(watcher.StartWatching(secondDirectory, false));

            System.IO.File.WriteAllText(Path.Combine(firstDirectory, "first.txt"), "first");
            System.IO.File.WriteAllText(Path.Combine(secondDirectory, "second.txt"), "second");

            // One write can report the same file more than once (created, then modified), in any order
            // between the two directories, so collect names until both files were seen.
            Assert.Equal(
                new HashSet<string> { "first.txt", "second.txt" },
                CollectChangedFiles(watcher, expectedCount: 2));

            watcher.StopWatching();
            var noChange = new FileChange();
            Assert.False(watcher.GetNextChange(ref noChange));
        }
        finally
        {
            watcher.StopWatching();
            Directory.Delete(firstDirectory, true);
            Directory.Delete(secondDirectory, true);
        }
    }

    [Fact]
    public void MultiFileWatcher_does_not_retain_failed_watchers()
    {
        using var watcher = new MultiFileWatcher();
        Assert.False(watcher.StartWatching(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), false));
        var change = new FileChange();
        Assert.False(watcher.GetNextChange(ref change));
    }

    private static HashSet<string> CollectChangedFiles(MultiFileWatcher watcher, int expectedCount)
    {
        var fileNames = new HashSet<string>();
        var change = new FileChange();
        var timer = Stopwatch.StartNew();
        while (fileNames.Count < expectedCount && timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (watcher.GetNextChange(ref change))
                fileNames.Add(change.FileName);
            else
                Thread.Sleep(10);
        }

        return fileNames;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltMultiFileWatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
