# Glow/light baking subsystem test plan

Source: `../RedBolt/src/RedBolt/Glow`. Existing tests are in `GlowTests`.

## Value and allocation types

- `BakedLight`, `BakedLightmap`, and `BakedSceneBackground`: verify defaults,
  directional/point/spot light conversion, radius-to-angle conversion, color
  space conversion, intensity, attenuation, visibility, square linear buffer
  allocation, out-of-range texel access, clone independence, background
  fallback color, and invalid dimensions/negative intensity.
- `GeometryIdToObjectMapping`, `LightmapSeam`, `LightmapGeometryBakingScene`,
  `LightmapGeometryBakingScenesArray`, and `LightmapChartGeometryBuffer`:
  verify invalid/default ids, by-value clone semantics, native/disposable
  members, seam transforms, chart/scene collection ownership, texel buffers,
  empty geometry, and disposal after partial initialization.
- `LightmapUVGenerationSettings`: cover every flag/scale/spacing/default,
  invalid geometry and missing second UV channel, deterministic output, and
  serialization/clone behavior where exposed.
- `LightmapChartRegion`, `LightmapChartElement`, `LightmapChart`, and
  `RequestedChartRegion`: test normalized transforms, empty/degenerate regions,
  overlap/containment, allocator boundaries, clone independence, and ordering.

## Classes

- `LightmapUVGenerator`: generate UVs for a simple mesh, multiple geometries,
  existing UV channels, mirrored/degenerate triangles, seams, invalid indices,
  chart padding, and no geometry. Verify metadata and deterministic repeated
  generation; test failure without a valid model or output channel.
- `LightmapCharter`: allocate and apply charts with one/many charts, rotated
  and padded regions, atlas limits, overlap, empty input, failed allocation,
  clone-before-mutation, and geometry mapping updates. Assert all output
  buffers and chart transforms after disposal/failure.
- `BakedSceneChunk`, `LightProbeCollectionForBaking`, `BakedSceneCollector`,
  and `DefaultBakedSceneCollector`: collect a static scene, light probes,
  terrain, empty scene, unloaded/locked geometry, and multiple chunks. Verify
  query lock/unlock in success and exception paths, chunk ordering, ownership,
  cancellation, retry, clone of base and baking data, and no stale references.
- `StaticModelForLightmap`: initialize model/material/geometry mappings,
  missing resources, unsupported vertex formats, lightmap UV generation,
  multiple LODs, empty model, rebuild after mutation, and disposal of temporary
  geometry/baking buffers.
- `Helpers`: table-test `ParallelFor` for empty, one, many, exception, and
  cancellation inputs; verify material lightmap helpers, static-model and
  terrain geometry selection, missing components, and thread-safe result
  aggregation.

## Integration gates

Run a headless bake with a small static scene and verify all locks release on
success and failure. Add one visual regression for a baked lightmap and one
allocation/parallelism smoke test; keep native XAtlas failures diagnosable as
test-environment failures rather than silent empty output.

## Verified checks

- [x] Baked-light values/backgrounds/lightmaps, clone ownership, chart allocation, UV generation, scene collection, and helper parallelism (`GlowTests`).
- [ ] Native XAtlas baking and visual lightmap comparison remain integration gates.
