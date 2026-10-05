// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

public sealed class ResourceTests
{
    [Fact]
    public void Resource_preserves_cpp_class_kind_and_base_state()
    {
        using var resource = new ResourceType();

        Assert.True(typeof(ResourceType).IsClass);
        Assert.False(typeof(ResourceType).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(ResourceType).GetInterfaces());
        Assert.Equal(typeof(int), typeof(ResourceType).GetProperty(nameof(ResourceType.MemoryUse))!.PropertyType);
        Assert.Equal(string.Empty, resource.GetName());
        Assert.Equal(StringHash.Empty, resource.GetNameHash());
        Assert.Equal(0, resource.GetMemoryUse());
        Assert.Equal(AsyncLoadState.Done, resource.GetAsyncLoadState());
        Assert.Equal(string.Empty, resource.GetAbsoluteFileName());
    }

    [Fact]
    public void Simple_resource_preserves_cpp_class_kind_and_save_format_selection()
    {
        using var resource = new EmptySimpleResource();
        using var destination = new VectorBuffer();

        Assert.True(typeof(SimpleResource).IsClass);
        Assert.True(typeof(SimpleResource).IsAbstract);
        Assert.False(typeof(SimpleResource).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(SimpleResource).GetInterfaces());
        Assert.True(typeof(InternalResourceFormat).IsValueType);

        resource.SetSaveFormatHint(InternalResourceFormat.Binary);
        Assert.True(resource.Save(destination));
        Assert.Equal((byte)'{', destination.Buffer[0]);
    }

    [Fact]
    public void Resource_with_metadata_preserves_cpp_class_kind()
    {
        Assert.True(typeof(ResourceWithMetadata).IsClass);
        Assert.False(typeof(ResourceWithMetadata).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(ResourceWithMetadata).GetInterfaces());
    }

    [Fact]
    public void Resource_metadata_copies_owned_variant_values()
    {
        using var source = new MetadataResource();
        using var copy = new MetadataResource();
        using var value = new Variant(new byte[] { 1, 2, 3 });

        source.AddMetadata("Buffer", value);
        value.GetBuffer()[0] = 9;
        copy.SetMetadata(source.GetMetadata());
        source.GetMetadata("Buffer").GetBuffer()[0] = 8;
        source.SetMetadata(source.GetMetadata());

        Assert.Equal(8, source.GetMetadata("Buffer").GetBuffer()[0]);
        Assert.Equal(1, copy.GetMetadata("Buffer").GetBuffer()[0]);

        source.RemoveMetadata("Buffer");
        Assert.True(source.GetMetadata("Buffer").IsEmpty);
        Assert.Equal(1, copy.GetMetadata("Buffer").GetBuffer()[0]);
    }

    [Fact]
    public void Resource_set_metadata_overwrites_all_old_values_and_copies_new_ones()
    {
        using var resource = new MetadataResource();
        resource.AddMetadata("Old", new Variant(1));
        resource.AddMetadata("Kept", new Variant(2));

        using var replacement = new StringVariantMap();
        replacement.Add("Kept", new Variant(3));
        replacement.Add("Buffer", new Variant(new byte[] { 1, 2, 3 }));
        resource.SetMetadata(replacement);
        replacement["Buffer"].GetBuffer()[0] = 9;

        Assert.Equal(2, resource.GetMetadata().Count);
        Assert.True(resource.GetMetadata("Old").IsEmpty);
        Assert.Equal(new Variant(3), resource.GetMetadata("Kept"));
        Assert.Equal(1, resource.GetMetadata("Buffer").GetBuffer()[0]);

        resource.SetMetadata(new StringVariantMap());
        Assert.False(resource.HasMetadata());
        Assert.Empty(resource.GetMetadata());
    }

    [Fact]
    public void Resource_metadata_add_get_has_remove_and_remove_all()
    {
        using var resource = new MetadataResource();
        Assert.False(resource.HasMetadata());
        Assert.True(resource.GetMetadata("Missing").IsEmpty);

        resource.AddMetadata("Count", new Variant(42));
        resource.AddMetadata("Label", new Variant("Smoke"));
        resource.AddMetadata("Count", new Variant(7));

        Assert.True(resource.HasMetadata());
        Assert.Equal(2, resource.GetMetadata().Count);
        Assert.Equal(new Variant(7), resource.GetMetadata("Count"));
        Assert.Equal(new Variant(7), resource.GetMetadata()["Count"]);

        resource.RemoveMetadata("Count");
        resource.RemoveMetadata("Missing");
        Assert.True(resource.GetMetadata("Count").IsEmpty);
        Assert.Equal(new Variant("Smoke"), resource.GetMetadata("Label"));

        resource.RemoveAllMetadata();
        Assert.False(resource.HasMetadata());
        Assert.Empty(resource.GetMetadata());
    }

    [Fact]
    public void Failed_load_restores_done_state()
    {
        var resource = new FailingResource();

        Assert.False(resource.Load(new MemoryBuffer(Array.Empty<byte>())));
        Assert.Equal(AsyncLoadState.Done, resource.AsyncLoadState);
    }

