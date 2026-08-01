# Graphics subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Graphics`. Existing unit and headless
coverage is in `GraphicsTests`; native/device and image comparisons are in
`VisualRegression`. The backend implementation plan is separate in
[`graphics-backends.md`](graphics-backends.md).

This plan is for behavior tests only. It covers the public classes and value
types in the subsystem, and names private helper classes where their state or
ownership must be observed through the owning public class.

## Verified coverage

- [x] Headless `Graphics` window/frame/render-target calls are no-ops with
  stable return values, and repeated `Close`/restore calls are safe
  (`GraphicsHeadlessLifecycleTests`).
- [x] Headless `Texture2D` validates dimensions and update bounds without
  requiring a native device; readback correctly reports unavailable GPU data
  (`GraphicsHeadlessLifecycleTests`, `TextureHeadlessTests`).
- [x] `RenderSurface` tracks slices, linked surfaces, viewport ownership,
  queued updates, resolve dirtiness, and disposal (`RenderSurfaceTests`).
- [x] Renderer viewport queue deduplication, uninitialized-frame behavior,
  default resources, frame counters, and device-state boundaries are covered
  (`RendererTests`, `RenderDeviceTests`).
- [x] The OpenGL fixture lifetime test is available as an opt-in native gate;
  it verifies context exposure, close, and reinitialization when a display is
  present (`GraphicsBackendLifecycleTests`).

## Test tiers and shared fixtures

- Use pure tests for enums, descriptors, value types, clone/equality/hash
  contracts, animation math, shader-source text processing, and geometry
  layout. Use `Context`/`HeadlessEngineFixture` for resources and components.
- Use a fake `RenderDevice`, `RenderContext`, `RawBuffer`, `RawTexture`, and
  pipeline/shader cache for GPU-facing classes. The fake must record creation,
  updates, bindings, invalidation, restoration, destruction, and ownership so
  tests can distinguish a missing handle from a failed operation.
- Keep SDL/window, HarfBuzz, glslang, SPIR-V Tools, shader compiler, and real
  backend tests opt-in. Each native gate needs diagnostics for missing loader,
  display, font, compiler, or driver and must still cover the pure failure
  path with a fake.
- Every `RedObject`, resource, buffer, texture, render surface, query, cloned
  value, subscription, and native wrapper must be disposed in success,
  exception, partial-initialization, device-loss, and repeated-dispose paths.
- Make time, frame number, random values, resource lookup, and event delivery
  deterministic. Record event sender, receiver, payload, order, duplicate
  suppression, and behavior when a receiver unsubscribes during dispatch.
- For all indexed APIs, table-test empty, one, last valid, out-of-range,
  negative, maximum, duplicate, null, and disposed inputs. For numeric state
  include zero, negative, maximum/minimum finite values, NaN, and Infinity
  wherever the implementation accepts a `float`.

## Definitions, settings, events, and utilities

- `LockState`, `LegacyVertexElement`, `CubeMapLayout`,
  `RenderSurfaceUpdateMode`, `FaceCameraMode`, and `ShadowQuality`: exact
  numeric values, default values, unknown enum values, and serialization or
  switch behavior. Verify that legacy element order, semantic/index pairs,
  offsets, and `GraphicsDefs.ElementTypeSizes` remain in sync.
- `GraphicsDefs`: all vertex/pixel shader parameter `StringHash` constants,
  `MaxRenderTargets`, `MaxVertexStreams`, legacy element span contents, and
  returned spans' read-only/bounds behavior. Test collisions or accidental
  renaming by comparing every constant to the shader parameter contract.
- `GraphicsSettings`: default render-device settings, shader translation and
  validation flags, shader-cache directory, source logging, cache discard,
  value-copy semantics, and invalid/partial combinations. Ensure mutating a
  copied settings value does not mutate the `Graphics` instance.
- `GraphicsEvents`: `ScreenModeEvent`, `WindowPosEvent`,
  `RenderSurfaceUpdateEvent`, `BeginRenderingEvent`, `EndRenderingEvent`,
  `BeginViewUpdateEvent`, `EndViewUpdateEvent`, `BeginViewRenderEvent`,
  `ViewBuffersReadyEvent`, `ViewGlobalShaderParametersEvent`,
  `EndViewRenderEvent`, `EndAllViewsRenderEvent`, `RenderPathEvent`,
  `DeviceLostEvent`, `DeviceResetEvent`, and `GpuResourceReleasedEvent`.
  Verify default payloads, null optional references, exact field preservation,
  and dispatch ordering through a fake renderer.
