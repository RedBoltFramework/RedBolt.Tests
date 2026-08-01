# RenderPipeline subsystem test plan

Source: `../RedBolt/src/RedBolt/RedBolt/RenderPipeline`. Existing tests are in
`RenderPipelineTests`.

## Verified coverage

- [x] `RenderPipeline` settings, render-path/pass/parameter ownership,
  change notifications, reload handling, and teardown callback clearing are
  covered (`RenderPipelineTests`, `RenderPipelineLifecycleBoundaryTests`).
- [x] Pipeline-view context ownership and post-dispose event unsubscription
  are covered (`RenderPipelineInterfaceTests`).
- [x] Debugger snapshot replacement, pass/quad ordering, clone ownership,
  per-view debugger isolation, and stereo debugger absence are covered
  (`RenderPipelineDebuggerTests`).
- [x] Instancing settings validation/copying, generated vertex elements,
  allocation/index progression, and replacement cleanup are covered
  (`InstancingBufferTests`).
- [x] Shadow-map allocation/reset/begin-render boundaries and render-buffer
  manager target behavior are covered (`ShadowMapAllocatorTests`,
  `RenderBufferManagerTargetTests`).

## Definitions, settings, and value types

- `BatchCompositorSubpass`, `DrawableProcessorPassFlag`, `BatchRenderFlag`,
  `RenderBufferFlag`, `RenderPipelineColorSpace`, `ColorSpaceTransition`,
  `RenderOutputFlag`, `StaticPipelineStateId`, `GeometryRenderFlag`, and all
  lighting/quality enums (`DrawableAmbientMode`, `DirectLightingMode`,
  `SpecularQuality`, `ReflectionQuality`): exact values, flag composition,
  unknown values, and every branch used by validation/adjustment.
- `RenderBufferParams`: default format/MSAA/flags, equality/hash, invalid
  samples/formats, color-space changes, and stable cache-key behavior.
- Internal `RenderPipelineColorSpaceUtility`: expected texture format for each
  color space, transition matrix (linear/sRGB/unknown), and unsupported HDR or
  backend cases.
- `RenderBufferManagerSettings` and `RenderBufferManagerFrameSettings`: default
  sizes/formats/flags, validation, pipeline-state hash, equality/hash,
  zero/negative/oversized dimensions, MSAA and depth/stencil combinations,
  frame-dependent changes, and persistence across copies.
- `RenderPipelineStats`, `RenderPipelineStatsHandler`, and
  `CommonFrameInfo`: default counters/frame data, ref mutation, callback order,
  accumulation/reset, negative/large timings, and no stale frame references.
- `DrawQuadParams` and `ShadowMapRegion`: default quad/material/texture fields,
  atlas region coordinates, implicit bool validity, split calculation for all
  grid sizes, invalid split indexes/zero grids, and view/texture ownership.
- `LightProcessorCacheSettings`, `DrawableProcessorSettings`,
  `InstancingBufferSettings`, `BatchRendererSettings`,
  `ShadowMapAllocatorSettings`, `OcclusionBufferSettings`,
  `SceneProcessorSettings`, `ShaderProgramCompositorSettings`, and
  `RenderPipelineSettings`: default values, `Validate`, equality/hash and
  pipeline-state hashes, implied settings, conversions between settings types,
  quality/lighting combinations, budgets/TTL, shadow atlas and occlusion
  limits, unsupported capability adjustment, render-path output adjustment,
  and preservation of explicit user choices. Include NaN/negative/overflow
  values and all enum combinations.
- `ShaderResourceDesc`, `ShaderParameterDesc`, `RenderOutputTargetInfo`,
  `IRenderPipelineInterface`, and `RenderPipelineView`: resource/parameter
  metadata, parameter clone/dispose/deep ownership, output target validity and
  resize, interface call order (`Define`, `Update`, `Render`, stats/frame
  access, debug draws), null render targets/viewports, context retrieval, and
  use after pipeline disposal.
- `BatchStateCacheCallback`, `UIBatchStateCacheCallback`, and
  `LightProcessorCallback`: fake callbacks should verify exact create/query
  arguments, output descriptors, light-volume callbacks, exception propagation,
  and no callback after cache invalidation/disposal.

