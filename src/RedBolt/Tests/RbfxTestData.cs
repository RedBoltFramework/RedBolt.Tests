namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Resource;

internal static class RbfxTestData
{
    public static string RepositoryRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string BuildRoot => Path.Combine(RepositoryRoot, "build");

    public static string Root => Path.Combine(RepositoryRoot, "bin");

    public static string CoreData => Path.Combine(Root, "CoreData");

    public static string Data => Path.Combine(Root, "Data");

    public static string EditorData => Path.Combine(Root, "EditorData");

    public static string BuildConfiguration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    public static string GetAppFile(string category, string fileName) =>
        Path.Combine(BuildRoot, "bin", BuildConfiguration, category, fileName);

    public static void AddResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        resources.AddResourceDir(CoreData);
        resources.AddResourceDir(Data);
    }

    public static void RemoveResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        resources.RemoveResourceDir(Data);
        resources.RemoveResourceDir(CoreData);
    }
}
