<!-- Copyright (c) 2026 RedBolt and contributors. -->

# Additional test strategy

This document extends the subsystem class plans in
[`test-plans/README.md`](test-plans/README.md). It describes test approaches
that complement ordinary unit and integration tests; it does not implement
tests.

## 1. Objective and scope

The goal is to detect failures that example-based unit and integration tests
often miss:

- combinations of valid inputs rather than a few hand-picked examples;
- malformed, adversarial, truncated, oversized, or hostile input;
- invalid lifecycle transitions and cleanup after partial failure;
- nondeterminism, races, deadlocks, and resource leaks;
- native ABI and backend differences;
- visual, performance, and compatibility regressions;
- tests that pass while failing to detect realistic defects.

The strategy applies to the production code in the sibling `../../RedBolt`
repository and to the test harness in this repository. The existing
class-by-class plans remain the source of truth for type-level coverage; this
document adds orthogonal ways to exercise those types.

## 2. Current baseline and constraints

The current test project provides:

- xUnit, Microsoft.NET.Test.Sdk, and coverlet collector in
  [`RedBolt.Tests.csproj`](../RedBolt.Tests.csproj);
- a shared headless engine fixture and a hidden native graphics fixture in
  [`HeadlessEngineFixture.cs`](../src/RedBolt/Tests/HeadlessEngineFixture.cs);
- a globally serialized xUnit runner configuration in
  [`xunit.runner.json`](../xunit.runner.json), plus disabled parallelism on
  engine and graphics collections;
- native binding, shader, headless rendering, and OpenGL graphics tests;
- a visual regression harness using fixed captures and software OpenGL.

The visual backlog and capture contract are documented in the source
repository's [`VISUAL_REGRESSION_TESTS.md`](../../RedBolt/doc/VISUAL_REGRESSION_TESTS.md).
It already covers deterministic captures and repeatability, but a CI visual
job and a stable second-backend matrix remain follow-up work.

The current serialization is a safe choice for global engine state, but it
does not test the system under meaningful parallel scheduling. Concurrency
tests must therefore run in an isolated process or in a separately controlled
test harness rather than simply enabling parallel xUnit collections.

## 3. Scoring and prioritization

Scores use a five-point scale:

- **Ease:** how readily the approach fits the current repository and fixtures;
- **Payoff:** expected reduction in escaped defects, regressions, or diagnosis
  time.

The scores are planning estimates, not quality measurements. Re-score after
the first implementation phase.

| Test approach | Ease | Payoff | Priority | Primary targets |
| --- | ---: | ---: | --- | --- |
| Contract and ABI tests | 4/5 | 5/5 | P0 | Generated bindings, native structs, enums, calling conventions |
| Snapshot/golden tests | 4/5 | 4/5 | P0 | Archives, shader output, generated reflection, render-pass graphs |
| Property-based tests | 3/5 | 5/5 | P1 | Math, geometry, containers, serialization, octree queries |
| State-machine/model-based tests | 3/5 | 5/5 | P1 | Resource lifecycle, caches, device loss, event subscriptions |
| Metamorphic/differential tests | 3/5 | 5/5 | P1 | Animation paths, geometry paths, shader variants, culling |
| Fault-injection tests | 3/5 | 5/5 | P1 | File I/O, native APIs, shader compilation, GPU/device failure |
| Harmony live API patching | 3/5 | 5/5 | P1 | Managed wrappers, generated C# native bindings, clocks, caches, callbacks |
| Security/adversarial tests | 3/5 | 5/5 | P1 | Package paths, archives, decompression, resource and shader input |
| Visual regression expansion | 3/5 | 5/5 | P1 | Rendering, materials, lighting, terrain, post-processing, backends |
| Performance regression tests | 3/5 | 4/5 | P2 | Serialization, culling, draw commands, shader cache, particles |
| Fuzz tests | 2/5 | 5/5 | P2 | Parsers, archives, resource loaders, command-line and shader source |
| Concurrency and race tests | 2/5 | 5/5 | P2 | WorkQueue, ResourceCache, ThreadSafeCache, watchers, event dispatch |
| Soak and leak tests | 2/5 | 4/5 | P2 | Startup/shutdown, reload, device loss, textures, models, native handles |
| Mutation testing | 2/5 | 4/5 | P3 | Core math, lifecycle, masks, render state, serialization |