## Batches, sorting, and state caches

- `PipelineBatch`, `PipelineBatchDesc`, `PipelineBatchByState`,
  `PipelineBatchBackToFront`, and `PipelineBatchGroup<T>`: default/constructed
  fields, drawable/material/geometry/pass identity, state/depth sort ordering,
  stable tie-breaking, transparent back-to-front ordering, comparison
  transitivity, group add/clear/count/ref access, invalid/default batches, and
  sorting with null/duplicate objects.
- `BatchStateLookupKey`, `BatchStateCreateKey`, `CachedBatchState`, and
  `BatchStateCreateContext`: every field in equality/hash, pass/subpass/shadow
  split identity, output/vertex-stride changes, null pass, and disposal of
  cached pipeline states.
- `BatchStateCache`: output descriptor changes, lookup hit/miss, invalidation,
  placeholder creation with valid/zero/negative strides, callback failure,
  duplicate keys, device loss/restore, and disposal exactly once.
- `UIBatchStateKey`, `CachedUIBatchState`, and `UIBatchStateCreateContext`:
  texture/filter/blend/layout identity, equality/hash, missing shader/resource,
  callback creation failure, invalidation, and disposal.
- `UIBatchStateCache` and `DefaultUIBatchStateCache`: generic cache forwarding,
  default pipeline-state creation, settings/output changes, invalidation,
  device loss, and context/disposal ordering.
- `StaticPipelineStateCache` and private `PerOutputCache`: per-output cache
  identity, lazy state construction, duplicates, output format/MSAA changes,
  invalidation, device restore, cache eviction, and owner disposal.

## Render buffers and resource managers

- `RenderBuffer`: enabled state, abstract texture/view/viewport contracts,
  null/invalid slice handling, disposal, and a fake implementation that records
  dirty/enable calls.
- `TextureRenderBuffer`: parameter-driven texture allocation, persistent versus
  transient flags, texture/view/read-only-depth access, viewport size, resize,
  format/MSAA mismatch, device loss, and disposal.
- `ViewportColorRenderBuffer` and `ViewportDepthStencilRenderBuffer`: viewport
  size/output selection, color-space/depth-format selection, resize on target
  changes, slice bounds, read-only-depth support, and no stale views after
  device loss.
- `RenderBufferManager`: settings/frame-settings application, viewport/render
  target changes, color/depth/normal/material buffers, cache reuse and release,
  output descriptions, MSAA/color-space validation, frame-end recycling,
  invalidate/restore, and disposal. Test zero-size/minimized viewports and
  repeated resize without unbounded allocations.
- `SharedRenderPassState`: shared device/context/pipeline state ownership,
  output targets, render-buffer and parameter updates, pass ordering, failed
  initialization, and repeated disposal.

## Drawable and batch processing

- `SortedOccluder` and `GeometryBatch`: sort comparison and stable ties,
  geometry/deferred/forward fields, source-batch/user-data identity, null and
  invalid indexes, and value-copy behavior.
- `DrawableProcessorPass` and nested `AddBatchResult`: flags, custom-batch
  override, normal batch addition, forward-lit/deferred routing, duplicate or
  missing technique, thread-index bounds, dispose, and exception cleanup.
- `DrawableProcessor`: settings/flags, pass registration, per-thread batch
  collection, geometry visibility and occluder sorting, empty/many drawables,
  dirty/removed drawables, custom batches, instancing eligibility, max light
  limits, transparent ordering, and disposal across worker threads.
- `BatchRenderingContext` and `BatchRenderer`: pass preparation, batch sort and
  draw-command generation for opaque/transparent/deferred/forward/light-volume
  batches, material/geometry/technique changes, instancing, empty batches,
  missing resources, scissor/viewport changes, device loss, and stats. Assert
  each batch is emitted once and state caches are invalidated at the correct
  boundary.
- Internal `BatchRendererHelpers`, `ObjectParameterBuilder`, and
  `DrawCommandCompositor` (including feature flags, dirty-state, cached-shared-
  state, instancing-group, vertex-light, and light-volume helper structs):
  camera/fog/clip/depth parameter math, enabled-feature decisions, parameter
  type/size failures, state/constants/resources dirty transitions, flush order,
  lightmap/reflection-probe/ambient inputs, unsupported backend branches, and
  no retained references after disposal.
