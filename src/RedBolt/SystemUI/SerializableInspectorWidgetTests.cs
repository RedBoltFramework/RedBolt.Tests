namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;

[Reflected("Tests")]
public partial class SerializableInspectorWidgetTestObject : Serializable
{
    [Serialized("Value")]
    public int Value { get; set; } = 1;

    public int ApplyCount { get; private set; }

    public int CallCount { get; private set; }

    public override void ApplyAttributes() => ++ApplyCount;

    [Callable("Run", LabelSource = nameof(GetRunLabel))]
    private void Run() => ++CallCount;

    private string GetRunLabel() => $"Run {CallCount}";
}

[Reflected("Tests")]
public partial class SerializableInspectorWidgetCopyTarget : Serializable
{
    [Serialized("Value")]
    public int Value { get; set; }
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class SerializableInspectorWidgetTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SerializableInspectorWidgetTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Attribute_hook_edits_all_objects_and_brackets_apply()
    {
        _fixture.Context.RegisterAssembly(typeof(SerializableInspectorWidgetTestObject).Assembly);
        using var first = new SerializableInspectorWidgetTestObject();
        using var second = new SerializableInspectorWidgetTestObject { Value = 2 };
        var firstReference = new WeakReference<Serializable>(first);
        using var widget = new SerializableInspectorWidget(
            [firstReference, new WeakReference<Serializable>(second)]);
        var events = new List<string>();
        var hookCalls = 0;

        bool EditValue(in AttributeHookContext context, ref Variant boxedValue)
        {
            ++hookCalls;
            Assert.Same(widget.GetObjects(), context.Objects);
            Assert.Equal("Value", context.Info!.Name);
            Assert.True(context.IsUndefined);
            Assert.True(context.IsDefaultValue);
            Assert.Equal(1, boxedValue.GetInt());
            boxedValue.Dispose();
            boxedValue = new Variant(7);
            return true;
        }

        SerializableInspectorWidget.RegisterAttributeHook<SerializableInspectorWidgetTestObject>("Value", EditValue);
        widget.OnEditAttributeBegin += (_, info) =>
        {
            events.Add($"begin:{info!.Name}:{first.Value},{second.Value}");
        };
        widget.OnEditAttributeEnd += (_, info) =>
        {
            events.Add($"end:{info!.Name}:{first.Value},{second.Value}");
        };

        try
        {
            using var context = new ImGuiContextScope();
            RenderWidgetFrame(widget);

            Assert.Equal(1, hookCalls);
            Assert.Equal(7, first.Value);
            Assert.Equal(7, second.Value);
            Assert.Equal(1, first.ApplyCount);
            Assert.Equal(1, second.ApplyCount);
            Assert.Equal(["begin:Value:1,2", "end:Value:7,7"], events);
            Assert.NotSame(firstReference, widget.GetObjects()[0]);
        }
        finally
        {
            SerializableInspectorWidget.UnregisterAttributeHook<SerializableInspectorWidgetTestObject>("Value");
        }
    }

    [NativeBindingFact]
    public void Action_button_invokes_all_objects_and_brackets_calls()
    {
        _fixture.Context.RegisterAssembly(typeof(SerializableInspectorWidgetTestObject).Assembly);
        using var first = new SerializableInspectorWidgetTestObject();
        using var second = new SerializableInspectorWidgetTestObject();
        using var widget = new SerializableInspectorWidget(
            [new WeakReference<Serializable>(first), new WeakReference<Serializable>(second)]);
        using var context = new ImGuiContextScope();
        var events = new List<string>();
        widget.OnActionBegin += _ => events.Add($"begin:{first.CallCount},{second.CallCount}");
        widget.OnActionEnd += _ => events.Add($"end:{first.CallCount},{second.CallCount}");

        var buttonPosition = RenderWidgetFrame(widget);
        var io = ImGui.GetIO();
        io.AddMousePosEvent(buttonPosition.X, buttonPosition.Y);
        io.AddMouseButtonEvent(0, true);
        RenderWidgetFrame(widget);
        io.AddMouseButtonEvent(0, false);
        RenderWidgetFrame(widget);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(["begin:0,0", "end:1,1"], events);
        Assert.Equal(0, first.ApplyCount);
        Assert.Equal(0, second.ApplyCount);
    }

