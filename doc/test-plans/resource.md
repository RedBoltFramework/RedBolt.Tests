# Resource subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Resource`. Existing tests are in
`ResourceTests`.

## Verified coverage

- [x] Synchronous resource begin/end lifecycle, failure propagation, and
  restoration to `Done` (`src/RedBolt/RedBolt/Resource/ResourceBoundaryTests.cs`).
- [x] `JSONValue` owned array mutation, deep clone independence, recursive
  value handling, and repeated disposal (`src/RedBolt/RedBolt/Resource/ResourceBoundaryTests.cs`,
  `src/RedBolt/RedBolt/Resource/JsonXmlResourceTests.cs`).
- [x] `JSONFile` empty/malformed input preservation, UTF-8/NUL-terminated
  input, indentation, archive callbacks, and callback error boundaries
  (`src/RedBolt/RedBolt/Resource/ResourceBoundaryTests.cs`,
  `src/RedBolt/RedBolt/Resource/JsonXmlResourceTests.cs`).
- [x] `PListValue` wrong-type/malformed geometry defaults, recursive clone
  ownership, and repeated disposal (`src/RedBolt/RedBolt/Resource/ResourceBoundaryTests.cs`,
  `src/RedBolt/RedBolt/Resource/PListFileTests.cs`).
- [x] Background success/failure/unknown-type events, queue cleanup, and
  disposal while a load is blocked (`src/RedBolt/RedBolt/Resource/ResourceCacheTests.cs`).

## Resource identity, formats, and ownership

- `InternalResourceFormat` and internal `ResourceFormat`: assert enum values
  and `PeekResourceFormat` for binary magic, XML/JSON markers, leading
  whitespace, empty/short/unknown data, NUL/non-ASCII bytes, and exhausted
  streams. Verify the source position is restored on success, unknown format,
  and exceptions; verify warning/error behavior without making logs the only
  assertion.
- `AsyncLoadState`: assert the complete queued/loading/success/fail/done state
  progression and invalid cast values. Test main-thread versus worker-thread
  `Resource.Load`, `BeginLoad` failure, `EndLoad` failure, state reset after a
  retry, and state visibility during background loading.
- `Resource`: use a minimal derived resource to test default load/save behavior,
  synchronous begin/end ordering, null streams, file load/save through a fake
  VFS, missing files, names/hashes/absolute names, memory-use and use-timer
  setters, asynchronous state, static resource-name/type/ref helpers, resource
  lists, type names, and disposal. Verify failed saves do not leave partially
  written files and that `LoadFromCache` handles empty names and missing cache.
- `ResourceGroup`: budget/use accounting, empty and populated dictionaries,
  type isolation, update after load/release/reload, and overflow/underflow
  policy. Do not mutate the exposed dictionary during normal tests unless the
  API explicitly promises that it is a live view.
- `ResourceRef` and `ResourceRefList`: default/empty/type-only/full values,
  parse with null/empty/missing fields/extra semicolons, Unicode names, clone
  independence, equality/hash, string and context-aware formatting, stable
  combined hashes, list ordering, duplicate names, and null enumerable input.
- `ResourceWithMetadata`: add/replace/remove/remove-all, insertion order,
  hash collisions, missing metadata returning `Variant.Empty`, ref-read
  behavior, deep copy, self-copy, disposal of replaced/removed values, and
  XML/JSON metadata load/save with empty, malformed, duplicate, custom, and
  resource-reference variants. Assert metadata survives derived-resource load
  and is not aliased between copies.
- `SimpleResource`: format hint selection, binary magic detection, JSON/XML/
  binary loading, custom XML root handling, unsupported/unknown format, empty
  and truncated streams, archive callback exceptions, save destination short
  writes, save-file failures, and preservation of the detected format. Use a
  small concrete derived resource with one scalar, one optional field, and a
  nested block to compare all three archive formats.
- `SerializableResource`: null/non-null `Serializable` values, clone and
  replacement ownership, archive serialization, JSON/XML/binary round trips,
  unknown factory/type, malformed payload, failed save/load, and repeated
  dispose.
- `ResourceRouter`: a recording derived router should cover route mutation,
  no-op routing, empty/invalid identifiers, router exceptions, recursion guard,
  and ordering when multiple routers are registered.

## Resource cache and background loading

