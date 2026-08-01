# Scene subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Scene`. Existing tests are in
`SceneTests`.

## Verified deterministic coverage

- [x] `SceneLookupFlag`, `ComponentSearchFlag`, `UpdateEvent`, and
  `SceneUpdateEventType` values/type identity, paused-scene forced updates, and
  safe empty/invalid hierarchy lookups (`SceneBoundaryTests`).
- [x] Node/component add/remove lifecycle ordering, event payload identity,
  scene detachment, and component attachment behavior (`SceneBoundaryTests`,
  `ComponentTests`, `SceneTests`).
- [x] Reflected serializable component hooks, prefab/archive round trips,
  resolver ID rewriting, world-origin event payloads, and animation ownership
  (`SerializableTests`, `NodeTests`, `SceneResolverTests`, `WorldOriginTests`,
  `ValueAnimationTests`).
- [x] `SplinePath` ID-vector expansion, listener cleanup, interpolation, and
  controlled-node movement (`SplinePathTests`).
- [x] `TrackedComponent` registry assignment/reconnect/removal and prefab
  reference/resource ownership (`TrackedComponentTests`, `PrefabReferenceTests`,
  `PrefabResourceTests`).

Remaining unchecked items are primarily async scene loading, fixed/network
dispatch, archive fault injection, and full prefab/resource failure cleanup.

## Flags, event types, and payload contracts

- `AutoRemoveMode`, `WrapMode`, `ComponentSearchFlag`, `TransformSpace`,
  `SceneLookupFlag`, `UpdateEvent`, `WorldOriginUpdateMode`, and
  `ValueAnimation.InterpMethod`: assert exact values and flag combinations,
  including unknown cast values and all boundary/invalid modes used by public
  methods.
- `AttributeId`, `SerializableId`, `PrefabArchiveFlag`, `PrefabSaveFlag`,
  `PrefabLoadFlag`, `PrefabInlineFlag`, and `PrefabInstanceFlag`: verify exact
  bitmasks and `PrefabTypes.ToNodeFlags`/`ToComponentFlags` mappings. Test all
  flag combinations that affect omission, compact names, temporary state,
  default values, IDs, type checks, child/component retention, and attribute
  application. Include mismatched save/load flags and unknown bits.
- `SceneConstants`, `AsyncProgress`, `SceneUpdateEventType`, and its private
  `Implementation<T>`: default progress values, progress monotonicity and
  clamping, generic event type identity/caching, exact `Type`, subscription to
  the correct event payload, duplicate subscriptions, and disposal during a
  callback.
- `SceneEvents` and `ISceneUpdateEvent`: instantiate every payload (forced,
  normal, attribute, subsystem, post, network, drawable, async, node,
  component, tag, enabled, temporary, clone, and world-origin events), assert
  field defaults/types, `TimeStep` projection, scene/node/component identity,
  ref-payload mutation, and event order. Verify paused scenes still emit the
  forced events and that node removal does not emit forbidden per-component
  events.
- `IFixedUpdateSource`: a fake source should prove fixed-update discovery,
  selection policy when there are zero or multiple sources, and behavior after
  source removal.

## Serializable, components, and update dispatch

- `Serializable`: use a reflected test class with scalar, enum, string,
  resource-reference, collection, read-only, temporary, node-ID, component-ID,
  and defaulted attributes. Cover reflection caching, get/set by index/name,
  invalid indices/names, default values, instance defaults, reset/copy with
  same and different reflection layouts, custom accessors, `ShouldLoad`/
  `ShouldSave`, save/load in binary/XML/JSON, missing/truncated/wrong variant
  data, callbacks, temporary state, disposal of stored variants, and failed
  accessor cleanup. Verify `LoadFile` extension routing and resource-cache
  failures.
- `Component`: node/scene assignment hooks, enabled and effective-enabled
  state, enable/disable idempotence and events, IDs, parent lookup, dependency
  nodes, debug/auxiliary serialization defaults, component save/load headers in
  all formats, remove/detach, full debug names, type lookup, null node access,
  and disposal while callbacks are active. Use a derived component to record
  `OnNodeSet`, `OnSceneSet`, `OnMarkedDirty`, and `OnNodeSetEnabled` order.
- `LogicComponent`: default update mask, mask transitions while enabled or
  disabled, all start/delayed-start/stop/update/post/fixed/world-origin hooks,
  selected custom `SceneUpdateEventType`, delayed-start exactly once per
  attachment, immediate unsubscribe when no event is requested, reattachment,
  exceptions in user callbacks, and removal during dispatch.
- `UnknownComponent`: binary/XML/JSON load sets temporary state, unknown
  attributes remain round-trippable, save behavior, missing/malformed data,
  scene instantiation, and disposal without attempting to create an unknown
  reflected type.

## Nodes and scene hierarchy

