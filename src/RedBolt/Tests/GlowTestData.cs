// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.IO;

using RedBolt.Resource;

internal static class GlowTestData
{
    public static string Root { get; } = Path.GetFullPath(
        Path.Combine(RbfxTestData.RepositoryRoot, "..", "RedBolt.Glow"));

    public static string CoreData { get; } = Path.Combine(Root, "bin", "CoreData");

    public static string Data { get; } = Path.Combine(Root, "bin", "Data");

    public static void AddResourceDirs(ResourceCache resources)
    {
        resources.AddResourceDir(CoreData);
        resources.AddResourceDir(Data);
    }

    public static IDisposable MountResourceDirs(ResourceCache resources)
    {
        var coreDataAdded = resources.AddResourceDir(CoreData);
        var dataAdded = resources.AddResourceDir(Data);
        return new ResourceDirectoryScope(resources, coreDataAdded, dataAdded);
    }

    private sealed class ResourceDirectoryScope(
        ResourceCache resources, bool coreDataAdded, bool dataAdded) : IDisposable
    {
        public void Dispose()
        {
            if (dataAdded)
                resources.RemoveResourceDir(Data);
            if (coreDataAdded)
                resources.RemoveResourceDir(CoreData);
        }
    }
}
