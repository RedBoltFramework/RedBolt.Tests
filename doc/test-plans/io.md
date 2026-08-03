# IO subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/IO`. Existing tests are in
`IOTests` (with package-tool coverage in the same area).

## Verified coverage

- [x] `MemoryBuffer` bounded reads/writes, seek clamping, sliced read-only
  memory, unmanaged/null pointers, clone state, and repeated disposal
  (`src/RedBolt/RedBolt/IO/MemoryBufferBoundaryTests.cs`).
- [x] VLE boundary values, embedded-NUL/UTF-8 strings, empty lines, and EOF
  behavior (`src/RedBolt/RedBolt/IO/SerializationBoundaryTests.cs`).
- [x] Serializer/deserializer compound values, short-write propagation,
  variant payloads, resource-reference lists, and nested archive ownership
  (`src/RedBolt/RedBolt/IO/SerializerTests.cs`, `src/RedBolt/RedBolt/IO/DeserializerTests.cs`,
  `src/RedBolt/RedBolt/IO/ArchiveTests.cs`).
- [x] Binary and human-readable archive round trips for vectors, matrices,
  variants, resource references, curves, and tetrahedral mesh data
  (`src/RedBolt/RedBolt/IO/ArchiveTests.cs`, `src/RedBolt/RedBolt/Resource/JSONArchiveTests.cs`).

## File identifiers, streams, and buffers

- `FileMode`, `VectorBinaryEncoding`, `ScanFlags`, `ArchiveBlockType`, and
  `FileChangeKind`: assert exact enum values and flag composition. Exercise
  every scan combination (files/dirs/hidden/recursive/append), invalid cast
  values, and all archive/vector encoding branches, including invalid or zero
  packing parameters.
- `FileIdentifier`: default/empty, scheme-plus-path, URI parsing for relative,
  Unix absolute, Windows-drive, `file:` with zero/one/two/three/four slashes,
  custom schemes, empty paths, missing colon, whitespace, backslashes, repeated
  separators, `.`/`..`, root preservation, and traversal above root. Verify
  `ToUri`, `AppendPath`, `+`, `SanitizeFileName`, bool operators, comparison,
  equality/hash, `ToString`, and clone independence. Include null string fields
  produced by default/interop construction and unusual Unicode names.
- `Serializer`: use a recording bounded serializer to verify little-endian
  bytes, return values, and failure propagation for every scalar, signed and
  unsigned width, floating point value (including infinities/NaN), bool,
  `IntRect`, integer/float vectors, packed vector and quaternion encodings,
  matrices, color, bounding box, strings, file IDs, hashes, buffers, resource
  refs/lists, every `Variant` type, vectors/maps/string maps, VLE boundaries,
  and lines. Test empty/large/embedded-NUL/Unicode strings, null values where
  accepted, short writes, zero-length spans, `Name`, disposal, and write-after-
  dispose behavior.
- `Deserializer`: pair with a short-read/short-seek fake and round-trip every
  `Serializer` overload. Cover position/size/tell/eof, relative seeks in both
  directions and at boundaries, truncated input, zero-length buffers, malformed
  or oversized lengths, invalid packed encodings/parameters, invalid UTF-8,
  unterminated lines, VLE overflow/truncation, unknown variant types, missing
  context for resource references, and disposal. Verify failed reads do not
  silently advance position unless that is the documented contract.
- `AbstractFile`: a minimal derived fake should cover name, open/closed state,
  absolute name, close/dispose idempotence, position/size defaults, and virtual
  `Read`/`Write`/`Seek`/`IsEof` delegation contracts.
- `MemoryBuffer`: construct from unmanaged memory, byte array, sliced and
  non-array `ReadOnlyMemory<byte>`, UTF-8 text, and `VectorBuffer`; test null
  pointers, zero size, read-only writes, exact/short reads and writes, seek
  clamping, position preservation, direct pointer lifetime, clone sharing or
  copying semantics as intended, pinned-handle release, and double dispose.
  Include a GC pressure test around array-backed and sliced buffers.
- `VectorBuffer`: empty/array/pointer/stream construction, short source reads,
  set-data replacement, zero and large resize, growth zero-fill, shrink
  clamping, seek/read/write at EOF, overflow, clear, raw buffer/data pointers,
  custom/default name, clone deep-copy and position/name preservation, repeated
  pin/unpin, finalizer/dispose cleanup, and access after disposal. Verify a
  source array or pointer is not unexpectedly aliased after `SetData`.

## Files and IPC

- `File`: open/read/write/read-write modes, missing paths, empty names,
  directories, permissions, files above the 4GB limit, `Flush`, `Close`,
  repeated open, and disposal. Test mode rejection for reads/writes, partial
  reads/writes, EOF and sparse seeks, read-to-write and write-to-read
  synchronization, checksum caching/restoration of the original position,
  binary/text helpers, UTF-8 and empty files, and `Copy` preconditions and
  exact byte counts. Use a real temporary file for normal paths and a fake
  stream/error seam for I/O exceptions.
