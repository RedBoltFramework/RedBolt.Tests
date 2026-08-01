# Math subsystem test plan

Source: `../../RedBolt/src/RedBolt/RedBolt/Math`. Existing tests are in
`MathTests`. Prefer table-driven exact-value tests plus property
tests for invariants; use the native parity cases as regression vectors.

## Scalars, ranges, and random values

- `MathDefs` (including the partial implementation): constants, epsilon-aware
  equality, angle/radian conversion, trigonometry, abs/modulo/positive modulo,
  clamp with inverted bounds, snap, bit helpers, next-power-of-two overflow,
  variable-length and zigzag integer encoding/decoding, half-float conversion,
  interpolation, min/max operand order, and all easing endpoints. Test zero,
  negative, max/min, NaN, Infinity, malformed/short buffers, and round trips.
- `EaseMath`: every Back/Bounce/Sine/Exponential/Elastic in/out/in-out branch
  at 0, 1, midpoint, out-of-range, and extreme period; assert endpoint and
  parity values.
- `NumericRange<T>`: default invalid range, ordered/empty ranges, inclusive
  boundaries, intersection/union, trimming, signed/unsigned types,
  overflow/inverted bounds, equality/hash, and generic numeric conversions.
- `WrappedScalarRange<T>` and `WrappedScalar<T>`: ordered/wrapped ranges,
  inclusive/exclusive endpoint membership, empty/full wrap, multiple wraps,
  min/max clamping, update wrapped/clamped with and without out-of-bounds
  return, delta overflow, invalid bounds, equality/hash, and signed/unsigned
  generic behavior.
- `RandomEngine` and global random helpers: deterministic seeded sequence,
  clone/state save-restore, invalid state rejection, integer/scalar/vector
  bounds, inclusive/exclusive endpoints, reversed/zero ranges, unit vectors,
  and global engine isolation/concurrency.
- `PerlinNoise`: default/seed copy, float/double overload parity, one-/two-/
  three-dimensional ranges, repeat periodicity, integer midpoint, negative and
  extreme coordinates, clone independence, and deterministic values.
- `AreaAllocator`: reset/default/fast/precise modes, non-overlapping
  allocation, exact fit, fragmentation/splitting/cleanup, alternating growth,
  max dimensions, zero/negative/oversize requests, coordinates near integer
  limits, clone independence, and allocation failure without state corruption.

## Vectors, colors, matrices, and transforms

- `BaseRealVector2<T>`/`Vector2f`/`Vector2d`,
  `BaseRealVector3<T>`/`Vector3f`/`Vector3d`,
  `BaseIntegerVector2<T>`/`Vector2i`/`Vector2u`,
  `BaseIntegerVector3<T>`/`Vector3i`/`Vector3u`, and `Vector4f`: default and
  scalar/component/array constructors, contiguous `Data`, index bounds,
  arithmetic/comparison/hash/order, length/dot/cross/normalize or
  normalized-or-default, conversions, min/max/free helpers, signed/unsigned
  semantics, zero/near-zero/NaN, and copy/ABI independence.
- `VectorMarshallers`, `Vector2fMarshaller`, `Vector2iMarshaller`,
  `Vector3fMarshaller`, `Vector4fMarshaller`, `Vector3fAbi`, and
  `ColorMarshaller`: managed/native round trips, field order/size/alignment,
  null and invalid pointer handling, and no alias of source storage.
- `Color` and `ChannelMask`: default opaque white, packed masks (ABGR/ARGB/
  RGB/custom), vector/list constructors, arithmetic, clip/invert, luma/chroma/
  hue/saturation/value/lightness, HSL/HSV conversion including hue wrap and
  degenerate saturation, gamma/linear conversion, lerp, hash/equality and
  string output, alpha clipping, and NaN/out-of-range channels.
- `Matrix2`, `Matrix3`, `Matrix3x4`, and `Matrix4`: identity/zero/scale/
  rotation/translation/projection factories, element/index/data layout,
  multiply/transpose/determinant/inverse, singular matrices, affine inverse,
  transform points/vectors, angle-axis/quaternion equivalence, decomposition,
  bulk versus individual transpose, copy independence, and extreme/NaN input.
- `Quaternion`: constructors, contiguous data, Euler/axis-angle/look-rotation
  (including failed look rotation preserving the value), normalize/inverse/
  conjugate, multiplication, slerp/nlerp, swing/twist decomposition,
  angular velocity, gimbal lock, antipodal rotations, zero quaternion,
  approximate/equality/hash, clone, and vector rotation.
- `Transform` and `DoubleTransform`: identity/default, translation/rotation/
  scale, matrix conversion/decomposition, multiply/inverse/lerp, point/vector
  transform, double precision round trip, non-uniform/zero scale, and value
  layout/copy independence.