## 4. Shared test infrastructure

Build the following seams before adding many specialized tests. They should
be small, deterministic, and usable from several subsystem plans.

### 4.1 Reproducible execution

- DeterministicTestSeed provides a stable SHA-256-derived seed for a test
  name and accepts REDBOLT_TEST_SEED as a replay override. Generative tests
  should include the returned seed, runtime identifier, backend, and relevant
  environment settings in their failure message or retained artifact.
- Allow a failed generated or fuzz case to be replayed with one command and
  no hidden dependency on wall-clock time, process ID, machine name, or random
  global state.
- Give every long-running test a cancellation and timeout boundary. A timeout
  must produce the operation trace and captured state, not only report a
  generic test failure.
- Separate deterministic PR cases from deliberately randomized nightly cases.

### 4.2 Resource and native ownership tracking

- TestLifetimeLedger is the common test-only ledger for created/disposed
  RedObject, resource, archive, file, native binding, render API, buffer,
  texture, and thread-owned objects. A tracked lease must be disposed exactly
  once; LiveOwners and AssertEmpty() expose the leak assertion. Subsystem
  tests should add owner names that identify the native handle or lifecycle
  boundary when a richer backend ledger is not available.
- Make the ledger usable after an exception and after device loss; cleanup
  assertions must run in `finally` paths.
- Add fake native collaborators that can return null handles, error codes,
  short data, delayed callbacks, and exceptions at a selected call number.

### 4.3 Test artifacts

Every nontrivial failure should be diagnosable from retained artifacts:

- generated seed and minimized input;
- operation/state-machine trace;
- serialized input and relevant resource files;
- native-call log and lifecycle ledger;
- screenshot/reference/difference images for visual cases;
- allocation, timing, and thread information for stress cases.

TestArtifactScope creates a unique per-test directory, rejects path escape,
and removes successful artifacts by default. A failing test can call Keep()
before cleanup so its seed, minimized input, operation trace, native-call log,
or image remains available. Artifacts must never overwrite a reference or
another test's evidence.

### 4.4 Isolation levels

Use the least expensive isolation that still exercises the risk:

1. in-process pure test for deterministic value and algorithm behavior;
2. fresh headless engine for resource/component behavior;
3. fake-native process for failure and lifecycle matrices;
4. hidden-window native process for graphics and ABI behavior;
5. dedicated worker process for race, leak, timeout, and crash containment.

Do not turn on global test parallelism as a substitute for a race harness.

### 4.5 Test-only live API patching with Harmony

Use the Harmony framework as an optional test seam when a managed API is
difficult to replace through an interface or constructor. A dedicated helper
can apply a prefix, postfix, finalizer, or narrowly scoped transpiler to a
managed method while a test is running. This includes the generated C# side of
native APIs: patch a generated binding wrapper or managed forwarder before it
crosses into native code. This is useful for:

- forcing file, resource-cache, shader-compiler, render-device, and callback
  wrappers to return errors, nulls, short data, or controlled values;
- patching generated binding surfaces such as `Diligent`,
  `Glslang`, `SpirVTools`, `HarfBuzz`, `Jolt`, and `RmlUi`
  to return fake handles, error codes, native structs, diagnostics, or
  callback results;
- replacing time, frame, random, or environment reads with deterministic
  values;
- recording calls, arguments, ordering, and disposal callbacks;
- injecting cancellation, delay, exception, device-loss, or callback
  reentrancy at a precise call;
- observing private-to-public collaboration without changing production
  visibility solely for tests.

Treat patches as process-wide mutable state. Every patch must have an explicit
owner, be applied immediately before the scenario, be removed in a `finally`
block, and assert that the original method is restored. The helper should
reject duplicate/conflicting patches, expose a call log, support a selected
call number or predicate, and fail if a patch remains installed after the
test. Patching must not silently alter the production test run or reference
baseline.

