# RenderAPI subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/RenderAPI`. Existing tests are in
`RenderApiTests`; visual/device gates are in
`VisualRegression`.

## Verified coverage

- [x] `RenderAPIUtils` backend/shader naming, backend selection, format
  mapping/categories, mip sizing, sRGB conversion, and archive round trips
  are covered (`RenderAPIUtilsTests`).
- [x] `DeviceObject` registration, invalidate/restore/destroy callbacks,
  data-loss state, debug names, and disposal are covered on a graphics
  fixture and with direct headless/reordered event tests
  (`RenderDeviceTests`, `DeviceObjectBoundaryTests`).
- [x] `RawBuffer` shadow/mapping/pool behavior and native loss/restore paths
  are covered (`RawBufferTests`).
- [x] `RawTexture` parameter validation, normalization, sampler-copy
  isolation, dirty/resolve state, pooling, and readback/upload paths are
  covered (`RawTextureTests`).
- [x] `RenderContext` swap-chain/custom-target binding, viewport state,
  output descriptors, reset, multi-target clears, and debug scopes are covered
  (`RenderContextTests`).
- [x] Render-device frame index/stats, defaults, screenshot, window settings,
  device loss/restore, and closest fullscreen-mode selection are covered
  (`RenderDeviceTests`).

## Definitions, descriptors, and cache keys

- `RenderApiLimits`, `FrameIndex`, `RenderBackend`, `WindowMode`, and
  `ShaderTranslationPolicy`: exact constants/enum values, flag/ordering
  assumptions, unknown values, and backend-specific policy combinations.
- `FullscreenMode` and `WindowSettings`: default constructor values, zero and
  negative sizes/refresh rates/monitor/MSAA, title and orientation ownership,
  equality/record behavior, and serialization if consumed by config code.
- `VulkanDescriptorPoolSize`, `RenderDeviceSettingsVulkan` and nested
  `QueryPoolSizeBuffer`, plus `RenderDeviceSettingsD3D12` and its
  `CpuDescriptorHeapAllocationSizeBuffer`, `DescriptorHeapSizeBuffer`, and
  `QueryPoolSizeBuffer`: inline-array length/index behavior, nullable override
  fields, defaults, copy semantics, extreme/negative values, backend isolation,
  and archive round trips. Verify no array element aliases another settings
  instance.
- `RenderDeviceSettings`, `RenderDeviceCaps`, `RenderDeviceStats`, and
  `RenderPoolSettings`: defaults, backend/window/native handle values,
  capability/stat mutation, cache frame thresholds/load factors/log period,
  invalid ranges, and deterministic serialization/hash behavior where used.
- `RawBufferParams`, `RawTextureParams`, and `RawTextureUAVKey`: all buffer/
  texture types, sizes, strides, flags, dimensions, formats, mip/slice counts,
  MSAA, usage, bind flags, and equality/hash. Test `ReadOnly`, `WriteOnly`,
  `FromLevel`, and `FromSlice` for zero/negative/out-of-range counts and
  chaining order; ensure keys differ whenever the native view differs.
- Buffer/texture/shader enums, `TextureFormatInfo`, `EmulatedTextureFormat`,
  `VertexShaderAttribute`, and all vertex/filter/address/coordinate/device/
  clear/stencil/parameter/pipeline enums: exact values and table coverage.
  Assert format component sizes, block dimensions, compressed flags, depth/
  stencil/sRGB classification, and unsupported/unknown formats.
- `SamplerStateDesc`: default/nearest/bilinear/trilinear constructors, all
  address modes, comparison/filter/anisotropy/border values, equality/hash/
  `ToHash`, invalid enum values, and stable key behavior.
- `InputLayoutElementDesc` and `InputLayoutDesc` (including `ElementBuffer`):
  empty/one/many elements, stream/offset/semantic/index/type, immutable copy,
  equality/hash, duplicate elements, maximum element/stream limits, invalid
  values, and mutable underlying-array isolation.
- `ImmutableSamplersDesc` (including `NameBuffer` and `DescBuffer`): clear/add,
  duplicate names, capacity limits, sampler replacement, ordering, equality/
  hash, inline-array boundaries, and disposal/ownership if native descriptors
  are attached.
