namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Input;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputCoverageGapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public InputCoverageGapTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private Input Input => _fixture.Input;

    private void ResetInput()
    {
        Input.Shutdown();
        Input.SetEnabled(true);
    }

    [Fact]
    public void Keyboard_modifiers_and_mouse_mappings_cover_active_and_inactive_paths()
    {
        ResetInput();
        using var map = new InputMap();
        map.MapKeyboardKey("Keyboard", Scancode.A);
        map.MapKeyboardKey("Modifier", Scancode.Ctrl);
        map.MapKeyboardKey("Modifier", Scancode.Shift);
        map.MapKeyboardKey("Modifier", Scancode.Alt);

        Assert.Equal(0.0f, map.Evaluate("Keyboard"));
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(Input.GetScancodeDown(Scancode.A));
        Assert.Equal(1.0f, map.Evaluate("Keyboard"));
        Input.SetKey(Key.A, Scancode.A, false);

        foreach (var (scancode, key) in new[]
        {
            (Scancode.RightCtrl, Key.RightCtrl),
            (Scancode.RightShift, Key.RightShift),
            (Scancode.RightAlt, Key.RightAlt)
        })
        {
            Assert.Equal(0.0f, map.Evaluate("Modifier"));
            Input.SetKey(key, scancode, true);
            Assert.True(Input.GetKeyDown(key));
            Assert.Equal(1.0f, map.Evaluate("Modifier"));
            Input.SetKey(key, scancode, false);
            Assert.False(Input.GetKeyDown(key));
            Assert.Equal(0.0f, map.Evaluate("Modifier"));
        }

        var mouseMapping = new InputMap.ActionMapping();
        mouseMapping.MouseButtons.Add(new MouseButtonMapping(0));
        Assert.False(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.Equal(0.0f, mouseMapping.Evaluate(Input, 0.1f, -1));

        Input.SetMouseButton(MouseButton.Left, true);
        Assert.True(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.Equal(1.0f, mouseMapping.Evaluate(Input, 0.1f, -1));
        Input.SetMouseButton(MouseButton.Left, false);
        Assert.False(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.Equal(0.0f, mouseMapping.Evaluate(Input, 0.1f, -1));
    }

    [Fact]
    public void Action_mapping_evaluates_generic_and_controller_buttons_and_hats()
    {
        ResetInput();
        var genericMapping = new InputMap.ActionMapping();
        genericMapping.ControllerButtons.Add(new ControllerButtonMapping(99));
        genericMapping.ControllerButtons.Add(new ControllerButtonMapping(7));
        genericMapping.ControllerHats.Add(new ControllerHatMapping((uint)HatPosition.Down));
        genericMapping.ControllerHats.Add(new ControllerHatMapping((uint)HatPosition.Up));

        Input.SetJoystickButton(7, true);
        Assert.True(Input.GetJoystickButtonDown(7));
        Assert.False(Input.GetControllerButtonDown(ControllerButton.B));
        var genericJoystick = Input.GetJoystickByIndex(0);
        Assert.True(genericJoystick.HasValue);
        Assert.False(genericJoystick.Value.IsController());
        Assert.True(genericJoystick.Value.GetButtonDown(7));
        Assert.Equal(1.0f, genericMapping.Evaluate(Input, 0.1f, -1));

        Input.SetJoystickButton(7, false);
        Input.SetHatPosition(HatPosition.RightUp);
        Assert.Equal(HatPosition.RightUp, Input.GetHatPosition());
        genericJoystick = Input.GetJoystick(0);
        Assert.True(genericJoystick.HasValue);
        Assert.Equal(1u, genericJoystick.Value.GetNumHats());
        Assert.Equal((int)HatPosition.RightUp, genericJoystick.Value.GetHatPosition(0));
        Assert.Equal(1.0f, genericMapping.Evaluate(Input, 0.1f, -1));

        Input.SetHatPosition(HatPosition.Center);
        Assert.Equal(0.0f, genericMapping.Evaluate(Input, 0.1f, -1));

        ResetInput();
        var controllerMapping = new InputMap.ActionMapping();
        controllerMapping.ControllerButtons.Add(new ControllerButtonMapping((uint)ControllerButton.B));
        controllerMapping.ControllerButtons.Add(new ControllerButtonMapping(ControllerButton.B));

        Input.SetControllerButton(ControllerButton.B, true);
        Assert.True(Input.GetControllerButtonDown(ControllerButton.B));
        Assert.False(Input.GetJoystickButtonDown((uint)ControllerButton.B));
        var controller = Input.GetJoystick(0);
        Assert.True(controller.HasValue);
        Assert.True(controller.Value.IsController());
        Assert.True(controller.Value.GetButtonDown((uint)ControllerButton.B));
        Assert.Equal(1.0f, controllerMapping.Evaluate(Input, 0.1f, -1));

        Input.SetControllerButton(ControllerButton.B, false);
        Assert.False(Input.GetControllerButtonDown(ControllerButton.B));
        Assert.Equal(0.0f, controllerMapping.Evaluate(Input, 0.1f, -1));
    }

    [Fact]
    public void Controller_axes_cover_positive_negative_dead_zone_and_clamp_paths()
    {
        ResetInput();
        using var map = new InputMap();
        map.SetDeadZone(0.2f);
        map.MapControllerAxis("Positive", ControllerAxis.LeftX, 0.0f, 1.0f);
        map.MapControllerAxis("Negative", ControllerAxis.LeftY, 0.0f, -1.0f);
        map.MapControllerAxis("Combined", ControllerAxis.LeftX, 0.0f, 1.0f);
        map.MapControllerAxis("Combined", ControllerAxis.LeftY, 0.0f, -1.0f);

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Assert.Equal(0.0f, Input.GetControllerAxisPosition(ControllerAxis.LeftX));
        Assert.Equal(0.0f, map.Evaluate("Positive"));

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.2f);
        Assert.Equal(0.0f, map.Evaluate("Positive"));
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.6f);
        Assert.InRange(map.Evaluate("Positive"), 0.49f, 0.51f);
        Input.SetControllerAxis(ControllerAxis.LeftX, 1.0f);
        Assert.Equal(1.0f, map.Evaluate("Positive"));

        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
        Assert.Equal(0.0f, map.Evaluate("Negative"));
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.2f);
        Assert.Equal(0.0f, map.Evaluate("Negative"));
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.6f);
        Assert.InRange(map.Evaluate("Negative"), 0.49f, 0.51f);
        Input.SetControllerAxis(ControllerAxis.LeftY, -1.0f);
        Assert.Equal(1.0f, map.Evaluate("Negative"));
        Assert.Equal(1.0f, map.Evaluate("Combined"));

        var joystick = Input.GetJoystick(0);
        Assert.True(joystick.HasValue);
        Assert.True(joystick.Value.IsController());
        Assert.True(joystick.Value.HasAxisPosition((uint)ControllerAxis.LeftX));
        Assert.Equal(-1.0f, joystick.Value.GetAxisPosition((uint)ControllerAxis.LeftY));
    }

    [Fact]
    public void Generic_axes_cover_public_state_queries_missing_axes_and_ignored_joysticks()
    {
        ResetInput();
        using var map = new InputMap();
        map.MapJoystickAxis("Generic", 0, 0.0f, 1.0f);

        Input.SetJoystickAxis(0, 1.0f);
        Input.SetJoystickAxis(1, 0.0f);
        Input.SetJoystickAxis(2, 0.0f);
        Assert.Equal(1.0f, Input.GetJoystickAxisPosition(0));
        Assert.True(Input.HasJoystickAxisPosition(0));
        Assert.Equal(1u, Input.GetNumJoysticks());
        Assert.Null(Input.GetJoystickByIndex(1));
        Assert.Equal(1.0f, map.Evaluate("Generic"));

        var joystick = Input.GetJoystick(0);
        Assert.True(joystick.HasValue);
        Assert.False(joystick.Value.IsController());
        Assert.Equal(3u, joystick.Value.GetNumAxes());
        Assert.True(joystick.Value.HasAxisPosition(2));

        var missingAxis = new InputMap.ActionMapping();
        missingAxis.ControllerAxes.Add(new ControllerAxisMapping(5, 0.0f, 1.0f));
        Assert.Equal(0.0f, missingAxis.Evaluate(Input, 0.1f, -1));

        var ignoredJoystick = new InputMap.ActionMapping();
        ignoredJoystick.ControllerAxes.Add(new ControllerAxisMapping(0u, 0.0f, 1.0f));
        Assert.Equal(0.0f, ignoredJoystick.Evaluate(Input, 0.1f, 0));
    }

    [Fact]
    public void Duplicate_missing_and_screen_mappings_have_deterministic_results()
    {
        ResetInput();
        using var map = new InputMap();
        map.MapKeyboardKey("Duplicate", Scancode.A);
        map.MapKeyboardKey("Duplicate", Scancode.A);
        map.MapKeyboardKey("OtherAction", Scancode.A);
        map.MapControllerButton("DuplicateButton", ControllerButton.A);
        map.MapControllerButton("DuplicateButton", ControllerButton.A);
        map.MapControllerAxis("DuplicateAxis", ControllerAxis.LeftX, 0.0f, 1.0f);
        map.MapControllerAxis("DuplicateAxis", ControllerAxis.LeftX, 0.0f, 1.0f);
        map.MapScreenButton("Screen", "FireButton");

        Assert.Equal(2, map.GetMapping("Duplicate").KeyboardKeys.Count);
        Assert.Equal(2, map.GetMapping("DuplicateButton").ControllerButtons.Count);
        Assert.Equal(2, map.GetMapping("DuplicateAxis").ControllerAxes.Count);
        Assert.Empty(map.GetMapping("Missing").KeyboardKeys);
        Assert.Equal(0.0f, map.Evaluate("Missing"));
        Assert.Equal(0.0f, map.EvaluateByHash(new StringHash("Missing")));

        Input.SetKey(Key.A, Scancode.A, true);
        Assert.Equal(1.0f, map.Evaluate("Duplicate"));
        Assert.Equal(1.0f, map.Evaluate("OtherAction"));
        Assert.Equal(0.0f, map.EvaluateByHash(new StringHash("Duplicate")));
        Input.SetKey(Key.A, Scancode.A, false);

        Input.SetScreenButton("FireButton", true);
        Assert.True(Input.GetScreenButtonDown("FireButton"));
        // ScreenButton evaluation is not wired until the built-in UI port is available.
        Assert.Equal(0.0f, map.Evaluate("Screen"));
        Input.SetScreenButton("FireButton", false);
        Assert.False(Input.GetScreenButtonDown("FireButton"));
    }
}