- `Node` and nested `Vector3AttributeAccessor`: construction/default transform,
  name/hash/tags/enabled/temporary/IDs, local-parent-world transform setters
  and getters, direction/scale/rotation/position math, parent/child insertion,
  reparenting, sibling order, duplicate names, null and self-parent attempts,
  remove/clear, recursive and non-recursive name/path lookup, lazy lookup with
  `ValidateName`/`ValidateRelation`, tag lookup, node/component ID allocation,
  component creation/removal/lookup/index, shared/temporary components, dirty
  propagation, listeners, dependency order, world transforms after hierarchy
  changes, bounding/debug methods, and disposal order. Include negative/large
  IDs, empty names/tags, duplicate tags, invalid child/component indices,
  non-invertible scale, and mutation during node/component events.
- Node serialization: binary/XML/JSON save/load, all attributes, variables,
  tags, child and component order, unknown components, missing optional blocks,
  duplicate IDs, invalid parent relations, wrong types, partial input, and
  archive exceptions. Assert that a failed load leaves no half-attached child
  or component and that serialization round trips preserve IDs when flags allow.
- `Scene`: root-node identity, node/component maps and indices, free ID reuse,
  required package files, manual update and pause state, time scale/effective
  time scale/elapsed time (including negative, huge, and overflow values),
  update-event list parsing/order, file/checksum metadata, empty/ignore-
  components state, tag and type indexes, world-origin absolute/relative
  position and transform conversions, clear/dispose, and threaded-update
  begin/end/dirty queue behavior.
- Scene serialization and resource loading: binary/XML/JSON save/load,
  indentation and short-write failures, malformed data, unknown component,
  duplicate IDs, required packages, lightmap reset/add/reload/index bounds,
  scene instantiation at position/rotation for all three formats, ID remapping,
  and error cleanup. Test `LoadMode.SceneOnly`, resources-only, and combined
  modes with missing resources and failing component loads.
- Async scene loading: `LoadAsync`, `LoadAsyncXML`, `LoadAsyncJSON`, progress
  counts/events, main-thread finish budget, state transitions, cancellation via
  `StopAsyncLoading`, repeated/reentrant load, disposed scene/file, worker
  exceptions, resource dependency completion, and exactly-once finished event.
  Use barriers/fake resources; do not make correctness depend on scheduler
  speed.
- Scene update dispatch: normal/forced/post/fixed/network/world-origin event
  order, pause/time-scale behavior, custom update events, component insertion
  or removal during update, delayed dirty components, dependency ordering,
  threaded snapshots, exception policy, and no callbacks after scene disposal.
- `Scene.Snapshot<T>`: empty/partial/full construction, count and array
  ownership, enumeration/consumer behavior, dispose idempotence, and snapshot
  safety when the source collection changes.

## Prefab value model

- `AttributePrefab` and nested `IdentifierType`: default/name/hash/id forms,
  value ownership and replacement, clone/equality/hash, type identifiers,
  compact versus full names, serialization flags, malformed/missing values,
  enum-as-string conversion, disposal, and repeated dispose.
- `SerializablePrefab`: type name/hash selection, ID and temporary state,
  effective scope lookup, clone/equality, `Import` with default/temporary/
  prefab/compact/enums flags, `Export` with type checking and temporary
  retention, unknown attributes/types, enum names/invalid enum values,
  archive flags that omit ID/type/temporary data, missing attribute arrays,
  duplicate attributes, and deep disposal.
- `NodePrefab`: empty/default node, component/child lists, node-name lookup,
  named and `#index` child paths, invalid/empty paths, duplicate names,
  recursive clone/copy/equality/hash, normalize IDs, effective scope, clear/
  dispose, and archive round trips with all prefab flags. Test partial child or
  component arrays, malformed counts, type/ID collisions, and exception cleanup.
- `PrefabNormalizer`: scan nested nodes/components, remap attribute/node/
  component references, prune unreferenced IDs, duplicate and missing IDs,
  unknown reflection, mixed node/scene attributes, cycles/deep hierarchies,
  stable ordering, and idempotence.
- `PrefabReader` and `PrefabWriter`: abstract call-order contract, disposal,
  EOF behavior, premature/out-of-order calls, zero/negative counts, and
  previously returned object lifetime. `PrefabReaderFromMemory` and
  `PrefabWriterToMemory` must cover depth-first traversal, empty/many children,
  component counts, overwrite/clear behavior, flags, EOF transition, and
  mutation of the source prefab during traversal. `PrefabReaderFromArchive` and
  `PrefabWriterToArchive` must cover root/no-root blocks, ordered/unordered/
  array archives, missing optional arrays, size hints, flags, nested children,
  archive exceptions, closing all blocks on dispose, and exact EOF timing.
- `PrefabTypes`: test all conversion helpers and verify component types retain
  type IDs while node types are omitted, including the duplicated/unknown flag
  cases that could otherwise silently serialize incompatible data.

## Prefab resources and references

- `PrefabResource`: load/save binary/XML/JSON and prefab formats, empty and
  malformed input, scene/node slices, ID normalization, mutable versus const
  prefab access, `GetNodePrefabSlice` named/index path errors, instantiate
  reference with persistent/temporary/ID-discard/attribute flags, parent/null
  handling, instantiation failure cleanup, memory use, and disposal.
