// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Collections;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneHierarchyWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneHierarchyWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Widget_renders_searches_selects_and_dispatches_pending_hierarchy_changes()
    {
        _fixture.ResetGraphics();
        var imguiContext = ImGui.CreateContext();
        SceneHierarchyWidget? widget = null;
        var frameStarted = false;
        try
        {
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            widget = new SceneHierarchyWidget();
            var defaultSettings = widget.GetSettings();
            Assert.False(defaultSettings.ShowTemporary);
            Assert.True(defaultSettings.ShowComponents);
            Assert.Equal(string.Empty, defaultSettings.FilterByName);

            using var scene = new Scene();
            var first = scene.CreateChild("Alpha");
            var middle = scene.CreateChild("Beta");
            var last = scene.CreateChild("Parent");
            var nested = last.CreateChild("Beetle");
            var temporary = scene.CreateChild("Temporary");
            temporary.SetTemporary(true);
            var firstCamera = first.CreateComponent<Camera>();
            var firstLight = first.CreateComponent<Light>();
            var selection = new SceneSelection();

            var settings = new SceneHierarchySettings
            {
                ShowTemporary = false,
                ShowComponents = true,
                FilterByName = "be",
            };
            widget.SetSettings(settings);

            ImGui.NewFrame();
            frameStarted = true;
            var isOpen = true;
            ImGui.Begin("Scene hierarchy widget test"u8, ref isOpen);
            widget.RenderContent(scene, selection);
            ImGui.End();
            ImGui.EndFrame();
            frameStarted = false;

            AssertSettings(settings, widget.GetSettings());
            Assert.Equal(2, GetSearchResultCount(widget));

            settings.FilterByName = "BEE";
            widget.SetSettings(settings);
            SetSearchQuery(widget, settings.FilterByName);
            Invoke(widget, "UpdateSearchResults", scene);
            Assert.Equal(1, GetSearchResultCount(widget));

            settings.FilterByName = string.Empty;
            widget.SetSettings(settings);
            SetSearchQuery(widget, settings.FilterByName);
            Invoke(widget, "UpdateSearchResults", scene);
            Assert.Equal(0, GetSearchResultCount(widget));

            Invoke(widget, "ProcessObjectSelected", selection, first, false, false);
            Assert.True(selection.IsSelected(first));
            Invoke(widget, "ProcessObjectSelected", selection, middle, false, false);
            Assert.False(selection.IsSelected(first));
            Assert.True(selection.IsSelected(middle));
            Invoke(widget, "ProcessObjectSelected", selection, middle, true, false);
            Assert.True(selection.IsEmpty);

            selection.SetSelected(first, true);
            SetField(widget, "_wasActiveObjectVisible", true);
            Invoke(widget, "ProcessObjectSelected", selection, nested, false, true);
            Invoke(widget, "BeginRangeSelection");
            Invoke(widget, "ProcessRangeSelection", scene, false);
            Invoke(widget, "ProcessRangeSelection", first, false);
            Invoke(widget, "ProcessRangeSelection", firstCamera, false);
            Invoke(widget, "ProcessRangeSelection", firstLight, false);
            Invoke(widget, "ProcessRangeSelection", middle, false);
            Invoke(widget, "ProcessRangeSelection", last, true);
            Invoke(widget, "ProcessRangeSelection", nested, false);
            Invoke(widget, "EndRangeSelection", selection);
            Assert.True(selection.IsSelected(first));
            Assert.True(selection.IsSelected(firstCamera));
            Assert.True(selection.IsSelected(firstLight));
            Assert.True(selection.IsSelected(middle));
            Assert.True(selection.IsSelected(last));
            Assert.True(selection.IsSelected(nested));

            using (var payload = CreatePayload(scene, middle))
                Assert.True(CanBeDroppedTo(widget, last, payload));
            using (var payload = CreatePayload(scene, last))
                Assert.False(CanBeDroppedTo(widget, nested, payload));
            using (var payload = CreatePayload(scene, scene))
                Assert.False(CanBeDroppedTo(widget, last, payload));
            using (var otherScene = new Scene())
            using (var payload = CreatePayload(otherScene, otherScene.CreateChild("Other")))
                Assert.False(CanBeDroppedTo(widget, last, payload));
            using (var payload = new NodeComponentDragDropPayload
                   { Scene = new WeakReference<Scene>(scene) })
            {
                Assert.False(CanBeDroppedTo(widget, last, payload));
            }

            (Node Node, int OldIndex, int NewIndex)? nodeReorder = null;
            (Component Component, int OldIndex, int NewIndex)? componentReorder = null;
            (Node Parent, Node Child)? reparent = null;
            widget.OnNodeReordered += (node, oldIndex, newIndex) =>
                nodeReorder = (node, oldIndex, newIndex);
            widget.OnComponentReordered += (component, oldIndex, newIndex) =>
                componentReorder = (component, oldIndex, newIndex);
            widget.OnNodeReparented += (parent, child) => reparent = (parent, child);

            SetPendingReorder(widget, "_pendingNodeReorder", middle.Id, 1, 0);
            SetPendingReorder(widget, "_pendingComponentReorder", firstLight.Id, 1, 0);
            AddPendingReparent(widget, last.Id, middle.Id);
            Invoke(widget, "ApplyPendingUpdates", scene);

            Assert.Equal((middle, 1, 0), nodeReorder);
            Assert.Equal((firstLight, 1, 0), componentReorder);
            Assert.Equal((last, middle), reparent);

            widget.Dispose();
            widget = null;
        }
        finally
        {
            if (frameStarted)
                ImGui.EndFrame();
            widget?.Dispose();
            ImGui.DestroyContext(imguiContext);
        }
    }

    private static NodeComponentDragDropPayload CreatePayload(Scene scene, Node node)
    {
        var payload = new NodeComponentDragDropPayload
        {
            Scene = new WeakReference<Scene>(scene),
        };
        payload.Nodes.Add(new WeakReference<Node>(node));
        return payload;
    }

    private static bool CanBeDroppedTo(
        SceneHierarchyWidget widget, Node parent, NodeComponentDragDropPayload payload)
    {
        return Assert.IsType<bool>(Invoke(widget, "CanBeDroppedTo", parent, payload));
    }

    private static int GetSearchResultCount(SceneHierarchyWidget widget)
    {
        var search = GetField(widget, "_search");
        var results = Assert.IsAssignableFrom<ICollection>(
            search.GetType().GetField("LastResults")!.GetValue(search));
        return results.Count;
    }

    private static void SetSearchQuery(SceneHierarchyWidget widget, string query)
    {
        var search = GetField(widget, "_search");
        search.GetType().GetField("CurrentQuery")!.SetValue(search, query);
    }

    private static void SetPendingReorder(
        SceneHierarchyWidget widget, string fieldName, uint id, int oldIndex, int newIndex)
    {
        var field = typeof(SceneHierarchyWidget).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var reorderType = Nullable.GetUnderlyingType(field.FieldType)!;
        var reorder = Activator.CreateInstance(reorderType)!;
        reorderType.GetField("Id")!.SetValue(reorder, id);
        reorderType.GetField("OldIndex")!.SetValue(reorder, oldIndex);
        reorderType.GetField("NewIndex")!.SetValue(reorder, newIndex);
        field.SetValue(widget, reorder);
    }

    private static void AddPendingReparent(
        SceneHierarchyWidget widget, uint parentId, uint childId)
    {
        var list = Assert.IsAssignableFrom<IList>(GetField(widget, "_pendingNodeReparents"));
        var reparentType = list.GetType().GetGenericArguments()[0];
        var reparent = Activator.CreateInstance(reparentType)!;
        reparentType.GetField("ParentId")!.SetValue(reparent, parentId);
        reparentType.GetField("ChildId")!.SetValue(reparent, childId);
        list.Add(reparent);
    }

    private static object? Invoke(
        SceneHierarchyWidget widget, string methodName, params object?[] parameters)
    {
        return typeof(SceneHierarchyWidget).GetMethod(
                methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(widget, parameters);
    }

    private static object GetField(SceneHierarchyWidget widget, string name)
    {
        return typeof(SceneHierarchyWidget).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(widget)!;
    }

    private static void SetField<T>(SceneHierarchyWidget widget, string name, T value)
    {
        typeof(SceneHierarchyWidget).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(widget, value);
    }

    private static void AssertSettings(
        SceneHierarchySettings expected, SceneHierarchySettings actual)
    {
        Assert.Equal(expected.ShowTemporary, actual.ShowTemporary);
        Assert.Equal(expected.ShowComponents, actual.ShowComponents);
        Assert.Equal(expected.FilterByName, actual.FilterByName);
    }
}