Use Harmony to create a seam, not to bypass the behavior under test. Prefer a
fake collaborator or an injected interface when the production design already
provides one. Do not patch the method whose behavior is being asserted, and
do not make tests depend on compiler-generated implementation details unless
the test is explicitly a compatibility or diagnostic test.

Harmony patches managed entry points, including patchable generated C# binding
entry points. It is not a replacement for native ABI probes, native fault
shims, or backend fakes; direct `extern`/P/Invoke methods that have no
patchable managed body should be reached through a managed caller/wrapper or a
native shim. Driver behavior, calling-convention correctness, and native
crashes still require the native test mechanisms described in the contract,
visual, and backend plans. Run patch-heavy scenarios in an isolated process
when they touch global state, asynchronous callbacks, or parallel schedules.

This seam enables most managed failure-path tests to run without loading the
native library or creating a GPU. Keep a smaller native smoke suite to prove
that the unpatched generated binding actually loads, calls the intended ABI,
returns correctly shaped data, and releases real native resources.

**Exit criteria:** a reusable patch scope can deterministically alter and
restore representative managed boundaries, produces call/lifecycle evidence,
and has tests proving cleanup after success, assertion failure, exception,
timeout, and process cancellation.

## 5. Test approach plans

### P0 — Contract and ABI tests

**Purpose:** catch native interop failures before they become platform-specific
crashes or corrupted state.

**Coverage:**

- generated Diligent, SDL, HarfBuzz, Jolt, RmlUi, glslang, SPIR-V, WebP, LZ4,
  and related binding surfaces;
- native struct size, field offset, packing, alignment, signedness, enum value,
  pointer ownership, and calling-convention checks;
- managed-to-native and native-to-managed round trips for strings, arrays,
  spans, callbacks, optional pointers, and handles;
- lifetime checks for borrowed versus owned pointers, callback registration,
  callback after disposal, and native error propagation;
- compile/load tests for generated reflection and binding output.

**Implementation:** keep a small native probe for each ABI family. Compare its
reported layout with the managed declaration and fail with the first differing
field. Run probes on every supported runtime identifier, not only on the
developer workstation.

**Exit criteria:** all externally consumed structs and enums have an explicit
contract test; generated output is compiled and loaded; ownership is asserted
for success and partial failure.

### P0 — Snapshot and golden tests

**Purpose:** detect accidental changes to stable output where a semantic
comparison is difficult or too permissive.

**Targets:**

- canonical serializer/archive output for representative resources;
- normalized shader source, define expansion, translation diagnostics, and
  generated shader metadata;
- generated reflection and binding source for minimal fixture inputs;
- render-path/pass graphs, draw-command snapshots, pipeline descriptors, and
  shader resource layouts;
- command-line help and package manifests where output is part of the tool
  contract.

**Rules:** canonicalize nondeterministic fields first; never snapshot native
  pointers, timestamps, hash-map iteration order, or machine paths. A changed
  golden file requires a human-reviewed reason and a separately reviewed
  update. Keep malformed-input snapshots for diagnostics as well as success
  snapshots.

**Exit criteria:** every stable generated or serialized contract has a checked-
  in fixture, a semantic companion assertion, and a documented update policy.

### P1 — Property-based and generative tests

**Purpose:** explore broad valid input spaces and assert invariants instead of
  enumerating only examples.

**Priority targets:**

- Math: vector and matrix identities, inverse/transpose relationships,
  quaternion rotation, ranges, interpolation, color conversion, and encoding;
- Container: allocators, vectors, maps, sparse spans, and clone/dispose
  ownership;
- IO/Resource: serializer-deserializer round trips, archive block boundaries,
  compression, virtual paths, metadata defaults, and resource references;
- Geometry/Graphics: generated vertex layouts, primitive/index ranges, tangent
  generation, texture mip dimensions, and draw-command descriptors;
- Scene/Animation/Actions: curve continuity, endpoint rules, reversible action
  behavior, prefab round trips, and node hierarchy invariants;
- Octree/navigation: compare indexed queries with a simple brute-force model.

