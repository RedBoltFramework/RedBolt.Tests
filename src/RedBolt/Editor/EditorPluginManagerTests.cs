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

    [Fact]
    public void Scoped_plugins_apply_to_existing_targets_and_unapply_on_removal()
    {
        using var manager = new EditorPluginManager();
        using var target = new Target();
        manager.Apply(target);
        var applied = 0;
        var removed = 0;

        var registration = manager.AddScopedPlugin<Target>("Scoped", _ =>
        {
            ++applied;
            return new CallbackRegistration(() => ++removed);
        });

        Assert.Equal(1, applied);
        registration.Dispose();
        Assert.Equal(1, removed);
        Assert.DoesNotContain(manager.Plugins, static plugin => plugin.Name == "Scoped");
    }

    [Fact]
    public void Failed_plugin_application_rolls_back_registration()
    {
        using var manager = new EditorPluginManager();
        using var target = new Target();
        manager.Apply(target);

        Assert.Throws<InvalidOperationException>(() => manager.AddScopedPlugin<Target>(
            "Broken", static _ => throw new InvalidOperationException("Failure")));

        Assert.DoesNotContain(manager.Plugins, static plugin => plugin.Name == "Broken");
    }

    private sealed class CallbackRegistration(Action callback) : IDisposable
    {
        public void Dispose() => callback();
    }

    private sealed class Target : RedObject
    {
    }

    private sealed class OtherTarget : RedObject
    {
    }
}