    [Fact]
    public void Empty_resource_name_is_allowed()
    {
        var resource = new FailingResource();

        resource.SetName(string.Empty);

        Assert.Equal(string.Empty, resource.Name);
    }

    [Fact]
    public void Resource_metadata_saves_sorted_keys_and_xml_roundtrips()
    {
        var resource = new MetadataResource();
        resource.AddMetadata("Label", new Variant("Smoke"));
        resource.AddMetadata("Count", new Variant(42));
        resource.AddMetadata("Count", new Variant(7));

        var xml = new XMLFile { Name = string.Empty };
        var root = xml.CreateRoot("resource");
        resource.SaveMetadata(root);

        var first = root.GetChild("metadata");
        Assert.Equal("Count", first.GetAttribute("name"));
        Assert.Equal("Label", first.GetNext("metadata").GetAttribute("name"));

        var loaded = new MetadataResource();
        loaded.AddMetadata("Stale", new Variant(1));
        loaded.LoadMetadata(root);
        Assert.Equal(2, loaded.GetMetadata().Count);
        Assert.True(loaded.GetMetadata("Stale").IsEmpty);
        Assert.Equal(new Variant(7), loaded.GetMetadata("Count"));
        Assert.Equal(new Variant("Smoke"), loaded.GetMetadata("Label"));
    }

    [Fact]
    public void Resource_metadata_created_from_xml_keeps_first_duplicate()
    {
        var xml = new XMLFile { Name = string.Empty };
        Assert.True(xml.FromString("""
            <resource>
              <metadata name="Value" type="Int" value="1" />
              <metadata name="Value" type="Int" value="2" />
            </resource>
            """));

        using var metadata = MetadataResource.Create(xml.GetRoot());

        Assert.Single(metadata);
        Assert.Equal(new Variant(1), metadata["Value"]);
    }

    [Fact]
    public void Resource_reference_helpers_preserve_names_and_types()
    {
        var resource = new FailingResource();
        resource.SetName("Textures/Test.png");
        var defaultType = new StringHash("DefaultResource");

        Assert.Equal("Textures/Test.png", ResourceType.GetResourceName(resource));
        Assert.Equal(resource.GetTypeNameHash(), ResourceType.GetResourceType(resource, defaultType));
        Assert.Equal(new ResourceRef(resource.GetTypeNameHash(), "Textures/Test.png"),
            ResourceType.GetResourceRef(resource, defaultType));
        Assert.Equal(string.Empty, ResourceType.GetResourceName(null));
        Assert.Equal(defaultType, ResourceType.GetResourceType(null, defaultType));

        var names = ResourceType.GetResourceNames<FailingResource>([resource, null]);
        Assert.Equal(["Textures/Test.png", string.Empty], names);

        var references = ResourceType.GetResourceRefList<FailingResource>([resource, null]);
        Assert.Equal(typeof(FailingResource).GetTypeId(), references.Type);
        Assert.Equal(["Textures/Test.png", string.Empty], references.Names);
    }

    private sealed class FailingResource : ResourceType
    {
        public FailingResource()
        {
        }

        public override bool BeginLoad(Deserializer source) => false;
    }

    private sealed class EmptySimpleResource : SimpleResource
    {
        public override void SerializeInBlock(Archive archive)
        {
        }
    }

    private sealed class MetadataResource : ResourceWithMetadata
    {
        public MetadataResource()
        {
        }

        public static StringVariantMap Create(XMLElement source) => CreateMetadataFromXML(source);

        public void LoadMetadata(XMLElement source)
        {
            using var metadata = CreateMetadataFromXML(source);
            SetMetadata(metadata);
        }

        public void SaveMetadata(XMLElement destination) => SaveMetadataToXML(destination);
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BinaryFileTests
{
    private sealed class DerivedBinaryFile : BinaryFile
    {
    }

    public BinaryFileTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Archive_callbacks_roundtrip_values()
    {
        var file = new BinaryFile();
        var outputValue = 42;

        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref outputValue);
        }));

        var inputValue = 0;
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref inputValue);
        }));
        Assert.Equal(outputValue, inputValue);
    }

    [Fact]
    public void ReadLines_does_not_append_line_after_terminal_newline()
    {
        var file = new BinaryFile();
        file.SetText("first\nsecond\n");

        Assert.Equal(["first", "second"], file.ReadLines());
    }

    [Fact]
    public void Class_kind_and_owned_buffer_lifetime_match_native()
    {
        var file = new DerivedBinaryFile();
        file.SetData([1]);
        var buffer = file.GetMutableBuffer();
        Assert.NotEqual(nint.Zero, buffer.GetData());

        file.Dispose();

        var pinnedBufferField = typeof(VectorBuffer).GetField("_pinnedBuffer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pinnedBuffer = (System.Runtime.InteropServices.GCHandle)pinnedBufferField.GetValue(buffer)!;
        Assert.False(pinnedBuffer.IsAllocated);
    }
}