**Oracle examples:**

- serialize then deserialize preserves the supported semantic value;
- clone then mutate does not mutate the original;
- normalized vectors have the expected length unless input is degenerate;
- every returned index is in range and every inserted object is discoverable;
- a query result is equivalent to the reference implementation;
- a failed operation leaves the prior state unchanged.

**Rules:** generators must include boundary, degenerate, and invalid-but-
accepted values. Each failure records a minimized case and seed. Use smaller
deterministic case counts on pull requests and larger distributions nightly.

**Exit criteria:** each high-risk pure subsystem has generators, shrinkers or a
minimal counterexample strategy, explicit invariants, and replayable failures.

### P1 — State-machine and model-based tests

**Purpose:** exercise legal and illegal sequences of operations, not just one
  call at a time.

**Models to implement:**

- resource: create → begin load → end load → reload → unload → dispose;
- device object: create → invalidate → restore → lose device → reset → destroy;
- texture/buffer: create → update → map/unmap → resolve → resize → dispose;
- cache: miss → create → hit → invalidate → evict → restore;
- VFS: mount/unmount, alias resolution, open/read/write, watcher events;
- scene: attach/detach, add/remove/reparent, component enable/disable,
  prefab save/load;
- event system: subscribe, dispatch, unsubscribe during dispatch, nested
  dispatch, receiver disposal;
- animation/action: play, pause, reverse, seek, loop, finish, cancel;
- render frame: configure target, update view, build buffers, render, present,
  device loss at each boundary.

**Invariants:** no event after disposal, no object in two exclusive owners, no
  stale cache entry after invalidation, no leaked native handle, no negative
  active count, deterministic event order, and successful retry after a
  transient failure.

**Exit criteria:** every lifecycle-heavy subsystem has a model with generated
  operation sequences, a reducer or trace printer, and a minimized sequence
  for failures.

### P1 — Metamorphic and differential tests

**Purpose:** validate behavior when an exact expected output is hard to write.

Use pairs or families of executions with an expected relationship:

- indexed and non-indexed geometry should render equivalent pixels;
- hardware and software skinning should produce equivalent poses within a
  documented tolerance;
- octree query results should match brute-force traversal;
- serialization through memory, file, and package mounts should agree;
- shader optimized and unoptimized paths should preserve reflection/output;
- a shader with equivalent define ordering should produce the same normalized
  variation key;
- repeated identical frames should produce identical or bounded-difference
  images;
- resizing a render target and rendering at the new native size should not
  retain the old dimensions;
- save-load-save should be canonical after the first normalization.

**Exit criteria:** each complex subsystem has at least one independent oracle
or equivalent implementation. The comparison tolerance and known exceptions
are documented rather than hidden in broad approximate assertions.

### P1 — Fault injection and resilience tests

**Purpose:** prove that failure paths clean up and recover instead of only
  proving that successful paths work.

Inject failures at every external boundary:

- file open/read/write/seek/close, short reads, permission failures, and full
  storage;
- archive header/block/checksum/decompression failures;
- resource missing, wrong type, malformed field, dependency cycle, and reload
  during use;
- native create/map/update/compile/reflect calls returning null or errors;
- device lost during upload, render, resolve, present, and disposal;
- callback exception, cancellation, timeout, allocation failure, and thread
  termination;
- invalid paths, unsupported formats, out-of-range indices, and oversized
  dimensions.

For each injected failure assert rollback, disposal order, event behavior,
cache poisoning absence, retry behavior, and useful diagnostics. Run the same
failure at first call, middle call, last call, and after partial ownership has
been established.

Use Harmony patches for managed wrappers—and for the generated C# binding seam
when the failure must occur at a specific native API call—when a direct fake
is unavailable. Combine the patch with a fake or native shim for unmanaged
behavior, and verify that a patched error follows the same cleanup and retry
contract as a real error. The same patch matrix should cover binding calls
used by `Graphics`, `RenderAPI`, shader translation/optimization, fonts,
physics, and Rml UI.

**Exit criteria:** every high-risk external boundary has a deterministic
failure injector and verifies both cleanup and successful retry where retry is
part of the contract.

