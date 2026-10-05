// Copyright (c) 2026 RedBolt and contributors.
using System.Runtime.InteropServices;

using RedBolt.Plugins;
using RedBolt.UserCode.TestDependency;

[assembly: PluginModule("RedBolt.Test.Reloadable", typeof(RedBolt.UserCode.TestPlugin.TestPlugin))]

namespace RedBolt.UserCode.TestPlugin;

public sealed class TestPlugin : Plugin
{
}

public static class PluginProbe
{
    [DllImport("libc", EntryPoint = "getpid")]
    private static extern int GetPid();

    public static string GetDependencyValue() => Dependency.Value;

    public static int GetProcessId() => GetPid();
}
