# Actions subsystem test plan

Source: `../RedBolt/src/RedBolt/Actions`. Existing tests are in this
repository's `ActionTests` project. Extend those tests only after the gaps
below are reviewed.

## Verified coverage

- [x] `ActionState` and `FiniteTimeActionState` lifecycle boundaries: weak
  target/original-target behavior, action identity, first-tick timing,
  negative-delta clamping, overshoot clamping, completion, and `Stop`.
- [x] `ActionManager` paused-action retention and explicit completion.
- [x] `ActionManager` stable same-target iteration when a callback schedules a
  second action during `Update`.
- [x] `FiniteTimeAction.GetOrDefault` returns the manager-owned empty action,
  while preserving a supplied action.
- [x] `ActionLibrary.RegisterActionLibrary` is repeatable in a fresh context
  without losing reflected action types.

## Shared fixture and seams

Use a disposable reflected `Serializable` target with bool, integer, float,
`Vector2f`, `Vector3f`, `IntVector2`, `IntVector3`, quaternion, and shader
parameter attributes. Use a recording `BaseAction`/`ActionState` pair to
observe start, update, stop, reverse, and disposal without relying on private
state classes. Drive `FiniteTimeActionState.Step` through a small derived test
state so first-tick behavior and overshoot are directly observable. Keep an
`ActionManager` on a context with a recording update event source.

## Class-by-class coverage

### State and action base classes

- `ActionState`, `FiniteTimeActionState`, `ActionInstantState`: construct with
  live and null targets; verify weak target versus original target, target
  expiration, `Stop`, `IsDone`, `Action`, first tick at elapsed zero, later
  elapsed accumulation, duration epsilon protection, clamped update time,
  zero/negative/very large/negative `dt`, and disposal idempotence.
- `BaseAction`, `FiniteTimeAction`, `ActionInstant`, `DelayTime`, and their
  nested `NoAction`/`NoActionState` types: verify default state, nullable
  `GetOrDefault`, duration serialization/default omission, duration clamping,
  reverse identity/round trip, instant actions ignoring serialized duration,
  and the exact state type returned by `StartAction`.
- `ActionBuilder`: cover empty builder, each fluent action modifier, action
  replacement versus wrapping, `Then`/`Also` nesting order, event builders
  with default and explicit struct data, `Run` with supplied and context action
  managers, reuse after `Run`, and null action/target failure behavior.

### Attribute actions

- `AttributeAction`, `AttributeActionConstants`, `AttributeActionState`,
  `SetAttributeState`, and `AttributeBlinkState`: resolve an existing,
  missing, read-only, wrong-type, non-serializable, and null target attribute;
  assert no write when resolution fails; verify variant disposal, one-shot set,
  interpolation, original-value capture, toggle count, restoration on stop,
  zero/negative blink counts, non-integral final time, and target expiration.
- `AttributeFromTo`, `AttributeTo`, `AttributeBlink`, and their private
  `AttributeFromToState`/`AttributeToState` implementations: test constructors,
  names, default `from`/`to` acquisition, vector/int/scalar variants,
  mismatched variant types, reverse swapping, serialization defaults and
  malformed values, and repeated starts proving action state snapshots rather
  than aliases mutable `Variant` values.
- `Show`, `Hide`, `Enable`, `Disable`, `RemoveSelf`, and private
  `RemoveSelfState`: assert the exact
  visibility/enabled mutation, no mutation on an incompatible target, one
  update only, and removal from a parent while the action manager is iterating.

### Movement and parameter actions

- `MoveBy` and its `MoveByVec3State`, `MoveByIntVec3State`,
  `MoveByVec2State`, and `MoveByIntVec2State`: cover all supported attribute
  types, changed action vectors after `StartAction`, zero duration, negative
  time, endpoint exactness, reverse, missing attributes, and integer rounding.
- `MoveByQuadratic`, `MoveByVec3QuadraticState`, `MoveByVec2QuadraticState`,
  `MoveByIntVec3QuadraticState`, and `MoveByIntVec2QuadraticState`: verify the
  quadratic midpoint,
  endpoint, external-value tracking, reverse, negative/zero duration, and each
  vector/integer representation.
