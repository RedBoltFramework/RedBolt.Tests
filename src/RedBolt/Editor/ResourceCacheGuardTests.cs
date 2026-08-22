// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.IO;

using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;

[Collection(nameof(ApplicationLifecycleCollection))]
public sealed class ResourceCacheGuardTests
{
    [Fact]
    public void Effective_resource_directory_uses_the_last_matching_mount()
    {
        using var scope = new TestArtifactScope(nameof(Effective_resource_directory_uses_the_last_matching_mount));
        var fallbackCoreData = Path.Combine(scope.DirectoryPath, "fallback", "CoreData");
        var explicitCoreData = Path.Combine(scope.DirectoryPath, "explicit", "CoreData");
        Directory.CreateDirectory(fallbackCoreData);
        Directory.CreateDirectory(explicitCoreData);

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            context.RegisterSubsystem(virtualFileSystem);
            virtualFileSystem.MountDir(fallbackCoreData);
            virtualFileSystem.MountDir(explicitCoreData);

            using var guard = new ResourceCacheGuard();

            Assert.Equal([
                Path.GetFullPath(fallbackCoreData).Replace('\\', '/').TrimEnd('/') + "/",
                Path.GetFullPath(explicitCoreData).Replace('\\', '/').TrimEnd('/') + "/",
            ], guard.ResourceDirectories);
            Assert.Equal(
                Path.GetFullPath(explicitCoreData).Replace('\\', '/').TrimEnd('/') + "/",
                guard.CoreData);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Resource_directories_exclude_the_resource_root_data_alias()
    {
        using var scope = new TestArtifactScope(nameof(Resource_directories_exclude_the_resource_root_data_alias));
        var dataPath = Path.Combine(scope.DirectoryPath, "Data");
        var moduleDataPath = Path.Combine(scope.DirectoryPath, "Data.Module");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(moduleDataPath);

        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            context.RegisterSubsystem(virtualFileSystem);
            virtualFileSystem.MountAliasRoot();
            var dataMount = virtualFileSystem.MountDir(dataPath);
            virtualFileSystem.MountAlias("res:Data", dataMount);
            virtualFileSystem.MountDir(moduleDataPath);

            using var guard = new ResourceCacheGuard();

            Assert.Equal([
                Path.GetFullPath(moduleDataPath).Replace('\\', '/').TrimEnd('/') + "/",
            ], guard.ResourceDirectories);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

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
