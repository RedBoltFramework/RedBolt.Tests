namespace RedBolt.Tests;

using System.Collections.Generic;
using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Input;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class CloseDialogTests
{
    [Fact]
    public void Close_resource_request_clone_has_independent_resource_names_and_copied_callbacks()
    {
        var saveCount = 0;
        var request = new CloseResourceRequest
        {
            ResourceNames = ["Scenes/First.xml"],
            OnSave = () => ++saveCount,
        };

        var clone = request.Clone();
        request.ResourceNames.Add("Scenes/Second.xml");
        request.OnSave = static () => { };

        Assert.Equal(["Scenes/First.xml"], clone.ResourceNames);
        clone.OnSave();
        Assert.Equal(1, saveCount);
        clone.OnDiscard();
        clone.OnCancel();
    }

    [Fact]
    public void Resolution_callbacks_are_exclusive_and_clear_the_active_request()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        using var dialog = new CloseDialog();

        try
        {
            var saveCount = 0;
            var discardCount = 0;
            var cancelCount = 0;

            dialog.RequestClose(new CloseResourceRequest
            {
                ResourceNames = ["Save"],
                OnSave = () => ++saveCount,
                OnDiscard = () => ++discardCount,
                OnCancel = () => ++cancelCount,
            });
            InvokeResolution(dialog, "CloseDialogSave");
            Assert.Equal((1, 0, 0), (saveCount, discardCount, cancelCount));
            Assert.False(dialog.IsActive);

            dialog.RequestClose(new CloseResourceRequest
            {
                ResourceNames = ["Discard"],
                OnSave = () => ++saveCount,
                OnDiscard = () => ++discardCount,
                OnCancel = () => ++cancelCount,
            });
            InvokeResolution(dialog, "CloseDialogDiscard");
            Assert.Equal((1, 1, 0), (saveCount, discardCount, cancelCount));
            Assert.False(dialog.IsActive);

            dialog.RequestClose(new CloseResourceRequest
            {
                ResourceNames = ["Cancel"],
                OnSave = () => ++saveCount,
                OnDiscard = () => ++discardCount,
                OnCancel = () => ++cancelCount,
            });
            InvokeResolution(dialog, "CloseDialogCancel");
            Assert.Equal((1, 1, 1), (saveCount, discardCount, cancelCount));
            Assert.False(dialog.IsActive);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [NativeBindingFact]
    public void Escape_cancels_pending_requests_and_request_input_is_copied()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        var imguiContext = ImGui.CreateContext();
        var frameStarted = false;
        var dialog = new CloseDialog();

        try
        {
            var cancelCount = 0;
            var request = new CloseResourceRequest
            {
                ResourceNames = ["Scenes/Second.xml", "Scenes/First.xml"],
                OnCancel = () => ++cancelCount,
            };
            dialog.RequestClose(request);
            request.ResourceNames.Clear();
            request.OnCancel = static () => { };

            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            var escapeKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
                (int)Key.Escape, (int)Input.GetScancodeFromKey(Key.Escape));
            io.AddKeyEvent(escapeKey, true);

            Assert.True(dialog.IsActive);
            ImGui.NewFrame();
            frameStarted = true;
            dialog.Render();

            Assert.Equal(1, cancelCount);
            Assert.False(dialog.IsActive);
            var items = Assert.IsType<List<CString>>(
                typeof(CloseDialog).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(dialog));
            Assert.Collection(
                items,
                item => Assert.Equal("Scenes/First.xml", item.ToString()),
                item => Assert.Equal("Scenes/Second.xml", item.ToString()));
        }
        finally
        {
            if (frameStarted)
                ImGui.EndFrame();
            dialog.Dispose();
            ImGui.DestroyContext(imguiContext);
            Context.SetInstance(previousContext);
        }
    }

    private static void InvokeResolution(CloseDialog dialog, string methodName)
    {
        typeof(CloseDialog).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialog, null);
    }
}