### P1 — Security and adversarial input tests

Treat resource and package files as untrusted input even when normal engine
assets are trusted.

- package/VFS path traversal, absolute paths, aliases, symlink-like paths,
  case collisions, and mount escape;
- archive decompression bombs, recursive nesting, integer overflow, huge counts,
  duplicate blocks, corrupt checksums, and truncated streams;
- malformed UTF-8, invalid XML/JSON, duplicate fields, deeply nested values,
  extremely long strings, and unexpected numeric values;
- shader source with pathological include depth, define expansion, invalid
  tokens, oversized source, and compiler error flooding;
- invalid native handles, stale pointers, invalid callback data, and null
  optional resources;
- denial-of-service cases: unbounded loops, excessive allocations, and files
  that consume the entire test timeout.

Keep security tests deterministic and bounded. They should assert controlled
failure and resource limits, not merely that a process eventually crashes.

### P1 — Visual regression expansion

The project already has a visual harness. The next work is operational and
matrix coverage:

- add the visual suite to CI using the existing off-screen software-renderer
  script;
- publish reference, actual-shot, amplified-diff, logs, and native diagnostics
  as CI artifacts;
- enforce native capture dimensions, explicit color space, warm-up frames,
  stable frame time, and reference-review rules;
- add a stable second backend when its runtime is available and do not compare
  backends pixel-for-pixel unless the contract says they should match;
- add visual cases for resource reload, device reset, texture arrays/cubes,
  terrain seams, transparency sorting, clipping, post-processing, and
  screenshots after repeated frames;
- periodically verify that reference files are neither missing nor orphaned.

**Exit criteria:** visual tests are reproducible in CI, failures retain all
  diagnostic images, reference updates are reviewed, and backend-specific
  tolerances are explicit.

### P2 — Concurrency and race tests

**Purpose:** test behavior under schedules that serialized xUnit execution
  cannot expose.

**Targets:** `WorkQueue`, `ThreadSafeCache`, `ResourceCache`, async resource
loading, file watchers, event dispatch, pipeline caches, render-pool reuse,
and device-loss notifications.

**Harness requirements:**

- use barriers, controlled schedulers, and repeatable schedule seeds;
- run with varied worker counts, including one, two, and oversubscribed counts;
- force cancellation, exception, disposal, and reentrancy at synchronization
  boundaries;
- detect deadlock with a hard timeout and capture thread stacks;
- run in a worker process so a native crash or stuck thread cannot poison the
  remainder of the test suite;
- repeat short schedules many times and run longer schedules nightly.

**Exit criteria:** no known race-prone subsystem relies solely on serialized
  tests; failures include a replayable schedule and thread trace.

### P2 — Soak, leak, and restart tests

**Purpose:** catch defects that only appear after repeated lifecycle churn.

Run bounded endurance loops covering:

- engine startup/shutdown and headless/native graphics initialization;
- resource load/unload/reload and cache invalidation;
- texture, buffer, model, shader, render-surface, and pipeline creation;
- device invalidate/restore and window resize;
- scene attach/detach, repeated prefab load, and animation start/stop;
- watcher, worker, timer, and callback creation/disposal.

Record managed heap, native allocations, open file descriptors, threads,
native/GPU handles, cache sizes, and execution time at regular checkpoints.
Compare against a baseline and detect monotonic growth rather than requiring
perfectly flat memory. Include a final cleanup pass and assert that process
exit does not depend on finalizers.

**Exit criteria:** repeated runs have a documented stable envelope, no leaked
owned resource remains, and growth thresholds are enforced nightly or before
release.

### P2 — Performance regression tests

Use benchmark-style tests with controlled data sizes and explicit budgets for:

- Math hot paths and allocations;
- variant/archive/resource serialization;
- VFS/package lookup and decompression;
- octree insertion, update, and queries;
- animation, particle, terrain, and culling updates;
- draw-command construction and constant-buffer packing;
- shader compile, translation, optimization, reflection, and cache hit;
- render-pipeline pass construction and render-buffer reuse.