- `DrawableEvents`: `BoneHierarchyCreatedEvent`, `AnimationTriggerEvent`,
  `AnimationFinishedEvent`, `ParticleEffectFinishedEvent`, and
  `TerrainCreatedEvent`. Test null owners, variant data, loop flags,
  duplicate event delivery, and handlers that remove themselves.
- `GraphicsUtils.InitializeInputLayout`,
  `InitializeInputLayoutAndPrimitiveType`, `SetBuffersFromGeometry`,
  `AddShaderResource`, and `AddNullableShaderResource`: empty and sparse
  streams, missing buffers, instancing stream placement, element/stream limit
  truncation, primitive propagation, index binding, null texture logging,
  backup texture selection, and no partial queue mutation after rejection.
- `Tangent`: triangle tangent generation for valid UVs, mirrored UVs,
  degenerate triangles, repeated vertices, non-indexed and indexed geometry,
  missing normals/UVs, zero-area triangles, and deterministic handedness.

## Graphics subsystem and frame resources

- `Graphics`: initialization with valid settings, headless/no-window mode,
  missing backend/factory, unsupported backend, invalid window size or
  multisampling, and failures after each native resource has been created.
  Test window position, title/icon, fullscreen/borderless/resizable/vsync,
  monitor and refresh rate, resize, screen-mode events, `Width`/`Height`,
  render-target dimensions, API name, native device/factory/swap-chain/window
  accessors, OpenGL support, max hardware bones, instancing and shadow
  capability queries, and current viewport/target state.

  Exercise default texture/material creation, clear color and clear flags,
  render-target binding, swap-chain present, framebuffer resolve, custom
  uniform packing for every supported scalar/vector/matrix/array field,
  alignment and overflow rejection, normal-buffer pass creation, fullscreen
  pipeline/cache reuse, shader diagnostic flags, GPU-resource-release events,
  device lost/reset restoration, and disposal after a partially completed
  frame. Assert that an error leaves no stale current target, pinned memory,
  cache entry, event subscription, or native handle.
- Private `StructCustomUniformWriter<T>`, pipeline-key records,
  `PipelineResources`, `FullscreenPipelineResources`,
  `TemporaryLayoutArray`, `TextureDescOwner`, `TextureViewDescOwner`,
  `ResolveTextureSubresourceAttribsOwner`, `UploadedGeometry`,
  `TemporaryImmutableSamplers`, `UploadedTexture`, and `PinnedBuffer`: test
  through `Graphics` with fake native objects. Cover key equality and cache
  separation, exact disposal order, pinned-buffer lifetime, upload reuse and
  invalidation, temporary descriptor cleanup, and a failed upload/compile that
  is retried successfully.
- `Renderer`: initialization and shutdown, default zone/material/light
  resources, backbuffer surface, viewport queueing/removal, texture filter,
  anisotropy and quality clamping, hardware/software skinning selection,
  frame statistics, frame info, reset-view behavior, device events, and
  rendering with no viewport, null surface, invalid viewport, or disposed
  scene. Verify viewport update/render event order and no double-render when a
  surface is queued through multiple paths.
- `LightVsVariation`, `VertexLightVsVariation`, `LightPsVariation`,
  `DeferredLightVsVariation`, `DeferredLightPsVariation`, `SkinningMode`,
  and `FrameStatistics`: exact variation partitions, mutable/ref-return
  semantics, zero-frame defaults, counter reset/increment behavior, and
  unsupported hardware fallback.
- `RenderSurface`: construction from each texture kind and slice, dimensions,
  sample count, auto-resolve, render-target/depth classification, view and
  read-only-depth view creation, update mode, manual/always/visible queueing,
  linked color/depth surfaces, viewport add/remove/replace, resolve-dirty
  transitions, invalid slice/parent/disposed texture, and repeated disposal.
- `Viewport`: scene/camera/cull-camera weak-reference behavior, viewport
  rectangle and clipping, debug drawing, auto render-pipeline selection,
  pipeline-view replacement, stereo/right-eye camera, expired references,
  invalid/negative rectangles, and disposal after the scene or pipeline has
  gone away.

## Vertex/index buffers and geometry

- `VertexMask` and `VertexElement`: every mask bit, combinations, unknown
  bits, semantic/index/step-rate/offset defaults, element type size, hash,
  equality, and duplicate/overlapping layout entries. Verify invalid enum
  values are either rejected or preserved according to the source contract.
