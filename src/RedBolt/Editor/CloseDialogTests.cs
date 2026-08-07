namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
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
}
