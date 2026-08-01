# Core subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Core`. Existing tests are in
`CoreTests`.

## Verified coverage

- [x] `StringHashRegister` registration, lookup, missing values, collision
  preservation, internal-map visibility, and thread-safe access.
- [x] `Spline` linear sampling clamps out-of-range input and cloned splines do
  not share mutable knot storage.
- [x] Ref-event payload mutation, active sender/handler visibility, and event
  state cleanup after dispatch.
- [x] Ref-event handler exceptions propagate while clearing active dispatch
  state.
- [x] `ArchiveException` preserves its serialization failure message and
  `InvalidOperationException` base contract.

## Object, reflection, and events

- `ArchiveException`: message, inner/archive failure context, serialization
  paths that throw it, and preservation of the original exception.
- `RedObject`, `ObjectExtensionMethods`, `ObjectCategory`, `CoreEvents`, and
  `EventHandler`: type/category names, assignability, subsystem forwarding,
  global variables, block-events behavior, archive default failure, event
  sender/handler access only during dispatch, disposal removing receiver and
  sender links, and all frame event payload defaults/order.
- `Context`, `RefEventHandler<T>`, `StructEventSubscriptionBucket`, and
  `ActiveEventInvocation`: fresh/default and custom runtime services, global
  instance replacement/reset, assembly/type registration, subsystem lookup by
  generic/interface/hash, duplicate and conflicting registrations, SDL
  ref-count and failed acquire/release, event subscription with general and
  sender-specific filters, priority/order, duplicate subscriptions, every
  unsubscribe overload, GC-collected receivers, removed senders, unsubscribe
  during dispatch, nested dispatch, blocked sender/receiver, event exceptions,
  active sender/handler visibility, global variant clone/replacement/destruction,
  and context disposal with live subsystems.
- `ObjectReflection<T>`, `ObjectReflection`, `ObjectReflectionRegistry`, and
  `GeneratedReflectionRegistration`/`ContextRegistration`: managed generic
  type naming and ids, attributes/categories/factory creation, inherited and
  removed attributes, default updates, category removal, duplicate reflection,
  type removal, disposal of owned `AttributeInfo`/`Variant` values, assembly
  registration order, external generated assembly refresh, and generated
  initializer isolation across contexts.
- `ReflectedAttribute`, `SerializedAttribute`, and `RemoveSerializedAttribute`:
  constructor defaults, named/value-type/accessor metadata, target usage,
  invalid declarations, inheritance, removal, and exact metadata order.
- `AttributeMode`, `AttributeScopeHint`, `AttributeMetadata`,
  `AttributeAccessor`, `DelegateAttributeAccessor`, `AttributeInfo`, and
  `AttributeHandle`: getter/setter and object conversion, missing accessor,
  read-only/file mode `ShouldLoad`/`ShouldSave`, defaults, enum name/value
  conversion, metadata missing/wrong type, clone independence, handle fluent
  metadata/scope updates, and disposal of metadata variants.

## Variants and custom values

- `VariantType`, `Variant`, `VariantStorage`, and `VariantValue`: table-test
  every scalar, string, `CString`, hash, pointer, vector/matrix, color/rect,
  buffer, resource reference/list, variant vector/map/string map, curve, and
  custom value type. Verify constructors, `Type`, `GetObject`, typed getters,
  optional getters, implicit conversions, `Clear`, `SetType`, `Set`, clone,
  equality/hash/string form, serialization names, zero values, and disposal.
- `Variant` interpolation and parsing: test `Lerp` for every supported
  interpolatable type, t=0/1/out-of-range/NaN, mismatched types, integer
  rounding, unsupported types, scalar/string conversion, JSON/archive parse
  success/failure, malformed tokens, wrong token counts, overflow, empty/null,
  and resource-reference context requirements. Verify deep copy at every
  setter/map/list boundary and destruction of replaced/removed payloads.
- `VariantMap`, `VariantVector`, `StringVector`, and `StringVariantMap`: test
  constructors, add/try-add/indexer replacement, remove with/without output,
  insert/range/remove-all/clear, duplicate keys, missing keys, cloneable
  `Variant` ownership, enumeration mutation policy, disposal, and deep clone.