- `VertexBuffer`: create empty and valid layouts, element-mask derivation,
  vertex count/stride, dynamic and shadowed flags, lock/map/unmap/update/
  resolve, raw-buffer access, debug name, device-loss transitions, invalid
  size/stride/data ranges, short and oversized data, layout mutation after
  creation, and repeated invalidate/restore/destroy. Assert the computed
  layout signature and pipeline-state hash change only when relevant data
  changes. Exercise the private `VertexRawBuffer` adapter through the owner
  and verify its `RawBuffer` callbacks are not invoked after owner disposal.
- `DynamicVertexBuffer`: first allocation, append/write ranges, growth at
  exact capacity and just over capacity, shrink/resize policy, zero vertices,
  oversized writes, shadow-data preservation, commit/update behavior, and
  disposal while a caller still holds the underlying `VertexBuffer`.
- `IndexBufferType` and `IndexBuffer`: 16/32-bit index size, count and range,
  dynamic/immutable/shadowed creation, update/map/unmap/resolve, raw buffer,
  debug name, data-loss and restore, byte-count overflow, misaligned input,
  invalid index width, and repeated disposal. Exercise the private
  `IndexRawBuffer` adapter through device loss, restore failure, and owner
  disposal.
- `DynamicIndexBuffer`: growth and reuse at capacity boundaries, index-size
  changes, zero/oversized updates, data preservation and disposal. Verify no
  old native buffer is used after resize and no stale shadow span is exposed.
- `PrimitiveType`, `GeometryType`, and `Geometry`: vertex-buffer count/order,
  index buffer, primitive, starts/counts, LOD distance, effective index
  count, empty/headless state, geometry object-ID acquire/release/reuse,
  clone/dispose, out-of-range LOD and buffer access, null buffers, and
  pipeline-state hash invalidation. Test indexed and non-indexed draw ranges,
  all primitive types, and mutation after a source batch has captured the
  geometry.
- `SourceBatch`: default identity transform, multiple world transforms,
  geometry/material/type/lightmap/instancing fields, clone independence,
  equality/hash, dispose of owned transform arrays, null members, zero and
  maximum transform counts, and no aliasing between default instances.
- `PipelineStateSubscription` and `PipelineStateTracker`: subscribe/unsubscribe
  callbacks, clone behavior, dirty/hash recalculation, duplicate and reentrant
  notifications, owner disposal, callback exceptions, and subscription
  disposal order. Use a minimal derived tracker to exercise protected seams.

## Textures, image resources, and surfaces

- Internal `TextureFormatUtility`: format conversion, component order,
  compressed/depth/stencil/sRGB classification, bytes-per-pixel/block,
  mip dimensions, unsupported/unknown formats, zero/negative dimensions,
  and overflow. Keep these tests independent of a GPU.
- `Texture`: resource load/save and metadata, format/width/height/depth/
  levels, filter/address/anisotropy/shadow-compare/min-max LOD/linear state,
  backup texture, render-target/depth/UAV flags, multisample/auto-resolve,
  level dimensions, render surfaces, data-lost state, lock/unlock/update,
  mip generation, readback, conversion, and restore/destroy. Cover malformed
  files, truncated level data, unsupported format, invalid level/slice,
  null backup, non-renderable usage, device loss, and repeated disposal.
- `Texture2D`: one-level and mipmapped 2D creation/load, image upload and
  readback, lock rectangle/row pitch, render target and multisample resolve,
  resize/recreate, format conversion, zero dimensions, invalid mip ranges,
  short buffers, and GPU restore.
- `Texture2DArray`: layer count and per-layer/mip upload/readback, layer
  bounds, render-surface slice selection, array format/size validation,
  partial updates, missing layer data, resolve, device loss, and disposal.
- `Texture3D`: depth slices, volume mip dimensions, sub-box update/readback,
  slice bounds, compressed/depth format restrictions, zero depth, row/slice
  pitch, render-target/UAV flags, and restore after loss.
- `CubeMapFace` and `TextureCube`: all six face values, face/mip update and
  readback, cube render surfaces, horizontal/vertical/cross/Blender/NVIDIA
  layout conversion, face ordering/orientation, incomplete cube rejection,
  invalid face/level, seamless/backup behavior, and disposal after a partial
  face upload.
- Private `TextureRawTexture`: test via `Texture` with fake `RawTexture`
  handles. Assert parameter forwarding, native view creation, dirty/data-lost
  transitions, restore failure cleanup, and owner-before-raw disposal order.

## Camera, drawables, culling, and scene integration

