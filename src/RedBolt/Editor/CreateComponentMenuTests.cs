// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed partial class CreateComponentMenuTests
{
    [NativeBindingFact]
    public void User_component_is_returned_when_selected_from_special_group()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        context.RegisterAttributedType<MenuComponent>();
        var componentReflection = context.GetReflection<MenuComponent>();
        Assert.Equal(ObjectCategory.Category_User, componentReflection.Category);
        Assert.True(componentReflection.HasObjectFactory());
        var imguiContext = ImGui.CreateContext();
        var frameStarted = false;
        var popupStarted = false;

        try
        {
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.OpenPopup("Create component"u8);
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;
            Assert.Null(CreateComponentMenuFunctions.RenderCreateComponentMenu());
            ImGui.EndPopup();
            popupStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            ImGui.NewFrame();
            frameStarted = true;
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;
            Assert.Null(CreateComponentMenuFunctions.RenderCreateComponentMenu());
            var popupPosition = ImGui.GetWindowPos();
            var popupSize = ImGui.GetWindowSize();
            var cursorPosition = ImGui.GetCursorScreenPos();
            var groupPosition = new Vector2f(
                popupPosition.X + popupSize.X * 0.5f,
                cursorPosition.Y - ImGui.GetTextLineHeightWithSpacing() * 0.5f);
            var componentPosition = new Vector2f(
                popupPosition.X + popupSize.X + 50.0f,
                groupPosition.Y);
            ImGui.EndPopup();
            popupStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddMousePosEvent(groupPosition.X, groupPosition.Y);
            io.AddMouseButtonEvent(0, true);
            ImGui.NewFrame();
            frameStarted = true;
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;
            Assert.Null(CreateComponentMenuFunctions.RenderCreateComponentMenu());
            ImGui.EndPopup();
            popupStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddMouseButtonEvent(0, false);
            ImGui.NewFrame();
            frameStarted = true;
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;
            Assert.Null(CreateComponentMenuFunctions.RenderCreateComponentMenu());
            ImGui.EndPopup();
            popupStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddMousePosEvent(componentPosition.X, componentPosition.Y);
            io.AddMouseButtonEvent(0, true);
            ImGui.NewFrame();
            frameStarted = true;
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;
            Assert.Null(CreateComponentMenuFunctions.RenderCreateComponentMenu());
            ImGui.EndPopup();
            popupStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddMouseButtonEvent(0, false);
            ImGui.NewFrame();
            frameStarted = true;
            Assert.True(ImGui.BeginPopup("Create component"u8));
            popupStarted = true;

            var selected = CreateComponentMenuFunctions.RenderCreateComponentMenu();

            Assert.Same(context.GetReflection<MenuComponent>(), selected);
        }
        finally
        {
            if (popupStarted)
                ImGui.EndPopup();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
            Context.SetInstance(previousContext);
        }
    }

    [Reflected(ObjectCategory.Category_User)]
    private sealed partial class MenuComponent : Component
    {
    }
}
