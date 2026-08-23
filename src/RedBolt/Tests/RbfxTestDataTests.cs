// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

public sealed class RbfxTestDataTests
{
    [Fact]
    public void Scoped_resource_dirs_preserve_fixture_owned_mounts()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var virtualFileSystem = new VirtualFileSystem();
            var resources = new ResourceCache();
            context.RegisterSubsystem(virtualFileSystem);
            context.RegisterSubsystem(resources);
            Assert.True(resources.AddResourceDir(RbfxTestData.CoreData));
            Assert.True(resources.AddResourceDir(RbfxTestData.SampleData));
            var originalMountCount = virtualFileSystem.NumMountPoints();

            RbfxTestData.AddResourceDirs(resources);
            RbfxTestData.RemoveResourceDirs(resources);

            Assert.Contains(RbfxTestData.CoreData, resources.GetResourceDirs());
            Assert.Contains(RbfxTestData.SampleData, resources.GetResourceDirs());
            Assert.Equal(originalMountCount, virtualFileSystem.NumMountPoints());
        }
        finally
        {
            context.Dispose();
            Context.SetInstance(previousContext);
        }
    }
}
