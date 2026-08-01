namespace RedBolt.Tests;

using RedBolt.DearImGui;
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

        Assert.Null(menu.Serializable);
        Assert.Null(menu.AttributeInfo);
        Assert.Null(modified.Serializable);
        Assert.Null(modified.AttributeInfo);
        Assert.Null(attribute.Serializable);
        Assert.Null(attribute.AttributeInfo);
        Assert.Null(attribute.Tooltip);
        Assert.Equal(default, gizmo.OldTransform);
        Assert.Equal(default, gizmo.NewTransform);

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
