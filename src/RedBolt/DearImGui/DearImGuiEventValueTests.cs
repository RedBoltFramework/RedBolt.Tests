// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Math;

public sealed class DearImGuiEventValueTests
{
    [Fact]
    public void Dear_imgui_event_payloads_keep_default_and_mutation_state()
    {
        var menu = new DearImGuiEvents.AttributeInspectorMenuEvent();
        var modified = new DearImGuiEvents.AttributeInspectorValueModifiedEvent();
        var attribute = new DearImGuiEvents.AttributeInspectorAttributeEvent();
        var gizmo = new DearImGuiEvents.GizmoNodeModifiedEvent();
        var endRendering = new DearImGuiEvents.EndRenderingDearImGuiEvent();
        var consoleClosed = new DearImGuiEvents.ConsoleClosedEvent();
        var selection = new DearImGuiEvents.GizmoSelectionChangedEvent();

        Assert.Null(menu.Serializable);
        Assert.Null(menu.AttributeInfo);
        Assert.Null(modified.Serializable);
        Assert.Null(modified.AttributeInfo);
        Assert.Null(attribute.Serializable);
        Assert.Null(attribute.AttributeInfo);
        Assert.Null(attribute.Tooltip);
        Assert.Equal(VariantType.None, modified.OldValue.Type);
        Assert.Equal(VariantType.None, modified.NewValue.Type);
        Assert.Equal(0u, modified.Reason);
        Assert.Null(gizmo.Node);
        Assert.Equal(default, gizmo.OldTransform);
        Assert.Equal(default, gizmo.NewTransform);
        _ = endRendering;
        _ = consoleClosed;
        _ = selection;

        attribute.Color = Color.Red;
        attribute.Hidden = true;
        attribute.Tooltip = "hidden";
        attribute.ValueKind = 7;
        var copy = attribute;
        copy.Hidden = false;
        copy.ValueKind = 2;

        Assert.Equal(Color.Red, attribute.Color);
        Assert.True(attribute.Hidden);
        Assert.Equal("hidden", attribute.Tooltip);
        Assert.Equal(7, attribute.ValueKind);
    }
}
