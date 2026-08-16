// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Scene;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class ResourceFileDescriptorTests
{
    [Fact]
    public void Drag_drop_payload_is_stored_as_the_global_strong_reference()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        using var payload = new DragDropPayload();

        try
        {
            DragDropPayload.Set(payload);
            using var stored = context.GetGlobalVar(new StringHash("SystemUI_DragDropPayload")).Clone();

            Assert.Same(payload, stored.GetCustom<DragDropPayload>());
            Assert.Equal("Drop me", payload.GetDisplayString());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [NativeBindingFact]
    public void Drag_drop_payload_get_returns_null_without_an_active_native_payload()
    {
        var imguiContext = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(imguiContext);

            Assert.Null(DragDropPayload.Get());
        }
        finally
        {
            ImGui.DestroyContext(imguiContext);
        }
    }

    [Fact]
    public void Object_types_extensions_and_value_clone_match_descriptor_behavior()
    {
        var descriptor = new ResourceFileDescriptor
        {
            LocalName = "Scene.XML",
            ResourceName = "Scenes/Scene.XML",
            FileName = "/project/Data/Scenes/Scene.XML",
            IsAutomatic = true,
        };

        descriptor.AddObjectType("SerializableResource");
        descriptor.AddObjectType<Node>();

        Assert.True(descriptor.HasObjectType("SerializableResource"));
        Assert.True(descriptor.HasObjectType(new StringHash("SerializableResource")));
        Assert.True(descriptor.HasObjectType<Node>());
        Assert.Equal("Node", descriptor.MostDerivedType);
        Assert.True(descriptor.HasExtension(".xml"));
        Assert.True(descriptor.HasExtension([".json", ".XML"]));
        Assert.False(descriptor.HasExtension([".json", ".yaml"]));

        var clone = descriptor.Clone();
        descriptor.AddObjectType("Material");
        descriptor.TypeNames.Remove("SerializableResource");

        Assert.True(clone.HasObjectType("SerializableResource"));
        Assert.False(clone.HasObjectType("Material"));
        Assert.Equal("Node", clone.MostDerivedType);
        Assert.NotSame(descriptor.TypeNames, clone.TypeNames);
        Assert.NotSame(descriptor.Types, clone.Types);
    }

    [Fact]
    public void Resource_payload_formats_single_and_multiple_resources()
    {
        using var payload = new ResourceDragDropPayload();

        Assert.Equal("0 items", payload.GetDisplayString());

        payload.Resources.Add(new ResourceFileDescriptor { LocalName = "Scene.xml" });

        Assert.Equal("Scene.xml", payload.GetDisplayString());

        payload.Resources.Add(new ResourceFileDescriptor { LocalName = "Scene.json" });

        Assert.Equal("2 items", payload.GetDisplayString());
    }

    [Fact]
    public void Node_component_payload_formats_summary_and_keeps_weak_references()
    {
        using var payload = new NodeComponentDragDropPayload();
        var node = new Node();
        var component = node.CreateComponent<Camera>();

        payload.Nodes.Add(new WeakReference<Node>(node));
        payload.Components.Add(new WeakReference<Component>(component));

        Assert.Equal("???", payload.GetDisplayString());
        Assert.True(payload.Nodes[0].TryGetTarget(out var storedNode));
        Assert.Same(node, storedNode);
        Assert.True(payload.Components[0].TryGetTarget(out var storedComponent));
        Assert.Same(component, storedComponent);

        payload.DisplayString = "1 node, 1 component";

        Assert.Equal("1 node, 1 component", payload.GetDisplayString());
    }
}
