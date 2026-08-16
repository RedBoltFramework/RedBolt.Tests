// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System.Reflection;

using ImGuizmo;
using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class TransformGizmoTests
{
    private readonly GraphicsEngineFixture _fixture;

    public TransformGizmoTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Generated_imguizmo_binding_maps_axes_and_runs_a_frame()
    {
        _fixture.ResetGraphics();
        var imguiContext = ImGui.CreateContext();
        var frameStarted = false;
        try
        {
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2f(640.0f, 480.0f);
            io.DeltaTime = 1.0f / 60.0f;
            io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsRendererHasTextures;
            ImGui.NewFrame();
            ImGui.BeginFrame();
            frameStarted = true;

            using var camera = new Camera();
            var gizmo = new TransformGizmo(camera);
            var transform = new Matrix4();

            Assert.Null(gizmo.ManipulateTransform(
                ref transform, TransformGizmoOperation.None, TransformGizmoAxis.None, false, Vector3f.Zero));
            Assert.Null(gizmo.ManipulateTransform(
                ref transform, TransformGizmoOperation.Translate,
                TransformGizmoAxis.X | TransformGizmoAxis.Z, false, Vector3f.Zero));
            Assert.True(transform.ApproximatelyEquivalent(new Matrix4()));

            Assert.Equal(
                Operation.TranslateX | Operation.TranslateZ,
                GetInternalOperation(TransformGizmoOperation.Translate,
                    TransformGizmoAxis.X | TransformGizmoAxis.Z));
            Assert.Equal(
                Operation.RotateY | Operation.RotateScreen,
                GetInternalOperation(TransformGizmoOperation.Rotate,
                    TransformGizmoAxis.Y | TransformGizmoAxis.Screen));
            Assert.Equal(
                Operation.ScaleXu | Operation.ScaleYu | Operation.ScaleZu,
                GetInternalOperation(TransformGizmoOperation.Scale,
                    TransformGizmoAxis.X | TransformGizmoAxis.Universal));
        }
        finally
        {
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
        }
    }

    [Fact]
    public void Node_gizmo_uses_active_single_and_center_transforms()
    {
        using var scene = new Scene();
        var first = scene.CreateChild("First");
        first.Position = new Vector3f(0.0f, 0.0f, 0.0f);
        var second = scene.CreateChild("Second");
        second.Position = new Vector3f(4.0f, 2.0f, 0.0f);

        using var multiple = new TransformNodesGizmo(second,
        [
            new WeakReference<Node>(first),
            new WeakReference<Node>(second),
        ]);
        Assert.Equal(second.WorldPosition, GetGizmoTransform(multiple, true).Translation);
        Assert.Equal(new Vector3f(2.0f, 1.0f, 0.0f), GetGizmoTransform(multiple, false).Translation);
        Assert.False(multiple.Manipulate(
            null!, TransformGizmoOperation.None, TransformGizmoAxis.None, false, false, Vector3f.Zero));

        using var single = new TransformNodesGizmo(first);
        Assert.Equal(first.WorldPosition, GetGizmoTransform(single, false).Translation);
    }

    private static Operation GetInternalOperation(
        TransformGizmoOperation operation, TransformGizmoAxis axes)
    {
        return Assert.IsType<Operation>(typeof(TransformGizmo).GetMethod(
                "GetInternalOperation", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [operation, axes]));
    }

    private static Matrix4 GetGizmoTransform(TransformNodesGizmo gizmo, bool pivoted)
    {
        return Assert.IsType<Matrix4>(typeof(TransformNodesGizmo).GetMethod(
                "GetGizmoTransform", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(gizmo, [pivoted]));
    }
}
