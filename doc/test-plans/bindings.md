# Handwritten bindings test plan

Source: `../RedBolt/src/RedBolt/Bindings`. Generated binding files are build artifacts and
are tested through their owning subsystem and the binding smoke tests.

- `BlendFactors`: verify constructor/property values, zero/one/mixed factors,
  value equality if exposed by the generated record, and native ABI layout.
- `ShaderCodeBufferDesc` extensions: pass null/empty/non-empty reflection
  descriptors, verify the helper returns the expected shader-variable names,
  types, array sizes, and offsets, and assert malformed native descriptors do
  not read outside their buffers.
- `JoltRayCastResult`: verify field order, size/alignment, default values,
  round-trip through a Jolt call, hit/miss values, maximum fraction, invalid
  body id, and safe conversion between RedBolt math types and projected Jolt
  types.

Run these as ABI/layout tests against the generated native library in addition
to pure value tests. Include missing-library and wrong-architecture failures
as explicit diagnostic cases; do not make the unit suite depend on a native
GPU/audio device.

## Verified checks

- [x] `BlendFactors` and `JoltRayCastResult` field order, size, defaults, and value-copy behavior (`HandwrittenBindingsValueTests`).
- [x] Generated shader-library contexts can initialize and finalize when their native libraries are present (`ShaderLibraryBindingTests`).
- [ ] Missing-library and wrong-architecture diagnostics are environment gates, not unconditional unit cases.
