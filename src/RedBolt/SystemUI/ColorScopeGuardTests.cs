namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class ColorScopeGuardTests
{
    [NativeBindingFact]
    public void Color_scope_guard_pushes_and_pops_all_color_forms()
    {
        Assert.True(typeof(ColorScopeGuard).IsValueType);

        var context = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(context);
            var originalText = ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText);
            var originalFrame = ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg);
            var red = new Vector4f(1.0f, 0.0f, 0.0f, 1.0f);
            var green = new Vector4f(0.0f, 1.0f, 0.0f, 1.0f);

            using (new ColorScopeGuard(ImGuiCol.ImGuiColText, red))
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));

            (ImGuiCol Id, Vector4f Color)[] vectorColors =
            [
                (ImGuiCol.ImGuiColText, red),
                (ImGuiCol.ImGuiColFrameBg, green),
            ];
            using (new ColorScopeGuard(vectorColors))
            {
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
                Assert.Equal(green, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));
            }
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalFrame, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));

            using (new ColorScopeGuard(ImGuiCol.ImGuiColText, 0xff0000ffu))
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));

            (ImGuiCol Id, uint Color)[] packedColors =
            [
                (ImGuiCol.ImGuiColText, 0xff0000ffu),
                (ImGuiCol.ImGuiColFrameBg, 0xff00ff00u),
            ];
            using (new ColorScopeGuard(packedColors))
            {
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
                Assert.Equal(green, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));
            }
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalFrame, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));

            using (new ColorScopeGuard(ImGuiCol.ImGuiColText, new Color(red)))
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));

            (ImGuiCol Id, Color Color)[] engineColors =
            [
                (ImGuiCol.ImGuiColText, new Color(red)),
                (ImGuiCol.ImGuiColFrameBg, new Color(green)),
            ];
            using (new ColorScopeGuard(engineColors))
            {
                Assert.Equal(red, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
                Assert.Equal(green, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));
            }
            Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
            Assert.Equal(originalFrame, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColFrameBg));

            using (new ColorScopeGuard(ImGuiCol.ImGuiColText, red, false))
                Assert.Equal(originalText, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