- `ViewOverride` and `Camera`: default projection and clip values, perspective
  versus orthographic projection, aspect auto/manual mode, FOV/zoom/ortho
  size, near/far validity and reversed ranges, projection offset, vertical
  flip/reverse culling, reflection plane, clip plane, reflection/clipping
  enablement, fill mode, zone and masks, effective ambient/fog fallback and
  zone override, mouse position validity, frustum/ray/screen-coordinate
  calculations, GPU view-projection, and repeated transform/viewport changes.
  Include zero/negative viewport, invalid projection, NaN/Infinity, null zone,
  expired references, and disposal.
- `CameraOperator`: track/untrack nodes, automatic bounding-box tracking,
  padding, center/size updates, empty/expired node sets, duplicate nodes,
  node removal during update, invalid padding, and disposal while tracking.
- `DrawableFlags`, `UpdateGeometryType`, `GlobalIlluminationType`,
  `ReflectionMode`, `FrameInfo`, `CachedDrawableZone`,
  `ReflectionProbeReferenceArray`, and `CachedDrawableReflection`: defaults,
  flags/masks, fixed-size probe-array indexing, cache revision/position/
  invalidation distance, ref-return mutation, null owners, and value copy.
- `Drawable`: bounding box/world bounding box and dirty invalidation, octree
  insertion/removal, draw/shadow/occluder/occludee masks, distance/LOD/sort
  values and bias, pipeline-state hash, source batches and world transforms,
  view-camera tracking, zone/reflection/light-probe cache invalidation,
  geometry update scheduling, occlusion triangle count/draw behavior, clone
  and component lifecycle, and abstract subclass contracts. Exercise the
  private `DrawablePipelineStateTracker` through derived drawables and assert
  hash invalidation follows drawable changes without retaining the owner
  after disposal. Test an object moving across octants, disappearing/
  reappearing, changing masks during a frame, and being disposed while queued.
- `StaticModelGeometryData` and `StaticModel`: geometry-data defaults and
  copy behavior, model assignment/replacement, geometry/LOD selection,
  bounding box, occlusion LOD, lightmap bake/index/scale/offset, material and
  resource-reference attributes, batch creation, invalid model/geometry,
  device loss, and clone/dispose.
- `StaticModelGroup`: instance-node add/remove, duplicate and expired weak
  references, per-instance transforms and batches, empty group, bounding-box
  updates, visibility and disposal during iteration.
- `AnimatedModel`: skeleton creation/reset, model replacement, animation LOD
  bias, update-invisible, morph list/weights, master/slave behavior, bone
  hierarchy events, animation-trigger/finished events, geometry and batch
  updates, invalid morph index, and disposal with active animation.
- `LightMode`, `LightType`, `LightImportance`, `BiasParameters`,
  `CascadeParameters`, `FocusParameters`, and `Light`: all
  bias/cascade/focus defaults, split/range/quantization behavior, all light
  modes/types/importance, color and physical temperature,
  brightness/indirect/specular/negative light, radius/length/range/FOV/
  aspect/fade distances, shadow bias/cascade/focus/intensity/resolution/
  near-far/extrusion, ramp/shape textures, effective light mask, batch queue,
  and pipeline hash. Test impossible ranges, baked-mask behavior, null
  textures, negative brightness, shadow-disabled combinations, and device
  loss. Exercise the private `ColorAttributeAccessor` through color attribute
  reflection and invalid variant conversion.
- `LightBatchQueue`: add/clear/sort light batches, capacity growth, duplicate
  lights, priority/distance ordering, empty queue, mutation during iteration,
  and disposal/owner reset.
- `Zone`: ambient/background/fog values, height fog and gradients, priority/
  override/background-static flags, ambient and zone textures, reflection
  probe data, bounding-box updates, effective lighting caches, mask changes,
  overlapping-zone selection, and invalid fog ranges.
- `Billboard` and `BillboardSet`: per-billboard position/size/rotation/color/
  direction/UV/animation fields, add/remove/update, relative/scaled/sorted/
  fixed-screen behavior, all `FaceCameraMode` values, angle and LOD bias,
  material reference, capacity growth, null/empty billboards, invalid UV and
  non-finite values, batch regeneration, and event/disposal behavior.
- `CustomGeometryVertex` and `CustomGeometry`: vertex and index data for all
  topologies, geometry/material count, dynamic versus static updates, multiple
  batches, out-of-range access, empty geometry, malformed indices, buffer
  regeneration, bounding-box calculation, and device loss.
