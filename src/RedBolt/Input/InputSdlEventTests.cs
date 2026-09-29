// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests;

using RedBolt.Bindings.Sdl;
using RedBolt.Graphics;
using RedBolt.Input;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class InputSdlEventTests
{
    private readonly GraphicsEngineFixture _fixture;

    public InputSdlEventTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private Input Input => _fixture.Input;

    [GraphicsFact]
    public void Active_input_device_switches_with_sdl_input()
    {
        Input.SetControllerButton(ControllerButton.A, true);
        Assert.Equal(InputDeviceType.Joystick, Input.ActiveInputDevice);

        using var fingerDown = new SdlEvent();
        fingerDown.Type = (uint)SdlEventType.SdlFingerdown;
        fingerDown.TfingerFingerId = 42;
        fingerDown.TfingerX = 0.25f;
        fingerDown.TfingerY = 0.5f;
        fingerDown.TfingerPressure = 1.0f;
        Assert.Equal(1, Sdl.SdlPushEvent(fingerDown));
        Input.BeginFrame();
        Input.PollEvents();
        Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);

        using var touchMouseMotion = new SdlEvent();
        touchMouseMotion.Type = (uint)SdlEventType.SdlMousemotion;
        touchMouseMotion.MotionWhich = uint.MaxValue;
        touchMouseMotion.MotionXrel = 1;
        Assert.Equal(1, Sdl.SdlPushEvent(touchMouseMotion));
        Input.PollEvents();
        Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);

        Input.SetScreenButton("Jump", true);

        using var keyDown = new SdlEvent();
        keyDown.Type = (uint)SdlEventType.SdlKeydown;
        keyDown.KeyKeysymScancode = SdlScancode.SdlScancodeW;
        keyDown.KeyKeysymSym = (int)SdlKeyCode.SdlkW;
        Assert.Equal(1, Sdl.SdlPushEvent(keyDown));
        Input.PollEvents();
        Assert.Equal(InputDeviceType.KeyboardMouse, Input.ActiveInputDevice);
        // Leaving touch modality releases on-screen controls.
        Assert.False(Input.GetScreenButtonDown("Jump"));

        using var fingerUp = new SdlEvent();
        fingerUp.Type = (uint)SdlEventType.SdlFingerup;
        fingerUp.TfingerFingerId = 42;
        fingerUp.TfingerX = 0.25f;
        fingerUp.TfingerY = 0.5f;
        Assert.Equal(1, Sdl.SdlPushEvent(fingerUp));
        Input.SetScreenButton("Jump", false);
        Input.SetControllerButton(ControllerButton.A, false);
        Input.SetKey(Key.W, Scancode.W, false);
        Input.PollEvents();
    }

    [GraphicsFact]
    public void Touch_emulation_keeps_mouse_events_in_touch_modality()
    {
        Input.SetTouchEmulation(true);
        try
        {
            using var mouseDown = new SdlEvent();
            mouseDown.Type = (uint)SdlEventType.SdlMousebuttondown;
            mouseDown.ButtonButton = 1;
            mouseDown.ButtonClicks = 1;
            Assert.Equal(1, Sdl.SdlPushEvent(mouseDown));
            Input.BeginFrame();
            Input.PollEvents();
            Input.PollEvents();

            Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);
            Assert.False(Input.GetMouseButtonDown(MouseButton.Left));
            Assert.Equal(1u, Input.GetNumTouches());

            Input.SetScreenButton("Move", true);
            using var mouseMotion = new SdlEvent();
            mouseMotion.Type = (uint)SdlEventType.SdlMousemotion;
            mouseMotion.MotionXrel = 10;
            Assert.Equal(1, Sdl.SdlPushEvent(mouseMotion));
            Input.PollEvents();
            Input.PollEvents();

            Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);
            Assert.True(Input.GetScreenButtonDown("Move"));
        }
        finally
        {
            using var mouseUp = new SdlEvent();
            mouseUp.Type = (uint)SdlEventType.SdlMousebuttonup;
            mouseUp.ButtonButton = 1;
            Sdl.SdlPushEvent(mouseUp);
            Input.PollEvents();
            Input.PollEvents();
            Input.SetScreenButton("Move", false);
            Input.SetTouchEmulation(false);
            Input.SetKey(Key.W, Scancode.W, true);
            Input.SetKey(Key.W, Scancode.W, false);
        }
    }

    // On-screen controls are driven through screen buttons and axes (see InputOverlay) and are only active in touch
    // modality.
    [GraphicsFact]
    public void Screen_controls_follow_active_input_device()
    {
        try
        {
            PushFingerEvent(SdlEventType.SdlFingerdown, 51, 0.8f, 0.75f);
            Input.BeginFrame();
            Input.PollEvents();
            Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);

            Input.SetScreenButton("Action", true);
            Input.SetScreenAxis("Move", 0.5f);
            Assert.True(Input.GetScreenButtonDown("Action"));
            Assert.True(Input.GetScreenButtonPress("Action"));
            Assert.Equal(0.5f, Input.GetScreenAxisPosition("Move"));
            Assert.False(Input.GetKeyDown(Key.Space));

            using var keyDown = new SdlEvent();
            keyDown.Type = (uint)SdlEventType.SdlKeydown;
            keyDown.KeyKeysymScancode = SdlScancode.SdlScancodeA;
            keyDown.KeyKeysymSym = (int)SdlKeyCode.SdlkA;
            Assert.Equal(1, Sdl.SdlPushEvent(keyDown));
            Input.PollEvents();

            Assert.Equal(InputDeviceType.KeyboardMouse, Input.ActiveInputDevice);
            Assert.False(Input.GetScreenButtonDown("Action"));
            Assert.False(Input.GetScreenButtonPress("Action"));
            Assert.Equal(0.0f, Input.GetScreenAxisPosition("Move"));

            PushFingerEvent(SdlEventType.SdlFingerdown, 52, 0.2f, 0.66f);
            Input.BeginFrame();
            Input.PollEvents();
            Assert.Equal(InputDeviceType.Touch, Input.ActiveInputDevice);

            Input.SetScreenButton("Action", true);
            Assert.True(Input.GetScreenButtonDown("Action"));

            PushFingerEvent(SdlEventType.SdlFingerup, 52, 0.2f, 0.66f);
            PushFingerEvent(SdlEventType.SdlFingerup, 51, 0.8f, 0.75f);
            Input.PollEvents();

            using var keyUp = new SdlEvent();
            keyUp.Type = (uint)SdlEventType.SdlKeyup;
            keyUp.KeyKeysymScancode = SdlScancode.SdlScancodeA;
            keyUp.KeyKeysymSym = (int)SdlKeyCode.SdlkA;
            Assert.Equal(1, Sdl.SdlPushEvent(keyUp));
            Input.PollEvents();
        }
        finally
        {
            Input.SetScreenButton("Action", false);
            Input.SetScreenAxis("Move", 0.0f);
            Input.SetKey(Key.A, Scancode.A, false);
        }
    }

    [GraphicsFact]
    public void Escape_key_does_not_request_exit()
    {
        InputEvents.KeyDownEvent keyDownEvent = default;
        var exitRequested = false;
        using var receiver = new TestEventReceiver();
        receiver.SubscribeToEvent<InputEvents.KeyDownEvent>(
            (ref InputEvents.KeyDownEvent value) => keyDownEvent = value);
        receiver.SubscribeToEvent<InputEvents.ExitRequestedEvent>(
            (ref InputEvents.ExitRequestedEvent _) => exitRequested = true);

        using var focusGained = new SdlEvent();
        focusGained.Type = (uint)SdlEventType.SdlWindowevent;
        focusGained.WindowEvent = (byte)SdlWindowEventID.SdlWindoweventFocusGained;
        Assert.Equal(1, Sdl.SdlPushEvent(focusGained));

        using var keyDown = new SdlEvent();
        keyDown.Type = (uint)SdlEventType.SdlKeydown;
        keyDown.KeyKeysymScancode = SdlScancode.SdlScancodeEscape;
        keyDown.KeyKeysymSym = (int)SdlKeyCode.SdlkEscape;
        Assert.Equal(1, Sdl.SdlPushEvent(keyDown));

        Input.BeginFrame();
        Input.PollEvents();

        Assert.Equal(Key.Escape, keyDownEvent.Key);
        Assert.False(exitRequested);
        Assert.False(Input.ExitRequested);

        using var keyUp = new SdlEvent();
        keyUp.Type = (uint)SdlEventType.SdlKeyup;
        keyUp.KeyKeysymScancode = SdlScancode.SdlScancodeEscape;
        keyUp.KeyKeysymSym = (int)SdlKeyCode.SdlkEscape;
        Assert.Equal(1, Sdl.SdlPushEvent(keyUp));
        Input.PollEvents();
    }

    [GraphicsFact]
    public void Disabled_input_still_completes_the_input_frame()
    {
        var inputEnded = false;
        using var receiver = new TestEventReceiver();
        receiver.SubscribeToEvent<InputEvents.InputEndEvent>(
            (ref InputEvents.InputEndEvent _) => inputEnded = true);

        Input.SetEnabled(false);
        try
        {
            Input.BeginFrame();
            Input.PollEvents();

            Assert.True(inputEnded);
        }
        finally
        {
            Input.SetEnabled(true);
        }
    }

    [GraphicsFact]
    public void Window_resize_updates_the_swap_chain_and_sends_screen_mode_event()
    {
        var screenModeEvents = 0;
        using var receiver = new TestEventReceiver();
        receiver.SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => ++screenModeEvents);

        using var resized = new SdlEvent();
        resized.Type = (uint)SdlEventType.SdlWindowevent;
        resized.WindowEvent = (byte)SdlWindowEventID.SdlWindoweventResized;
        Assert.Equal(1, Sdl.SdlPushEvent(resized));

        Input.BeginFrame();
        Input.PollEvents();

        Assert.Equal(1, screenModeEvents);
    }

    private static void PushFingerEvent(SdlEventType type, long fingerId, float x, float y)
    {
        using var @event = new SdlEvent();
        @event.Type = (uint)type;
        @event.TfingerFingerId = (nint)fingerId;
        @event.TfingerX = x;
        @event.TfingerY = y;
        @event.TfingerPressure = type == SdlEventType.SdlFingerup ? 0.0f : 1.0f;
        Assert.Equal(1, Sdl.SdlPushEvent(@event));
    }
}
