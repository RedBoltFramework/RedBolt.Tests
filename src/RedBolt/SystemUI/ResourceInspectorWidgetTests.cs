namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

public sealed class ResourceInspectorWidgetTests
{
    [Fact]
    public void Constructor_requires_a_resource()
    {
        Assert.Throws<ArgumentException>(() => new ResourceInspectorWidget(
            Array.Empty<Resource>(),
            Array.Empty<ResourceInspectorWidget.PropertyDesc>()));
    }

    [NativeBindingFact]
    public void String_property_edit_is_applied_to_all_resources_between_events()
    {
        // ImGuiKey is currently projected as an opaque enum by the generated binding.
        const int imguiKeyEnter = 525;
        using var first = new TestResource { Name = "First", Value = "seed" };
        using var second = new TestResource { Name = "Second", Value = "different" };
        var property = new ResourceInspectorWidget.PropertyDesc
        {
            Name = "Value",
            DefaultValue = new Variant("seed"),
            Getter = static resource => new Variant(((TestResource)resource).Value),
            Setter = static (Resource resource, in Variant value) =>
                ((TestResource)resource).Value = value.GetString(),
            Hint = "Editable text",
        };
        using var widget = new ResourceInspectorWidget(
            new Resource[] { first, second },
            new[] { property });
        var edits = new List<string>();
        widget.OnEditBegin += () => edits.Add($"begin:{first.Value}:{second.Value}");
        widget.OnEditEnd += () => edits.Add($"end:{first.Value}:{second.Value}");

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
            var isOpen = true;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource Inspector"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;
            uint propertyId;
            using (new IdScopeGuard("RenderProperties"u8))
            using (new IdScopeGuard("Value"u8))
                propertyId = ImGui.GetID(""u8);
            ImGui.ActivateItemByID(propertyId);
            widget.RenderTitle();
            widget.RenderContent();
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource Inspector"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;
            widget.RenderContent();
            Assert.NotEqual(0u, ImGui.GetActiveID());
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddInputCharacter((uint)'!');
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource Inspector"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;
            widget.RenderContent();
            Assert.Empty(edits);
            ImGui.End();
            windowStarted = false;
            ImGui.EndFrame();
            frameStarted = false;

            io.AddKeyEvent((ImGuiKey)imguiKeyEnter, true);
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.Begin("Resource Inspector"u8, ref isOpen, ImGuiWindowFlags.ImGuiWindowFlagsNone);
            windowStarted = true;
            widget.RenderContent();

            Assert.Equal("!", first.Value);
            Assert.Equal("!", second.Value);
            Assert.Equal(["begin:seed:different", "end:!:!"], edits);
            Assert.Equal([first, second], widget.GetResources());
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

    private sealed class TestResource : Resource
    {
        public string Value { get; set; } = string.Empty;
    }
}
