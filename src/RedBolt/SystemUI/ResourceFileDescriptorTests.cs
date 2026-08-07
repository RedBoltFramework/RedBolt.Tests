namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Scene;
using RedBolt.SystemUI;

public sealed class ResourceFileDescriptorTests
{
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
}
