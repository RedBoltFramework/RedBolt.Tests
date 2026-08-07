namespace RedBolt.Tests;

using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Input;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class HotkeyManagerTests
{
    [NativeBindingFact]
    public void Builder_formats_hotkeys_and_classifies_text_input_conflicts()
    {
        var hotkey = new EditorHotkey("Save").Ctrl().MaybeAlt().Shift().Press(Key.S);
        var f2 = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)Key.F2, (int)Scancode.F2);
        var f3 = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)Key.F3, (int)Scancode.F3);

        Assert.True(hotkey.IsValid());
        Assert.Equal("Ctrl+[Alt?]+Shift+S", hotkey.ToString());
        Assert.NotEqual((ImGuiKey)0, f2);
        Assert.NotEqual(f2, f3);
        Assert.True(new EditorHotkey().Ctrl().Press(Scancode.B).IsTextInputFriendly());
        Assert.False(new EditorHotkey().Ctrl().Press(Scancode.C).IsTextInputFriendly());
        Assert.False(new EditorHotkey().Press(Key.A).IsTextInputFriendly());
        Assert.False(new EditorHotkey().Press(Key.Backspace).IsTextInputFriendly());
        Assert.True(new EditorHotkey().Press(Key.F2).IsTextInputFriendly());
        Assert.True(new EditorHotkey().Press(MouseButton.Left).IsTextInputFriendly());
    }

    [NativeBindingFact]
    public void Bindings_copy_hotkey_values()
    {
        WithManager(manager =>
        {
            var source = new EditorHotkey("Save").Ctrl().Press(Key.S);
            manager.BindPassiveHotkey(source);

            source.Alt().Press(Key.D);

            Assert.Equal("Ctrl+S", manager.GetHotkeyLabel(new EditorHotkey("Save")));
        });
    }

    [NativeBindingFact]
    public void A_command_is_invoked_only_once_per_update()
    {
        var imguiContext = ImGui.CreateContext();
        var frameStarted = false;

        try
        {
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640, 480);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            io.AddMouseButtonEvent(0, true);

            ImGui.NewFrame();
            frameStarted = true;

            WithManager(manager =>
            {
                using var firstOwner = new HotkeyOwner();
                using var secondOwner = new HotkeyOwner();
                var hotkey = new EditorHotkey("Select").Press(MouseButton.Left);
                manager.BindHotkey(firstOwner, hotkey, static owner => ++owner.InvocationCount);
                manager.BindHotkey(secondOwner, hotkey, static owner => ++owner.InvocationCount);

                manager.Update();
                manager.InvokeFor(firstOwner);
                manager.InvokeFor(secondOwner);

                Assert.Equal(1, firstOwner.InvocationCount);
                Assert.Equal(0, secondOwner.InvocationCount);
            });
        }
        finally
        {
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
        }
    }

    [Fact]
    public void Expired_owner_bindings_are_removed()
    {
        WithManager(manager =>
        {
            var owner = BindTemporaryOwner(manager);

            for (var i = 0; i < 5; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(owner.TryGetTarget(out _));
            manager.RemoveExpired();
            Assert.Empty(manager.Bindings);
        });
    }

    private static void WithManager(Action<HotkeyManager> test)
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var manager = new HotkeyManager();
            test(manager);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<HotkeyOwner> BindTemporaryOwner(HotkeyManager manager)
    {
        var owner = new HotkeyOwner();
        manager.BindHotkey(owner, new EditorHotkey("Temporary").Press(MouseButton.Left), static _ => { });
        return new WeakReference<HotkeyOwner>(owner);
    }

    private sealed class HotkeyOwner : RedObject
    {
        public int InvocationCount { get; set; }
    }
}
