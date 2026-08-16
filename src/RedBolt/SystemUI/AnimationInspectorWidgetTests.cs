// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.SystemUI;

public sealed class AnimationInspectorWidgetTests
{
    [NativeBindingFact]
    public void Length_property_uses_animation_accessors_and_is_read_only()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var animation = new Animation { Name = "Animations/Test.ani" };
            animation.SetLength(3.5f);
            using var widget = new AnimationInspectorWidget([animation]);

            Assert.False(widget.CanSave());
            Assert.Equal([animation], widget.GetResources());

            var properties = (IReadOnlyList<ResourceInspectorWidget.PropertyDesc>)
                typeof(ResourceInspectorWidget)
                    .GetField("_properties", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(widget)!;
            var property = Assert.Single(properties);
            Assert.Equal("Length", property.Name);
            Assert.Equal("Length in seconds", property.Hint);
            Assert.Equal(new Variant(0.0f), property.DefaultValue);
            using (var value = property.Getter(animation))
                Assert.Equal(3.5f, value.GetFloat());
            using (var value = new Variant(-1.0f))
                property.Setter(animation, in value);
            Assert.Equal(0.0f, animation.Length);
            animation.SetLength(3.5f);

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
                ImGui.Begin("Animation Inspector"u8, ref isOpen);
                windowStarted = true;
                uint propertyId;
                using (new IdScopeGuard("RenderProperties"u8))
                using (new IdScopeGuard("Length"u8))
                    propertyId = ImGui.GetID(""u8);
                ImGui.ActivateItemByID(propertyId);

                widget.RenderTitle();
                widget.RenderContent();

                Assert.Equal(0u, ImGui.GetActiveID());
                Assert.Equal(3.5f, animation.Length);
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
