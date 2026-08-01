namespace RedBolt.Tests;

using RedBolt.Input;
using RedBolt.Math;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputBoundaryTests
{
    private readonly HeadlessEngineFixture _fixture;

    public InputBoundaryTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Input_constants_preserve_masks_and_native_sentinel_values()
    {
        Assert.Equal(0u, (uint)MouseButton.None);
        Assert.Equal(uint.MaxValue, (uint)MouseButton.Any);
        Assert.Equal(1u, (uint)Qualifier.Shift);
        Assert.Equal(8u, (uint)Qualifier.Any);
        Assert.Equal((uint)Key.Unknown, (uint)RedBolt.Bindings.Sdl.SdlKeyCode.SdlkUnknown);
        Assert.Equal(0u, (uint)Scancode.Unknown);
    }

    [Fact]
    public void Invalid_joystick_lookups_and_disabled_input_are_deterministic()
    {
        var input = _fixture.Input;
        // ID lookup returns a value-type default for an unknown ID; index/name
        // lookups are the nullable bounds-checked APIs.
        Assert.True(input.GetJoystick(int.MaxValue).HasValue);
        Assert.Null(input.GetJoystickByIndex(uint.MaxValue));
        Assert.Null(input.GetJoystickByName("does-not-exist"));

        // Controller state is retained independently of the enabled flag. Set
        // the queried axis explicitly so this boundary test is isolated from
        // earlier input-event tests in the shared headless fixture.
        input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        input.SetEnabled(false);
        try
        {
            Assert.Equal(0.0f, input.GetControllerAxisPosition(ControllerAxis.LeftX));
            Assert.False(input.GetControllerButtonDown(ControllerButton.A));
            Assert.Equal(Vector2f.Zero, input.GetRelativeMousePosition());
        }
        finally
        {
            input.SetEnabled(true);
        }
    }

    [Fact]
    public void Focus_loss_event_clears_adapter_state_without_stale_direction()
    {
        var input = _fixture.Input;
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        input.SetKey(Key.A, Scancode.A, true);
        Assert.NotEqual(Vector2f.Zero, aggregator.GetDirection());

        var focus = new InputEvents.InputFocusEvent { Focus = false, Minimized = true };
        input.SendEvent(ref focus);

        Assert.Equal(Vector2f.Zero, aggregator.GetDirection());
        input.SetKey(Key.A, Scancode.A, false);
    }

    [Fact]
    public void Input_event_payloads_have_zero_defaults_and_preserve_fields()
    {
        var empty = new InputEvents.MouseMoveEvent();
        Assert.Equal(0, empty.X);
        Assert.Equal(0, empty.DX);
        Assert.Equal(Qualifier.None, empty.Qualifiers);

        var eventData = new InputEvents.TouchMoveEvent
        {
            TouchID = 3, X = 10, Y = 11, DX = -2, DY = 4, Pressure = 0.75f
        };
        Assert.Equal((3, 10, 11, -2, 4, 0.75f),
            (eventData.TouchID, eventData.X, eventData.Y, eventData.DX, eventData.DY, eventData.Pressure));
    }
}