## Geometry primitives and volumes

- `Rect` and `IntRect` plus `IntRectIterator`: default/undefined state,
  scalar/vector construction, exclusive max containment, merge/clip, overlap,
  arithmetic, data layout, iterator row-major order/empty range, equality/hash,
  and overflow/negative dimensions.
- `BoundingBox`: all definitions (point/list/rect/sphere/frustum/polyhedron),
  undefined and degenerate boxes, merge/clip/pad, transformed/projected boxes,
  center/half-size, signed/unsigned point and box distances, inside/intersection
  classification including tangent boundaries, copy/equality, and invalid
  vertices/matrices.
- `Plane`: construction/normalize/transform/reflection, distance/project,
  ray intersection, one/two/three-plane intersections, parallel/coincident
  cases, point-side classification, clone/copy, and matrix transforms.
- `Ray` and `DistanceAndNormal`: point/distance, transformed non-normalized
  direction, plane/sphere/box/triangle/quad/indexed geometry hits, normals and
  miss sentinels, degenerate rays, backface/edge boundaries, and copy/clone.
- `Sphere` and `Circle`: default/point-list/center-radius definitions, merge,
  equal spheres, point/sphere/box intersection and classification, distances,
  spherical coordinates, circle behavior, degenerate/negative radius,
  transformed sphere, clone/copy, and tangent strictness.
- `Frustum` and `FrustumPlane`: perspective/orthographic/split/box/projection
  definitions, transforms and plane refresh, point/sphere/box inside/intersect/
  outside (fast and exact), distance, projection, clone, near/far/invalid plane
  cases, and degenerate FOV/aspect/near/far values.
- `Polyhedron` and nested face storage: default/face sizing, add/clear/
  transform, copy/assignment ownership, box/frustum/plane clipping, vertices
  inside/outside, empty/degenerate faces, and clone independence.
- `Intersection`: exact enum values and classification table.

## IK and spherical geometry

- `IKSettings`, `IKNode`, `IKNodeSegment`, `IKChain`, `IKFabrikChain`,
  `IKSpineChain`, `IKTrigonometricChain`, and `IKEyeChain`: copy/clone
  ownership, shared node alias versus copied container, clear/add/update
  lengths, segment direction/rotation/previous/current/original transforms,
  FABRIK reachable/unreachable targets and fixed lengths, spine bend/twist/
  projected callbacks, eye look-to/local transform, zero-length chains,
  iteration/angle limits, and world-origin changes.
- `SphericalHarmonicsFloat9`, `SphericalHarmonicsVector9`,
  `SphericalHarmonicsColor9`, `SphericalHarmonics9`, and
  `SphericalHarmonicsDot9`: default and fixed storage, basis coefficients,
  directional evaluation, constant color, dot/color conversion by value,
  accumulation/scaling/operators, copy independence, and invalid directions.
- `TetrahedralMesh` and nested implementation context/edge/inline arrays:
  clone all owned containers, define simple and degenerate point sets, inner/
  outer tetrahedra, barycentric coordinates, sample vector and spherical
  harmonic values, invalid/outside points, duplicate/coplanar points,
  adjacency/face ownership, and disposal.
- `HighPrecisionSphere`, `TetrahedralMeshSurfaceTriangle`/`UIntArray3`,
  `TetrahedralMeshSurfaceEdge`/`UIntArray2`, `TetrahedralMeshSurface`, and
  `Tetrahedron`/`UIntArray4`: POD size/layout, inline array copy independence,
  surface clone/adjacency, inner barycentric matrix, boundary faces, invalid
  indices, and equality where exposed.

## Cross-cutting gates

For every struct assert native field order/size, default construction, copy
independence, and exact equality/hash behavior. For every geometry predicate
test interior, boundary/tangent, exterior, degenerate, and invalid inputs.
Add randomized round-trip/property tests for matrix inverse, transforms,
normalization, volume classification, and packed color conversion.

## Verified checks

- [x] Scalars/easing/ranges/randomness/noise/allocator behavior and geometry primitives (`MathTests`).
- [x] Vector/color/matrix/quaternion ABI and parity cases (`MathTests`).
- [x] `Transform`/`DoubleTransform` identity, matrix round trip, interpolation, and value semantics (`MathTests/TransformTests`).
- [x] Deterministic scalar/easing/range/wrapped-range boundary properties, matrix inverse cases, and public math value ABI/layout assertions (`MathTests/DeterministicBoundaryPropertyTests`).
- [x] Seeded replayable scalar property coverage is included in
  `DeterministicBoundaryPropertyTests`; malformed-buffer fuzzing and native
  parity remain separate nightly/backend work.
