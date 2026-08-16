// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class WidgetColorTests
{
    [NativeBindingFact]
    public void Item_colors_follow_undefined_default_and_modified_states()
    {
        var context = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(context);
            var disabledText = new Color(ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColTextDisabled));
            var frameBackground = new Color(ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));

            Assert.Equal(disabledText, Widgets.GetItemLabelColor(true, false));
            Assert.Equal(disabledText, Widgets.GetItemLabelColor(true, true));
            Assert.Equal(new Color(0.85f, 0.85f, 0.85f, 1.0f), Widgets.GetItemLabelColor(false, true));
            Assert.Equal(new Color(1.0f, 1.0f, 0.75f, 1.0f), Widgets.GetItemLabelColor(false, false));

            Assert.Equal(new Color(0.09f, 0.09f, 0.09f, 1.0f), Widgets.GetItemBackgroundColor(true));
            Assert.Equal(frameBackground, Widgets.GetItemBackgroundColor(false));
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
