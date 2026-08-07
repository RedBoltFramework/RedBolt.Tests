namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class ModelInspectorWidgetTests
{
    [NativeBindingFact]
    public void Model_widget_is_read_only_and_has_no_editable_properties()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var model = new Model { Name = "Models/Test.mdl" };
            using var widget = new ModelInspectorWidget([model]);
            var editBeginCount = 0;
            var editEndCount = 0;
            widget.OnEditBegin += () => ++editBeginCount;
            widget.OnEditEnd += () => ++editEndCount;

            Assert.False(widget.CanSave());
            Assert.Equal([model], widget.GetResources());
            var properties = (IReadOnlyList<ResourceInspectorWidget.PropertyDesc>)
                typeof(ResourceInspectorWidget)
                    .GetField("_properties", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(widget)!;
            Assert.Empty(properties);

            var imguiContext = ImGui.CreateContext();
            var frameStarted = false;
            var windowStarted = false;
            try
            {
                ImGui.SetCurrentContext(imguiContext);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(640.0f, 480.0f);
                io.DeltaTime = 1.0f / 60.0f;
                io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
                ImGui.NewFrame();
                frameStarted = true;
                var isOpen = true;
                ImGui.Begin("Model Inspector"u8, ref isOpen);
                windowStarted = true;

                widget.RenderTitle();
                widget.RenderContent();

                Assert.Equal(0, editBeginCount);
                Assert.Equal(0, editEndCount);
            }
            finally
            {
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
                ImGui.DestroyContext(imguiContext);
            }
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