- `DecalVertex`, `Decal`, and `DecalSet`: packed blend indices/weights and
  out-of-range component selection, clone independence, decal add/remove/
  clear, material, max/optimized buffer sizes, vertex/index counts, update
  geometry type, invalid projection/size, buffer growth and shrink, and
  partial update failure cleanup.
- `OutlineGroup`: color/render-order/debug flags, drawable add/remove,
  duplicate handling, membership queries, empty-group rendering, material-key
  grouping, and disposal when a drawable is removed or destroyed.
- `GlobalIllumination`: resource file reference, offset and emission
  brightness, ambient spherical-harmonics and average sampling, tetrahedron
  hint mutation/reuse, positions outside the volume, missing/corrupt data,
  and disposal.
- `DebugLine`, `DebugTriangle`, and `DebugRenderer`: line/triangle fields,
  depth/no-depth queues, line antialias, view/projection/frustum state,
  content detection, inside tests, draw/clear/update lifecycle, empty and
  very large batches, invalid geometry, and queued debug work after owner
  disposal.

## Spatial index, occlusion, and queries

- `Octant`: root/child creation and split/merge thresholds, world/culling
  boxes, level/parent/octree links, drawable add/remove/move, empty state,
  duplicate insertion, stale octant cleanup, query traversal, and recursive
  disposal. Test boundary points exactly on each face and objects spanning
  multiple child boxes.
- `ZoneLookupIndex`: add/remove/update zones, background-zone selection,
  priority and override rules, overlapping and equal-priority ties, masks,
  empty index, stale weak references, query mutation during enumeration, and
  disposal.
- `Octree`: root/level configuration, drawable and zone registration,
  update queue, culling box, all-drawables view, ray/frustum/point/sphere/box
  query integration, background zone, resize/rebuild, duplicate/remove,
  disposed drawables, and safe behavior while a query is active.
- `OctreeQuery`, `PointOctreeQuery`, `SphereOctreeQuery`, `BoxOctreeQuery`,
  `FrustumOctreeQuery`, and `AllContentOctreeQuery`: exact containment and
  intersection classifications, inside-shortcut behavior, masks, drawable
  callback order, duplicate suppression, empty tree, boundary/tangent cases,
  null/disposed drawables, callback exceptions, and disposal/reuse.
- `OctreeQueryResult`, `RayQueryLevel`, `RayQueryResult`, and
  `RayOctreeQuery`: equality/hash and default values; ray nearest/all-hit
  ordering, triangle/geometry/subobject filtering, level selection, backface
  and max-distance behavior, hit position/normal/UV interpolation, no-hit and
  degenerate rays, boundary boxes, disposed geometry, and query disposal.
- `DepthValue`, `OcclusionBufferData`, `OcclusionBatch`, and
  `OcclusionBuffer`: depth encoding/decoding, clear/reset, view/projection/
  dimensions, cull mode, maximum triangle limits, threaded versus single
  buffer, triangle rasterization, clipping, degenerate/near-plane triangles,
  depth ordering, buffer reuse, timer reset/measurement, invalid dimensions,
  and disposal. Verify no stale depth data survives `Reset` and that a
  rejected batch cannot corrupt counters.

## Models, skeletons, animation, and resources

- `VertexBufferMorph`, `ModelMorph`, `VertexBufferDesc`, `IndexBufferDesc`,
  and `GeometryDesc`: defaults, names/weights, buffer and geometry ranges,
  morph data, clone deep-copy behavior, null nested descriptors, invalid
  indices/counts, and archive/resource round trips. Assert cloned lists and
  disposable buffers are independent.
- `Model`: load/save of valid, empty, truncated, malformed, and versioned
  model data; bounding box, vertex/index buffers, skeleton, bone mappings,
  geometry centers/LODs, morph lookup by name/index/hash, morph ranges,
  object ownership, GPU creation failure, resource dependencies, clone, and
  disposal after partial load.
- `ModelVertexFormat`, `ModelVertex`, `ModelVertexMorph`, `GeometryLODView`,
  `GeometryView`, `BoneView`, `ModelMorphView`, and `ModelViewExportFlag`:
  exact field defaults and packed data, position/normal/tangent/color access,
  presence flags, primitive classification/counts, offsets and inverse
  matrices, shape reset, equality/hash, clone independence, invalid color
  index, empty/degenerate geometry, and export flag combinations.
- `ModelViewFunctions` and `ModelView`: import/export between resource model
  and view data, empty and multiple LODs/geometries/bones/morphs, format
  conversion, generated vertex/index buffers, stable ordering, duplicate
  names, malformed data, native buffer failure, disposal, and private
  `VertexBufferBuilder`/`IndexBufferBuilder` cleanup on exceptions.
