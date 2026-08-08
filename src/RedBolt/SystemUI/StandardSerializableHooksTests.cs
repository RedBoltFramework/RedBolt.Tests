namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class StandardSerializableHooksTests
{
    private static readonly StringHash CameraHookWidgetVariable = "Camera_Hook_Widget";

    private readonly GraphicsEngineFixture _fixture;

    public StandardSerializableHooksTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Standard_hooks_edit_attributes_and_render_live_camera_preview()
    {
        _fixture.ResetGraphics();
        DearImGuiSubsystem? systemUI = null;
        SceneRendererToTexture? preview = null;
        try
        {
            systemUI = new DearImGuiSubsystem();
            _fixture.Context.RegisterSubsystem(systemUI);

            var morphHook = Assert.IsType<AttributeHookFunction>(
                SerializableInspectorWidget.GetAttributeHook((nameof(AnimatedModel), "Morphs")));
            var renderPassHook = Assert.IsType<AttributeHookFunction>(
                SerializableInspectorWidget.GetAttributeHook(("RenderPipeline", "Render Passes")));
            var cameraHook = Assert.IsType<ObjectHookFunction>(
                SerializableInspectorWidget.GetObjectHook((nameof(Camera), ObjectHookType.Append)));

            using var morphInfo = new AttributeInfo("Morphs", typeof(byte[]));
            var morphContext = new AttributeHookContext
            {
                Info = morphInfo,
                IsUndefined = true,
            };
            var wrongMorphValue = new Variant(42);
            try
            {
                Assert.False(morphHook(in morphContext, ref wrongMorphValue));
            }
            finally
            {
                wrongMorphValue.Dispose();
            }

            var morphValue = new Variant(new byte[] { 0, 128, 255 });
            try
            {
                var morphResult = RenderAttributeHook(
                    "Morph hook test"u8, morphHook, in morphContext, ref morphValue);
                Assert.False(morphResult.Modified);
                Assert.Equal([0, 128, 255], morphValue.GetBuffer());
            }
            finally
            {
                morphValue.Dispose();
            }

            using var renderPassInfo = new AttributeInfo("Render Passes", typeof(VariantVector));
            var renderPassContext = new AttributeHookContext { Info = renderPassInfo };
            var renderPassValues = new VariantVector();
            using (var name = new Variant("Opaque"))
                renderPassValues.Add(name);
            using (var enabled = new Variant(true))
                renderPassValues.Add(enabled);
            var renderPassValue = new Variant(renderPassValues);
            renderPassValues.Dispose();
            try
            {
                var firstFrame = RenderAttributeHook(
                    "Render pass hook test"u8,
                    renderPassHook,
                    in renderPassContext,
                    ref renderPassValue);
                Assert.False(firstFrame.Modified);
                Assert.True(firstFrame.ItemPosition.X > 0.0f);
                Assert.True(firstFrame.ItemPosition.Y > 0.0f);
                Assert.True(renderPassValue.GetVariantVector()[1].GetBool());
            }
            finally
            {
                renderPassValue.Dispose();
            }

            using var scene = new Scene();
            scene.CreateComponent<Octree>();
            var cameraNode = scene.CreateChild("Camera");
            cameraNode.SetWorldPosition(new Vector3f(1.0f, 2.0f, 3.0f));
            cameraNode.SetWorldRotation(new Quaternion(10.0f, 20.0f, 30.0f));
            var camera = cameraNode.CreateComponent<Camera>();
            camera.SetFov(37.0f);
            camera.SetAutoAspectRatio(false);
            camera.SetAspectRatio(2.0f);
            camera.SetDrawDebugGeometry(true);
            IReadOnlyList<WeakReference<Serializable>?> objects =
                [new WeakReference<Serializable>(camera)];

            RenderCameraHook(cameraHook, objects);

            Assert.True(_fixture.Context.GetGlobalVar(CameraHookWidgetVariable)
                .TryGetCustom(out preview));
            Assert.NotNull(preview);
            Assert.Same(scene, preview.Scene);
            Assert.True(preview.IsActive);
            Assert.True(preview.Texture.Width > 0);
            Assert.True(preview.Texture.Height > 0);
            Assert.Equal(37.0f, preview.Camera!.GetFov());
            Assert.False(preview.Camera.GetDrawDebugGeometry());
            Assert.True(cameraNode.GetWorldTransform()
                .ApproximatelyEquivalent(preview.CameraNode!.GetWorldTransform()));

            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
            Assert.False(preview.IsActive);

            RenderCameraHook(cameraHook, objects);
            Assert.True(_fixture.Context.GetGlobalVar(CameraHookWidgetVariable)
                .TryGetCustom(out SceneRendererToTexture? samePreview));
            Assert.Same(preview, samePreview);
            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();

            using var secondScene = new Scene();
            secondScene.CreateComponent<Octree>();
            var secondCamera = secondScene.CreateChild("Camera").CreateComponent<Camera>();
            IReadOnlyList<WeakReference<Serializable>?> secondObjects =
                [new WeakReference<Serializable>(secondCamera)];
            RenderCameraHook(cameraHook, secondObjects);

            Assert.True(_fixture.Context.GetGlobalVar(CameraHookWidgetVariable)
                .TryGetCustom(out SceneRendererToTexture? replacement));
            Assert.NotNull(replacement);
            Assert.NotSame(preview, replacement);
            Assert.Null(preview.Scene);
            preview = replacement;
            Assert.Same(secondScene, preview.Scene);
            _fixture.Context.GetSubsystem<WorkQueue>()!.Update();
        }
        finally
        {
            preview?.Dispose();
            _fixture.Context.SetGlobalVar(CameraHookWidgetVariable, default);
            if (systemUI is not null)
            {
                _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
                systemUI.Dispose();
            }
        }
    }

    private static (bool Modified, Vector2f ItemPosition) RenderAttributeHook(
        ReadOnlySpan<byte> windowTitle,
        AttributeHookFunction hook,
        in AttributeHookContext context,
        ref Variant value)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;

        ImGui.NewFrame();
        var open = true;
        ImGui.Begin(windowTitle, ref open);
        var modified = hook(in context, ref value);
        var itemPosition = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
        ImGui.End();
        ImGui.EndFrame();
        return (modified, itemPosition);
    }

    private static void RenderCameraHook(
        ObjectHookFunction hook,
        IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2f(800.0f, 600.0f);
        io.DeltaTime = 1.0f / 60.0f;

        ImGui.NewFrame();
        ImGui.SetNextWindowSize(new Vector2f(400.0f, 300.0f));
        var open = true;
        ImGui.Begin("Camera hook test"u8, ref open);
        hook(objects);
        Assert.True(ImGui.GetItemRectSize().X > 0.0f);
        Assert.True(ImGui.GetItemRectSize().Y > 0.0f);
        ImGui.End();
        ImGui.EndFrame();
    }
}
