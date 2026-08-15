namespace RedBolt.Tests;

using System.Runtime.CompilerServices;

using RedBolt;
using RedBolt.Resource;

internal static class RbfxTestData
{
    private readonly record struct AddedResourceDirs(bool CoreData, bool Data);

    private static readonly object ResourceDirScopesLock = new();
    private static readonly ConditionalWeakTable<ResourceCache, Stack<AddedResourceDirs>> ResourceDirScopes = new();

    public static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string BuildRoot => Path.Combine(RepositoryRoot, "build");

    public static string Root => Path.Combine(RepositoryRoot, "bin");

    public static string CoreData => Path.Combine(Root, "CoreData");

    public static string Data => Path.Combine(Root, "Data");

    public static string EditorData => Path.Combine(Root, "EditorData");

    public static string BuildConfiguration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Parent!.Name;

    public static string TargetPlatform => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string GetAppFile(string category, string fileName) =>
        Path.Combine(BuildRoot, "bin", BuildConfiguration, TargetPlatform, category, fileName);

    public static string GetHostAppFile(string category, string fileName) =>
        Path.Combine(BuildRoot, "bin", BuildConfiguration, "host", category, TargetPlatform, fileName);

    public static void AddResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        lock (ResourceDirScopesLock)
        {
            var added = new AddedResourceDirs(
                resources.AddResourceDir(CoreData),
                resources.AddResourceDir(Data));
            ResourceDirScopes.GetOrCreateValue(resources).Push(added);
        }
    }

    public static void RemoveResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        lock (ResourceDirScopesLock)
        {
            if (!ResourceDirScopes.TryGetValue(resources, out var scopes) || scopes.Count == 0)
                throw new InvalidOperationException("Resource directory scope was not added.");

            var added = scopes.Pop();
            if (added.Data)
                resources.RemoveResourceDir(Data);
            if (added.CoreData)
                resources.RemoveResourceDir(CoreData);
        }
    }
}