- Packaged `File` reads: create valid uncompressed and compressed package
  fixtures, open existing/missing entries, offsets, entry checksums, seeks
  forward/backward, block-boundary reads, truncated block headers/data,
  decompression failure, and package close while an entry is live. Verify
  packaged files are read-only, `IsPackaged`, `AbsoluteName`, `Name`, and
  position/size remain entry-relative. Add an Android/SDL asset smoke gate for
  `/apk/` paths, read-only enforcement, null handles, and native read/seek
  failures; keep it separate from portable tests.
- `NamedPipe`: server/client handshake with unique names, asynchronous data
  arrival, partial and empty reads, EOF before/after disconnect, server reopen
  requirement, write-before-connect, peer disconnect, invalid/duplicate names,
  zero-timeout client failure, cancellation, close during pending read, and
  repeated close/dispose. Assert no deadlock, no leaked task, and that
  `SetName` reports its unsupported operation without changing the name.

## Archives and binary/base64 serialization

- `ArchiveBlock`, `ArchiveBlockBase`, `Archive`, and `ArchiveBase`: test empty
  and archive-backed construction, move construction/assignment, swap,
  disposal ownership, inline-block open/close, sequential/unordered/array and
  safe block helpers, nested block paths, flushing, version values, name
  validation, duplicate/missing/unexpected element errors, EOF/error paths,
  delayed exception storage/flush, invalid hex decoding, close-on-dispose, and
  operations after a block/archive has been closed. Verify block size hints,
  safe flags, and `CurrentBlockPath` diagnostics at multiple nesting levels.
- `BinaryOutputArchiveBlock` and `BinaryInputArchiveBlock`: verify parent
  serializer/deserializer selection, block type/name, inline close behavior,
  unordered support reporting, next-element position for input, clone state,
  close exactly once, and rejection of unsupported random access. Test parent
  disposal and child disposal in both orders.
- `BinaryOutputArchive` and `BinaryInputArchive`: round-trip all scalar,
  string, byte-span, VLE, nested block, safe block, sequential, unordered,
  and array operations. Assert duplicate names, illegal names, missing names,
  wrong block type, wrong value type, truncated data, trailing bytes, size-hint
  mismatch, version mismatch, malformed length, short underlying I/O, and
  delayed write/read failures. Exercise zero/maximum integer values, empty
  arrays, nested arrays, and serialized values containing `Variant` payloads.
- `Base64OutputArchive` and `Base64InputArchive`: base64 text round trips for
  empty, one/two/three-byte tails, binary zero/high-bit bytes, line endings,
  nested blocks, and versioned data. Test invalid characters, bad padding,
  whitespace policy, truncated input, non-ASCII input, output flush/close, and
  preservation of archive diagnostics. Compare the decoded payload to the
  binary archive byte-for-byte where formats are intended to match.

## Generic archive serialization

- `ArchiveValueSerializer<T>` and the primitive/vector overloads in the partial
  `ArchiveSerialization`: verify each delegate routes to the archive exactly
  once and round-trips `CString`, `StringHash` (with and without a string hint),
  float/double/int vectors, matrices, rectangles, quaternions, colors, and all
  supported engine math values. Include zero/default values, unusual numeric
  values, and wrong archive modes.
- Enum and optional helpers: `SerializeEnum`,
  `SerializeStrictlyOptionalValue`, `SerializeOptionalValue`, tuple/value
  overloads, and nullable/reference overloads must cover every enum entry,
  unknown numeric value, missing name, duplicate name, invalid index/string,
  absent optional value, present default value, null reference, construction
  failure, and version/backward-compatibility behavior. Assert strict optional
  fields fail when absent while permissive fields retain their default.
- Variant helpers: `SerializeValue(VariantType)`,
  `SerializeVariantInBlock`, `SerializeValue(Variant)`,
  `SerializeVariantAsType`, `SerializeValue` for `VariantVector`, `VariantMap`,
  `StringVector`, `StringVariantMap`, and `SerializeStringVector` should use a
  matrix containing every scalar, string, hash, resource reference/list,
  buffer, math, curve, vector/map, and custom value variant. Test missing type
  blocks, type coercion, mismatched types, null/empty collections, duplicate
  keys, deep clone/ownership, unsupported custom values, context-dependent
  deserialization, malformed primitive text, overlong arrays, and exceptions
  from nested serializers.
- Partial specializations in `ArchiveSerialization.Object`,
  `.LightProbeGroup`, and `.TetrahedralMesh`: round-trip null/non-null shared
  objects, object identity/reference reuse, light probes and baked collection
  data (including empty lists and spherical-harmonic arrays), tetrahedra,
  vertices, matrices, and meshes with empty/large/mismatched arrays. Verify
  malformed counts, missing blocks, invalid indices, ownership of reconstructed
  objects, and cleanup when a child serializer throws.
- `ConsumeArchiveException`: success, `ArchiveException`, non-archive
  exception, error logging enabled/disabled, exception swallowed versus
  rethrown behavior, null action, and archive state after a failed callback.

## Compression

