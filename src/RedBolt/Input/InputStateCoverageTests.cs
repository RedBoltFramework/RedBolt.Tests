// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;

using RedBolt.Container;
using RedBolt.Input;
using RedBolt.Math;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputStateCoverageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public InputStateCoverageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Active_input_device_follows_meaningful_control_input()
    {
        var input = _fixture.Input;
        input.Shutdown();

        Assert.Equal(InputDeviceType.KeyboardMouse, input.ActiveInputDevice);
        input.SetControllerAxis(ControllerAxis.LeftX, 0.1f);
        Assert.Equal(InputDeviceType.KeyboardMouse, input.ActiveInputDevice);
        input.SetControllerAxis(ControllerAxis.LeftX, 0.5f);
        Assert.Equal(InputDeviceType.Joystick, input.ActiveInputDevice);

        input.SetScreenButton("Jump", true);
        Assert.Equal(InputDeviceType.Joystick, input.ActiveInputDevice);
        input.SetKey(Key.W, Scancode.W, true);
        Assert.Equal(InputDeviceType.KeyboardMouse, input.ActiveInputDevice);

        input.SetKey(Key.W, Scancode.W, false);
        input.SetScreenButton("Jump", false);
    }

    [Fact]
    public void Touch_state_does_not_synthesize_keyboard_or_mouse_input()
    {
        var input = _fixture.Input;
        var touches = Assert.IsType<Dictionary<int, TouchState>>(
            typeof(Input).GetField("_touches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(input));

        try
        {
            touches[7] = new TouchState { TouchID = 7, Position = new Vector2i(120, 80) };

            Assert.Equal(Vector2i.Zero, input.GetMousePosition());
            Assert.False(input.GetMouseButtonDown(MouseButton.Left));
            Assert.False(input.GetMouseButtonPress(MouseButton.Left));
            Assert.False(input.GetKeyDown(Key.W));
            Assert.False(input.GetScancodeDown(Scancode.W));
        }
        finally
        {
            touches.Clear();
        }
    }

    [Fact]
    public void Joystick_and_touch_value_types_cover_defaults_clone_and_bounds()
    {
        var state = new JoystickState();
        Assert.False(state.IsController());
        Assert.Equal(0u, state.GetNumButtons());
        Assert.Equal(0u, state.GetNumAxes());
        Assert.Equal(0u, state.GetNumHats());
        Assert.False(state.GetButtonDown(0));
        Assert.False(state.GetButtonPress(0));
        Assert.Equal(0.0f, state.GetAxisPosition(0));
        Assert.False(state.HasAxisPosition(0));
        Assert.Equal((int)HatPosition.Center, state.GetHatPosition(0));

        state.Initialize(2, 2, 1);
        state.Buttons[1] = true;
        state.ButtonPress[0] = true;
        state.Axes[1] = 0.5f;
        state.ValidAxis[1] = true;
        state.Hats[0] = (int)HatPosition.Left;
        state.Controller = (nint)1;
        Assert.True(state.IsController());
        Assert.True(state.GetButtonDown(1));
        Assert.True(state.GetButtonPress(0));
        Assert.Equal(0.5f, state.GetAxisPosition(1));
        Assert.True(state.HasAxisPosition(1));
        Assert.Equal((int)HatPosition.Left, state.GetHatPosition(0));
        Assert.False(state.GetButtonDown(99));
        Assert.False(state.GetButtonPress(99));
        Assert.False(state.HasAxisPosition(99));
        Assert.Equal((int)HatPosition.Center, state.GetHatPosition(99));

        var clone = (JoystickState)state.Clone();
        clone.Buttons[1] = false;
        clone.Axes[1] = -1;
        Assert.True(state.Buttons[1]);
        Assert.Equal(0.5f, state.Axes[1]);
        state.Reset();
        Assert.False(state.Buttons[1]);
        Assert.False(state.ValidAxis[1]);
        Assert.Equal((int)HatPosition.Center, state.Hats[0]);

        var touch = new TouchState
        {
            TouchID = 7,
            Position = new Vector2i(1, 2),
            LastPosition = new Vector2i(0, 1),
            Delta = new Vector2i(1, 1),
            Pressure = 0.5f,
            TouchedElement = "button"
        };
        Assert.Equal("button", touch.GetTouchedElement());
    }

    [Fact]
    public void Input_inactive_state_covers_mappings_coordinates_modes_and_resets()
    {
        var input = _fixture.Input;
        input.Shutdown();
        input.SetEnabled(true);
        input.SetMouseMode(MouseMode.Absolute, suppressEvent: true);
        input.SetMouseVisible(true, suppressEvent: true);
        input.SetMouseGrabbed(false, suppressEvent: true);

        Assert.Equal(MouseMode.Absolute, input.GetMouseMode());
        Assert.Equal(Vector2i.Zero, input.GetMousePosition());
        Assert.Equal(Vector2i.Zero, input.GetGlobalWindowPosition());
        Assert.Equal(Vector2i.Zero, input.GetGlobalWindowSize());
        Assert.Equal(Vector2i.Zero, input.GetBackbufferSize());
        Assert.Equal(Vector2f.Zero, input.GetRelativeMousePosition());
        Assert.Equal(Vector2i.Zero, input.GetMouseMove());
        Assert.Equal(0, input.GetMouseMoveX());
        Assert.Equal(0, input.GetMouseMoveY());
        Assert.Equal(0, input.GetMouseMoveWheel());
        Assert.Null(input.GetTouch(uint.MaxValue));
        _ = input.GetTouchById(int.MinValue);
        _ = input.GetJoystick(int.MinValue);
        Assert.Null(input.GetJoystickByIndex(uint.MaxValue));
        Assert.Null(input.GetJoystickByName("missing"));
        Assert.Throws<ArgumentNullException>(() => input.GetJoystickByName(null!));
        Assert.False(input.IsMouseLocked());

        Assert.Equal("LMB", Input.GetMouseButtonName(MouseButton.Left));
        Assert.Equal("MMB", Input.GetMouseButtonName(MouseButton.Middle));
        Assert.Equal("RMB", Input.GetMouseButtonName(MouseButton.Right));
        Assert.Equal("X1", Input.GetMouseButtonName(MouseButton.X1));
        Assert.Equal("X2", Input.GetMouseButtonName(MouseButton.X2));
        Assert.Equal("?", Input.GetMouseButtonName((MouseButton)128));

        Assert.False(input.GetQualifierDown(Qualifier.Any));
        Assert.False(input.GetQualifierPress(Qualifier.Any));
        input.SetKey(Key.LeftShift, Scancode.LeftShift, true);
        input.SetKey(Key.LeftCtrl, Scancode.LeftCtrl, true);
        input.SetKey(Key.LeftAlt, Scancode.LeftAlt, true);
        Assert.True(input.GetQualifierDown(Qualifier.Any));
        Assert.True(input.GetQualifierDown(Qualifier.Shift));
        Assert.True(input.GetQualifierPress(Qualifier.Any));
        input.SetKey(Key.LeftShift, Scancode.LeftShift, false);
        input.SetKey(Key.LeftCtrl, Scancode.LeftCtrl, false);
        input.SetKey(Key.LeftAlt, Scancode.LeftAlt, false);

        input.SetKey(Key.A, Scancode.A, true);
        Assert.True(input.GetKeyDown(Key.A));
        Assert.True(input.GetKeyPress(Key.A));
        Assert.True(input.GetScancodeDown(Scancode.A));
        Assert.True(input.GetScancodePress(Scancode.A));
        input.SetKey(Key.A, Scancode.A, false);
        Assert.False(input.GetKeyDown(Key.A));
        input.SetKey(Key.B, Scancode.Unknown, true);
        Assert.True(input.GetKeyDown(Key.B));
        Assert.False(input.GetScancodeDown(Scancode.Unknown));
        input.SetKey(Key.B, Scancode.Unknown, false);

        input.SetMouseButton(MouseButton.Left, true, clicks: 2);
        Assert.True(input.GetMouseButtonDown(MouseButton.Left));
        Assert.True(input.GetMouseButtonPress(MouseButton.Left));
        input.SetMouseButton(MouseButton.Left, false);
        Assert.True(input.GetMouseButtonClick(MouseButton.Left));
        Assert.False(input.GetMouseButtonDown(MouseButton.Left));
        input.SetMouseButton(MouseButton.Right, false);
        input.SetMouseWheel(0);
        input.SetMouseWheel(2);
        Assert.Equal(2, input.GetMouseMoveWheel());

        input.SetControllerAxis(ControllerAxis.LeftX, 4);
        input.SetJoystickAxis(2, -4);
        Assert.Equal(1.0f, input.GetControllerAxisPosition(ControllerAxis.LeftX));
        Assert.Equal(-1.0f, input.GetJoystickAxisPosition(2));
        Assert.True(input.HasJoystickAxisPosition(2));
        input.SetControllerButton(ControllerButton.A, true);
        input.SetJoystickButton(4, true);
        Assert.True(input.GetControllerButtonDown(ControllerButton.A));
        Assert.True(input.GetJoystickButtonDown(4));
        input.SetControllerButton(ControllerButton.A, false);
        input.SetJoystickButton(4, false);
        input.SetHatPosition(HatPosition.Down);
        Assert.Equal(HatPosition.Down, input.GetHatPosition());

        input.SetScreenButton("fire", true);
        Assert.True(input.GetScreenButtonDown("fire"));
        input.SetScreenButton("fire", false);
        Assert.False(input.GetScreenButtonDown("fire"));
        Assert.Throws<ArgumentException>(() => input.SetScreenButton(" ", true));
        Assert.Throws<ArgumentException>(() => input.GetScreenButtonDown(" "));

        input.SetMouseMode(MouseMode.Relative);
        Assert.Equal(MouseMode.Relative, input.GetMouseMode());
        Assert.True(input.IsMouseLocked());
        input.SetMouseVisible(true);
        Assert.False(input.IsMouseVisible());
        input.SetMouseMode(MouseMode.Wrap);
        input.SetMouseGrabbed(true);
        input.SetMouseMode(MouseMode.Free);
        Assert.False(input.IsMouseLocked());
        input.ResetMouseMode();
        input.ResetMouseVisible();
        input.ResetMouseGrabbed();
        input.SetToggleFullscreen(false);
        Assert.False(input.GetToggleFullscreen());
        input.SetTouchEmulation(true);
        Assert.True(input.GetTouchEmulation());
        input.SetTouchEmulation(false);
        Assert.False(input.GetTouchEmulation());
        input.SetMouseEmulation(true);
        Assert.True(input.GetMouseEmulation());
        Assert.False(input.GetTouchEmulation());
        input.SetTouchEmulation(true);
        Assert.True(input.GetTouchEmulation());
        Assert.False(input.GetMouseEmulation());
        input.SetTouchEmulation(false);
        input.CenterMousePosition();
        input.Shutdown();
    }
}
