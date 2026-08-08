namespace RedBolt.Tests;

using System.Reflection;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Math;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SystemMessageBoxTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SystemMessageBoxTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Message_box_is_reflected_renders_and_reports_ok_and_cancel()
    {
        _fixture.ResetGraphics();
        _fixture.Graphics.SetMode(640, 480);
        _fixture.Context.RegisterAssembly(typeof(SystemMessageBox).Assembly);
        Assert.True(_fixture.Context.IsReflected<SystemMessageBox>());
        using (var reflected = Assert.IsType<SystemMessageBox>(
                   _fixture.Context.CreateObject(ObjectReflection<SystemMessageBox>.TypeId)))
        {
            Assert.Equal(string.Empty, reflected.GetMessage());
            Assert.StartsWith("##0x", reflected.GetTitle(), StringComparison.Ordinal);
            Assert.True(reflected.IsOpen());
        }

        var imguiContext = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;

            using (var messageBox = new SystemMessageBox("Original message", "Original title"))
            using (var receiver = new AckReceiver(messageBox))
            {
                var titleSuffix = messageBox.GetTitle()["Original title".Length..];
                messageBox.SetTitle("Updated title");
                messageBox.SetMessage("Updated message");
                Assert.Equal("Updated title" + titleSuffix, messageBox.GetTitle());
                Assert.Equal("Updated message", messageBox.GetMessage());
                Assert.True(_fixture.Context.HasSubscribedToEvent<CoreEvents.UpdateEvent>(messageBox));

                RenderFrame(messageBox);
                using var title = new CString(messageBox.GetTitle());
                Assert.False(ImGui.FindWindowByName(title.ContentBytes).IsNull);

                Acknowledge(messageBox, true);

                Assert.Equal([true], receiver.Statuses);
                Assert.Same(messageBox, receiver.LastSender);
                Assert.False(messageBox.IsOpen());

                messageBox.Dispose();
                Assert.False(_fixture.Context.HasSubscribedToEvent<CoreEvents.UpdateEvent>(messageBox));
            }

            using (var messageBox = new SystemMessageBox("Cancel message", "Cancel title"))
            using (var receiver = new AckReceiver(messageBox))
            {
                RenderFrame(messageBox);
                Acknowledge(messageBox, false);

                Assert.Equal([false], receiver.Statuses);
                Assert.Same(messageBox, receiver.LastSender);
                Assert.False(messageBox.IsOpen());
            }
        }
        finally
        {
            ImGui.DestroyContext(imguiContext);
            _fixture.ResetGraphics();
        }
    }

    private void RenderFrame(SystemMessageBox messageBox)
    {
        ImGui.NewFrame();
        _fixture.Context.SendEvent<CoreEvents.UpdateEvent>();

        using var title = new CString(messageBox.GetTitle());
        var window = ImGui.FindWindowByName(title.ContentBytes);
        Assert.False(window.IsNull);

        ImGui.EndFrame();
    }

    private static void Acknowledge(SystemMessageBox messageBox, bool status)
    {
        messageBox.GetType().GetMethod(
                "Acknowledge", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(messageBox, [status]);
    }

    private sealed class AckReceiver : RedObject
    {
        public AckReceiver(SystemMessageBox messageBox)
        {
            SubscribeToEvent<DearImGuiEvents.MessageAckEvent>(messageBox, OnMessageAck);
        }

        private void OnMessageAck(ref DearImGuiEvents.MessageAckEvent args)
        {
            Statuses.Add(args.OK);
            LastSender = GetEventSender();
        }

        public List<bool> Statuses { get; } = [];
        public RedObject? LastSender { get; private set; }
    }
}
