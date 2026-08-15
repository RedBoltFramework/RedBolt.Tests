namespace RedBolt.UserCode.TestDependency;

using RedBolt.Core;

public static class Dependency
{
    public static string Value => "Private dependency";
}

[Reflected("Tests/PluginDependency")]
public sealed partial class ReflectedDependency : RedObject
{
}