    [NativeBindingFact]
    public void Hook_registries_replace_default_rendering_and_titles_prune_selection()
    {
        _fixture.Context.RegisterAssembly(typeof(SerializableInspectorWidgetTestObject).Assembly);
        using var first = new SerializableInspectorWidgetTestObject();
        using var second = new SerializableInspectorWidgetTestObject();
        using var widget = new SerializableInspectorWidget(
            [new WeakReference<Serializable>(first), null, new WeakReference<Serializable>(second)]);
        var order = new List<string>();
        AttributeHookFunction attributeHook = (in AttributeHookContext _, ref Variant _) =>
        {
            order.Add("attribute");
            return false;
        };
        ObjectHookFunction prependHook = _ => order.Add("prepend");
        ObjectHookFunction replaceHook = _ => order.Add("replace");
        ObjectHookFunction appendHook = _ => order.Add("append");

        SerializableInspectorWidget.RegisterAttributeHook<SerializableInspectorWidgetTestObject>("Value", attributeHook);
        SerializableInspectorWidget.RegisterObjectHook<SerializableInspectorWidgetTestObject>(ObjectHookType.Prepend, prependHook);
        SerializableInspectorWidget.RegisterObjectHook<SerializableInspectorWidgetTestObject>(ObjectHookType.Replace, replaceHook);
        SerializableInspectorWidget.RegisterObjectHook<SerializableInspectorWidgetTestObject>(ObjectHookType.Append, appendHook);
        SerializableInspectorWidget.CopyAttributeHook<SerializableInspectorWidgetTestObject, SerializableInspectorWidgetCopyTarget>("Value");
        SerializableInspectorWidget.CopyObjectHook<SerializableInspectorWidgetTestObject, SerializableInspectorWidgetCopyTarget>();

        try
        {
            widget.RenderContent();

            Assert.Equal(["prepend", "replace", "append"], order);
            Assert.Equal("2x SerializableInspectorWidgetTestObject", widget.GetTitle());
            Assert.Equal(2, widget.GetObjects().Count);
            Assert.Same(
                attributeHook,
                SerializableInspectorWidget.GetAttributeHook(
                    (typeof(SerializableInspectorWidgetCopyTarget).GetClassName(), "Value")));
            Assert.Same(
                replaceHook,
                SerializableInspectorWidget.GetObjectHook(
                    (typeof(SerializableInspectorWidgetCopyTarget).GetClassName(), ObjectHookType.Replace)));

            using var node = new Node();
            using var nodeWidget = new SerializableInspectorWidget([new WeakReference<Serializable>(node)]);
            Assert.Equal("Node (0)", nodeWidget.GetTitle());

            using var component = new Component();
            using var componentWidget = new SerializableInspectorWidget([new WeakReference<Serializable>(component)]);
            Assert.Equal("Component (0)", componentWidget.GetTitle());
        }
        finally
        {
            SerializableInspectorWidget.UnregisterAttributeHook<SerializableInspectorWidgetTestObject>("Value");
            SerializableInspectorWidget.UnregisterAttributeHook<SerializableInspectorWidgetCopyTarget>("Value");
            SerializableInspectorWidget.UnregisterObjectHook<SerializableInspectorWidgetTestObject>();
            SerializableInspectorWidget.UnregisterObjectHook<SerializableInspectorWidgetCopyTarget>();
        }

        Assert.Null(SerializableInspectorWidget.GetAttributeHook(
            (typeof(SerializableInspectorWidgetCopyTarget).GetClassName(), "Value")));
        Assert.Null(SerializableInspectorWidget.GetObjectHook(
            (typeof(SerializableInspectorWidgetCopyTarget).GetClassName(), ObjectHookType.Replace)));

        widget.Dispose();
        Assert.Empty(widget.GetObjects());
    }

    private static Vector2f RenderWidgetFrame(SerializableInspectorWidget widget)
    {
        var frameStarted = false;
        var windowStarted = false;
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(500.0f, 360.0f);
            io.DeltaTime = 1.0f / 60.0f;
            ImGui.NewFrame();
            frameStarted = true;
            ImGui.SetNextWindowSize(new Vector2f(440.0f, 320.0f));
            var isOpen = true;
            ImGui.Begin("Serializable Inspector"u8, ref isOpen);
            windowStarted = true;

            widget.RenderTitle();
            widget.RenderContent();

            var labelMinimum = ImGui.GetItemRectMin();
            var labelMaximum = ImGui.GetItemRectMax();
            var style = ImGui.GetStyle();
            var buttonWidth = ImGui.CalcTextSize("Run"u8).X + style.FramePadding.X * 2.0f;
            var buttonRight = labelMinimum.X - style.ItemSpacing.X;
            return new Vector2f(buttonRight - buttonWidth * 0.5f, (labelMinimum.Y + labelMaximum.Y) * 0.5f);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
        }
    }

    private sealed class ImGuiContextScope : IDisposable
    {
        private readonly nint _context = ImGui.CreateContext();

        public ImGuiContextScope()
        {
            ImGui.SetCurrentContext(_context);
            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
        }

        public void Dispose()
        {
            ImGui.DestroyContext(_context);
        }
    }
}
