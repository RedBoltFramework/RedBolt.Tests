namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;

public sealed class ResourceCacheGuardTests
{
    [Fact]
    public void Dispose_replaces_project_mounts_with_original_mounts_in_order()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "RedBoltResourceCacheGuard-" + Guid.NewGuid().ToString("N"));
        var coreDataPath = Path.Combine(rootPath, "CoreData");
        var editorDataPath = Path.Combine(rootPath, "EditorData");
        Directory.CreateDirectory(coreDataPath);
        Directory.CreateDirectory(editorDataPath);

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            context.RegisterSubsystem(virtualFileSystem);
            var coreData = virtualFileSystem.MountDir(coreDataPath);
            var editorData = virtualFileSystem.MountDir(editorDataPath);
            var guard = new ResourceCacheGuard();

            Assert.EndsWith("/CoreData/", guard.CoreData);
            Assert.EndsWith("/EditorData/", guard.EditorData);

            var projectMount = new TrackingMountedRoot();
            virtualFileSystem.Mount(projectMount);

            guard.Dispose();

            Assert.Equal(1, projectMount.DisposeCount);
            Assert.Equal(2u, virtualFileSystem.NumMountPoints());
            Assert.Same(coreData, virtualFileSystem.GetMountPoint(0));
            Assert.Same(editorData, virtualFileSystem.GetMountPoint(1));
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(rootPath, true);
        }
    }

    private sealed class TrackingMountedRoot : MountedRoot
    {
        public int DisposeCount { get; private set; }

        public override void Dispose()
        {
            ++DisposeCount;
            base.Dispose();
        }
    }
}