- `BatchCompositorPass` and `BatchCompositor` (including `ShadowBatchTask`):
  pass flags and subpass collection, forward/deferred/shadow/light-volume
  output descriptors, compose ordering, sort-key filling, empty/multiple pass
  behavior, shadow task completion, callback exceptions, cache invalidation,
  device loss, and disposal while composition is in flight.
- `ScenePass`, `UnorderedScenePass`, `BackToFrontScenePass`, and
  `OutlineScenePass`: scene batch grouping, deferred/base/light separation,
  transparent back-to-front sorting, refraction detection, instancing buffer
  preparation, outline filtering, empty/removed drawables, and state cleanup.

## Cameras, lights, and shadows

- `CameraProcessor`: camera set replacement, null/duplicate cameras, viewport
  and stereo data, update/render begin/end ordering, active camera selection,
  frame-info mutation, and disposal.
- `LightDataForAccumulator`, `LightAccumulatorContext`, `LightAccumulator`,
  `LightAccumulator.LightData`, `VertexLightContainer`,
  `ReflectionProbeContainer`, and private `LightContainer`: clear/add lights,
  directional/point/spot/vertex/probe categorization, max-light limits,
  shadow/reflection metadata, sorting, invalid/null lights, transform updates,
  and ref/value ownership. Verify accumulator output is deterministic.
- `LightGeometryQueryResult`, `PointLightGeometryQuery`,
  `SpotLightGeometryQuery`, and `DirectionalLightShadowCasterQuery`: octree
  intersection classification, sphere/frustum edge/tangent/inside/outside
  cases, caster filtering, empty/many drawables, view masks, and result
  accumulation/reset.
- `CookedLightParams`, `LightMatrixContainer`, `ShadowParameterContainer`,
  `LightProcessor`, private active-split containers, `LightProcessorCache`,
  and private `CachedLightProcessor` instances: light setup for each type, shadow split
  allocation, camera/frustum changes, light/scene updates, cache TTL/budget,
  reuse and eviction, invalid/missing light data, device loss, callback order,
  and disposal of split processors and native resources.
- `ShadowSplitProcessor`: spot and cube-face initialization, split matrices,
  shadow batch composition/finalization, PCF kernel boundaries, subpixel
  offsets, empty/invalid shadow regions, and cleanup after partial init.
- `ShadowMapAllocator` and private `AtlasPage`: settings validation, atlas page
  creation/reuse, rectangle allocation/packing, zero/oversized/non-power-of-two
  regions, exhaustion, reset-all, begin-render validation, texture/view
  lifetime, device loss, and deterministic allocation order.

## Instancing and shader composition

- `InstancingBuffer`: settings validation, begin/end frame, instance index
  allocation and capacity growth, byte and unmanaged element writes, zero/
  negative/out-of-range indexes/counts, stride mismatch, reuse after end,
  device loss, and disposal.
- `ShaderProgramDesc`: shader/resource/parameter lists, clone/deep ownership,
  duplicate names, missing shader stages, output formats, equality/hash if
  consumed by the compositor, and disposal of `ShaderParameterDesc` values.
- `ShaderProgramCompositor`: settings and render-path inputs, shader/resource
  parameter collection, variation/cache key stability, pipeline-state creation,
  material/lighting/output changes, missing/failed shaders, invalid parameters,
  device loss, and disposal. Use fake shader compiler/pipeline cache seams.
- `ShaderConsts` and `ShaderResources`: assert all constant/resource names and
  hashes are unique, stable, correctly typed, and map to the expected shader
  slots; include a generated collision/duplicate check.
- `PipelineStateBuilder`: graphics/compute descriptor construction, input
  layouts, immutable samplers, blend/depth/rasterizer/output setup, batch/UI
  callback integration, missing or mismatched shader reflection, cache hits,
  invalid settings, and disposal.

## Render paths and built-in passes

- `RenderPath`: load/save/parse XML/JSON/binary path definitions, pass order,
  parameters, missing/duplicate/unknown pass types, invalid settings,
  mutation after load, hot reload, disposal, and resource dependencies.