- `PrefabReference`: set/replace/clear prefab, path and resource-ref attribute,
  delayed dirty application, reload event subscription/unsubscription, slice
  versus whole-prefab behavior, `InlineConservative`/`InlineAggressive`,
  temporary component/child retention, `CommitChanges` success and refusal for
  slices/missing resources, instance recreation, node removal, and disposal.
  Verify no stale instance remains after reload failure or prefab replacement.
- `SceneResource`: construct/dispose owned scene and cached format files,
  save/load XML/JSON/binary and as prefab, format hints, custom root, empty/
  malformed/truncated data, file save failures, begin/end load separation,
  reload-begin cancellation, reload-end success/failure event arguments,
  preservation of active scene on failed reload, prefab flag, and cleanup of
  temporary JSON/XML/binary resources.

## Animations and motion components

- `ValueAnimationInfo`: default and assigned attribute metadata, clone/copy,
  variant ownership, interpolation/wrap settings, missing attribute, wrong
  type, serialization, and disposal after replacement or failed load.
- `VAnimKeyFrame` and `VAnimEventFrame`: time/value/event fields, clone,
  disposal, deep variant ownership, ordering/equal timestamps, malformed
  archive data, and repeated dispose.
- `ValueAnimation`: empty/one/many keyframes and event frames, duration,
  interpolation methods, wrap modes (loop/clamp/once), speed and time
  boundaries, negative/NaN/large time, duplicate timestamps, value-type
  mismatch, interpolation of every supported variant, event crossing forward
  and backward, event ordering, serialization, clone/disposal, and animation
  state after a failed load.
- `ObjectAnimation`: add/replace/remove attribute animations by name/object,
  missing values, wrap mode and speed defaults/bounds, info lookup, load/save
  XML/JSON/binary, duplicate attributes, events for add/remove, resource
  disposal, and malformed animation data.
- `AttributeAnimationInfo` and `Animatable`: animated attribute discovery,
  application order, interpolation against current/default values, missing or
  non-animatable attributes, update event subscription, paused/zero timestep,
  animation replacement/removal, and event cleanup on dispose.
- `SplinePath`: control-point add/remove/clear/order, weak-node lifetime,
  ID-attribute parsing and resolver application, interpolation mode, length,
  position/clamp/finished state, speed/time movement, zero/negative speed,
  one-point/empty spline, controlled-node movement, node removal, debug
  geometry, serialization, and disposal of listeners/variants. Include a
  count larger than the supplied ID vector and an ID count above `int.MaxValue`.

## Tracking and reference resolution

- `TrackedComponentBase` and `ReferencedComponentBase`: registry assignment,
  reconnect, enabled/disabled callbacks, component removal, destructor/clear,
  and references during scene reparenting or registry disposal.
- `TrackedComponentRegistryBase` and `ReferencedComponentRegistryBase`:
  add/remove/index/reference lookup, duplicate components, invalid indexes,
  stale versions, version increment/wrap, reverse lookup, disposal order, and
  registry recreation. Verify a component cannot be returned after removal.
- `ComponentReference` and static `TrackedComponent`: exact bit packing,
  `ConstructComponentReference`/`DeconstructComponentReference`, max/negative
  index behavior, version boundaries, formatting, and invalid references.
- `TrackedComponent<T>`: generic registry type isolation, reconnect after
  registry creation, enabled callback, serialization, and cleanup when the
  generic registry or component is disposed.
- `SceneResolver`: add node/component mappings including nulls and duplicate
  old IDs, resolve forward/backward references, missing targets, multiple
  scenes, reset, idempotence, and disposal. Assert unresolved references stay
  safely null rather than pointing to a prior scene.

## Built-in behavior components

- `WorldOrigin`: default max distance/step/axis flags, threshold equality and
  overshoot, per-axis enablement, snap-rounding for positive/negative values,
  zero step, large coordinates, repeated rebases, delayed main-thread task,
  scene/node disposal before the task runs, failed movement disabling the
  component, and world-origin event order.
- `ShakeComponent`: trauma clamping/addition/falloff, power/time-scale/ranges,
  zero and negative timesteps, deterministic noise seam, original transform
  capture, external node transform changes, return to original transform at
  zero trauma, event-mask enable/disable, removal/disposal, and no-node update.
- `PrefabReference`, `WorldOrigin`, `ShakeComponent`, and `SplinePath`: test
  attribute application after scene load/network updates, listener cleanup,
  temporary state, and interactions with node removal and world-origin changes.

## Scene-wide gates

Build fixtures with a minimal reflected node/component family, deterministic
archives, disposal-counting variants, a controllable work queue, and event
recorders. For every tree/prefab/resource round trip assert identity, IDs,
ordering, ownership, temporary state, and failed-load cleanup—not merely that
serialization returned `true`. Keep asynchronous loading, threaded updates,
native/resource-backed loading, and world-origin delayed tasks as separate
integration gates with bounded completion and no-callback-after-dispose checks.