- `AnimationKeyFrame`, `AnimationTrack`, and `VariantAnimationTrack`: key
  time ordering, interpolation, duplicate times, before/after range behavior,
  constant/linear/step curves, variant type changes, empty tracks, clone,
  dispose, malformed serialization, and revision/dirty propagation.
- `AnimationTriggerPoint` and `Animation`: trigger ordering and duplicate
  times, name/data ownership, duration and tracks, resource load/save,
  invalid or non-monotonic keyframes, revision increments, clone/dispose,
  and trigger crossing for forward, reverse, looped, and skipped updates.
- `AnimationParameters`: default/empty singleton, animation pointer/name,
  time wrapper, loop/layer/instance/start-bone/blend/weight fields, clone and
  equality/hash, time mutation, null animation, invalid layer/index, and
  independence of nested state.
- `NodeAnimationStateTrack`, `NodeAnimationOutput`,
  `ModelAnimationStateTrack`, `ModelAnimationOutput`, `AnimatedAttributeType`,
  `AnimatedAttributeReference`, and `AttributeAnimationStateTrack`: exact
  equality/hash, node/model/attribute binding, variant conversion, invalid
  attribute type/index, duplicate references, and output default/ownership.
- `AnimationBlendMode` and `AnimationState`: adding and clearing
  node/model/attribute tracks, enabled and weighted state,
  loop/blend/start-bone/time/length, weak model reference,
  animation revision dirty detection, track rebuild, reset/dispose, null or
  changed animation, and callback/event output.
- `AnimationStateSource`: state collection ownership, dirty marking,
  skeleton-reset and throttling policy, derived source behavior, duplicate
  state registration, and disposal with active states.
- `AnimationController`: add/remove/play/stop/fade/seek animations, layers,
  looping, blending and weights, start bone, instance indexes, time updates,
  animation triggers and finish events, state reset, revision and dirty
  propagation, throttling/invisible updates, duplicate animation instances,
  invalid parameters, callback reentrancy, and disposal during update. Test
  private `AnimationInstance` clone, priority/order, and cleanup through the
  public controller.
- `AnimationChannel`, `BoneCollisionShape`, `Skeleton`, and `Bone`: channel
  and collision-shape enum values, hierarchy construction, parent indices and
  sorted bone order, root selection, name/hash lookup, local/world/offset/
  inverse transforms, collision shape and radius/bounding volume,
  animation channels,
  clone independence, invalid parent/cycle, duplicate names, and disposal.
- `VertexBufferAnimationData` and `SoftwareModelAnimator`: model/skeleton
  binding, software versus hardware animation paths, geometry buffers,
  morphs, per-frame update, invisible/throttled update, buffer creation and
  resize, invalid model, cancellation/disposal, and no native buffer leak.

## Materials, techniques, and shader processing

- `CullMode`, `FillMode`, `MaterialQuality`, `MaterialShaderParameter`,
  `MaterialTexture`, `ShaderParameterAnimationInfo`, and `TechniqueEntry`:
  defaults, value/variant ownership, clone/dispose, texture/name identity,
  animation metadata, quality/LOD ordering, equality/hash/comparison, null
  values, and mutation after insertion into a material.
- `Material`: valid/empty/malformed material archive load, shader parameter
  add/get/remove and hash invalidation, texture slots and backup/default
  textures, technique ordering and LOD selection, render/cull/depth/alpha/
  occlusion/specular/order/scene fields, shader defines and exclusions,
  default pass lookup, resource references, clone/dispose, cache dependency
  reset, the private `MaterialPipelineStateTracker`, and failure while loading
  a nested pass. Verify the owner tracker is not used after material disposal.
- `BlendMode`, `CompareMode`, `LightingMode`, and `Pass`: constructor name
  normalization/index, blend/cull/depth/color-write/alpha-to-coverage state,
  shader names/defines/excludes and texture defines, shader variation loading
  and release, extra shaders, alpha-mask detection, load-frame tracking,
  pipeline hash changes, unknown enum values, and dispose/reload ordering.
- `Technique`: pass add/remove/replace, name/index lookup, vertex/pixel
  shader arrays, effective defines and excludes, quality/LOD selection,
  resource load/save, missing/duplicate pass names, malformed XML, shader
  release/reload, dependency notifications, and disposal of all passes.
- `ShaderStage`, `Shader`: source load with nested includes, include cycles,
  missing files, path normalization, timestamp propagation, source validation
  warnings, shader name extraction, reload event ordering, variation caching,
  define normalization, duplicate variation aliases, memory accounting,
  malformed source, and disposal after a variation has been created.
