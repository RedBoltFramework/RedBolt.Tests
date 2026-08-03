# Particles subsystem test plan

Source: `../../RedBolt/src/RedBolt/Particles`. Existing tests are in
`ParticlesTests`.

- `EmitFrom`: assert native enum/name order, name lookup for every value,
  invalid values, and stable error behavior.
- `Emitter`: construct defaults, set/get rate, duration, burst, lifetime,
  shape, direction, size, color, and randomization; test zero/negative rates,
  duration boundaries, reset, serialization, invalid enum values, and
  deterministic emission with a seeded source.
- `ParticleGraphContainerType`, `SparseSpan`, and all `Span`/`SpanVariants`
  types: default/empty spans, dense and sparse indexing, mutable ref indexer,
  bounds and missing-index behavior, scalar versus container capacity, reset,
  clone/value semantics, variant conversion, and invalid strides.
- `ParticleGraphSpan`, `AttrSpan`, `PinSpan`, and `ParticleGraphMemory` value
  paths: verify byte offset/size/alignment, zero length, overflow, overlapping
  spans, container capacity one-versus-many, and safe access after reset.
- `ParticleGraphAttributeLayout`: reset, insertion order, reuse of matching
  name/type, rejection or replacement of conflicting type/container, contiguous
  offsets, alignment, capacity changes, empty layout, and overflow.
- `ParticleGraphBufferLayout`: scalar allocation versus container capacity,
  repeated reset retaining spans, allocation exhaustion, zero capacity, and
  deterministic offsets.
- `ParticleGraphPinFlag`, `ParticleGraphPinRef`, `ParticleGraphPin`, and
  `ParticleGraphTypedPin`: default and requested constructors, direction and
  type flags, name changes, source assignment, input/output connection rules,
  clone independence, generic type mapping, invalid/null source, and
  protected mutator rejection. Test self-connect, repeated connect, and
  incompatible pin type/container combinations.

Add a layout/ABI test for every public value type and a fuzz/property test for
random valid pin/layout graphs. Assert all rejected operations leave the graph
unchanged.

## Verified checks

- [x] Emit-from native order/name table, spans, sparse indexing, memory/layout offsets, pin construction/connection, clone independence, and typed variant mapping (`ParticlesTests`).
- [x] Public particle value ABI/layout and boundary assertions for spans, layouts, pin connections, and enum underlying types (`src/RedBolt/Particles/ParticlePublicValueBoundaryTests`).
- [x] Seeded replayable pin-operation coverage is included in
  `ParticleGraphPinTests`; full graph-evaluator fuzzing is an explicit gate
  because the managed surface has no evaluator or native particle backend
  entry point.
