namespace RedBolt.Tests;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class WidgetLayoutTests
{
    [Fact]
    public void Fit_content_preserves_aspect_ratio()
    {
        Assert.Equal(Vector2f.Zero, Widgets.FitContent(Vector2f.Zero, Vector2f.One));
        Assert.Equal(Vector2f.Zero, Widgets.FitContent(Vector2f.One, Vector2f.Zero));
        Assert.Equal(new Vector2f(100.0f, 100.0f),
            Widgets.FitContent(new Vector2f(200.0f, 100.0f), new Vector2f(100.0f, 100.0f)));
        Assert.Equal(new Vector2f(100.0f, 50.0f),
            Widgets.FitContent(new Vector2f(100.0f, 200.0f), new Vector2f(200.0f, 100.0f)));
    }

    [NativeBindingFact]
    public void Item_label_and_toolbar_preserve_native_layout_and_scopes()
    {
        var context = ImGui.CreateContext();
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            frameStarted = true;
            var isOpen = true;
            ImGui.SetNextWindowSize(new Vector2f(400.0f, 300.0f));
            ImGui.Begin("Widget Layout"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;

            var originalTextColor = ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText);
            ImGui.PushItemWidth(100.0f);
            var leftStart = ImGui.GetCursorScreenPos();
            Widgets.ItemLabel("Left Label"u8, Color.Red);
            ImGui.PopItemWidth();
            var leftMinimum = ImGui.GetItemRectMin();
            var leftMaximum = ImGui.GetItemRectMax();
            AssertClose(leftStart.X, leftMinimum.X);
            Assert.True(leftMaximum.X > leftMinimum.X);
            Assert.True(ImGui.GetCursorScreenPos().X >= leftMaximum.X);
            Assert.Equal(originalTextColor, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColText));

            ImGui.NewLine();
            ImGui.PushItemWidth(80.0f);
            var rightStart = ImGui.GetCursorScreenPos();
            Widgets.ItemLabel("Right Label"u8, isLeft: false);
            ImGui.PopItemWidth();
            Assert.True(ImGui.GetItemRectMin().X > rightStart.X);
            AssertClose(rightStart.X, ImGui.GetCursorScreenPos().X);
            AssertClose(rightStart.Y, ImGui.GetCursorScreenPos().Y);

            ImGui.NewLine();
            var unscopedId = ImGui.GetID("Item"u8);
            using (new Widgets.ItemLabelScopeGuard("Scope"u8))
                Assert.NotEqual(unscopedId, ImGui.GetID("Item"u8));
            Assert.Equal(unscopedId, ImGui.GetID("Item"u8));
            Assert.True(typeof(Widgets.ItemLabelScopeGuard).IsValueType);

            ImGui.NewLine();
            var originalButtonColor = ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColButton);
            var buttonSize = Widgets.GetSmallButtonSize();
            Assert.False(Widgets.ToolbarButton("Toolbar"u8, active: true));
            var renderedButtonSize = ImGui.GetItemRectSize();
            AssertClose(buttonSize, renderedButtonSize.X);
            AssertClose(buttonSize, renderedButtonSize.Y);
            Assert.Equal(originalButtonColor, ImGui.GetStyleColorVec4(ImGuiCol.ImGuiColButton));
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(context);
        }
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(MathF.Abs(expected - actual), 0.0f, 0.001f);
}
