# RedBolt class test plans

These plans cover the production C# code in the sibling `../../RedBolt` repository.
They are the traceability documents for the executable tests in this project;
each verified entry names the focused test or the explicit native/backend gate.

The inventory was taken from the current worktree on 2026-08-01:

- 469 production `.cs` files under `../../RedBolt/src/RedBolt` (the removed legacy test tree
  is excluded).
- Public, internal, and nested classes are covered through their owning
  subsystem. Nested state, cache, listener, and adapter types are exercised
  through public behavior unless a public seam cannot observe them.
- Public structs, records, enums, delegates, and event payloads are included
  where their layout, serialization, equality, or dispatch is observable.

## Common test contract

Every plan should be implemented with a small set of deterministic fixtures:

- A fresh `Context` and `HeadlessEngineFixture` for engine-owned objects.
- Explicit `Dispose`/`try-finally` ownership for every `RedObject`, archive,
  resource, native wrapper, and cloned value.
- A recording event receiver and a recording logger. Assert sender, receiver,
  payload, order, duplicate suppression, and behavior when handlers unsubscribe
  during dispatch.
- Fake or in-memory collaborators for file, archive, resource, renderer,
  audio, navigation, physics, and native-binding boundaries. Integration tests
  should be separate from pure tests and should state their required backend.
- Boundary values: empty/default values, one item, maximum/minimum numeric
  values, negative values where accepted, NaN/Infinity where relevant, repeated
  calls, invalid enum values, null/expired weak references, and disposal during
  an active operation.
- For every mutable value that implements `ICloneable`, verify that the clone
  is independent, including nested lists, variants, native handles, and
  disposable members.
- For every serializer, test missing fields/defaults, extra fields, wrong
  types, malformed input, round-trip equality, archive block boundaries, and
  disposal after both success and failure.

## Cross-cutting strategy

- [Additional test strategy](../ADDITIONAL_TEST_STRATEGY.md): property-based,
  fuzz, state-machine, differential, fault-injection, concurrency, leak,
  contract, visual, performance, security, and mutation testing.

## Subsystem plans

- [x] [Actions](actions.md)
- [x] [Audio](audio.md)
- [x] [Bindings](bindings.md)
- [x] [Dear ImGui](dear-imgui.md)
- [x] [Generator](generator.md)
- [x] [Glow](glow.md)
- [x] [Graphics backends](graphics-backends.md)
- [x] [IK](ik.md)
- [x] [Jolt physics](jolt-physics.md)
- [x] [Navigation](navigation.md)
- [x] [Particles](particles.md)
- [x] [Platform](platform.md)
- [x] [Container](container.md)
- [x] [Core](core.md)
- [x] [Engine](engine.md)
- [x] [Graphics](graphics.md)
- [x] [IO](io.md)
- [x] [Input](input.md)
- [x] [Logging](logging.md)
- [x] [Math](math.md)
- [x] [Render API](render-api.md)
- [x] [Render pipeline](render-pipeline.md)
- [x] [Resource](resource.md)
- [x] [Scene](scene.md)
- [x] [Rml UI](rml-ui.md)
- [x] [Package tool](tools.md)
- [x] [Samples](samples.md)

The checkbox is a completion claim for the complete plan, not a progress
estimate. Set it only after every inventory item is linked to an implemented
test or to an explicitly documented manual/backend gate, and after the
relevant focused test command has passed. Individual plan bullets use the same
rule so partial work remains visible.

## Execution modes

The default suite is safe to run without a display. Pure and headless tests
use `HeadlessEngineFixture`; native SDL/OpenGL, clipboard, and visual tests
are discovery-time skipped unless their capability is configured. For SDL
video-only checks, set `SDL_VIDEODRIVER=dummy` before starting the test
process.

The software-rendered native path is provided by
[`run_visual_tests_offscreen.sh`](../../RedBolt/scripts/run_visual_tests_offscreen.sh).
It starts Xvfb, selects llvmpipe, enables the native graphics lifecycle gate,
and runs the hidden-window/visual checks. A user-local Xvfb bundle can be
selected with `REDBOLT_XVFB_ROOT`; the script fails with a diagnostic when
that capability is not installed.

## Completion gate

Before implementing any test, check every type in the relevant inventory
section against the source file and mark whether it has a pure unit test, a
fixture-backed integration test, a platform/backend test, or an intentional
manual/visual gate. A green test run is not sufficient if a listed error path,
ownership path, serialization path, or boundary partition is absent.