- `ResourceCache`: construct with an isolated context/VFS and cover default and
  custom loaders, file-backed factories, manual-resource insertion and
  duplicate rejection, resource directories and mount cleanup, type/name/hash
  resolution, canonical/resolved identifiers, search-packages-first behavior,
  missing resources, return-failed-resources policy, resource-not-found and
  unknown-type events, temporary versus cached resources, loader exceptions,
  null/invalid names, and resource type mismatches.
- Cache budgets and release: test per-type and total memory budgets, current
  memory use, automatic eviction candidates, exact use-timer ordering, forced
  versus non-forced release, release by type/name/partial name/all, duplicate
  names across types, dependency cleanup, and resource disposal exactly once.
  Include a resource whose memory use changes during load and a resource that
  cannot be evicted because it is still referenced.
- Reloading and routing: verify reload started/finished/failed events, reload
  preserves the existing object on failure, dependent reload order, dependency
  reset, ignored paths, suspended reload queue and flush-on-resume, duplicate
  file-change coalescing, reflection removal, file timestamp lookup, and
  exception handling from a router or resource loader. Add concurrent read/
  reload/release tests if the cache is intended to be used from workers.
- `BackgroundLoadItem`: required failure flag, default completed task, mutable
  resource/task fields, dependency/dependent set identity, duplicate dependency
  behavior, and state transitions under a synthetic load.
- `BackgroundLoader`: queue deduplication, unknown type, missing file, caller/
  dependency graph, dependency completion, `WaitForResource`, bounded
  `FinishResources`, zero/negative/large time budgets, success/failure events,
  return-failed-resources policy, `GetNumQueuedResources`, task exceptions,
  cancellation, queue disposal while tasks are running, and repeated dispose.
  Use controllable derived resources with barriers so tests do not rely on
  scheduler timing or `Thread.Sleep` for correctness.
- `ResourceEvents` payloads and `Localization.ChangeLanguage`: default values,
  reload/load/background success and failure payloads, file/resource-name
  fields, unknown type field, nullable background resource, ref mutation, and
  event order. Assert events are emitted once per operation and not after
  cache/resource disposal.

## JSON values and JSON resources

- `JSONValueType`, `JSONNumberType`, and `JSONValue`: construct every scalar,
  null, array, and object form; preserve integer/unsigned/real number type;
  test type names and reverse lookup with unknown/case-varied names; getters
  with defaults and wrong types; equality/hash/clone; set/reset/clear; array
  push/pop/insert/erase/resize/indexing; object get/set/erase/contains; missing
  key/index behavior; recursive replacement; self-assignment; null children;
  aliasing of source arrays/dictionaries; disposal of replaced/removed children;
  and repeated disposal. Include negative indexes, invalid erase ranges,
  duplicate keys, empty strings, Unicode, very large numbers, NaN/infinities,
  and deep nesting.
- JSON/variant conversion on `JSONValue`: round-trip every supported scalar,
  vector/map/string-map/string-vector/resource-ref/resource-ref-list and
  serializable custom variant; missing/wrong `type`/`value`, null context,
  unknown factory, malformed nested data, unsupported custom values, context
  propagation, deep ownership, and cleanup after a conversion failure.
- `JSONFile`: load empty/whitespace/NUL-terminated/UTF-8 data, comments,
  trailing commas, scalar/array/object roots, malformed JSON, short reads,
  oversized input, duplicate/invalid constructs, and source-name diagnostics.
  Test save indentation (empty, one-character, multi-character, Unicode),
  exact round-trip, short/failing destination, `ToString`, `FromString`, root
  replacement/disposal, memory use, and parse error reporting on/off.
- `JSONFile` archive callbacks: save/load named `IArchiveSerializable` and
  `RedObject` values, root naming, nested blocks, empty arrays, optional fields,
  duplicate/missing/wrong JSON types, archive exceptions clearing output but
  preserving a usable file, null callbacks/values, and callback exceptions.
- `JSONArchiveBase<TBlock>`, `JSONOutputArchiveBlock`,
  `JSONOutputArchive`, `JSONInputArchiveBlock`, and `JSONInputArchive`: test
  root construction/from-existing values, human-readable flags, ordered versus
  unordered versus array blocks, size hints, root-name mode, inline blocks,
  duplicate names, missing elements, safe blocks, long/ulong representation,
  hex bytes, VLE values, wrong JSON types, malformed numbers/strings, array
  bounds, delayed exceptions, block-path diagnostics, clone state of input/
  output blocks, and disposal of block/value ownership. Assert output never
  claims unordered lookup and input `HasElementOrBlock` is exact.

