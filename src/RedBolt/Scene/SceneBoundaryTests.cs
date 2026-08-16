// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneBoundaryTests
{
    [Fact]
    public void Scene_flags_and_update_event_type_keep_the_native_contract()
    {
        Assert.Equal(0, (int)SceneLookupFlag.None);
        Assert.Equal(1, (int)SceneLookupFlag.Recursive);
        Assert.Equal(4, (int)SceneLookupFlag.ValidateName);
        Assert.Equal(8, (int)SceneLookupFlag.ValidateRelation);
        Assert.Equal(ComponentSearchFlag.SelfOrChildrenRecursive, ComponentSearchFlag.Default);
        Assert.Equal(typeof(SceneEvents.SceneUpdateEvent),
            SceneUpdateEventType.Get<SceneEvents.SceneUpdateEvent>().Type);
        Assert.Equal(UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate,
            UpdateEvent.UseUpdate | UpdateEvent.UsePostUpdate);
    }

    [Fact]
    public void Hierarchy_and_component_lifecycle_events_carry_objects_and_order()
    {
        using var scene = new Scene();
        using var receiver = new TestEventReceiver();
        var events = new List<string>();

        receiver.SubscribeToEvent<SceneEvents.NodeAddedEvent>(scene,
            (ref SceneEvents.NodeAddedEvent value) =>
            {
                events.Add("node-added");
                Assert.Same(scene, value.Scene);
                Assert.Same(scene, value.Parent);
            });
        receiver.SubscribeToEvent<SceneEvents.ComponentAddedEvent>(scene,
            (ref SceneEvents.ComponentAddedEvent value) =>
            {
                events.Add("component-added");
                Assert.Same(scene, value.Scene);
                Assert.Same(value.Node, value.Component.Node);
            });
        receiver.SubscribeToEvent<SceneEvents.ComponentRemovedEvent>(scene,
            (ref SceneEvents.ComponentRemovedEvent value) => events.Add("component-removed"));
        receiver.SubscribeToEvent<SceneEvents.NodeRemovedEvent>(scene,
            (ref SceneEvents.NodeRemovedEvent value) => events.Add("node-removed"));

        var child = scene.CreateChild("Child");
        var camera = child.CreateComponent<Camera>();
        child.RemoveComponent(camera);
        child.Remove();

        Assert.Equal(["node-added", "component-added", "component-removed", "node-removed"], events);
        Assert.Null(child.Parent);
        Assert.Null(child.Scene);
        Assert.Equal(0u, child.Id);
    }

    [Fact]
    public void Disabled_scene_forces_update_but_does_not_advance_time_or_regular_logic()
    {
        using var scene = new Scene();
        var forcedTimeStep = 0.0f;
        var regularUpdates = 0;
        scene.SubscribeToEvent<SceneEvents.SceneForcedUpdateEvent>(
            (ref SceneEvents.SceneForcedUpdateEvent value) => forcedTimeStep = value.TimeStep);
        scene.SubscribeToEvent<SceneEvents.SceneUpdateEvent>(
            (ref SceneEvents.SceneUpdateEvent value) => ++regularUpdates);

        scene.SetElapsedTime(2.0f);
        scene.SetUpdateEnabled(false);
        scene.Update(0.5f);

        Assert.Equal(0.5f, forcedTimeStep);
        Assert.Equal(0, regularUpdates);
        Assert.Equal(2.0f, scene.GetElapsedTime());
    }

    [Fact]
    public void Empty_and_invalid_hierarchy_lookups_are_safe()
    {
        using var scene = new Scene();
        var parent = scene.CreateChild("Parent");
        var child = parent.CreateChild("Child");

        Assert.Null(scene.FindChild("Parent/Missing"));
        Assert.Null(scene.GetChild(-1));
        Assert.Null(parent.GetChild(99));
        Assert.False(parent.IsChildOf(child));
        Assert.True(child.IsChildOf(parent));
        Assert.Empty(scene.GetChildrenWithTag("missing", recursive: true));
    }
}