- `JumpBy`, `JumpByVec3State`, `JumpByVec2State`, `JumpByIntVec3State`, and
  `JumpByIntVec2State`: verify jump count, vertical offset at
  start/midpoint/end, one-shot state initialization, reverse, zero/negative
  jumps and duration, and integer conversion.
- `ScaleBy`, `ScaleByState`, `ScaleByVec2State`, `RotateBy`, `RotateByState`,
  `RotateAround`, and `RotateAroundState`: cover scalar/vector scale, rotation
  accumulation, parent/local/world transform selection, target removal during
  update, zero duration, reverse, and invalid attribute access.
- `ShaderParameterAction`, `ShaderParameterTo`, `ShaderParameterFromTo`,
  `ShaderParameterToState`, `ShaderParameterFromToState`, and their nested
  state behavior: serialize name and values, snapshot source values from a
  `Material`, fall back to destination on type mismatch, interpolate scalar and
  vector values, handle missing/non-material targets, reverse from/to, dispose
  cloned variants, and prove source action mutation after start cannot alter the
  running state.

### Composition and timing

- `Sequence`/`SequenceState`: empty and one-child sequences, total duration,
  exact boundary and overshoot transitions, child stop/disposal, child action
  that schedules another action, repeat-forever child, reverse child order,
  serialization, and target expiration.
- `Parallel`/`ParallelState`/`StateAndDuration`: empty and one-child cases,
  longest-duration selection, normalized child time for unequal durations,
  completed-child retention/removal, reverse and serialization, and child
  disposal when the parent stops early.
- `Repeat`/`RepeatState` and `RepeatForever`/`RepeatForeverState`: one,
  multiple, zero, and negative repeat counts; exact duration boundaries;
  restart count and overshoot; inner action disposal; max-duration behavior;
  reverse; serialization; stop before completion; and non-terminating update
  protection for zero-duration inner actions.
- `ActionEase`/`ActionEaseState`, `EaseElastic`, `EaseBackIn`, `EaseBackOut`,
  `EaseBackInOut`, `EaseBounceIn`, `EaseBounceOut`, `EaseBounceInOut`,
  `EaseSineIn`, `EaseSineOut`, `EaseSineInOut`, `EaseExponentialIn`,
  `EaseExponentialOut`, `EaseExponentialInOut`, `EaseElasticIn`,
  `EaseElasticOut`, and `EaseElasticInOut`: test identity at 0/1, monotonic and
  overshoot characteristics at midpoint, every reverse pairing, duration
  propagation, period/amplitude defaults and extreme values, serialization,
  null/instant inner actions, and exact child update values. Use a table-driven
  test for all easing variants plus focused tests for each branch.

### Invocation and manager

- `ActionCallHandler` and `ActionCallHandlerImpl<T>`: invoke a receiver,
  dispose the handler, invoke after disposal, null receiver, receiver
  collection, and generic type/exception propagation.
- `CallFunc`/`CallFuncState` and `SendEvent<T>`/`SendEventState`: snapshot the
  handler/data at start, invoke once despite multiple updates, dispatch the
  typed event from the target, handle null targets, handler exceptions, target
  disposal, reverse behavior, and handler disposal.
- `ActionManager`, `HashElement`, `EmptyAction`, and `EmptyState`: add valid
  actions, reject null action/target and duplicate action instances, pause and
  resume one/all targets, cancel one/all, complete one/all, query counts,
  remove actions while updating, schedule during update, expired weak targets,
  target identity reuse, empty action reuse, action state disposal exactly once,
  and manager disposal/unsubscription. Assert stable iteration when the list
  changes in every callback path.
- `ActionLibrary`: register the assembly once and repeatedly; verify reflected
  action types are available in a fresh context and no duplicate registration
  changes behavior.

## Cross-cutting gates

Check every action type for reflected metadata order, archive round trip, clone
or snapshot independence, reverse symmetry, and deterministic disposal. Add a
small allocation smoke test around manager update and composition actions, and
keep visual/action sample coverage separate from these unit tests.
