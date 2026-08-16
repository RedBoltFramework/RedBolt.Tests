// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class CameraControllerTests
{
    private readonly GraphicsEngineFixture _fixture;

    public CameraControllerTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [NativeBindingFact]
    public void Settings_render_and_serialize_native_values()
    {
        var settings = new CameraController.Settings();
        Assert.Equal("Editor.Scene:Camera", settings.UniqueName);
        Assert.Equal(0.25f, settings.MouseSensitivity);
        Assert.Equal(2.0f, settings.MinSpeed);
        Assert.Equal(10.0f, settings.MaxSpeed);
        Assert.Equal(3.5f, settings.ScrollSpeed);
        Assert.Equal(1.0f, settings.Acceleration);
        Assert.Equal(4.0f, settings.ShiftFactor);
        Assert.Equal(10.0f, settings.FocusDistance);
        Assert.Equal(17.0f, settings.FocusSpeed);
        Assert.False(settings.Orthographic);
        Assert.Equal(10.0f, settings.OrthoSize);

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
            var open = true;
            ImGui.Begin("Camera settings test"u8, ref open);
            windowStarted = true;

            using var drawList = ImGui.GetWindowDrawList();
            var verticesBefore = drawList.VtxBufferSize;
            settings.RenderSettings();
            Assert.True(drawList.VtxBufferSize > verticesBefore);
            Assert.Equal(10.0f, settings.OrthoSize);
        }
        finally
        {
            if (windowStarted)
                ImGui.End();
            if (frameStarted)
                ImGui.EndFrame();
            ImGui.DestroyContext(imguiContext);
        }

        settings.MouseSensitivity = 0.5f;
        settings.FocusSpeed = 31.0f;
        settings.Orthographic = true;
        settings.OrthoSize = 25.0f;
        using var file = new JSONFile();
        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("Settings");
            settings.SerializeInBlock(archive);
        }));
        var json = file.ToString();
        Assert.Contains("MouseSensitivity", json, StringComparison.Ordinal);
        Assert.Contains("OrthographicSize", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusSpeed", json, StringComparison.Ordinal);

        var loaded = new CameraController.Settings { FocusSpeed = 47.0f };
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("Settings");
            loaded.SerializeInBlock(archive);
        }));
        Assert.Equal(0.5f, loaded.MouseSensitivity);
        Assert.True(loaded.Orthographic);
        Assert.Equal(25.0f, loaded.OrthoSize);
        Assert.Equal(47.0f, loaded.FocusSpeed);
    }

    [NativeBindingFact]
    public void Page_state_looks_at_bounds_and_restores_serialized_view()
    {
        var state = new CameraController.PageState();
        Assert.Equal(new Vector3f(0.0f, 5.0f, -10.0f), state.LastCameraPosition);

        var box = new BoundingBox(-1.0f, 1.0f);
        state.LookAt(box);
        var expectedPosition = box.Center + box.Size.Length * Vector3f.One;
        Assert.Equal(expectedPosition, state.LastCameraPosition);
        Assert.True((state.LastCameraRotation * Vector3f.Forward)
            .ApproximatelyEquivalent((box.Center - expectedPosition).Normalized));

        using var file = new JSONFile();
        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("State");
            state.SerializeInBlock(archive);
        }));

        var loaded = new CameraController.PageState
        {
            CurrentMoveSpeed = 91.0f,
            PendingOffset = new Vector3f(2.0f, 3.0f, 4.0f),
            OrbitPosition = new Vector3f(5.0f, 6.0f, 7.0f),
        };
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("State");
            loaded.SerializeInBlock(archive);
        }));
        Assert.True(state.LastCameraPosition.ApproximatelyEquivalent(
            loaded.LastCameraPosition, MathDefs.LargeEpsilon));
        Assert.True(state.LastCameraRotation.Equivalent(loaded.LastCameraRotation));
        Assert.Equal(loaded.LastCameraRotation.YawAngle, loaded.Yaw);
        Assert.Equal(loaded.LastCameraRotation.PitchAngle, loaded.Pitch);
        Assert.Equal(91.0f, loaded.CurrentMoveSpeed);
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), loaded.PendingOffset);
        Assert.Equal(new Vector3f(5.0f, 6.0f, 7.0f), loaded.OrbitPosition);
    }

    [NativeBindingFact]
    public void Process_input_binds_hotkeys_applies_camera_settings_and_smooths_focus()
    {
        _fixture.ResetGraphics();
        var systemUI = new DearImGuiSubsystem();
        _fixture.Context.RegisterSubsystem(systemUI);
        using var hotkeyManager = new HotkeyManager();
        using var controller = new CameraController(hotkeyManager);
        using var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>();

        try
        {
            Assert.Equal(9, hotkeyManager.Bindings.Count);
            Assert.Contains("EditorCamera.MoveForward", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveBackward", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveLeft", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveRight", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveUp", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveDown", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.MoveAccelerate", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.LookAround", hotkeyManager.Bindings.Keys);
            Assert.Contains("EditorCamera.OrbitAround", hotkeyManager.Bindings.Keys);

            camera.SetAspectRatioInternal(1.75f);
            cameraNode.SetPosition(new Vector3f(100.0f, 100.0f, 100.0f));
            cameraNode.SetRotation(new Quaternion(20.0f, 40.0f, 0.0f));
            var state = new CameraController.PageState
            {
                LastCameraPosition = new Vector3f(1.0f, 2.0f, 3.0f),
                LastCameraRotation = new Quaternion(),
                CurrentMoveSpeed = 99.0f,
                PendingOffset = new Vector3f(0.0f, 0.0f, 4.0f),
                OrbitPosition = new Vector3f(9.0f, 9.0f, 9.0f),
            };
            var settings = new CameraController.Settings
            {
                MinSpeed = 3.0f,
                FocusSpeed = 17.0f,
                Orthographic = true,
                OrthoSize = 23.0f,
            };

            var frameStarted = false;
            var windowStarted = false;
            var timeStarted = false;
            try
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2f(128.0f, 128.0f);
                io.DeltaTime = 0.25f;
                ImGui.NewFrame();
                frameStarted = true;
                ImGui.SetNextWindowSize(new Vector2f(96.0f, 96.0f));
                var open = true;
                ImGui.Begin("Camera controller test"u8, ref open);
                windowStarted = true;
                ImGui.TextUnformatted("Camera"u8);

                _fixture.Time.BeginFrame(0.25f);
                timeStarted = true;
                controller.ProcessInput(camera, ref state, settings);
            }
            finally
            {
                if (timeStarted)
                    _fixture.Time.EndFrame();
                if (windowStarted)
                    ImGui.End();
                if (frameStarted)
                    ImGui.EndFrame();
            }

            var factor = MathDefs.InverseExponentialDecay(17.0f * 0.25f);
            Assert.True(camera.Orthographic);
            Assert.Equal(23.0f, camera.OrthoSize);
            Assert.Equal(1.75f, camera.AspectRatio);
            Assert.Equal(3.0f, state.CurrentMoveSpeed);
            Assert.Null(state.OrbitPosition);
            Assert.True(new Vector3f(1.0f, 2.0f, 3.0f + 4.0f * factor)
                .ApproximatelyEquivalent(cameraNode.Position));
            Assert.True(new Vector3f(0.0f, 0.0f, 4.0f * (1.0f - factor))
                .ApproximatelyEquivalent(state.PendingOffset));
            Assert.Equal(cameraNode.Position, state.LastCameraPosition);
            Assert.Equal(cameraNode.Rotation, state.LastCameraRotation);
        }
        finally
        {
            _fixture.Context.RemoveSubsystem<DearImGuiSubsystem>();
            systemUI.Dispose();
        }
    }
}