## XML values, elements, archives, and XPath

- `XMLFile`: parse valid XML with declaration/comments/processing instructions/
  doctypes (and verify their removal), empty/invalid/short/UTF-8 input,
  multiple roots, malformed inheritance patches, inherited resource lookup,
  dependency registration, clone/copy semantics, root matching, create/get-or-
  create behavior, save indentation, `ToString`, `FromString`, memory use,
  document disposal, archive callbacks, and destination short writes.
- `XMLElement`: null/default handles, validity and navigation (`GetChild`,
  `GetNext`, `GetParent`, `NextResult`), clone/copy/dispose, names/value/text,
  child creation/removal, attributes and attribute order, case transforms,
  all scalar/math/color/rect/matrix/quaternion/bounding-box getters/setters,
  buffers/hex, every variant/container/resource-ref conversion, defaults for
  missing or malformed values, duplicate attributes, invalid destinations,
  XPath-result lifetime, and use after owning `XMLFile` disposal.
- `XMLArchiveBase<TBlock>`, `XMLAttributeReference`,
  `XMLOutputArchiveBlock`, `XMLOutputArchive`, `XMLInputArchiveBlock`, and
  `XMLInputArchive`: cover attribute-versus-child encoding, root-name mode,
  ordered/unordered/array blocks, size hints, inline blocks, duplicate names,
  missing children/attributes, wrong element types, long/ulong/string/hex/VLE
  encoding, path diagnostics, delayed close errors, child ordering, and block
  clone/disposal. Verify XML output is stable and XML input does not consume a
  sibling when a field fails.
- `XPathResultSet`: empty/first/index/out-of-range/count/size/empty/enumeration,
  clone and copy independence, navigator cloning, result order, weak file
  lifetime, dispose and post-dispose behavior, and `NextResult` linkage.
- `XPathQuery` plus `XPathVariableContext`, `XPathVariable`, and
  `XElementXPathNodeIterator`: compile/evaluate boolean, number, string, and
  node-set expressions; empty/missing/disposed elements; variables of all
  supported types, duplicate/conflicting declarations, invalid variable syntax
  and names, rebinding/clear/dispose, wrong return type, no result, node-set
  clone/current-position/move-next behavior, XPath whitespace/number/string
  conversion including NaN/infinities/negative zero, and query exceptions.

## Property lists

- `PListValueType`, `PListValue`, and private `ValueUnion`: construct null,
  int/bool/float/string/map/vector values; set/copy/clone/equality/hash;
  explicit bool conversion; map/vector deep ownership; replacement/removal;
  empty and duplicate keys; missing/wrong typed getters; malformed nesting;
  null input; overflow/NaN; disposal of children; and repeated dispose.
- `PListFile`: parse valid plist dictionaries/arrays/scalars, empty and
  malformed XML, missing `<plist>`/`<dict>`, dangling keys, duplicate keys,
  nested values, source short reads, root replacement after reload, memory use,
  root lifetime, disposal, and error handling without retaining partial values.

## Graph resources

- `GraphNodeProperty`: name/hash synchronization, scalar and compound variant
  ownership, archive serialization, clone/replacement/dispose, empty names,
  malformed property blocks, and disposal when serialization fails.
- `GraphPin`, `GraphDataPin`, `GraphOutPin`, `GraphInPin`, `GraphEnterPin`, and
  `GraphExitPin`: name/type/value state, input/output/enter/exit distinctions,
  connect/disconnect, repeated or incompatible connections, missing/expired
  targets, connected state, ref-read values, variant replacement/disposal,
  archive round-trips, invalid target ids/names, and repeated dispose.
- `GraphPinRef<TPin>`: default/null truth operators and conversions, valid node/
  pin identity, connected-pin lookup for exit/input pins, invalid/unconnected
  refs, wrong connected pin type, disposed graph/node, and the documented
  exception behavior for unsupported pin kinds.
- `GraphNode`: id/graph/name/hash, property add/get/ref mutation, duplicate
  property replacement, input/output/enter/exit add/get by name/index, counts,
  fluent `With*` overloads, duplicate names, invalid indices, connection
  setup, archive serialization of omitted/empty arrays, duplicate ids, and
  disposal order for every owned pin/property.
