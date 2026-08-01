# Rml UI subsystem test plan

Source: `../../RedBolt/src/RedBolt/RmlUI`. Use fake RmlUi bindings for deterministic tests,
plus headless and visual tests for native rendering. The current source has
many private data-model definitions; test them through their `DataVariable`
public behavior and dispose the complete ownership graph.

## Core subsystem and native adapters

- `RmlUI`, `RmlContext`, `RmlEventListenerInstancer`, `RmlContextInstancer`,
  and `RmlPlugin`: construct first, second, and last UI instances; verify
  global backend initialization/shutdown reference counting, unique context
  names, sibling cleanup, context ownership, document load/reload/unload,
  placeholder insertion, font/debugger setup, scale, render target variants,
  rendering toggle, input capture/hover, frame update/render order, resource
  reload, and idempotent disposal after context creation failure.
- `RmlRenderer`, `EventReceiver`, `RmlVertex`, and `CachedRmlTexture`: begin/
  end rendering, texture generation/load/release, retain/release counts,
  invalid/null native pointers, image size overflow, scissor enable/rect and
  transforms, geometry with empty/invalid indices, batch/material selection,
  resize, device loss/reset, and borrowed texture lifetime. Assert no release
  until the final retain and no callback after renderer disposal.
- `RmlSystem`: elapsed time monotonicity, translation success/failure, log
  level mapping, cursor/clipboard calls, keyboard activation/deactivation,
  text-input frame flag reset, and native exceptions/null strings.
- `RmlFile`: open/read/seek/tell/length/close with valid, empty, missing,
  non-seekable, negative/overflow offsets, short buffers, repeated close, and
  unknown handles; verify loaded-resource tracking and clearing.
- `RmlCanvasComponent`, `RmlUIComponent`, and `RmlSerializableInspector`:
  node attach/detach, enabled state, UI size/texture/remap/clear color,
  normalized coordinates, resource load, document lifecycle hooks, focus and
  modal behavior, em size, data-model bindings/events/dirty flags, position/
  size/autosize, missing documents, malformed attributes, serializable target
  connect/disconnect, scalar/vector/enum getter/setter behavior, and disposal
  with pending callbacks.

## Data-model and listener types

- `RmlEventListeners`, `RmlCustomEvent`, `PipeEventListener`,
  `SoundEventListener`, and `CustomEventListener`: parse empty/whitespace,
  pipe lists, sound volume/resource syntax, JSON event payloads and all JSON
  scalar/array/object types; reject malformed input; verify event listener
  creation, process order, detach cleanup, missing sound/UI/node, and exactly
  one engine event.
- `IVariantBinding`, `VariantBinding`, `VariantVectorItemBinding`,
  `VariantMapItemBinding`, `NestedVariantVectorItemBinding`,
  `NestedVariantMapItemBinding`, `ResourceRefListItemBinding`,
  `StringVectorItemBinding`, `DirectStringVectorItemBinding`, and
  `IntegerBinding`: get/set scalar and nested values, valid/invalid indexes,
  absent map keys, incompatible variant types, read-only integer values,
  resource/string conversion, and mutation visibility through parent bindings.
- `DataVariableBindingKey` and `DataVariableStorage`: key equality, unique
  pointer allocation, parent/name/index reuse, stale pointer rejection,
  generic type mismatch, and disposal of every stored binding exactly once.
- `VariantMapDefinition`, `VariantDefinition`, `VariantVectorDefinition`,
  `StringVectorDefinition`, `VariantDefinitions`, and the corresponding
  inspector `RmlSerializableAttribute`, `AttributeType`, `AttributeMember`,
  `AttributeBinding`, `AttributeMemberBinding`, `AttributeSizeBinding`,
  `AttributeStorage`, `AttributeMemberDefinition`, `AttributeDefinition`,
  `AttributeVectorDefinition`, and `AttributeDefinitions`: test data-model
  child/name/size lookup, map/vector/string nesting, scalar conversion,
  read-only members, absent members, out-of-range indices, type registration,
  storage reuse, list mutation, and teardown after a native callback.

## Input and integration gates

Table-test every key/modifier mapping, mouse/touch coordinate conversion,
wheel, text input, drag/drop, focus, modal capture, and screen resize. Run a
headless document test with a bound scalar, nested variant map/vector, sound
event, custom event, and serialized inspector. Add a visual test for texture,
scissor, transform, resize, and debugger paths, and a web test for browser
thread/task callback restrictions.

## Verified checks

- [x] Rml file open/read/seek/length/loaded-resource lifecycle (`RmlFileTests`).
- [x] Rml system translation, logging, input, and cursor behavior are covered
  by `RmlSystemTests`; clipboard round-trip is a separate discovery-time SDL
  backend gate in the same test class.
- [x] Detached component state/binding failure and scalar Rml variant conversion (`RmlUIValueTests`).
- [x] Native director/interface ABI round trips, document/data-model behavior, renderer texture callbacks, and graphics rendering paths (`RmlUiBindingsTests`, `RmlUITests`, `RmlUIComponentTests`, `RmlRendererTests`).
- [x] Web callback restrictions and full visual debugger coverage remain
  explicit backend gates. Web tests run under the browser harness/worker and
  graphics tests use the discovery-time `GraphicsFact` gate.