- `VariantTypeUtility`: assert exact native sizes, CLR type mapping, generic
  and runtime type inference, interpolation support, serialize/deserialize
  round trips for all types, malformed JSON, null/default values, and unknown
  enum/type values.
- `CustomVariantValue`, `IArchiveSerializable`, and
  `CustomVariantValueImpl<T>`: empty/object/type construction, typed retrieval,
  zero/equality/hash/string behavior, clone independence, cloneable/disposable
  payload ownership, serialization, wrong-type retrieval, null/default
  payload, and double dispose.

## Curves, spline, time, and small services

- `Spline` and `InterpolationMode`: empty/one/many knots, linear/Bezier/
  Catmull-Rom interpolation, t boundaries/outside range, mismatched variant
  types, knot insert/remove/index errors, clone/equality/disposal, and mutation
  after a returned value clone.
- `VariantCurve`, `VariantCurvePoint`, and `KeyFrameInterpolation`: sorted
  keyframes, duplicate times, index hints, linear/step/Bezier/Catmull-Rom,
  integer fallback, empty curve, serialization, clone, and owned variant
  disposal.
- `ObjectRevisionTracker`: initial revision, repeated updates, wraparound
  skipping zero, and derived-class visibility.
- `StopToken`: default state, stop idempotence, copies sharing state, and
  concurrent stop/read visibility.
- `StringHashRegister`: register/lookup/copy, duplicate/conflicting strings,
  empty/missing values, thread-safe versus non-thread-safe operation, and
  disposal/global-register lifetime.
- `SubsystemCache`: cached and dynamic slots, fully-qualified C# type names,
  interface/base lookup, explicit hash ids, duplicate replacement, remove,
  clear, enumeration, and disposal of owned disposable subsystems.
- `ThreadSafeCache<T>`: invalidated default, restore/get, invalidate, concurrent
  first restore, exception/cancellation, and no stale value after invalidation.
- `Time`, `Timer`, `HiresTimer`, and `TimedCounter`: frame begin/end progress,
  elapsed/reset, frequency/zero frequency, interval accumulation over multiple
  updates, zero/negative intervals, and thread/time-source determinism.
- `Profiler`: constants, unset versus existing thread name, empty/Unicode name,
  repeated calls, and unsupported runtime behavior.
- `ProcessUtils`, `PlatformId`, and `Print/argument/system helpers`: quoted
  argument parsing, escapes/empty quotes/whitespace, skip-first semantics,
  explicit list input, platform name/id, CPU and memory lower bounds, mini-dump
  directory slash normalization, UUID/process/user/host/OS values, URL success
  and failure, and output/error paths without corrupting process state.
- `CommandLineParser`, private `CommandLineOption`, `FlagCommandLineOption`,
  `ValueCommandLineOption`, and `CommandLineParseResult`: alias normalization,
  flags/options, repeated and
  conflicting aliases, missing/extra values, `--help`, unknown options,
  positional arguments, callback order/exceptions, usage formatting, empty
  aliases/descriptions/value names, and exact success/help/failure records.
- `WorkQueue`, `WorkItem`, `WorkQueueVector<T>`, `ParallelForState`,
  `ParallelForStateCache`, `ReadOnlyListParallelForState<T>`,
  `ReadOnlyListParallelForStateCache<T>`, `MultiVectorParallelForState<T>`,
  and `MultiVectorParallelForStateCache<T>`: subsystem registration,
  zero/multiple initialization,
  thread count/index, priority queues, immediate/main-thread/delayed tasks,
  post order, completion and incomplete counts, exceptions/cancellation,
  reentrancy, work-item reuse, `ForEachParallel` empty/small/large ranges and
  collections, full coverage/no duplicate indices, and disposal while work is
  pending. Add race tests and a no-allocation warmed-path test where required.

## Cross-cutting gates

Every disposable core value needs success, exception, repeated-dispose, and
clone-then-mutate coverage. Event tests must assert payload mutation by `ref`
handlers and safe removal during dispatch. Variant tests should be generated
from one authoritative type matrix so no supported type is accidentally left
untested.
