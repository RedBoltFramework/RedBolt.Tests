namespace RedBolt.Tests;

public sealed class DeterministicTestSupportTests
{
    [Fact]
    public void Seed_is_stable_and_can_be_overridden()
    {
        var original = Environment.GetEnvironmentVariable("REDBOLT_TEST_SEED");
        try
        {
            Environment.SetEnvironmentVariable("REDBOLT_TEST_SEED", null);
            Assert.Equal(DeterministicTestSeed.Get("same-test"), DeterministicTestSeed.Get("same-test"));
            Assert.NotEqual(DeterministicTestSeed.Get("same-test"), DeterministicTestSeed.Get("other-test"));

            Environment.SetEnvironmentVariable("REDBOLT_TEST_SEED", "17");
            Assert.Equal(17, DeterministicTestSeed.Get("same-test"));
            var first = DeterministicTestSeed.Create("same-test");
            var second = DeterministicTestSeed.Create("same-test");
            Assert.Equal(first.Next(100), second.Next(100));
        }
        finally
        {
            Environment.SetEnvironmentVariable("REDBOLT_TEST_SEED", original);
        }
    }

    [Fact]
    public void Artifact_scope_rejects_path_escape_and_cleans_by_default()
    {
        string directoryPath;
        using (var scope = new TestArtifactScope(nameof(Artifact_scope_rejects_path_escape_and_cleans_by_default)))
        {
            directoryPath = scope.DirectoryPath;
            var path = scope.Write("nested/data.bin", [1, 2, 3]);
            Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
            Assert.Throws<ArgumentException>(() => scope.Write("../escape.bin", [4]));
        }

        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public void Lifetime_ledger_tracks_and_releases_handles()
    {
        using var ledger = new TestLifetimeLedger();
        var lease = ledger.Track("native-buffer");
        using (lease)
        {
            Assert.Contains("native-buffer", ledger.LiveOwners);
        }

        ledger.AssertEmpty();
        Assert.Throws<InvalidOperationException>(lease.Dispose);
    }
}
