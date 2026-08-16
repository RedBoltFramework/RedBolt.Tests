// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Security.Cryptography;
using System.Text;

/// <summary>Creates reproducible random inputs and reports the seed used by a test.</summary>
public static class DeterministicTestSeed
{
    public static int Get(string testName)
    {
        ArgumentException.ThrowIfNullOrEmpty(testName);

        var configuredSeed = Environment.GetEnvironmentVariable("REDBOLT_TEST_SEED");
        if (int.TryParse(configuredSeed, out var seed))
            return seed;

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(testName));
        return BitConverter.ToInt32(digest, 0);
    }

    public static Random Create(string testName)
    {
        return new Random(Get(testName));
    }
}

/// <summary>Owns per-test diagnostic files without allowing one test to overwrite another.</summary>
public sealed class TestArtifactScope : IDisposable
{
    private bool _keep;
    private bool _disposed;

    public TestArtifactScope(string testName)
    {
        ArgumentException.ThrowIfNullOrEmpty(testName);

        var safeName = string.Concat(testName.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        DirectoryPath = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", safeName,
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Keep()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _keep = true;
    }

    public string Write(string fileName, ReadOnlySpan<byte> content)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        var fullPath = Path.GetFullPath(Path.Combine(DirectoryPath, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(DirectoryPath) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            throw new ArgumentException("Artifact path must stay inside the test artifact directory.", nameof(fileName));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content.ToArray());
        return fullPath;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_keep && Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, recursive: true);
    }
}

/// <summary>Tracks test-owned handles and detects leaks and double release.</summary>
public sealed class TestLifetimeLedger : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<long, string> _live = new();
    private long _nextId;
    private bool _disposed;

    public IDisposable Track(string owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var id = ++_nextId;
            _live.Add(id, owner);
            return new Lease(this, id);
        }
    }

    public IReadOnlyCollection<string> LiveOwners
    {
        get
        {
            lock (_gate)
                return _live.Values.ToArray();
        }
    }

    public void AssertEmpty()
    {
        var liveOwners = LiveOwners;
        Assert.Empty(liveOwners);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            Assert.Empty(_live.Values);
        }
    }

    private void Release(long id)
    {
        lock (_gate)
        {
            if (!_live.Remove(id))
                throw new InvalidOperationException("Test lifetime " + id + " was released more than once or is unknown.");
        }
    }

    private sealed class Lease(TestLifetimeLedger ledger, long id) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                throw new InvalidOperationException("Test lifetime " + id + " was released more than once.");

            ledger.Release(id);
        }
    }
}