Measure throughput, p50/p95 latency, allocations, peak memory, and cache hit
rate where relevant. Warm up the runtime, separate startup from steady state,
and use fixed data sets. Store historical results and fail only on a reviewed
budget breach to avoid noisy CI.

**Exit criteria:** critical hot paths have baselines, allocation budgets, and
reviewed regression thresholds.

### P3 — Mutation testing

Mutation testing evaluates whether the existing tests detect plausible defects.
Start with a small manually selected mutation set:

- change `<` to `<=` in clipping, ranges, and LOD decisions;
- remove a mask check or invert a visibility condition;
- skip a disposal, event, cache invalidation, or revision increment;
- change blend/depth/cull state or a shader define;
- alter a serialization default or byte order;
- remove a device-loss restore branch;
- change an interpolation endpoint or integer rounding rule.

Use mutation results to strengthen weak assertions, not to chase a universal
score. Run focused mutation sets on pull requests and broader sets on a
schedule.

**Exit criteria:** critical subsystems have a reviewed mutation baseline and
mutations representing their most likely regression classes are killed.

## 6. Subsystem-to-approach map

| Subsystem | First additional approaches | Main risks addressed |
| --- | --- | --- |
| Math and Container | Property, mutation, performance, ABI | Numeric edge cases, invariants, allocation regressions |
| Core and Actions | State-machine, property, concurrency, mutation | Reentrancy, lifecycle, timing, variant ownership |
| IO and Resource | Fuzz, fault injection, security, golden | Malformed data, path escape, partial load, schema drift |
| Scene and Generator | State-machine, golden, compile/contract | Hierarchy transitions, generated output, reflection drift |
| Graphics and Render API | Metamorphic, fault injection, visual, soak | Device loss, state leakage, native failures, pixel regressions |
| Render Pipeline | Snapshot, differential, visual, performance | Pass ordering, cache keys, post-processing, frame cost |
| Bindings and Platform | ABI/contract, compatibility, fault injection | Layout/calling convention and environment differences |
| Navigation, IK, Physics | Property, differential, soak, fault injection | Geometry boundaries, native failure, long-run stability |
| Audio and Rml UI | Contract, state-machine, native fault, soak | Native ownership, callback order, context shutdown |
| Samples and Tools | End-to-end smoke, golden, security, compatibility | Packaging, startup, generated/output contracts |

Every type in a subsystem plan should be assigned at least one additional
approach when its risk profile justifies it. A type does not need every test
category; the assignment and intentional exclusions should be recorded.

## 7. Execution tiers and CI cadence

The repository currently has no project-owned CI workflow in these two
checkouts. Add the following tiers when CI is introduced.

| Tier | Runs | Time/behavior | Failure policy |
| --- | --- | --- | --- |
| Pull request | Existing unit/headless tests, contract tests, small property set, golden tests, deterministic fuzz smoke, targeted fault tests | Fast, deterministic, no GPU requirement except explicitly labeled tests | Block merge; no unexplained skips |
| Pull request graphics | Existing headless graphics plus opt-in off-screen visual smoke where runner supports it | Fixed OpenGL software backend and small reference set | Block graphics changes; publish images and diffs |
| Nightly | Larger property distributions, fuzzing, state machines, race schedules, leak/soak loops, full visual suite, performance sampling | Longer and process-isolated; retain all failure artifacts | Alert owner; promote recurring failures to PR coverage |
| Backend matrix | Native OpenGL, Vulkan/other supported backends, platform/runtime identifiers | Environment-labeled; no false cross-backend pixel equality | Block release backend, not unrelated PRs |
| Release qualification | Full compatibility/ABI, security corpus, mutation set, performance budgets, visual references, restart/upgrade fixtures | Clean machines or containers with pinned dependencies | Release-blocking and reviewed exception only |

Each test must declare its required tier, backend, runtime, data fixtures,
timeout, and whether it is expected to be skipped when an external dependency
is unavailable. An environment skip must be visible in the report.

## 8. Quality signals and gates

Track multiple signals; none is sufficient alone:

