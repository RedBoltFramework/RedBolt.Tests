# Input subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Input`. Existing tests are in
`InputTests`.

## Verified deterministic coverage

- [x] Axis dead-zone/sensitivity/inversion transforms, raw configuration,
  clone independence, and optional archive round trips (`AxisAdapterTests`).
- [x] Keyboard/controller aggregation, opposing-source averaging, dead-zone,
  focus-loss clearing, subscription masks, and directional-pad hysteresis and
  synthesized repeat/release events (`DirectionAggregatorTests`,
  `DirectionalPadAdapterTests`).
- [x] Input key/mouse/controller state transitions, begin-frame press reset,
  disabled-input behavior, invalid joystick lookups, focus-loss clearing, SDL
  key injection, and event payload value contracts (`InputTests`,
  `InputSdlEventTests`, `InputBoundaryTests`).
- [x] InputMap typed/generic mappings, metadata ownership, archive round trips,
  and controller/hat/screen mapping shape (`InputMapTests`).
- [x] Free-fly, move-and-orbit, multitouch, and pointer attachment/update,
  source-priority, matching-release, and disposal paths (`ControllerTests`,
  `MultitouchAdapterTests`, `PointerAdapterTests`).

Remaining unchecked items include malformed SDL event/error-return partitions,
UI weak-reference expiry, property-based normalization, and display-dependent
mouse/backbuffer scaling.

- `AxisAdapter`: default/configured dead zone, positive/negative sensitivity,
  neutral, inversion, saturation, zero/negative/greater-than-one dead zones,
  zero/negative sensitivities, NaN/Infinity, clone independence, and archive
  optional/default round trip.
- `DirectionAggregator`, nested axis state, and its masks: keyboard,
  controller axis, joystick axis/hat, touch and UI-element sources; opposing
  and simultaneous sources, averaging, dead zone, focus loss, source release,
  enable/disable, mask changes, expired UI weak reference, and unsubscribe
  cleanup.
- `DirectionalPadAdapter`, nested `AggregatedState`, input enums/masks, and
  synthesized transitions: keyboard aliases, controller/joystick buttons,
  axis upper/lower hysteresis, hats, source aggregation, repeat delay/interval,
  key down/up ordering, last-source release, focus loss/disconnect, invalid
  thresholds/intervals, clone/disposal, and configuration archive round trip.
- `JoystickState` and `Input`: empty/default state, initialize/reset sizes,
  clone deep copy, controller versus joystick identity, button/axis/hat bounds,
  valid-axis flags, key/scancode/name conversions, begin-frame press reset,
  mouse button/click/wheel/move state, touch and joystick lookup by id/index/
  name, qualifiers, relative/backbuffer scaling, focus/minimize, disabled
  input, mouse visibility/grab/mode/lock reset, fullscreen/touch emulation,
  screen keyboard/joystick/gesture APIs, serialization failures, and event
  payloads. Use fake SDL events for key, mouse, touch, joystick, window/focus,
  drop-file, and text input paths; include unknown/malformed events and SDL
  error returns.
- `InputConstants` and `InputEvents`: assert native enum values, bit masks,
  event struct layout/defaults, pointer/key/button/axis/touch fields, and
  sender/order for every event family.
- `InputMap`, `ActionMapping`, `KeyboardKeyMapping`,
  `ControllerButtonMapping`, `ControllerAxisMapping`,
  `ControllerHatMapping`, `MouseButtonMapping`, and `ScreenButtonMapping`:
  dead-zone clamp, every map method, new/existing action, generic versus typed
  controller/axis values, axis translation/overlap, modifiers, hat/mouse/screen
  evaluation, missing action, invalid mapping, metadata add/remove/clear,
  resource load, and binary/XML/JSON archive round trips. Verify all nested
  value copies clone owned variants and replacing/removing mappings destroys
  the old value exactly once.
- `FreeFlyController` and nested `Movement`: activation/deactivation mouse
  state, keyboard translation, mouse/wheel/controller/flight-stick/generic
  joystick/touch movement, speed versus accelerated speed, pitch limits,
  sensitivity, zero timestep, node/camera missing, disabled state, and event
  subscription changes.
- `MoveAndOrbitComponent`: node attach/detach, velocity/yaw/pitch setters,
  distance limit ordering and invalid values, yaw/pitch quaternion, and
  external transform changes.
- `MoveAndOrbitController`: input-map resource load/set/attribute application,
  missing map, movement/rotation touch rectangles, sensitivity metadata,
  scene/node/component attach, update subscription, input-end reset, enable/
  disable, and disposal with a pending event.
- `MultitouchAdapter`: enable/disable subscription, non-UI and UI touch
  filtering, begin/move/end/cancel, multiple touches, pointer positions,
  release without begin, focus loss, and emitted event order.
- `PointerAdapter`: enable and subscription mask, mouse/touch/joystick source
  priority, press/release/move state, cursor speed/acceleration, direction
  aggregation, matching touch release, focus loss, invalid configuration,
  synthesized events, and disposal/unsubscription.

## Cross-cutting gates

Use a deterministic event injector and assert no duplicate or stale state after
focus loss/disconnect. Add property tests for axis normalization, dead-zone
translation, and action evaluation. Keep SDL display-dependent tests separate
from pure managed state tests.