- `Compression`: table-test `EstimateCompressBound` around zero, tiny, block,
  and large sizes; compress/decompress empty, incompressible, highly
  compressible, and binary data; exact destination sizing; undersized or
  oversized buffers; invalid/truncated/corrupt input; destination size
  mismatch; and repeated operations. Test `CompressStream` and
  `DecompressStream` with short/failing serializers, empty streams, partial
  reads, compression levels outside the normal range, and checksum/position
  preservation. Test `CompressVectorBuffer`/`DecompressVectorBuffer` for empty
  buffers, names/positions, round-trip content, and failure behavior without
  leaking intermediate buffers.

## Mount points and package files

- `MountPoint` and `WatchableMountPoint`: use a minimal fake mount to assert
  scheme acceptance, existence/open/scan/absolute-name/identifier defaults,
  last-modified defaults, watch enable/disable idempotence, disposal, and
  abstract start/stop calls. Test a watch callback that fails and verify the
  mount remains in a defined state.
- `MountedRoot`: accept only the file scheme case-insensitively, map absolute
  paths, reject relative/wrong-scheme paths, open read/write/read-write files,
  create missing parent directories, scan files/directories/hidden and
  recursive entries, timestamp behavior, identifier round trips, and failed
  permissions/nonexistent paths.
- `MountedDirectory`: normalize relative/absolute/trailing-separator paths,
  scheme matching, safe path joins, exists/open/read/write, parent directory
  creation and failure, scan filter/append/recursive/files/dirs/hidden
  semantics, absolute-name conversions, timestamps, and watcher enable/disable
  event emission. Include path traversal and wrong-scheme cases.
- `MountedExternalMemory`: verify construction from a `VectorBuffer`, scheme
  and name, read-only versus writable behavior, entry bounds, existence/open,
  timestamp/absolute-name/identifier semantics, scanning, source buffer
  disposal/lifetime, and invalid/null/empty backing data.
- `MountedAliasRoot`: add/remove aliases with and without a scheme, weak mount
  references, alias and scheme matching, nested paths, replacement/removal,
  expired mounts, wrong schemes, exists/open/timestamp/absolute-name routing,
  alias sanitization, and disposal without retaining mounts. Assert unsupported
  scan and absolute-name-to-identifier behavior explicitly.
- `PackageEntry` and `PackageFile`: build valid UPAK/ULZ4/RPAK/RLZ4 fixtures,
  with zero/many entries, offsets, checksums, compressed payloads, external
  file-list offsets, and trailer-discovered start offsets. Verify metadata
  getters, entry order/name/index bounds, platform-specific case sensitivity,
  scheme acceptance, read-only `OpenFile`, timestamps, filtered and recursive
  scans, path-prefix boundaries, malformed IDs/headers/counts/offsets/sizes,
  duplicate names, truncated package data, invalid compressed data, reopen,
  and disposal clearing all state.

## Virtual filesystem and guards

- `VirtualFileSystem`: mount root/alias/directory/package and explicit mount
  points; exercise mount ordering/precedence, duplicate mounts, null mounts,
  unmount/unmount-all, automatic directory/package discovery, mount count/index
  bounds, aliases, canonical and absolute identifier conversions, exists/open,
  read/write text, timestamps, scan append/recursive/filter flags, watch state,
  concurrent mount/read/unmount access, and disposal ownership. Verify failed
  mounts do not alter ordering or leak a mount.
- `MountPointGuard`: null and live construction, automatic mount/unmount,
  `Get`, `Release`, dispose idempotence, move assignment including self/empty
  guards, transfer of ownership, context/subsystem absence, and disposal
  ordering when the VFS is already disposed. Use a disposal-counting mount to
  prove the guard does not double-dispose or retain a released mount.

## Watchers and event payloads

- `FileChange`, `IOEvents.LogMessage`, and `IOEvents.AsyncExecFinished`: default
  field values, all change kinds, old-name rules, clone value independence,
  event payload mutation, and exact primitive field types.
- `FileWatcher`: invalid/missing/inaccessible directories, start replacement,
  stop/dispose idempotence, subdirectory option, path normalization, delay
  clamping and zero-delay delivery, add-change coalescing by filename,
  renamed old-name preservation, unrelated changes, queue order policy,
  concurrent producers/consumers, and change removal only after delivery.
  Exercise native `FileSystemWatcher` created/deleted/changed/renamed events in
  a temporary tree, while using `AddChange` for deterministic timing tests.
  Isolate the environment-clock delay from portable unit tests and assert no
  event/task survives disposal.
- `MultiFileWatcher`: multiple valid/invalid roots, delay propagation to
  existing watchers, first-ready watcher selection, stop/dispose cleanup,
  repeated start/stop, concurrent change delivery, and behavior when one
  watcher fails or is disposed. Verify no duplicate change is returned across
  roots.

## IO-wide gates

Use bounded in-memory serializer/deserializer fakes to make every short-read,
short-write, malformed-input, and exception path deterministic. Use isolated
temporary directories and package fixtures for filesystem tests; never depend
on repository files or the current working directory. Every disposable/native
resource needs repeated-dispose and exception-cleanup coverage, and every
round-trip test must also assert position, size, name, ownership, and checksum
side effects. Keep Android SDL, named-pipe, filesystem-watcher, and compressed
package tests as explicit integration gates with timeout and leak detection.
