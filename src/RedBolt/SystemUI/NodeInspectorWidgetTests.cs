// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

[Reflected("Tests")]
public partial class NodeInspectorSharedTestComponent : Component
{
    [Serialized("Value")]
    public int Value { get; set; } = 1;

    public int ApplyCount { get; private set; }

    public int CallCount { get; private set; }

    public override void ApplyAttributes() => ++ApplyCount;

    [Callable("Run", LabelSource = nameof(GetRunLabel))]
    private void Run() => ++CallCount;

    private string GetRunLabel() => $"Run {CallCount}";
}

[Reflected("Tests")]
public partial class NodeInspectorSkippedTestComponent : Component
{
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class NodeInspectorWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public NodeInspectorWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Shared_components_forward_edits_actions_and_deferred_removal()
    {
        _fixture.ResetGraphics();
        _fixture.Context.RegisterAssembly(typeof(NodeInspectorSharedTestComponent).Assembly);
        var firstNode = new Node();
        var secondNode = new Node();
        firstNode.SetName("First");
        secondNode.SetName("Second");
        var firstShared = new NodeInspectorSharedTestComponent();
        var skipped = new NodeInspectorSkippedTestComponent();
        var firstTrailing = new NodeInspectorSharedTestComponent { Value = 3 };
        var secondShared = new NodeInspectorSharedTestComponent { Value = 2 };
        var secondTrailing = new NodeInspectorSharedTestComponent { Value = 4 };
        firstNode.AddComponent(firstShared);
        firstNode.AddComponent(skipped);
        firstNode.AddComponent(firstTrailing);
        secondNode.AddComponent(secondShared);
        secondNode.AddComponent(secondTrailing);
        var widget = new NodeInspectorWidget(
        [
            new WeakReference<Node>(firstNode),
            null,
            new WeakReference<Node>(secondNode),
        ]);
        var events = new List<string>();
        var nodeHookCalls = 0;
        var componentHookCalls = 0;
        var widgetDisposed = false;

        bool EditNodeName(in AttributeHookContext context, ref Variant boxedValue)
        {
            ++nodeHookCalls;
            if (nodeHookCalls != 1)
                return false;

            Assert.Equal(2, context.Objects!.Count);
            Assert.Equal("First", boxedValue.GetString());
            boxedValue.Dispose();
            boxedValue = new Variant("Changed");
            return true;
        }

        bool EditComponentValue(in AttributeHookContext context, ref Variant boxedValue)
        {
            ++componentHookCalls;
            if (componentHookCalls != 1)
                return false;

            Assert.Equal(2, context.Objects!.Count);
            Assert.True(context.IsUndefined);
            Assert.True(context.IsDefaultValue);
            boxedValue.Dispose();
            boxedValue = new Variant(7);
            return true;
        }

        SerializableInspectorWidget.RegisterAttributeHook<Node>("Name", EditNodeName);
        SerializableInspectorWidget.RegisterAttributeHook<NodeInspectorSharedTestComponent>(
            "Value", EditComponentValue);
        widget.OnEditNodeAttributeBegin += (_, info) =>
            events.Add($"node-begin:{info!.Name}:{firstNode.Name},{secondNode.Name}");
        widget.OnEditNodeAttributeEnd += (_, info) =>
            events.Add($"node-end:{info!.Name}:{firstNode.Name},{secondNode.Name}");
        widget.OnEditComponentAttributeBegin += (_, info) =>
            events.Add($"component-begin:{info!.Name}:{firstShared.Value},{secondShared.Value}");
        widget.OnEditComponentAttributeEnd += (_, info) =>
            events.Add($"component-end:{info!.Name}:{firstShared.Value},{secondShared.Value}");
        widget.OnActionBegin += _ => events.Add(
            $"action-begin:{firstShared.CallCount},{secondShared.CallCount}");
        widget.OnActionEnd += _ => events.Add(
            $"action-end:{firstShared.CallCount},{secondShared.CallCount}");
        widget.OnComponentRemoved += component =>
        {
            Assert.NotNull(component.TryGetNode());
            events.Add($"remove:{((NodeInspectorSharedTestComponent)component).Value}");
        };

        var context = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(context);
            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            RenderWidgetFrame(widget, firstShared, Activation.Action);

            Assert.Equal(2, widget.GetNodes().Count);
            Assert.Equal("Changed", firstNode.Name);
            Assert.Equal("Changed", secondNode.Name);
            Assert.Equal(7, firstShared.Value);
            Assert.Equal(7, secondShared.Value);
            Assert.Equal(3, firstTrailing.Value);
            Assert.Equal(4, secondTrailing.Value);
            Assert.Equal(2, firstShared.ApplyCount);
            Assert.Equal(2, secondShared.ApplyCount);
            Assert.Equal(1, firstTrailing.ApplyCount);
            Assert.Equal(1, secondTrailing.ApplyCount);
            Assert.Equal(1, nodeHookCalls);
            Assert.Equal(2, componentHookCalls);
            Assert.Equal(1u, GetField<uint>(widget, "_numSkippedComponents"));
            var componentInspectors = GetComponentInspectors(widget);
            Assert.Equal(2, componentInspectors.Count);
            var firstInspector = componentInspectors[0];

            RenderWidgetFrame(widget, firstShared, Activation.Remove);

            Assert.Equal(1, firstShared.CallCount);
            Assert.Equal(1, secondShared.CallCount);
            Assert.Same(firstInspector, GetComponentInspectors(widget)[0]);

            RenderWidgetFrame(widget, null, Activation.None);

            Assert.Null(firstShared.TryGetNode());
            Assert.Null(secondShared.TryGetNode());
            Assert.DoesNotContain(firstShared, firstNode.GetComponents());
            Assert.DoesNotContain(secondShared, secondNode.GetComponents());
            Assert.Equal(2, events.Count(static item => item.StartsWith("remove:", StringComparison.Ordinal)));

            RenderWidgetFrame(widget, null, Activation.None);

            Assert.Single(GetComponentInspectors(widget));
            Assert.Equal(1u, GetField<uint>(widget, "_numSkippedComponents"));
            Assert.Empty(firstInspector.GetObjects());
            Assert.Equal(
            [
                "node-begin:Name:First,Second",
                "node-end:Name:Changed,Changed",
                "component-begin:Value:1,2",
                "component-end:Value:7,7",
                "action-begin:0,0",
                "action-end:1,1",
                "remove:7",
                "remove:7",
            ], events);

            widget.Dispose();
            widgetDisposed = true;
            Assert.Empty(widget.GetNodes());
        }
        finally
        {
            SerializableInspectorWidget.UnregisterAttributeHook<Node>("Name");
            SerializableInspectorWidget.UnregisterAttributeHook<NodeInspectorSharedTestComponent>(
                "Value");
            if (!widgetDisposed)
                widget.Dispose();
            ImGui.DestroyContext(context);
            firstNode.Dispose();
            secondNode.Dispose();
            firstShared.Dispose();
            skipped.Dispose();
            firstTrailing.Dispose();
            secondShared.Dispose();
            secondTrailing.Dispose();
        }
    }

    private static readonly CString _removeComponentLabel = new($"{Icons.TrashCan}##RemoveComponent");

    private static void RenderWidgetFrame(
        NodeInspectorWidget widget, Component? component, Activation activation)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 760.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(560.0f, 700.0f));
            var isOpen = true;
            ImGui.Begin("Node Inspector"u8, ref isOpen);
            windowStarted = true;

            widget.RenderTitle();
            ImGui.Separator();
            widget.RenderContent();

            if (component is not null)
            {
                using var componentGuard = new IdScopeGuard(RuntimeHelpers.GetHashCode(component));
                if (activation == Activation.Action)
                {
                    using var attributeGuard = new IdScopeGuard("Run"u8);
                    ImGui.ActivateItemByID(ImGui.GetID("Run"u8));
                }
                else if (activation == Activation.Remove)
                {
                    ImGui.ActivateItemByID(ImGui.GetID(_removeComponentLabel));
                }
            }
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private static IReadOnlyList<SerializableInspectorWidget> GetComponentInspectors(
        NodeInspectorWidget widget)
    {
        return GetField<IReadOnlyList<SerializableInspectorWidget>>(widget, "_componentInspectors");
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private enum Activation
    {
        None,
        Action,
        Remove,
    }
}