- line and branch coverage, with changed-code and high-risk subsystem views;
- mutation score for selected critical mutation sets;
- number and diversity of generated cases, minimized failures, and replay rate;
- fuzz corpus growth, executions, unique failures, timeout count, and crash
  minimization status;
- lifecycle ledger leaks, double releases, and unexpected callbacks;
- race test schedules completed, deadlocks, and reproducibility rate;
- visual pass rate, image difference classification, and orphaned references;
- performance p50/p95, allocations, peak memory, and cache hit rate;
- ABI/compatibility matrix pass rate and explicitly supported environments.

Required gates:

1. A production bug gets a deterministic regression test in the most suitable
   category before or with the fix.
2. A changed high-risk boundary has success, malformed-input, partial-failure,
   cleanup, and retry coverage where applicable.
3. No test may silently convert a crash, timeout, leak, or native failure into
   a pass.
4. Generated, serialized, and visual reference changes require reviewable
   artifacts and a stated reason.
5. Thresholds are baselined first, then only changed through an explicit
   review; a green test run does not justify a performance or coverage
   regression.
6. Every nondeterministic failure records enough information to replay it.

## 9. Phased implementation roadmap

### Phase 0 — Harness foundations

- Add seed, timeout, artifact, lifecycle-ledger, and fake-native primitives.
- Add a scoped Harmony patch helper for managed API seams, with automatic
  unpatching and conflict/leak detection.
- Define test naming, tier traits, environment reporting, and failure paths.
- Establish baseline branch coverage, allocations, startup time, visual
  repeatability, and native-handle counts.
- Add a CI entry point for the existing fast suite.

**Exit:** any failure can be reproduced locally, and cleanup diagnostics run
even when the test throws.

### Phase 1 — High-payoff, low-to-medium effort

- Add ABI/contract probes for native bindings and generated code.
- Add golden tests for stable serialization, shader normalization, and
  generated reflection.
- Add property tests for Math, Container, IO serialization, geometry, and
  animation invariants.
- Add the smallest deterministic visual CI smoke suite.

**Exit:** the highest-risk pure and native contracts have non-example-based
  coverage and are merge-gated.

### Phase 2 — Failure and behavioral exploration

- Add state-machine tests for resources, caches, events, scenes, and device
  lifecycle.
- Add fault injection at native, file, archive, resource, shader, and device
  boundaries.
- Use Harmony patches to reach managed failure points that cannot be replaced
  cleanly through existing interfaces, while keeping native fault shims for
  unmanaged calls.
- Add metamorphic comparisons for serialization, octree queries, animation,
  geometry, shaders, and repeated rendering.
- Add bounded security/adversarial corpora.

**Exit:** partial initialization, recovery, and malformed-input behavior are
  directly exercised rather than inferred from successful cases.

### Phase 3 — Long-running and environment coverage

- Add process-isolated race schedules for asynchronous components.
- Add leak/soak and restart tests with native-resource accounting.
- Add performance baselines and allocation budgets.
- Expand visual coverage and add stable backend/runtime matrix jobs.

**Exit:** concurrency, long-run, performance, and backend failures are visible
  before release.

### Phase 4 — Test effectiveness and release confidence

- Add focused mutation campaigns and review surviving mutants.
- Expand fuzz corpus from production bug fixes and field failures.
- Run full ABI, security, compatibility, performance, and visual qualification
  before releases.
- Periodically remove redundant tests and refresh baselines deliberately.

**Exit:** the suite demonstrates that it detects realistic code changes, not
  only that it contains many assertions.

## 10. Definition of done for a new test category

A category is considered adopted when it has:

- a documented purpose and risk owner;
- deterministic local reproduction and CI tier assignment;
- reusable fixtures or generators rather than one-off infrastructure;
- failure artifacts sufficient for diagnosis;
- cleanup and timeout behavior;
- at least one pilot subsystem and one documented limitation;
- a baseline metric and a reviewable regression policy;
- links from the relevant subsystem plan and this strategy document.

Do not add a new category merely to increase a test count. Add it when it
provides a distinct oracle, execution dimension, failure model, or quality
signal that the existing suite cannot provide.