- `Graph`: add/create/remove/clear, id allocation/reuse/wraparound and
  collision handling, moving nodes between graphs, null/already-owned nodes,
  id list order, connected-pin lookup after node removal, XML/JSON load/save,
  empty/malformed/duplicate-id documents, archive failure cleanup, and graph
  disposal. Explicitly pin the expected behavior of the `Connect` entry points
  (including whether they should mutate pin targets); test that behavior rather
  than accepting a no-op accidentally.

## Images, DDS, and decompression

- `Image`: use small deterministic 1/2/3/4-component images to test size/data
  allocation, zero/negative/overflow dimensions, set/clear pixels, bounds,
  compressed-image rejection, alpha and format flags, raw/bilinear/trilinear
  sampling at edges/outside range, horizontal/vertical flips, resize including
  one-pixel dimensions, subimages with clipped/empty rectangles, level/sibling
  chains, precalculation/cleanup, conversion to RGBA, compressed-level access,
  GPU format selection, memory-use updates, clone-like decompressed outputs,
  disposal, and all save/load methods. Include BMP/PNG/TGA/JPG/DDS/WEBP success,
  unsupported formats, invalid names, quality/compression boundaries, short
  destinations, and native codec failure gates.
- `ImageCube`: load six-face and parameter XML inputs, missing/duplicate faces,
  mismatched dimensions/formats, compressed/decompressed mip levels, face
  indexing, `GetImage(s)`, parameter lifetime, spherical-harmonics mip level,
  nearest sampling and direction/UV projection at axes/edges/zero direction,
  spherical-harmonics calculation/create/reuse, invalid face/level, disposal,
  and malformed cube resources.
- DDS layout structs `DDColorKey`, `DDPixelFormat` (including
  `MultiSampleCapabilities`), `DDSCaps2`, `DDSHeader10`, `DDSurfaceDesc2`, and
  `CompressedLevel`: default/field layout, exact sizes/packing, FourCC masks,
  clone/value behavior, compressed level dimensions/offset/size/bounds, and
  ABI-compatible serialization fixtures.
- `ImageDds` and internal `DdsFormatInfo`: `MakeFourCC`, format selection for
  legacy masks/DXGI formats, component matching, valid BC/ETC/PVRTC/float/
  uncompressed headers, mip/array/cube/depth combinations, pitch/size
  calculations, truncated/oversized/invalid headers, unsupported formats,
  destination bounds, and decompression error paths. Add a native decoder
  smoke suite only where the codec is available.
- `Decompress`: for every implemented texture format, compare horizontal and
  vertical block flips against a reference block transform; test unsupported
  formats, invalid block sizes, undersized/oversized destination/source,
  overlapping spans, one-block and odd block counts, and no-op behavior for
  formats that are not implemented. `IsFlipBlockImplemented` must be checked
  against the actual switch table.

## Localization and generic binary resources

- `ChangeLanguage` and `Localization`: language list/index/name defaults,
  case/whitespace behavior, string-pattern formatting, language changes and
  event emission, missing IDs/indexes/fallbacks, duplicate translations,
  reset, single- and multi-language JSON shapes, malformed values, empty
  language, file load failures, and disposal. Test replacement versus merge
  semantics and deterministic selection when multiple languages exist.
- `BinaryFile`: empty/binary/text/UTF-8 data, short source/destination,
  clear/set data/set text/lines, memory use, mutable buffer and serializer/
  deserializer views, archive callback round trips, null callbacks/values,
  callback exceptions and buffer cleanup, save failure, reload replacement,
  and disposal.
- `Decompress`/image/resource integration: verify a resource loaded through a
  cache preserves decoded dimensions, memory accounting, name, async state,
  and ownership when decode or post-load processing fails.

## Resource-wide gates

Use deterministic in-memory streams, a fake `ResourceCache`/VFS, disposal-counting
resources and variants, and a controllable background scheduler. Every parser
must test malformed/truncated input and leave the object in a reusable state;
every owner must test replacement, exception cleanup, and repeated dispose. Keep
native codecs, filesystem reloads, background threads, and platform-specific
image formats in separate integration gates with bounded timeouts and leak
detection.
