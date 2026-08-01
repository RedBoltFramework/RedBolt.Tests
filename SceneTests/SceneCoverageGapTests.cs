namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneCoverageGapTests
{
    [Fact]
    public void Hierarchy_lookups_cover_direct_recursive_indexed_and_lazy_paths()
    {
        using var scene = new Scene();
        var root = scene.CreateChild("Root");
        var first = root.CreateChild("First");
        var second = root.CreateChild("Second");
        var nested = first.CreateChild("Nested");
        var target = second.CreateChild("Target");

        first.AddTag("marked");
        target.AddTag("marked");
        first.AddComponent(new CoverageProbe());
        target.AddComponent(new CoverageProbe());

        Assert.Same(first, root.GetChild(0));
        Assert.Same(second, root.GetChild(new StringHash("Second")));
        Assert.Null(root.GetChild(-1));
        Assert.Null(root.GetChild(99));
        Assert.Null(root.GetChild("Nested"));
        Assert.Same(nested, root.GetChild("Nested", recursive: true));
        Assert.Null(root.GetChild("Missing", recursive: true));

        Assert.Equal([first, second], root.GetChildren());
        Assert.Equal([first, nested, second, target], root.GetChildren(recursive: true));
        var children = new List<Node> { scene };
        root.GetChildren(children);
        Assert.Equal([first, second], children);

        Assert.Equal([first], root.GetChildrenWithTag("marked"));
        Assert.Equal([first, target], root.GetChildrenWithTag("marked", recursive: true));
        children.Add(scene);
        root.GetChildrenWithTag(children, "marked", recursive: true);
        Assert.Equal([first, target], children);

        Assert.Equal([first], root.GetChildrenWithComponent<CoverageProbe>());
        Assert.Equal([first, target], root.GetChildrenWithComponent<CoverageProbe>(recursive: true));
        children.Add(scene);
        root.GetChildrenWithComponent(children, typeof(CoverageProbe), recursive: true);
        Assert.Equal([first, target], children);

        Assert.Null(root.FindChild(string.Empty));
        Assert.Null(root.FindChild("Nested"));
        Assert.Same(nested, root.FindChild("First/Nested"));
        Assert.Same(first, root.FindChild("#0"));
        Assert.Same(nested, root.FindChild("#0/Nested"));
        Assert.Same(nested, root.FindChild("Nested", firstRecursive: true));
        Assert.Same(nested, root.FindChild("**/Nested"));
        Assert.Null(root.FindChild("Missing/Child"));

        Node? cached = first;
        Assert.True(root.GetChildLazy(ref cached, first.NameHash));
        Assert.Same(first, cached);

        cached = first;
        Assert.True(root.GetChildLazy(
            ref cached, new StringHash("Second"), SceneLookupFlag.ValidateName));
        Assert.Same(second, cached);

        using var otherScene = new Scene();
        cached = otherScene.CreateChild("First");
        Assert.True(root.GetChildLazy(
            ref cached, first.NameHash, SceneLookupFlag.ValidateRelation));
        Assert.Same(first, cached);

        cached = null;
        Assert.True(root.GetChildLazy(
            ref cached, nested.NameHash, SceneLookupFlag.Recursive));
        Assert.Same(nested, cached);

        CoverageProbe? cachedComponent = null;
        Assert.True(first.GetNthComponentLazy(ref cachedComponent));
        Assert.NotNull(cachedComponent);
        Assert.True(first.GetNthComponentLazy(ref cachedComponent));
    }

    [Fact]
    public void Relationships_tags_and_enabled_state_cover_empty_and_recursive_edges()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");
        var grandchild = child.CreateChild("Grandchild");
        var sibling = scene.CreateChild("Sibling");
        var component = new CoverageProbe();
        parent.AddComponent(component);

        Assert.Equal([parent, sibling], Node.GetParentNodes([parent, child, sibling]));
        Assert.Equal([child], Node.GetParentNodes([child]));
        Assert.Same(child, parent.GetDirectChildFor(grandchild));
        Assert.Null(parent.GetDirectChildFor(sibling));
        Assert.True(grandchild.IsChildOf(parent));
        Assert.False(parent.IsChildOf(null));
        Assert.True(parent.IsTransformHierarchyRoot());
        Assert.False(grandchild.IsTransformHierarchyRoot());
        Assert.False(grandchild.IsTemporaryEffective());

        var temporary = scene.CreateChild("Temporary", temporary: true);
        var temporaryChild = temporary.CreateChild("TemporaryChild");
        Assert.True(temporaryChild.IsTemporaryEffective());

        parent.AddTags("one;two;one;;");
        parent.AddTag(string.Empty);
        Assert.Equal(["one", "two"], parent.Tags);
        Assert.Contains(parent, scene.GetNodesWithTag("one"));
        Assert.Empty(scene.GetNodesWithTag("missing"));
        Assert.False(parent.RemoveTag("missing"));
        Assert.True(parent.RemoveTag("one"));
        parent.SetTags(["three", "four"]);
        Assert.Equal(["three", "four"], parent.Tags);
        parent.RemoveAllTags();
        Assert.Empty(parent.Tags);

        scene.SetEnabled(false);
        Assert.True(scene.IsEnabled);

        parent.SetEnabled(false);
        Assert.False(parent.IsEnabled);
        Assert.False(parent.IsEnabledSelf);
        Assert.True(child.IsEnabled);
        Assert.False(component.IsEnabledEffective);

        parent.SetEnabled(true);
        child.SetEnabled(false);
        parent.SetDeepEnabled(false);
        Assert.False(parent.IsEnabled);
        Assert.False(child.IsEnabled);
        parent.ResetDeepEnabled();
        Assert.True(parent.IsEnabled);
        Assert.False(child.IsEnabled);

        parent.SetEnabledRecursive(true);
        Assert.True(parent.IsEnabled);
        Assert.True(child.IsEnabled);
        Assert.True(grandchild.IsEnabled);

        var standalone = new Node();
        standalone.SetParent(scene);
        Assert.Same(scene, standalone.Parent);
        standalone.SetParent(null);
        Assert.Same(scene, standalone.Parent);
    }

    [Fact]
    public void Component_search_covers_exact_derived_parent_children_and_enabled_filters()
    {
        using var scene = new Scene();
        var root = scene.CreateChild("Root");
        var parent = root.CreateChild("Parent");
        var current = parent.CreateChild("Current");
        var child = current.CreateChild("Child");
        var grandchild = child.CreateChild("Grandchild");

        var rootProbe = new CoverageProbe();
        root.AddComponent(rootProbe);
        var parentDerived = new DerivedCoverageProbe();
        parent.AddComponent(parentDerived);
        var currentProbe = new CoverageProbe();
        current.AddComponent(currentProbe);
        var childDerived = new DerivedCoverageProbe();
        child.AddComponent(childDerived);
        var grandchildProbe = new CoverageProbe();
        grandchild.AddComponent(grandchildProbe);

        Assert.Null(parent.FindComponent<CoverageProbe>(ComponentSearchFlag.Self));
        Assert.Same(parentDerived, parent.FindComponent<CoverageProbe>(
            ComponentSearchFlag.Self | ComponentSearchFlag.Derived));
        Assert.Same(parentDerived, parent.FindComponent(
            typeof(CoverageProbe), ComponentSearchFlag.Self | ComponentSearchFlag.Derived));

        parentDerived.SetEnabled(false);
        Assert.Null(parent.FindComponent<CoverageProbe>(
            ComponentSearchFlag.Self | ComponentSearchFlag.Derived | ComponentSearchFlag.EnabledOnly));
        Assert.Empty(parent.FindComponents<CoverageProbe>(
            ComponentSearchFlag.Self | ComponentSearchFlag.Derived | ComponentSearchFlag.EnabledOnly));
        parentDerived.SetEnabled(true);

        current.SetEnabled(false);
        Assert.Same(currentProbe, current.FindComponent<CoverageProbe>(ComponentSearchFlag.Self));
        Assert.Null(current.FindComponent<CoverageProbe>(
            ComponentSearchFlag.Self | ComponentSearchFlag.EnabledOnly));
        current.SetEnabled(true);

        Assert.Equal([parent], GetNodes(current.FindComponents<CoverageProbe>(
            ComponentSearchFlag.Parent | ComponentSearchFlag.Derived)));
        Assert.Equal([parent, root], GetNodes(current.FindComponents<CoverageProbe>(
            ComponentSearchFlag.ParentRecursive | ComponentSearchFlag.Derived)));
        Assert.Equal([child], GetNodes(current.FindComponents<CoverageProbe>(
            ComponentSearchFlag.Children | ComponentSearchFlag.Derived)));
        Assert.Equal([child, grandchild], GetNodes(current.FindComponents<CoverageProbe>(
            ComponentSearchFlag.ChildrenRecursive | ComponentSearchFlag.Derived)));
        Assert.Equal([parent, root], GetNodes(current.FindComponents(
            typeof(CoverageProbe), ComponentSearchFlag.ParentRecursive | ComponentSearchFlag.Derived)));

        Assert.Same(parentDerived, current.FindComponent<CoverageProbe>(
            ComponentSearchFlag.Parent | ComponentSearchFlag.Derived));
        Assert.Same(childDerived, current.FindComponent<CoverageProbe>(
            ComponentSearchFlag.Children | ComponentSearchFlag.Derived));
        Assert.Same(parentDerived, current.FindComponent(
            typeof(CoverageProbe), ComponentSearchFlag.Parent | ComponentSearchFlag.Derived));
    }

    [Fact]
    public void Scene_and_component_relationship_maps_cover_missing_and_detached_values()
    {
        using var scene = new Scene();
        Assert.True(scene.IsEmpty());
        Assert.True(scene.IsEmpty(ignoreComponents: true));
        Assert.Null(scene.GetNode(uint.MaxValue));
        Assert.Null(scene.GetComponent(uint.MaxValue));
        Assert.Empty(scene.GetNodesWithTag("missing"));

        var parent = scene.CreateChild("Parent");
        var parentComponent = new CoverageProbe();
        parent.AddComponent(parentComponent);
        var child = parent.CreateChild("Child");
        var component = new CoverageProbe();
        child.AddComponent(component);

        Assert.False(scene.IsEmpty());
        Assert.Same(parent, scene.GetNode(parent.Id));
        Assert.Same(component, scene.GetComponent(component.Id));
        Assert.Contains(component, scene.GetAllComponents().Values);
        Assert.Contains(child, scene.GetAllNodes().Values);

        var allComponents = new List<CoverageProbe> { new() };
        scene.GetComponents(allComponents);
        Assert.Equal([parentComponent, component], allComponents);
        Assert.Throws<ArgumentNullException>(() => scene.GetComponents<CoverageProbe>(null!));

        Assert.Same(parentComponent, child.GetParentComponent<CoverageProbe>());
        Assert.Same(parentComponent, child.GetParentComponent(typeof(CoverageProbe)));
        Assert.Same(child, component.TryGetNode());
        Assert.Same(scene, component.Scene);
        Assert.Same(component, component.GetComponent<CoverageProbe>());
        Assert.Same(component, component.GetComponent(typeof(CoverageProbe)));
        Assert.Equal(0, component.GetIndexInParent());
        Assert.Contains("CoverageProbe", component.GetFullNameDebug());

        component.SetEnabled(false);
        Assert.False(component.Enabled);
        Assert.False(component.IsEnabledEffective);
        component.SetEnabled(true);
        Assert.True(component.IsEnabledEffective);

        child.RemoveComponent(component);
        Assert.Null(component.TryGetNode());
        Assert.Null(component.Scene);
        Assert.False(component.IsEnabledEffective);
        Assert.Equal(int.MaxValue, component.GetIndexInParent());
        component.Remove();
    }

    [Fact]
    public void Node_serialization_skips_temporary_values_and_accepts_missing_sections()
    {
        using var source = new Node();
        source.SetName("Root");
        source.AddComponent(new Component());
        var temporaryComponent = new Component();
        source.AddComponent(temporaryComponent);
        temporaryComponent.SetTemporary(true);
        source.CreateChild("Persistent");
        source.CreateTemporaryChild("Temporary");

        using var json = new JSONValue(JSONValueType.Object);
        Assert.True(source.SaveJSON(json));
        Assert.Equal(1, json.Get("components").GetArray().Count);
        Assert.Equal(1, json.Get("children").GetArray().Count);

        using var missingJson = new JSONValue(JSONValueType.Object);
        var attributes = new JSONValue(42);
        missingJson.Set("attributes", attributes);
        using var loaded = new Node();
        Assert.True(loaded.LoadJSON(missingJson));
        Assert.Empty(loaded.Children);
        Assert.Empty(loaded.Components);

        using var xmlFile = new XMLFile { Name = string.Empty };
        var root = xmlFile.CreateRoot("node");
        root.SetUInt("id", 7);
        Assert.True(loaded.LoadXML(root));
        Assert.Empty(loaded.Children);
        Assert.Empty(loaded.Components);

        using var savedXml = new XMLFile { Name = string.Empty };
        Assert.True(source.SaveXML(savedXml.CreateRoot("node")));
        Assert.NotNull(savedXml.GetRoot().GetChild("component"));
        Assert.NotNull(savedXml.GetRoot().GetChild("node"));
    }

    [Fact]
    public void Prefab_empty_and_missing_paths_cover_find_copy_and_archive_sections()
    {
        using var prefab = new NodePrefab();
        AddName(prefab.Node, "Root");
        var child = new NodePrefab();
        AddName(child.Node, "Child");
        prefab.Children.Add(child);

        Assert.Equal("Root", prefab.GetNodeName());
        Assert.Same(prefab, prefab.FindChild(string.Empty));
        Assert.Same(child, prefab.FindChild("Child"));
        Assert.Same(child, prefab.FindChild("#0"));
        Assert.Same(child, prefab.FindChild("#bad"));
        Assert.Same(NodePrefab.Empty, prefab.FindChild("#-1"));
        Assert.Same(NodePrefab.Empty, prefab.FindChild("#9"));
        Assert.Same(NodePrefab.Empty, prefab.FindChild("/Child"));
        Assert.Same(NodePrefab.Empty, prefab.FindChild("Missing"));

        var copy = new NodePrefab();
        copy.CopyFrom(prefab);
        Assert.Equal("Root", copy.GetNodeName());
        Assert.Single(copy.Children);
        copy.CopyFrom(copy);
        copy.Clear();
        Assert.True(copy.IsEmpty());
        Assert.Equal(AttributeScopeHint.Attribute, copy.GetEffectiveScopeHint());
        Assert.NotEqual(prefab, copy);
        copy.Dispose();

        using var emptyOutput = new TestJSONOutputArchive();
        using (emptyOutput.OpenUnorderedBlock("prefab"))
            NodePrefab.Empty.SerializeInBlock(emptyOutput);

        using var loaded = new NodePrefab();
        loaded.Components.Add(new SerializablePrefab());
        loaded.Children.Add(new NodePrefab());
        using var emptyInput = new TestJSONInputArchive(emptyOutput.ToJsonString());
        using (emptyInput.OpenUnorderedBlock("prefab"))
            loaded.SerializeInBlock(emptyInput);
        Assert.Empty(loaded.Components);
        Assert.Empty(loaded.Children);
    }

    [Fact]
    public void Standalone_component_paths_cover_serialization_and_no_scene_behavior()
    {
        using var component = new Component();
        Assert.Null(component.TryGetNode());
        Assert.Null(component.Scene);
        Assert.False(component.IsEnabledEffective);
        Assert.Equal(int.MaxValue, component.GetIndexInParent());
        Assert.Equal(component.GetTypeName(), component.GetFullNameDebug());
        Assert.Null(component.GetComponent(typeof(Component)));
        Assert.Null(component.GetComponent<Component>());
        component.SetEnabled(false);
        Assert.False(component.Enabled);
        component.SetEnabled(false);
        component.SetEnabled(true);

        component.GetDependencyNodes([]);
        component.DrawDebugGeometry(null, depthTest: false);
        Assert.False(component.HasAuxiliaryData());
        Assert.Equal(AttributeScopeHint.Attribute, component.GetEffectiveScopeHint());

        using var binary = new VectorBuffer();
        Assert.True(component.Save(binary));
        using var json = new JSONValue(JSONValueType.Object);
        Assert.True(component.SaveJSON(json));
        using var xmlFile = new XMLFile { Name = string.Empty };
        Assert.True(component.SaveXML(xmlFile.CreateRoot("component")));
    }

    private static IReadOnlyList<Node> GetNodes(IReadOnlyList<CoverageProbe> components)
    {
        return components.Select(static component => component.Node).ToArray();
    }

    private static IReadOnlyList<Node> GetNodes(IReadOnlyList<Component> components)
    {
        return components.Select(static component => component.Node).ToArray();
    }

    private static void AddName(SerializablePrefab prefab, string name)
    {
        var attribute = new AttributePrefab(new StringHash("Name"));
        using var value = new Variant(name);
        attribute.SetValue(value);
        prefab.Attributes.Add(attribute);
    }

    private class CoverageProbe : Component
    {
    }

    private sealed class DerivedCoverageProbe : CoverageProbe
    {
    }
}
