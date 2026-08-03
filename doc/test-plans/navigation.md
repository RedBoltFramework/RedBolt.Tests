# Navigation subsystem test plan

Source: `../../RedBolt/src/RedBolt/Navigation`. Existing tests are in
`src/RedBolt/Navigation/NavigationTests.cs`. Detour/Recast calls need a
fake seam for unit tests and one real headless integration fixture.

## Verified deterministic coverage

- [x] Tile-offset alignment/integrality failures, geometry bounds and empty
  tiles, area deduction, and build-data/allocation release behavior
  (`NavigationContractTests`).
- [x] `NavArea`, `Navigable`, `Obstacle`, and `OffMeshConnection` defaults,
  effective area behavior, endpoint ownership, and scene attachment
  (`NavigationContractTests`).
- [x] Reflected crowd attributes are covered (`NavigationTests`).
- [x] Empty/invalid navigation queries and crowd queries without a mesh return
  documented sentinel values, clear empty paths deterministically, and ignore
  invalid filter identifiers (`NavigationContractTests`).
- [x] Real headless mesh/path, baked-scene, and crowd movement scenarios pass
  in `NavigationTests`; the staged `CoreData`/`Data` resource links are part of
  the test output contract and the tests do not require a graphics backend.

Remaining unchecked items include a fake Detour seam, query-filter boundary
coverage, dynamic tile-cache serialization/fault injection, async cancellation
exactly-once tests, and native crowd recreation/disposal cases.

## Data, filters, and events

- `NavigationDefs`, `NavmeshPartitionType`, `NavigationPathPointFlag`, and
  `NavigationPathPoint`: verify native enum values, default flags, equality and
  ordering, path-point position/flags, and invalid values.
- `NavigationUtils` and `NavigationGeometryInfo`: convert geometry from
  `Drawable`, `StaticModel`, `Terrain`, and `CustomGeometry`; cover empty,
  disabled, non-navigable, transformed, malformed-index, and non-uniform-scale
  inputs plus area ids and bounds.
- `CrowdObstacleAvoidanceParams`, `CrowdQueryFilter`, and the nested
  `NavigationQueryFilter`: test include/exclude flags, all area costs,
  out-of-range area/filter ids, pass/reject behavior, cost calculation,
  obstacle parameters, and conversion to/from Detour values.
- `NavigationEvents` and every payload (`NavigationMeshRebuiltEvent`, tile
  added/removed/all-removed, crowd formation/reposition/failure/state events,
  and obstacle added/removed): assert fields, sender, event ordering, one event
  per transition, and handler removal during dispatch.

## Navigation mesh and build data

- `NavArea`, `Navigable`, `Obstacle`, and `OffMeshConnection`: verify defaults,
  reflected attributes, node attach/detach, world bounds, area/walkability and
  effective area id, obstacle geometry/radius/height, off-mesh endpoints,
  enabled/recursive behavior, invalid nodes, scene removal, and debug drawing.
- `NavAreaStub`, `DetourAllocation`, `NavBuildData`, `SimpleNavBuildData`, and
  `DynamicNavBuildData`: test empty and allocated buffers, byte-span bounds,
  native allocation ownership, release/dispose once, partial build cleanup,
  compact-heightfield/contour/poly/detail data defaults, and subclass-specific
  resources after a failed build.
- `NavigationMesh`: allocate/clear/rebuild, tile bounds and index conversion,
  build one/many/empty/invalid regions, synchronous and asynchronous builds,
  cancellation before/during/after completion, callback success/failure and
  exceptions, add/remove/remove-all tiles, duplicate and malformed tile data,
  save/load navigation bytes, area costs, nearest/move/path/random/wall/raycast
  queries with no mesh, no path, zero radius, empty destination, invalid
  filters, maxVisited limits, and world-origin offsets. Verify rebuild/tile
  events, lock/thread behavior, and release after a failed native allocation.
- `Navigation` static registration: register repeatedly, verify reflected
  factories and file loaders, and ensure a disposed context does not retain
  the assembly.

## Dynamic mesh and crowd

- `DynamicNavigationMesh`, nested `ObstacleUpdate`, and `MeshProcess`: test
  tile-cache allocation, add/remove/replace tile, all-tile removal, byte
  offset for tile and world-origin changes, obstacle add/change/remove batching,
  overlapping updates, off-mesh collection, serialization/deserialization,
  malformed/truncated tile streams, rebuild failure, and release order. Verify
  debug output and no stale obstacle references after scene removal.
- `CrowdAgent` and its requested-target/state/quality enums: attach to a
  manager, add/remove from crowd, target position/node/velocity, reset target,
  radius/height/max speed/acceleration, avoidance/query filters, enabled and
  scene transitions, callback-driven velocity/position overrides, failure
  event, state transitions, world-origin update, missing mesh/manager, and
  expired node. Assert no duplicate native agent and correct removal on dispose.
- `CrowdManager`, `CrowdObstacleAvoidanceParams`, and nested
  `FilterConfiguration`: create/destroy crowd, add/remove agents, update
  ordering, set/reset target or velocity for one/all nodes, filter/area-cost
  configuration and round trip, avoidance configuration, get agents with and
  without filters, nearest/move/path/random/wall/raycast queries, invalid
  filter ids, navigation rebuild, scene component changes, world-origin, and
  callback exceptions. Verify settings survive a native crowd recreation and
  no callback is sent after disposal.

## Integration gates

Run the current small mesh/path scenario, a dynamic obstacle/tile-cache
scenario, and a crowd movement scenario. Add deterministic property tests for
tile index/bounds and query filters. Every async path must assert callback
exactly-once semantics and cleanup on cancellation or exception.
