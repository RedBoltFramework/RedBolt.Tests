// Copyright (c) 2026 RedBolt and contributors.
using System.Runtime.InteropServices;

using RedBolt.Plugins;
using RedBolt.UserCode.TestDependency;

[assembly: PluginModule("RedBolt.Test.Reloadable", typeof(RedBolt.UserCode.TestPlugin.TestPluginApplication))]

namespace RedBolt.UserCode.TestPlugin;

public sealed class TestPluginApplication : PluginApplication
{
}

public static class PluginProbe
{
    [DllImport("libc", EntryPoint = "getpid")]
    private static extern int GetPid();

    public static string GetDependencyValue() => Dependency.Value;

    public static int GetProcessId() => GetPid();
}
