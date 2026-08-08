namespace RedBolt.UserCode.TestPlugin;

using System.Runtime.InteropServices;

using RedBolt.UserCode.TestDependency;

public static class PluginProbe
{
    [DllImport("libc", EntryPoint = "getpid")]
    private static extern int GetPid();

    public static string GetDependencyValue() => Dependency.Value;

    public static int GetProcessId() => GetPid();
}
