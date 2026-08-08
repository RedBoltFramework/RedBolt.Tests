namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Editor.Core;

public sealed class EditorPluginManagerTests
{
    [Fact]
    public void Plugins_apply_to_matching_targets_in_registration_order()
    {
        using var manager = new EditorPluginManager();
        var calls = new List<string>();
        manager.AddPlugin<Target>("First", _ => calls.Add("First"));
        manager.AddPlugin<OtherTarget>("Other", _ => calls.Add("Other"));
        manager.AddPlugin<Target>("Second", _ => calls.Add("Second"));
        using var target = new Target();

        manager.Apply(target);

        Assert.Equal(["First", "Other", "Second"], manager.Plugins.Select(static plugin => plugin.Name));
        Assert.Equal(["First", "Second"], calls);
    }

    private sealed class Target : RedObject
    {
    }

    private sealed class OtherTarget : RedObject
    {
    }
}
