# Container subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Container`. Existing tests are in
`ContainerTests`.

## Verified coverage

- [x] `CString` malformed, unclosed, negative, and out-of-range formatting
  placeholders (`ContainerTests/BoundaryOwnershipTests.cs`).
- [x] `KeyFrameSet<T>` sorted insertion, before/between/after lookup, empty
  lookup, clone ownership, and disposal of stored cloneable values
  (`ContainerTests/BoundaryOwnershipTests.cs`,
  `ContainerTests/KeyFrameSetTests.cs`).
- [x] `Array2D<T>` row-major indexing, clamped/wrapped access, ref mutation,
  deep cloning, and disposable-element lifetime
  (`ContainerTests/Array2DTests.cs`).
- [x] `IndexAllocator`/`IDFamily<T>` allocation, reuse, shrink, clear, and
  idempotent release (`ContainerTests/IndexAllocatorTests.cs`).
- [x] `CString` UTF-8/embedded storage, no-allocation hot paths, clone
  independence, formatting, and repeated disposal (`ContainerTests/CStringTests.cs`).

- `ArrayDimensions2D`: default/positive/zero/negative dimensions, capacity
  overflow, containment, clamp and wrap (including negative coordinates and
  zero dimensions), row-major index/offset round trips, invalid indices, clone,
  and swap. Define whether invalid inputs are rejected or only debug-asserted.
- `Array2D<T>`: all constructors and resets, default versus fill values,
  mutable ref indexer, optional null ref, wrapped/clamped access, begin/end and
  enumeration, resize clearing old disposable/cloneable elements, swap, deep
  clone for `ICloneable`, shallow copy for ordinary values, dispose exactly once,
  and allocation/overflow failures. Include reference-containing structs and
  a post-dispose access test.
- `CString`, `SmallBuffer`, and `CStringInterpolatedStringHandler`: empty,
  ASCII, multibyte UTF-8, embedded NUL, inline-to-heap growth, pointer
  materialization, terminated bytes, clear, clone, equality/hash, implicit
  conversion, format indexes/alignment/specifiers/escaped braces/nulls,
  interpolated strings, long formatting, unsupported allocating types,
  malformed/unclosed/negative/out-of-range placeholders, format exceptions,
  `ReadOnlySpan<byte>` input, and double dispose. Add no-allocation tests for
  the documented hot paths and verify heap pointers are not aliased by clones.
- `ConstString`: default/string construction, cached hash, conversion to
  string/hash, clone identity, empty and Unicode values, and immutable behavior
  after source-string changes.
- `IndexAllocator`: sequential allocation, reuse after release, invalid,
  duplicate, zero, and very large release, shrink tail behavior, clear,
  next-free/size invariants, and concurrent allocate/release if thread safety
  is part of the contract.
- `IDFamily<T>`: one id per instance, explicit acquire/release, repeated
  release, finalizer/Dispose path if applicable, generic-family isolation,
  reuse after destruction, and static allocator thread safety.
- `KeyFrameSet<T>`: sorted insertion, duplicate and equal timestamps, index
  lookup before/at/between/after frames, missing index, ref mutation, remove
  one/all, cloneable/disposable element copy/destruction, clone independence,
  and disposal after partial construction.
- `ManagedSequence`: equal lists for empty/one/many values, length mismatch,
  null/reference values, comparer/default equality, and mutation after compare.
- `MultiVector<T>` and nested `Index`: resize/clear while preserving outer
  vectors, push/emplace/pop, empty and invalid outer/inner indices, mutable
  refs, size and flattening order, `CopyTo` replacement semantics, mutable
  underlying collection, cloneable/disposable element ownership, managed
  reference-containing structs, repeated resize, and concurrent access policy.
- `StringHash`, `NoReverse`, and `StringHashRegister`: empty/default hash,
  UTF-8/Unicode and case sensitivity, exact FNV/hash constants, span/array
  length overloads, conversion/equality/order/bool/hash formatting, mutable
  value, reverse lookup with registered/unregistered/no-reverse values,
  thread-safe and non-thread-safe register, duplicate/conflicting strings,
  missing lookup, and clearing/lifetime of global registration.

## Container-wide gates

For every container with `ICloneable`/`IDisposable`, prove ownership with a
clone-and-mutate test and a disposal counter. For every ref-returning API,
test that mutation reaches storage and invalid access cannot create an unsafe
write. Add property tests for index/offset and collection size invariants.
