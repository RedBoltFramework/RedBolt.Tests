# Engine subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Engine`. Existing tests are in
`EngineTests`.

## Verified deterministic coverage

- [x] Flavor parsing/matching, deterministic formatting, universal/empty
  values, and clone independence (`ApplicationFlavorTests`,
  `EngineBoundaryTests`).
- [x] Config definition ownership/clone flags, VFS defaults and overrides,
  malformed/missing defaults, and preservation of an existing value after a
  failed load (`ConfigFileTests`, `EngineBoundaryTests`).
- [x] Command-line aliases/values, unknown-option and startup-exception error
  paths, engine parameter defaults, headless initialization, frame event
  order, resize, exit, resource-root parsing, and context restoration
  (`ApplicationCommandLineTests`, `EngineTests`).
- [x] Application-state update subscription, copied transition bundles,
  transition event order, reset, and null-state enqueue (`StateManagerTests`,
  `EngineBoundaryTests`).

Remaining unchecked items include exhaustive parameter-binding coercion/error
partitions, child-process command-line smoke coverage, minimized/FPS policies,
plugin reload payloads, and complete subsystem disposal-order assertions.

- `ApplicationFlavorComponent`, `ApplicationFlavorMap`,
  `ApplicationFlavorPattern`, and `ApplicationFlavor`: parse empty, wildcard,
  multiple components/tags, whitespace, duplicate tags, malformed components,
  matching specificity/distance, universal/empty/platform flavors, clone
  independence, case sensitivity, and deterministic `ToString` output.
- `ConfigVariableDefinition`, `ConfigFile.ConfigFlavor`, and `ConfigFile`:
  define required/optional variables and defaults, overridable and command-line
  priority flags, clone/dispose variant ownership, current versus default
  values, flavor selection, changed-variable calculation, priority updates,
  missing/default/override files, valid and malformed JSON, wrong JSON shapes,
  unknown variables, wrong types, command-line precedence, save/load round trip,
  missing VFS mounts, empty/default flavor arrays, duplicate entries, and
  disposal after partial input failure.
- `EngineParameters`: assert every default (including nested graphics/audio/
  debug/screenshot settings), clone independence and value semantics, invalid
  ranges/enums, nullable/optional values, and command-line/config overrides.
- `EngineParameter`, `EngineParameterBinding`, and binding helpers: known and
  unknown names, case sensitivity, get/set for every property type, nullable
  values, enum/string/int/uint/float coercion, invalid overflow/format/null,
  read-only or unsupported properties, and no mutation on failed conversion.
- `EngineCommandLine`: register all options, environment defaults, aliases,
  integer/unsigned parsing, screenshot parameters, log/texture/filter enum
  parsing, invalid values, duplicate options, missing values, and exact error
  messages/usage. Exercise through `CommandLineParser` and through a real
  application startup.
- `EngineEvents`, `StateManagerEvents`, and all event payloads: default empty
  strings/values, command/URI event dispatch, initialized/started/stopped and
  plugin reload ordering, transition started/leaving/entering/complete order,
  sender identity, and handlers that unsubscribe during dispatch.
- `ApplicationState`: activate/deactivate idempotence, update subscription,
  mouse visibility/grab/mode before and after activation, viewport count/index/
  scene lookup, null viewports, fog save/restore, `CanLeaveState`, transition
  hooks, web mouse-mode request/change, node/scene removal, and disposal while
  active.
- `StateManager`, nested `QueueItem` and transition states: cache live/expired
  states, enqueue instance/type/generic and copied bundle values, reject null
  state and non-main-thread enqueue, sustain/fade-out/fade-in/wait transitions,
  overshoot time, state that refuses to leave, transition queue ordering,
  reset while active/queued, state creation failure, event order, bundle clone
  independence, weak-cache cleanup, iteration guard, and disposal exactly once.
- `Engine` and nested `ResourceRootEntry`: construct with executable/resource
  roots, initialize success/failure/headless/configured paths, context and
  subsystem registration, VFS mount/unmount and resource-root manifest parsing,
  config lifecycle, parameter application, frame begin/update/render/end event
  order, fixed/next/smoothed time step, min/max/inactive FPS limits, pause when
  minimized, auto-exit/exit request, canvas resize, screenshot-on-exit, log
  file mapping, profiler/resource/memory dumps, repeated shutdown, and cleanup
  after failure. Assert borrowed global context restoration and every owned
  subsystem disposal order.

## Engine integration gates

Run a headless engine fixture through initialize, one frame, config load,
state transition, resize, and shutdown. Add a malformed config and missing
resource-root scenario. Run command-line tests in a child process so process
exit/error output and unhandled exception formatting are observable without
terminating the test runner.
