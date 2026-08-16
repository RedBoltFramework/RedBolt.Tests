// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Container;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CoreReflectionBranchGapTests
{
    [Fact]
    public void Object_reflection_covers_missing_attribute_metadata_and_factory_paths()
    {
        using var reflection = new ObjectReflection(typeof(ReflectionProbe));
        using var attribute = new AttributeInfo("Value", typeof(int), 1);
        reflection.AddAttribute(attribute);

        reflection.RemoveAttribute("Missing");
        reflection.UpdateAttributeDefaultValue("Missing", new Variant(3));
        Assert.Equal(uint.MaxValue, reflection.GetAttributeIndex("Missing"));
        Assert.Null(reflection.GetAttribute("Missing"));
        Assert.Equal(Variant.Empty, reflection.GetMetadata("Missing"));

        using var metadata = new Variant(17);
        reflection.SetMetadata("Number", metadata);
        Assert.Equal(17, reflection.GetMetadata<int>("Number"));
        using var replacement = new Variant(23);
        reflection.SetMetadata("Number", replacement);
        Assert.Equal(23, reflection.GetMetadata<int>("Number"));

        using var copied = new ObjectReflection(typeof(ReflectionProbe));
        copied.CopyAttributesFrom(reflection);
        Assert.Equal(1u, copied.NumAttributes);
        copied.RemoveAllAttributes();

        Assert.Null(reflection.CreateObject());
        reflection.SetObjectFactory(static () => new ReflectionProbe());
        using var created = Assert.IsType<ReflectionProbe>(reflection.CreateObject());
        Assert.True(reflection.HasObjectFactory());
    }

    [Fact]
    public void Object_reflection_registry_covers_invalid_generated_types_and_missing_forwarders()
    {
        using var registry = new ObjectReflectionRegistry();
        Assert.Throws<ArgumentException>(() => registry.AddGeneratedReflection(typeof(string), "Invalid", null));

        using var reflection = registry.AddGeneratedReflection(typeof(ReflectionProbe), "Probe", null);
        Assert.False(reflection.HasObjectFactory());
        Assert.Throws<InvalidOperationException>(() =>
            registry.AddGeneratedReflection(typeof(ReflectionProbe), "Duplicate", static () => new ReflectionProbe()));

        registry.RemoveAttribute<UnreflectedProbe>("Missing");
        using var value = new Variant(2);
        registry.UpdateAttributeDefaultValue<UnreflectedProbe>("Missing", value);
        registry.RemoveReflection("Missing");
    }

    [Fact]
    public void Generated_reflection_registration_covers_unregistered_assembly_and_base_search()
    {
        using var context = new Context();
        Assert.Throws<InvalidOperationException>(() =>
            GeneratedReflectionRegistration.RegisterAssembly(context, typeof(string).Assembly));
        Assert.Null(GeneratedReflectionRegistration.FindBaseReflection(context, typeof(ReflectionProbe)));
        Assert.Throws<InvalidOperationException>(() =>
            GeneratedReflectionRegistration.RegisterType(context, typeof(ReflectionProbe)));
    }

    [Fact]
    public void Context_subsystem_and_event_queries_cover_empty_and_filtered_paths()
    {
        using var context = new Context();
        var subsystem = new ReflectionProbe();
        context.RegisterSubsystem(subsystem, new StringHash("ProbeSubsystem"));
        context.RemoveSubsystem(new StringHash("MissingSubsystem"));
        context.RemoveSubsystem(new StringHash("ProbeSubsystem"));
        subsystem.Dispose();

        var sender = new ReflectionProbe();
        var receiver = new ReflectionProbe();
        Assert.False(context.HasSubscribedToEvent(receiver));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver));
        Assert.False(context.HasSubscribedToEvent<PingEvent>(receiver, sender));

        var calls = 0;
        context.SubscribeToEvent<PingEvent>(receiver, sender, (ref PingEvent _) => ++calls);
        Assert.True(context.HasSubscribedToEvent(receiver));
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver));
        Assert.True(context.HasSubscribedToEvent<PingEvent>(receiver, sender));

        context.UnsubscribeAllExcept(receiver, Array.Empty<RedObject>());
        var ping = new PingEvent();
        context.SendEvent(sender, ref ping);
        Assert.Equal(0, calls);
        context.UnsubscribeAllExcept(receiver, Array.Empty<Type>());
        receiver.Dispose();
        sender.Dispose();
    }

    private sealed class ReflectionProbe : RedObject
    {
    }

    private sealed class UnreflectedProbe : RedObject
    {
    }

    private struct PingEvent
    {
    }
}