- `ShaderVariation`: vertex/pixel/compute variation names, define
  normalization, source preparation/version insertion, backend defines,
  cache naming, cached-bytecode hit/miss/stale/corrupt paths, compile and
  translation fallback, shader resource creation failure, reload recreation,
  source logging, disposal, and owner-before-variation cleanup.
- `SpirVShader`, `TargetShaderLanguage`, and `ShaderDefineArray`: empty/valid
  bytecode, implicit validity, clone deep copy, define append/size/order,
  duplicate definitions, invalid target language, and compiler-output
  preservation across failure.
- `ShaderTranslator`: version-tag detection at start/middle/end and absent
  newline, universal GLSL define insertion, every shader stage and target
  language, successful SPIR-V parse/translation, preprocessing/parse/link/
  reflection failure, empty output, compiler diagnostics, vertex-attribute
  extraction and malformed declarations, native resource cleanup, and
  repeated calls with reused output structs.
- `ShaderCompiler`: valid HLSL vertex/pixel/compute input, empty/malformed
  source, invalid stage, compiler diagnostics, empty bytecode, byte-count
  correctness, native allocation cleanup, and repeatability. Keep native
  compiler coverage separate from pure argument/output tests.
- `ShaderOptimizer`: valid and empty SPIR-V, Vulkan legalization and
  validation, universal target, optimizer failure fallback policy, diagnostic
  accumulation, bytecode replacement only on success, thread-local output,
  repeated calls, invalid native handles, and cleanup after every failure.
- `ShaderSourceLogger`: disabled logging no-op, enabled virtual-file write,
  header/defines/source byte exactness, empty source/defines, failed/open-null
  file, write exception, close-on-error, and interaction with `Graphics`
  settings. Use an in-memory VFS.
- `TextureFormatUtility`/shader/material cross-check: verify material texture
  defines and shader variation keys change when texture presence, format,
  quality, alpha mode, or backend changes, without compiling duplicate
  equivalent variations.

## Lighting, probes, cubemaps, terrain, and effects

- `LightBakingSettings` and every nested settings struct (`LightmapChartingSettings`,
  `LightmapGeometryBakingSettings`, `GeometryBufferPreprocessSettings`,
  `EmissionLightTracingSettings`, `DirectLightTracingSettings`,
  `IndirectLightTracingSettings`, `EdgeStoppingGaussFilterParameters`,
  `LightmapStitchingSettings`, `LightCalculationProperties`, and
  `IncrementalLightBakerSettings`): defaults, nested value-copy semantics,
  sample counts/radii/bounces/filter widths, invalid negative/zero values,
  extreme values, and archive/config serialization.
- `LightBakingQuality`, `LightBaker`, and private `TaskData`: quality mapping,
  start/stop/cancel, progress and timer behavior, asynchronous completion,
  exception propagation, scene/resource prerequisites, repeated bake,
  cancellation at each stage, stop-token disposal, and no callback after the
  owner is disposed.
- `LightProbe`, `LightProbeCollectionBakedData`, `LightProbeCollection`, and
  `LightProbeGroup`: probe position/coefficients, baked-data offsets/groups,
  clone deep-copy and counts, auto-placement and step, light/zone masks,
  local/world bounds, node transform changes, file references, empty/malformed
  baked data, duplicate probes, invalid group offsets, and disposal.
- `InternalReflectionProbeData`, `ReflectionProbeData`,
  `ReflectionProbeReference`, `ReflectionProbeBVH`, and `ReflectionProbeType`:
  clone/equality/default data, box/sphere/priority/importance fields, mask and
  distance selection, BVH bounds and references, invalid extents, and stable
  ordering/hash behavior.
- `ReflectionProbeManager` and `ReflectionProbe`: registry add/remove/update,
  tracked-component lifecycle, static/dynamic probe selection, box projection,
  priority/importance/tie rules, view masks and zone filtering, movement and
  dirty updates, cubemap allocation/update stages, invalid/deleted resources,
  and disposal while a probe is registered.
- `CubemapUpdateStage`, `CubemapUpdateResult`, `CubemapRenderingSettings`,
  `CubemapUpdateParameters`, and `CubemapRenderer`: settings equality/hash and
  clone independence, update stage transitions, face order, texture size,
  view mask, clip values, camera modes, incremental face updates, empty scene,
  invalid texture/size, render-surface failures, cache reuse, cancellation,
  and cleanup of private `CachedPipelineStates` after partial rendering.