- `RenderPassTraits` and abstract `RenderPass`: type/name/flags, attribute
  reflection, parameter collection/update, view initialization, update/render
  call order, render-pass exceptions, and disposal.
- `FullScreenShaderPass`: shader/parameter setup, input/output texture binding,
  viewport/scissor, missing shader/resource, resize, and render invocation.
- `ToneMappingPass` and nested `Mode`: all modes, exposure/gamma parameters,
  HDR/sRGB output, invalid mode/value, cache reuse, and render output.
- `AutoExposurePass` and its parameter/texture/state caches: luminance reduction,
  adaptation speed/time-step boundaries, empty/invalid input, cache invalidation,
  resize, and output texture lifetime.
- `BloomPass` and its parameter/pipeline/texture caches: threshold/knee/blur
  settings, downsample/upsample levels, zero/odd/minimal sizes, missing input,
  cache keys, output transitions, and cleanup.
- `AmbientOcclusionPass` and its parameter/texture/pipeline caches: quality,
  radius/bias/fade settings, depth/normal input requirements, unsupported
  formats, resize, temporal/random state, empty scene, and disposal.
- `OutlineRenderPass` and its private pipeline cache: outline mask/color/width,
  selected geometry, missing target, output format/MSAA changes, cache reuse,
  and cleanup.
- `RenderPathBuiltInPasses` private cache/parameter/state structs and
  `ShaderParameterNameAttribute`: unique parameter names, reflection metadata,
  default values, equality/hash of cache keys, disposal of cached states, and
  unknown attributes/types.

## Pipelines and views

- `RenderPipeline`: component/node/scene attachment, settings and render-path
  attributes, view creation, enable/disable, output target and viewport changes,
  update/render/debug delegation, scene events, resource reload, missing
  pipeline view, and disposal. Verify no render callbacks occur after detach.
- `DefaultRenderPipelineView` and private `DeferredLightingData`: settings and
  render-path replacement, parameter dirty/rebuild behavior, `Define`/`Update`/
  `Render` order, deferred/forward/ambient/shadow paths, frame info/stats,
  debug draws, empty scene, minimized target, unsupported capabilities, device
  loss, and disposal.
- `StereoFrustumOctreeQuery`, `StereoOccluderOctreeQuery`,
  `StereoOccludedFrustumOctreeQuery`, and `StereoSceneProcessor`: two-frustum
  intersection, per-eye occlusion, combined visibility without duplicates,
  buffer replacement/null buffers, scene updates, and disposal.
- `StereoRenderPipelineView`: two-camera/view setup, stereo settings/path,
  parameter dirty state, define/update/render, eye target/viewport mapping,
  stats/frame data, debug draws, missing eye, mismatched targets, and cleanup.
- `StereoRenderPipeline`: construction, view instantiation, scene attachment,
  settings, render-path, update/render, device loss, and disposal.
- `LightmapRenderPipelineView`: lightmap geometry-buffer rendering, viewport/
  texture-size validation, missing render surface/scene, output lifetime,
  interface contract, and disposal.

## Debugging and snapshots

- `DebugFrameSnapshotBatch`, `DebugFrameSnapshotQuad`,
  `DebugFrameSnapshotPass`, and `DebugFrameSnapshot`: default fields, clone
  depth/ownership, ordering, string formatting, empty/many batches/quads/passes,
  mutation after clone, and snapshot disposal behavior.
- `RenderPipelineDebugger`: begin/end snapshot state machine, pass nesting,
  scene-batch/quad reporting, invalid calls, static null/in-progress helper,
  snapshot replacement, pipeline/material/shader summaries, string stability,
  and concurrent/read-only access policy. Verify partial snapshots are either
  rejected or closed deterministically.

## Render-pipeline gates

Run pure settings/hash/sort/parameter tests first, then fake-renderer tests that
record pass order, resource transitions, pipeline-cache calls, draw batches,
and stats. Add headless backend tests for one forward, deferred, shadow,
post-processing, lightmap, and stereo frame. Every cache/resource owner needs
success, miss, invalidation, device-loss/restore, exception cleanup, and
repeated-dispose coverage; visual regression tests should validate pixels only
after those state and lifetime contracts pass.