- `PipelineStateOutputDesc` (including `RenderTargetFormatBuffer`): default
  depth/stencil/render-target formats, sample count, blend/write masks, array
  bounds, equality/hash, invalid target counts/formats, and stable cache keys.
- `GraphicsPipelineStateDesc`, `ComputePipelineStateDesc`, and
  `PipelineStateDesc`: graphics/compute initialization, shader identity,
  layout, rasterizer/depth/blend/output/immutable sampler fields, debug names,
  null/partial descriptors, `IsInitialized`, pipeline type, equality/hash,
  copy independence, and cache-key changes for each field. Test wrapping a
  graphics descriptor as compute and vice versa, and mutation after wrapping.

## Serialization and utility functions

- `ShaderBytecode`: empty/type/data construction, deep copy/clone, archive
  serialization, save/load short streams, invalid type/length, null data,
  repeated load, and name/position preservation.
- `RenderAPIUtils`: backend predicates/name conversion, shader type conversion,
  vertex attribute parsing (all valid names, indexes, whitespace, unknown and
  malformed names), shader input names, mip count/size/byte-size for 1D/2D/3D,
  compressed/depth/sRGB formats, zero/negative/large dimensions and invalid
  levels, backend selection with requested/unavailable/unsupported backend,
  texture-format mapping from internal backend formats, and serialization of
  Vulkan/D3D12 settings and descriptor pool sizes with nullable/invalid values.

## Device-object lifetime

- `DeviceObject`: construction flags, registration with a device, debug name,
  data-lost state, `Invalidate`/`Restore`/`Destroy`/`ProcessDeviceObjectEvent`
  transitions, duplicate/reordered events, device disposal, removal while an
  event is active, and repeated dispose. Use a fake device/object registry to
  assert exact callbacks and no stale registrations.
- `RenderDevice`: isolated fake/native device construction, post-initialize,
  swapchain creation/resize/present, window-setting changes, default filter and
  anisotropy clamping, restore/invalidate/emulate-loss, queued pipeline reload,
  screenshot success/failure, capability/format/MSAA queries, backend/settings/
  stats/frame-index access, default textures/queue/context/pool handles,
  fullscreen mode enumeration/closest-mode selection (empty/ties), device
  object add/remove, and disposal ordering. Include no-window/headless,
  missing factory, native initialization failure, and device loss during a
  frame. Backend-specific behavior belongs in D3D/OpenGL/Vulkan smoke gates.
- `RenderContext`: target reset/swapchain targets/custom targets, viewport and
  clip-plane state, clear depth/stencil/color validation, execute empty and
  populated queues, render-target binding detection, stats mutation, current
  target/viewport descriptors, debug-scope flag, and disposal after device loss.
  Verify duplicate targets, null depth, too many render targets, incompatible
  formats/MSAA, invalid viewport, and command execution failure cleanup.
- `RenderScope`: null/non-debug contexts, all formatted constructors/acquire
  overloads, nested scopes, native GL error lookup unavailable/failed, format
  arguments, and repeated/disordered dispose. Assert a scope never changes
  rendering state when acquisition fails.

## Buffers, textures, and views

- `RawBuffer` and private `MapKind`: create empty/valid buffers with all types,
  strides, sizes, shadowed/dynamic/immutable flags; update whole/range with
  exact, short, oversized, negative, and overlapping ranges; map/unmap read,
  write and discard paths; double map/unmap; resolve/invalidate/restore/destroy;
  shadow-data aliasing; handles; device loss; and disposal. Include native
  map failure and a fake handle that records update ranges.
- `RawTextureHandles` and `RawTexture`: validity/default handles, construction
  from parameters/native handles (D3D11/D3D12/Vulkan), format/MSAA validation,
  sampler descriptors, UAV creation/cache/retrieval for read/write level/slice
  keys, level generation, resolve, read into span/array, bounds/short buffer,
  dirty flags, GPU memory calculation for all formats/mips/slices, invalidate/
  restore/destroy, native handle ownership, and disposal. Test unsupported
  external handles, null/zero handles, mismatched parameters, device loss, and
  duplicate UAV keys.
- `RenderTargetView`: swapchain color/depth, raw/resource texture, slices,
  read-only depth, null/invalid textures, dirty marking, format/MSAA queries,
  view identity/hash, device resolution, out-of-range slices, and use after
  texture/device disposal.