- `Terrain`, `TerrainPatch`, and terrain settings/state: height-map load,
  replace/remove, spacing/patch size/vertex and patch counts, smoothing,
  max LOD and LOD error selection, neighbor linking/expiry and stitched versus
  independent borders, visibility/draw/shadow/occlusion masks, material and
  lightmap data, debug geometry, height sampling, invalid image dimensions,
  patch-size limits, missing neighbor, geometry creation failure, and
  disposal. Test all `TerrainCreatedEvent` paths and patch disposal while a
  terrain update is queued.
- `EmitterType`, `ParticleEffect`, `ColorFrame`, and `TextureFrame`: emitter
  type, material, particle count,
  relative/scaled/sorted/fixed-screen/update-invisible flags,
  forces, active/inactive time, emission/size/velocity/rotation ranges,
  animation LOD, color/texture frame insertion and time ordering, face-camera
  mode, random-range boundaries, min clamps, invalid/non-finite values,
  duplicate frame times, resource serialization, and clone/disposal.
- `ParticleEmitter`: effect assignment, particle count and serialization,
  emitting/auto-remove state, simulation/update and reset, lifetime and
  particle event, effect-finished/auto-remove behavior, invisible updates,
  invalid effect, removal during callback, and disposal with active particles.
- `TrailType`, `TrailPoint`, and `RibbonTrail`: point insertion/removal and
  lifetime expiry, vertex-distance sampling, start/end color/scale/width,
  base velocity, sorted/emitting/update-invisible flags, animation LOD,
  tail-column handling, all trail types, degenerate/teleporting points,
  buffer growth, `Commit` dirtying, and disposal during update.
- `HorizontalAlignment`, `VerticalAlignment`, `Corner`, `Text3D`, and private
  `TextLine`: font/text/size/alignment/row spacing/word wrap, row widths and
  counts, per-corner colors/opacity/depth test, Unicode and combining marks,
  empty/long text, newline/whitespace/wrap boundaries, invalid font, atlas
  failure, geometry rebuild, and disposal after a font reload.
- `Font`, `TextFontGlyph`, `TextFontFace`, `HarfBuzzFontFace`, `SdfFontData`,
  and `SdfFontFace`: TrueType/OpenType and SDF load, malformed/empty font,
  glyph lookup/missing glyph, kerning and codepoint boundaries, point-size
  face caching, atlas packing/page limits, texture creation/readback, SDF
  metadata, failed glyph/texture allocation, native HarfBuzz lifetime,
  repeated load, and disposal of every cached face. Use a deterministic fake
  HarfBuzz gate plus one real-font smoke test.
- `Skybox`: inherited model/material setup plus cubemap selection, face/layout
  orientation, zone/background interaction, camera distance and culling,
  invalid/missing cube texture, render-order behavior, and disposal.

## Resource ownership and cross-subsystem gates

- Pair every pure graphics test with a headless integration case where the
  class owns a `Resource`, `Component`, `Drawable`, `RawBuffer`, `RawTexture`,
  `RenderSurface`, or native handle. Assert both the public result and the
  resulting event/cache/pipeline state.
- Run a device-loss matrix across `Graphics`, `Renderer`, `Texture*`,
  `VertexBuffer`, `IndexBuffer`, `Geometry`, `Material`, `ShaderVariation`,
  `RenderSurface`, `CubemapRenderer`, and all drawable buffer owners:
  loss before creation, during update, after creation, reset success, reset
  failure, repeated loss/reset, and disposal before reset.
- Run a null/error matrix: missing resource, malformed archive, invalid enum,
  out-of-range index, short data span, oversized data span, unsupported
  format/backend, native creation failure, compile/translation failure,
  expired weak reference, callback exception, and owner disposal. Verify
  counters, hashes, dirty flags, and caches are unchanged or rolled back as
  specified after rejection.
- Run deterministic visual gates for camera projection, material state,
  mesh/index ranges, textures and cube faces, billboards/particles/trails,
  terrain LOD/neighbors, shadows/lights/zones, reflection probes, outlines,
  text, debug geometry, and full renderer passes. Capture both the first and
  repeated frame to detect stale buffers, nondeterministic sorting, and cache
  leakage. Keep image baselines separate from unit assertions.
- Before implementation, map each existing `GraphicsTests` file to the type
  bullets above and record missing partitions. A passing existing suite does
  not close a plan item unless success, rejection, ownership, device-loss,
  serialization, and boundary behavior are all represented.
