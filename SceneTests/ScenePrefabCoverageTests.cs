namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class ScenePrefabCoverageTests
{
    [Fact]
    public void Attribute_prefab_roundtrips_all_identifiers_through_human_and_binary_archives()
    {
        using var id = CreateAttribute((AttributeId)7, new Variant(42));
        using var named = CreateAttribute("Label", new Variant("human"));
        using var hashed = CreateAttribute(new StringHash("Compact"), new Variant(11));

        Assert.Equal(AttributePrefab.IdentifierType.Id, id.GetIdentifierType());
        Assert.Equal(AttributePrefab.IdentifierType.Name, named.GetIdentifierType());
        Assert.Equal(AttributePrefab.IdentifierType.NameHash, hashed.GetIdentifierType());
        Assert.Equal(named, named.Clone());
        Assert.NotEqual(named, hashed);

        using var humanOutput = new TestJSONOutputArchive();
        using (humanOutput.OpenUnorderedBlock("root"))
        {
            using (humanOutput.OpenUnorderedBlock("id"))
                id.SerializeInBlock(humanOutput);
            using (humanOutput.OpenUnorderedBlock("named"))
                named.SerializeInBlock(humanOutput);
            using (humanOutput.OpenUnorderedBlock("hashed"))
                hashed.SerializeInBlock(humanOutput);
        }

        using var humanInput = new TestJSONInputArchive(humanOutput.ToJsonString());
        using var loadedId = new AttributePrefab();
        using var loadedNamed = new AttributePrefab();
        using var loadedHashed = new AttributePrefab();
        using (humanInput.OpenUnorderedBlock("root"))
        {
            using (humanInput.OpenUnorderedBlock("id"))
                loadedId.SerializeInBlock(humanInput);
            using (humanInput.OpenUnorderedBlock("named"))
                loadedNamed.SerializeInBlock(humanInput);
            using (humanInput.OpenUnorderedBlock("hashed"))
                loadedHashed.SerializeInBlock(humanInput);
        }

        Assert.Equal(id, loadedId);
        Assert.Equal(named, loadedNamed);
        Assert.Equal(hashed, loadedHashed);
        Assert.Equal(named.NameHash, loadedNamed.NameHash);

        using var binaryBuffer = new VectorBuffer();
        using (var binaryOutput = new BinaryOutputArchive(Context.Instance, binaryBuffer))
        {
            using (binaryOutput.OpenUnorderedBlock("root"))
            {
                id.SerializeInBlock(binaryOutput);
                named.SerializeInBlock(binaryOutput);
                hashed.SerializeInBlock(binaryOutput);
            }
            binaryOutput.Flush();
        }

        binaryBuffer.Seek(0);
        using (var binaryInput = new BinaryInputArchive(Context.Instance, binaryBuffer))
        {
            using (binaryInput.OpenUnorderedBlock("root"))
            {
                loadedId.Dispose();
                loadedNamed.Dispose();
                loadedHashed.Dispose();
                loadedId.SerializeInBlock(binaryInput);
                loadedNamed.SerializeInBlock(binaryInput);
                loadedHashed.SerializeInBlock(binaryInput);
            }
            binaryInput.Flush();
        }

        Assert.Equal(id, loadedId);
        Assert.Equal(named, loadedNamed);
        Assert.Equal(hashed, loadedHashed);

        using var compactBuffer = new VectorBuffer();
        using (var compactOutput = new BinaryOutputArchive(Context.Instance, compactBuffer))
        {
            using (compactOutput.OpenUnorderedBlock("root"))
                named.SerializeInBlock(compactOutput, compactSave: true);
            compactOutput.Flush();
        }
        compactBuffer.Seek(0);
        using var compact = new AttributePrefab();
        using (var compactInput = new BinaryInputArchive(Context.Instance, compactBuffer))
        {
            using (compactInput.OpenUnorderedBlock("root"))
                compact.SerializeInBlock(compactInput, compactSave: true);
            compactInput.Flush();
        }
        Assert.Equal(AttributePrefab.IdentifierType.NameHash, compact.GetIdentifierType());
        Assert.Equal(named.NameHash, compact.NameHash);
    }

    [Fact]
    public void Serializable_prefab_import_export_and_archive_flags_filter_attributes()
    {
        using var source = new PrefabSerializable
        {
            Saved = 7,
            NonPrefab = 8,
            EnumValue = 1,
            TemporaryOnly = 9,
            TemporaryReadOnly = 10,
            EditOnly = 11
        };
        source.SetTemporary(true);

        using var prefab = new SerializablePrefab();
        prefab.Import(source, PrefabSaveFlag.Prefab | PrefabSaveFlag.SaveTemporary
            | PrefabSaveFlag.EnumsAsStrings | PrefabSaveFlag.CompactAttributeNames);

        Assert.Equal(ObjectReflection<PrefabSerializable>.ClassName, prefab.TypeName);
        Assert.Equal(4, prefab.Attributes.Count);
        Assert.All(prefab.Attributes, attribute => Assert.Equal(string.Empty, attribute.Name));
        Assert.Equal("One", prefab.Attributes.Single(attribute =>
            attribute.NameHash == new StringHash("EnumValue")).Value.GetString());

        using var destination = new PrefabSerializable
        {
            NonPrefab = 80,
            EditOnly = 81,
            Defaulted = 82
        };
        prefab.Export(destination);

        Assert.Equal(7, destination.Saved);
        Assert.Equal(80, destination.NonPrefab);
        Assert.Equal(1, destination.EnumValue);
        Assert.Equal(9, destination.TemporaryOnly);
        Assert.Equal(10, destination.TemporaryReadOnly);
        Assert.Equal(81, destination.EditOnly);
        Assert.Equal(82, destination.Defaulted);
        Assert.True(destination.IsTemporary);

        using var keepStateDestination = new PrefabSerializable();
        prefab.Export(keepStateDestination, PrefabLoadFlag.KeepTemporaryState);
        Assert.False(keepStateDestination.IsTemporary);

        using var manual = new SerializablePrefab();
        manual.SetType(ObjectReflection<PrefabSerializable>.ClassName);
        manual.Attributes.Add(CreateAttribute("Saved", new Variant(55)));
        manual.Attributes.Add(CreateAttribute("Missing", new Variant(1)));
        manual.Attributes.Add(CreateAttribute("EditOnly", new Variant(56)));
        manual.Attributes.Add(CreateAttribute("TemporaryOnly", new Variant(57)));
        manual.Attributes.Add(CreateAttribute("EnumValue", new Variant("unknown")));
        manual.Attributes.Add(CreateAttribute((AttributeId)9, new Variant(58)));

        using var manualDestination = new PrefabSerializable();
        manual.Export(manualDestination);
        Assert.Equal(55, manualDestination.Saved);
        Assert.Equal(57, manualDestination.TemporaryOnly);
        Assert.Equal(0, manualDestination.EnumValue);
        Assert.Equal(0, manualDestination.EditOnly);

        using var mismatchByName = new SerializablePrefab();
        mismatchByName.SetType("DifferentType");
        mismatchByName.Export(manualDestination, PrefabLoadFlag.CheckSerializableType);
        using var mismatchByHash = new SerializablePrefab();
        mismatchByHash.SetType(new StringHash("DifferentHash"));
        mismatchByHash.Export(manualDestination, PrefabLoadFlag.CheckSerializableType);

        using var unreflected = new UnreflectedSerializable();
        using var unreflectedPrefab = new SerializablePrefab();
        unreflectedPrefab.Import(unreflected);
        unreflectedPrefab.Export(unreflected);
        Assert.Empty(unreflectedPrefab.Attributes);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("prefab"))
            prefab.SerializeInBlock(output, PrefabArchiveFlag.SerializeTemporary);
        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var loaded = new SerializablePrefab();
        using (input.OpenUnorderedBlock("prefab"))
            loaded.SerializeInBlock(input, PrefabArchiveFlag.SerializeTemporary);
        Assert.Equal(prefab, loaded);

        using var ignoredOutput = new TestJSONOutputArchive();
        const PrefabArchiveFlag ignoredFlags = PrefabArchiveFlag.IgnoreSerializableId
            | PrefabArchiveFlag.IgnoreSerializableType;
        using (ignoredOutput.OpenUnorderedBlock("prefab"))
            prefab.SerializeInBlock(ignoredOutput, ignoredFlags);
        using var ignoredInput = new TestJSONInputArchive(ignoredOutput.ToJsonString());
        using var ignored = new SerializablePrefab();
        ignored.SetId((SerializableId)99);
        ignored.SetType("OldType");
        using (ignoredInput.OpenUnorderedBlock("prefab"))
            ignored.SerializeInBlock(ignoredInput, ignoredFlags);
        Assert.Equal(SerializableId.None, ignored.Id);
        Assert.Equal(string.Empty, ignored.TypeName);
        Assert.Equal(StringHash.Empty, ignored.TypeNameHash);
        Assert.Equal(prefab.Attributes, ignored.Attributes);

        using var missingOutput = new TestJSONInputArchive("{\"prefab\":{\"_id\":1}}");
        using var missing = new SerializablePrefab();
        missing.Attributes.Add(CreateAttribute("Saved", new Variant(1)));
        using (missingOutput.OpenUnorderedBlock("prefab"))
            missing.SerializeInBlock(missingOutput);
        Assert.Empty(missing.Attributes);
    }

    [Fact]
    public void Node_prefab_roundtrips_hierarchy_clears_missing_lists_and_normalizes_ids()
    {
        using var source = CreateHierarchyPrefab();

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("prefab"))
            source.SerializeInBlock(output);
        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var loaded = new NodePrefab();
        using (input.OpenUnorderedBlock("prefab"))
            loaded.SerializeInBlock(input);
        Assert.Equal(source, loaded);

        using var binaryBuffer = new VectorBuffer();
        using (var binaryOutput = new BinaryOutputArchive(Context.Instance, binaryBuffer))
        {
            using (binaryOutput.OpenUnorderedBlock("prefab"))
                source.SerializeInBlock(binaryOutput, compactSave: true);
            binaryOutput.Flush();
        }
        binaryBuffer.Seek(0);
        using (var binaryInput = new BinaryInputArchive(Context.Instance, binaryBuffer))
        {
            using (binaryInput.OpenUnorderedBlock("prefab"))
                loaded.SerializeInBlock(binaryInput, compactSave: true);
            binaryInput.Flush();
        }
        Assert.Equal(source.Node.Id, loaded.Node.Id);
        Assert.Single(loaded.Components);
        Assert.Single(loaded.Children);
        Assert.Equal(string.Empty, loaded.Components[0].Attributes[0].Name);

        using var seeded = new NodePrefab();
        seeded.Components.Add(new SerializablePrefab());
        seeded.Children.Add(new NodePrefab());
        using var missing = new TestJSONInputArchive("{\"prefab\":{\"attributes\":[]}}");
        using (missing.OpenUnorderedBlock("prefab"))
            seeded.SerializeInBlock(missing);
        Assert.Empty(seeded.Components);
        Assert.Empty(seeded.Children);

        var child = source.Children[0];
        var grandchild = child.Children[0];
        Assert.Same(source, source.FindChild(string.Empty));
        Assert.Same(grandchild, source.FindChild("Child/Grandchild"));
        Assert.Same(child, source.FindChild("#0"));
        Assert.Same(child, source.FindChild("#bad"));
        Assert.Same(NodePrefab.Empty, source.FindChild("#-1"));
        Assert.Same(NodePrefab.Empty, source.FindChild("/Child"));
        Assert.Same(NodePrefab.Empty, source.FindChild("Missing"));
        Assert.Equal(string.Empty, new NodePrefab().GetNodeName());

        using var normalized = new NodePrefab();
        normalized.Node.SetId((SerializableId)50);
        var component = new SerializablePrefab();
        component.SetType(ObjectReflection<NormalizerProbe>.ClassName);
        component.SetId((SerializableId)70);
        component.Attributes.Add(CreateAttribute("NodeRef", new Variant(50u)));
        using (var nodeIds = new VariantVector { new Variant(50u), new Variant(99u) })
        using (var nodeIdsValue = new Variant(nodeIds))
            component.Attributes.Add(CreateAttribute("NodeRefs", nodeIdsValue));
        component.Attributes.Add(CreateAttribute("ComponentRef", new Variant(70u)));
        normalized.Components.Add(component);
        var normalizedChild = new NodePrefab();
        normalizedChild.Node.SetId((SerializableId)99);
        normalized.Children.Add(normalizedChild);
        var unreferencedChild = new NodePrefab();
        unreferencedChild.Node.SetId((SerializableId)123);
        normalized.Children.Add(unreferencedChild);

        Assert.Equal(AttributeScopeHint.Scene, normalized.GetEffectiveScopeHint());
        normalized.NormalizeIds();

        Assert.Equal((SerializableId)1, normalized.Node.Id);
        Assert.Equal((SerializableId)1, normalized.Components[0].Id);
        Assert.Equal((SerializableId)2, normalized.Children[0].Node.Id);
        Assert.Equal(SerializableId.None, normalized.Children[1].Node.Id);
        Assert.Equal(1u, normalized.Components[0].Attributes[0].Value.GetUInt());
        Assert.Equal([new Variant(1u), new Variant(2u)],
            normalized.Components[0].Attributes[1].Value.GetVariantVector());
        Assert.Equal(1u, normalized.Components[0].Attributes[2].Value.GetUInt());

        using var unknownScope = new SerializablePrefab();
        unknownScope.SetType("MissingReflection");
        Assert.Equal(AttributeScopeHint.Attribute, unknownScope.GetEffectiveScopeHint());
    }

    [Fact]
    public void Serializable_attribute_access_and_json_xml_error_paths_are_managed()
    {
        using var serializable = new PrefabSerializable();
        Assert.True(serializable.GetAttribute(-1).IsEmpty);
        Assert.True(serializable.GetAttribute(999).IsEmpty);
        Assert.True(serializable.GetAttribute("Missing").IsEmpty);
        Assert.True(serializable.GetAttributeDefault(-1).IsEmpty);
        Assert.True(serializable.GetAttributeDefault("Missing").IsEmpty);
        Assert.Throws<ArgumentException>(() => serializable.GetAttribute(" "));
        Assert.Throws<ArgumentException>(() => serializable.GetAttributeDefault(" "));
        Assert.Throws<ArgumentException>(() => serializable.SetAttribute(" ", new Variant(1)));
        Assert.Throws<ArgumentException>(() => serializable.SetInstanceDefault(" ", new Variant(1)));

        using var savedValue = new Variant(12);
        using var wrongValue = new Variant("wrong");
        Assert.False(serializable.SetAttribute(-1, savedValue));
        Assert.False(serializable.SetAttribute(999, savedValue));
        Assert.False(serializable.SetAttribute("Missing", savedValue));
        Assert.False(serializable.SetAttribute("Saved", wrongValue));
        Assert.True(serializable.SetAttribute("Saved", savedValue));
        Assert.Equal(12, serializable.Saved);

        serializable.SetInstanceDefault(true);
        using var instanceValue = new Variant(42);
        Assert.True(serializable.SetAttribute("Saved", instanceValue));
        using var instanceDefault = serializable.GetInstanceDefault("Saved");
        Assert.Equal(42, instanceDefault.GetInt());
        serializable.SetInstanceDefault(false);
        serializable.RemoveInstanceDefault();
        Assert.True(serializable.GetInstanceDefault("Saved").IsEmpty);

        using var source = new PrefabSerializable { Saved = 21, EnumValue = 1 };
        serializable.CopyAttributes(source);
        Assert.Equal(21, serializable.Saved);
        Assert.Equal(1, serializable.EnumValue);
        serializable.CopyAttributes(source, resetToDefault: false);
        Assert.Equal(21, serializable.Saved);

        using var other = new OtherSerializable { Saved = 31 };
        serializable.CopyAttributes(other);
        Assert.Equal(31, serializable.Saved);

        using var nullXml = new XMLElement();
        Assert.False(serializable.LoadXML(nullXml));
        using var xmlFile = new XMLFile { Name = string.Empty };
        var xmlRoot = xmlFile.CreateRoot("object");
        var unknownEnum = xmlRoot.CreateChild("attribute");
        unknownEnum.SetString("name", "EnumValue");
        unknownEnum.SetString("value", "unknown");
        var knownEnum = xmlRoot.CreateChild("attribute");
        knownEnum.SetString("name", "EnumValue");
        knownEnum.SetString("value", "One");
        var unknownAttribute = xmlRoot.CreateChild("attribute");
        unknownAttribute.SetString("name", "Missing");
        unknownAttribute.SetInt("value", 1);
        Assert.True(serializable.LoadXML(xmlRoot));
        Assert.Equal(1, serializable.EnumValue);

        using var savedXml = new XMLFile { Name = string.Empty };
        Assert.True(serializable.SaveXML(savedXml.CreateRoot("object")));
        var savedEnum = savedXml.GetRoot().GetChild("attribute");
        while (savedEnum.NotNull && savedEnum.GetAttribute("name") != "EnumValue")
            savedEnum = savedEnum.GetNext("attribute");
        Assert.True(savedEnum.NotNull);
        Assert.Equal("One", savedEnum.GetAttribute("value"));
        Assert.False(serializable.SaveXML(new XMLElement()));

        using var nullJson = new JSONValue();
        Assert.False(serializable.LoadJSON(nullJson));
        using var nonObjectJson = new JSONValue(JSONValueType.Object);
        nonObjectJson.Set("attributes", new JSONValue(42));
        Assert.True(serializable.LoadJSON(nonObjectJson));

        using var json = new JSONValue(JSONValueType.Object);
        using var attributes = new JSONValue(JSONValueType.Object);
        attributes.Set("Saved", new JSONValue(37));
        attributes.Set("EnumValue", new JSONValue("unknown"));
        attributes.Set("Missing", new JSONValue(1));
        json.Set("attributes", attributes);
        Assert.True(serializable.LoadJSON(json));
        Assert.Equal(37, serializable.Saved);
        Assert.Equal(1, serializable.EnumValue);

        using var savedJson = new JSONValue(JSONValueType.Object);
        Assert.True(serializable.SaveJSON(savedJson));
        Assert.Equal(37, savedJson.Get("attributes").Get("Saved").GetInt());
        Assert.Equal("One", savedJson.Get("attributes").Get("EnumValue").GetString());

        using var archiveOutput = new TestJSONOutputArchive();
        using (archiveOutput.OpenUnorderedBlock("object"))
            source.SerializeInBlock(archiveOutput, serializeTemporary: true);
        using var archiveInput = new TestJSONInputArchive(archiveOutput.ToJsonString());
        using var archiveResult = new PrefabSerializable();
        using (archiveInput.OpenUnorderedBlock("object"))
            archiveResult.SerializeInBlock(archiveInput, serializeTemporary: true);
        Assert.Equal(source.Saved, archiveResult.Saved);
        Assert.Equal(source.EnumValue, archiveResult.EnumValue);
        Assert.Equal(1, archiveResult.ApplyCount);
    }

    [Fact]
    public void Value_animation_covers_ordering_validity_interpolation_and_invalid_sources()
    {
        using var animation = new ValueAnimation { Name = "Animations/Coverage", Owner = this };
        Assert.Equal(MathDefs.Infinity, animation.BeginTime);
        Assert.Equal(-MathDefs.Infinity, animation.EndTime);
        Assert.True(animation.SetKeyFrame(2.0f, 20.0f));
        Assert.True(animation.SetKeyFrame(0.0f, 0.0f));
        Assert.True(animation.SetKeyFrame(1.0f, 10.0f));
        Assert.False(animation.SetKeyFrame(1.0f, 11.0f));
        Assert.False(animation.SetKeyFrame(3.0f, new Vector3f(1, 2, 3)));
        Assert.Equal([0.0f, 1.0f, 2.0f], animation.KeyFrames.Select(frame => frame.Time));
        Assert.Equal(0.0f, animation.BeginTime);
        Assert.Equal(2.0f, animation.EndTime);
        Assert.True(animation.IsValid());

        animation.InterpolationMethod = InterpMethod.None;
        animation.InterpolationMethod = InterpMethod.None;
        using (var noneValue = animation.GetAnimationValue(1.5f))
            Assert.Equal(10.0f, noneValue.GetFloat());

        animation.SetEventFrame(2.0f, new StringHash(2u));
        animation.SetEventFrame(0.5f, new StringHash(1u));
        animation.SetEventFrame(3.0f, new StringHash(3u));
        Assert.True(animation.HasEventFrames);
        var eventFrames = new List<VAnimEventFrame>();
        animation.GetEventFrames(0.5f, 2.0f, eventFrames);
        Assert.Equal([1u, 2u], eventFrames.Select(frame => frame.EventType.Value));

        animation.ValueType = VariantType.Vector3;
        Assert.Empty(animation.KeyFrames);
        Assert.False(animation.HasEventFrames);
        Assert.Equal(MathDefs.Infinity, animation.BeginTime);
        Assert.Equal(-MathDefs.Infinity, animation.EndTime);

        using var vectorAnimation = new ValueAnimation();
        Assert.True(vectorAnimation.SetKeyFrame(0.0f, new Vector2f(0, 0)));
        Assert.True(vectorAnimation.SetKeyFrame(1.0f, new Vector2f(10, 20)));
        using (var value = vectorAnimation.GetAnimationValue(0.5f))
            Assert.True(value.GetVector2().ApproximatelyEquivalent(new Vector2f(5, 10)));

        using var intAnimation = new ValueAnimation { ValueType = VariantType.Vector2i };
        intAnimation.InterpolationMethod = InterpMethod.Spline;
        Assert.Equal(InterpMethod.Linear, intAnimation.InterpolationMethod);
        Assert.False(intAnimation.IsValid());
        Assert.True(intAnimation.SetKeyFrame(0.0f, new Vector2i(0, 0)));
        Assert.True(intAnimation.SetKeyFrame(1.0f, new Vector2i(10, 20)));
        using (var value = intAnimation.GetAnimationValue(0.5f))
            Assert.Equal(new Vector2i(5, 10), value.GetIntVector2());

        using var spline = new ValueAnimation { InterpolationMethod = InterpMethod.Spline };
        Assert.True(spline.SetKeyFrame(0.0f, new Vector3f(0, 0, 0)));
        Assert.True(spline.SetKeyFrame(1.0f, new Vector3f(10, 0, 0)));
        Assert.True(spline.SetKeyFrame(2.0f, new Vector3f(0, 0, 0)));
        Assert.True(spline.IsValid());
        using (var value = spline.GetAnimationValue(0.5f))
            Assert.Equal(VariantType.Vector3, value.Type);

        using var nonInterpolatable = new ValueAnimation { ValueType = VariantType.String };
        Assert.True(nonInterpolatable.SetKeyFrame(0.0f, "first"));
        Assert.True(nonInterpolatable.SetKeyFrame(1.0f, "last"));
        using (var value = nonInterpolatable.GetAnimationValue(0.5f))
            Assert.Equal("first", value.GetString());

        using var invalidXml = new XMLElement();
        Assert.True(animation.LoadXML(invalidXml));
        using var invalidJson = new JSONValue();
        Assert.True(animation.LoadJSON(invalidJson));

        using var json = new JSONValue(JSONValueType.Object);
        json.Set("interpolationmethod", new JSONValue("not-a-method"));
        json.Set("keyframes", new JSONValue(JSONValueType.Array));
        json.Set("eventframes", new JSONValue(JSONValueType.Array));
        Assert.True(animation.LoadJSON(json));
        Assert.Equal(InterpMethod.Linear, animation.InterpolationMethod);

        var keyFrame = new VAnimKeyFrame { Time = 1.0f, Value = new Variant(new byte[] { 1, 2 }) };
        var keyFrameClone = keyFrame.Clone();
        keyFrame.Dispose();
        Assert.Equal(new byte[] { 1, 2 }, keyFrameClone.Value.GetBuffer());
        keyFrameClone.Dispose();

        using var eventData = new VariantMap { [new StringHash("value")] = new Variant(4) };
        var eventFrame = new VAnimEventFrame { Time = 1.0f, EventType = new StringHash(9u), EventData = eventData };
        var eventFrameClone = eventFrame.Clone();
        using (var changedValue = new Variant(8))
            eventFrame.EventData[new StringHash("value")] = changedValue;
        Assert.Equal(4, eventFrameClone.EventData[new StringHash("value")].GetInt());
        eventFrame.Dispose();
        eventFrameClone.Dispose();
    }

    [Fact]
    public void Prefab_reference_attribute_and_error_exits_do_not_require_files()
    {
        using var reference = new PrefabReference();
        Assert.Null(reference.GetPrefab());
        Assert.Equal(string.Empty, reference.GetPath());

        reference.SetPrefabAttr(ResourceRef.Empty);
        reference.ApplyAttributes();
        Assert.Equal(ResourceRef.Empty, reference.GetPrefabAttr());

        var missing = new ResourceRef(nameof(PrefabResource), "Missing.prefab");
        reference.SetPrefabAttr(missing);
        reference.ApplyAttributes();
        Assert.Equal(missing, reference.GetPrefabAttr());
        Assert.Null(reference.GetPrefab());

        reference.SetPath("Slice/Child");
        reference.SetPath("Slice/Child");
        Assert.Equal("Slice/Child", reference.GetPath());
        reference.CommitChanges();
        reference.SetPath(string.Empty);
        reference.CommitChanges();
        reference.Inline(PrefabInlineFlag.None);
        reference.InlineConservative();
        reference.InlineAggressive();
        reference.SetEnabled(false);
        reference.SetEnabled(true);
    }

    [Fact]
    public void Prefab_reference_updates_root_attributes_and_inline_modes_in_memory()
    {
        using var scene = new Scene();
        using var resource = new PrefabResource { Name = "Memory.prefab" };
        var prefabNode = resource.GetMutableNodePrefab();
        using (var sourceNode = new Node())
        {
            sourceNode.SetName("PrefabRoot");
            sourceNode.AddTag("PrefabTag");
            sourceNode.SetPosition(new Vector3f(1, 2, 3));
            sourceNode.SetRotation(new Quaternion(15, Vector3f.Up));
            sourceNode.SetScale(new Vector3f(2, 3, 4));
            using var writer = new PrefabWriterToMemory(prefabNode, PrefabSaveFlag.SaveDefaultValues);
            Assert.True(sourceNode.Save(writer));
        }
        prefabNode.Children.Add(new NodePrefab());

        var host = scene.CreateChild("Host");
        var reference = host.CreateComponent<PrefabReference>();
        reference.SetPrefab(resource, instanceFlags: PrefabInstanceFlag.UpdateAll);
        Assert.Equal("PrefabRoot", host.Name);
        Assert.Equal(["PrefabTag"], host.Tags);
        Assert.True(host.Position.ApproximatelyEquivalent(new Vector3f(1, 2, 3)));
        Assert.True(host.Scale.ApproximatelyEquivalent(new Vector3f(2, 3, 4)));
        Assert.Single(host.Children);
        Assert.True(host.Children[0].IsTemporary);

        var extra = host.CreateTemporaryChild("Extra");
        reference.InlineConservative();
        Assert.Null(host.GetComponent<PrefabReference>());
        Assert.False(host.Children[0].IsTemporary);
        Assert.True(extra.IsTemporary);

        var aggressiveHost = scene.CreateChild("Aggressive");
        var aggressiveReference = aggressiveHost.CreateComponent<PrefabReference>();
        aggressiveReference.SetPrefab(resource);
        aggressiveHost.CreateTemporaryChild("Extra");
        aggressiveReference.InlineAggressive();
        Assert.All(aggressiveHost.Children, child => Assert.False(child.IsTemporary));

        var warningHost = scene.CreateChild("TemporaryReference");
        var warningReference = warningHost.CreateComponent<PrefabReference>();
        warningReference.SetPrefab(resource);
        warningReference.SetTemporary(true);
        warningReference.SetEnabled(false);
        Assert.False(warningReference.IsTemporary);
    }

    private static NodePrefab CreateHierarchyPrefab()
    {
        var root = new NodePrefab();
        root.Node.SetId((SerializableId)7);
        root.Node.Attributes.Add(CreateAttribute("Name", new Variant("Root")));

        var component = new SerializablePrefab();
        component.SetType("CoverageComponent");
        component.SetId((SerializableId)8);
        component.Attributes.Add(CreateAttribute("Value", new Variant(12)));
        root.Components.Add(component);

        var child = new NodePrefab();
        child.Node.SetId((SerializableId)9);
        child.Node.Attributes.Add(CreateAttribute(new StringHash("Name"), new Variant("Child")));
        var grandchild = new NodePrefab();
        grandchild.Node.Attributes.Add(CreateAttribute("Name", new Variant("Grandchild")));
        child.Children.Add(grandchild);
        root.Children.Add(child);
        return root;
    }

    private static AttributePrefab CreateAttribute(string name, Variant value)
    {
        var result = new AttributePrefab(name);
        result.SetValue(value);
        return result;
    }

    private static AttributePrefab CreateAttribute(StringHash nameHash, Variant value)
    {
        var result = new AttributePrefab(nameHash);
        result.SetValue(value);
        return result;
    }

    private static AttributePrefab CreateAttribute(AttributeId id, Variant value)
    {
        var result = new AttributePrefab(id);
        result.SetValue(value);
        return result;
    }

    [Reflected]
    private sealed partial class PrefabSerializable : Serializable
    {
        [Serialized("Saved")]
        public int Saved { get; set; }

        [Serialized("Defaulted")]
        public int Defaulted { get; set; }

        [Serialized("NonPrefab", Mode = AttributeMode.File)]
        public int NonPrefab { get; set; }

        [Serialized("EnumValue", EnumNames = ["Zero", "One"])]
        public int EnumValue { get; set; }

        [Serialized("TemporaryOnly", Mode = AttributeMode.Prefab | AttributeMode.Temporary)]
        public int TemporaryOnly { get; set; }

        [Serialized("TemporaryReadOnly", Mode = AttributeMode.File | AttributeMode.Prefab
            | AttributeMode.Temporary | AttributeMode.ReadOnly)]
        public int TemporaryReadOnly { get; set; }

        [Serialized("EditOnly", Mode = AttributeMode.Edit)]
        public int EditOnly { get; set; }

        public int ApplyCount { get; private set; }

        public override void ApplyAttributes()
        {
            ++ApplyCount;
        }
    }

    [Reflected]
    private sealed partial class OtherSerializable : Serializable
    {
        [Serialized("Saved")]
        public int Saved { get; set; }
    }

    [Reflected]
    private sealed partial class NormalizerProbe : Component
    {
        [Serialized("NodeRef", Mode = AttributeMode.NodeId, ScopeHint = AttributeScopeHint.Scene)]
        private uint _nodeRef;

        [Serialized("NodeRefs", Mode = AttributeMode.NodeIdVector)]
        private VariantVector _nodeRefs = [];

        [Serialized("ComponentRef", Mode = AttributeMode.ComponentId)]
        private uint _componentRef;

        public uint NodeRef => _nodeRef;

        public VariantVector NodeRefs => _nodeRefs;

        public uint ComponentRef => _componentRef;

        public override void Dispose()
        {
            _nodeRefs.Dispose();
            base.Dispose();
        }
    }

    private sealed class UnreflectedSerializable : Serializable
    {
    }
}