- `ConstantBufferCollectionRef` and `ConstantBufferCollection`: alignment
  initialization including zero/non-power-of-two values, block growth and
  padding, buffer count/used/GPU size, returned references/data lifetime,
  clear/reinitialize, and overflow. `StoreParameter` must table-test every
  supported `Variant`, unmanaged scalar/vector, matrix, vector array, and
  matrix array against exact byte layout, alignment, transpose/row-major
  rules, destination short/oversized spans, unsupported types, and no partial
  writes on failure.
- `RenderPool` and private `TextureCacheEntry`/`TextureCacheEntryGroup`: fake
  device allocation/reuse of uniform buffers and textures, persistence keys,
  incompatible parameter reuse, scratch allocation/release including null/
  duplicate/unknown pointers, frame-end recycling, min/max frame and load
  factor policies, invalidate/restore, device loss, and disposal. Assert no
  returned object is recycled while still in use.

## Shader reflection and pipeline state

- `UniformBufferReflection`: default/size/hash/group, variable registration,
  offsets/sizes, duplicate names, invalid group/offset, disposal of native
  variables, and lookup after disposal.
- `UniformReflection`: name/hash, offset/size/type, equality/record behavior,
  and exact lookup for missing/colliding names.
- `ShaderResourceReflection`: resource name/type/variable, null native
  variable, disposal, duplicate resources, and lookup by name/hash.
- `ShaderProgramReflection`: construct from empty/null/mixed shaders and a
  temporary GL program, uniform/resource/UAV discovery, duplicate shader
  declarations, cross-stage layout conflicts, stable hashes, lookup of every
  group/name, native reflection failure, connection to shader variables for
  graphics/compute, incompatible bindings, and disposal. Use fake `RawShader`
  bytecode/reflection metadata plus a native smoke gate.
- `PipelineState`: create from graphics/compute descriptors, object-ID family
  allocation/acquire/release, handle/reflection/binding access, invalidate/
  restore/destroy, shader reflection failure, owner/cache disposal, repeated
  reload, and private temporary layout/immutable-sampler cleanup. Verify IDs
  are reused only after release and no pipeline uses a destroyed descriptor.
- `PipelineStateCache`: initialize empty/valid/corrupt/truncated cached bytes,
  serialize/deserialize round trips, graphics/compute cache hits/misses,
  descriptor mutation, release/refcount/eviction, native handle absence,
  duplicate pipeline requests, device loss/reload, and disposal. Assert cached
  data is deterministic and a failed cache entry does not poison future loads.

## Command recording and execution

- `ShaderResourceRange`, `RawVertexBufferArray`, `ConstantBufferRefArray`, and
  `DrawCommandDescription`: default fields, range start/end invariants,
  vertex stream/count/offset bounds, constant-buffer group bounds, resource/
  UAV ranges, scissor deduplication, draw/dispatch parameters, and value-copy
  behavior. Test maximum limits and invalid ranges.
- `DrawCommandQueue` and private `ConstantBuffersData`, `ShaderResourceData`,
  `UnorderedAccessViewData`, and `Temporary`: reset state, pipeline-first
  contract, stencil/scissor/clip state, parameter group cache hits/misses and
  layout changes, every parameter overload, wrong size/type, missing shader
  variable, nullable and backup resources, resource/UAV commit ranges, vertex/
   index binding, non-indexed/indexed/instanced draw and compute dispatch, zero
  and invalid counts, command ordering, and execution into a fake context.
  Verify constants/resources are copied at commit time, queues can be reused
  after reset, stats count only executed commands, and an execution exception
  does not leave native bindings or pooled buffers leaked.
- Execute with capability matrices: missing base-vertex/base-instance,
  unsupported clip distance, unavailable compute, read-only depth and MSAA
  incompatibilities, missing default/backup textures, null shader handles,
  device loss during execution, empty queue, and multiple queues/contexts.

## RenderAPI-wide gates

Start with pure descriptor/hash/serialization tests and fake native handles;
then run a headless backend smoke suite for one graphics and one compute path,
followed by device-loss/restore and screenshot/visual gates where supported.
Every native object must be tested through success, creation failure, loss,
restore, destroy, repeated dispose, and owner-disposal paths. Assert native
resource ownership and cache-key equality explicitly; reflection/shape tests
alone are insufficient for these contracts.
