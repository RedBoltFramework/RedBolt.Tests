# Dear ImGui subsystem test plan

Source: `../../RedBolt/src/RedBolt/DearImGui`. Existing event/value coverage should be
extended with a headless renderer fixture.

- `DearImGui`: construct/register/dispose and repeated initialization; verify
  context and renderer ownership, console open/close, attribute inspector
  selection/edit events, gizmo selection/node modification, frame begin/end,
  scene/node changes, missing graphics subsystem, device lost/reset, and
  unsubscription on disposal. Assert that UI state survives a frame but not a
  full subsystem teardown.
- `ImGuiDiligentRendererEx`: use fake device/context/swap chain objects to
  cover initialization, render-target resize, per-frame render, empty draw
  data, texture registration/removal, invalid texture ids, device reset,
  backend exceptions, and exact disposal order. Verify no native object is
  released when it is borrowed.
- `DearImGuiEvents` and all payload structs (`EndRenderingDearImGuiEvent`,
  `ConsoleClosedEvent`, `AttributeInspectorMenuEvent`,
  `AttributeInspectorValueModifiedEvent`,
  `AttributeInspectorAttributeEvent`, `GizmoNodeModifiedEvent`, and
  `GizmoSelectionChangedEvent`): assert default/null payloads, sender identity,
  event ordering, mutation data, and handlers that unsubscribe during dispatch.

Separate deterministic event tests from a visual/headless smoke test. Include
invalid native handles, missing context, and disposal during callback as error
paths.

## Verified checks

- [x] All Dear ImGui event payload defaults, including empty marker events and value-copy behavior (`DearImGuiEventValueTests`).
- [x] Context/renderer ownership, device reset, and visual rendering remain
  explicit graphics/backend gates. `GraphicsFact` prevents fixture
  construction in the display-free deterministic suite; run those tests on a
  configured desktop/CI graphics worker.
