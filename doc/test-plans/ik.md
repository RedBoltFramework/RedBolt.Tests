# Inverse-kinematics subsystem test plan

Source: `../../RedBolt/src/RedBolt/IK`. Existing tests are in
`IKTests/IKSolverTests.cs`.

## Shared fixture

Build a small node tree with named bones, a target, optional frame-of-reference
node, `AnimatedModel`, `DebugRenderer`, and an `IKSolver`. Use deterministic
poses and a recording event receiver. Run tests with and without animation,
paused scenes, world-origin shifts, expired weak nodes, and missing names.

## Types and classes

- `IK`, `IKEvents`, `IKPreSolveEvent`, and `IKPostSolveEvent`: verify library
  registration, event payload/sender/order, nested dispatch, and handler
  removal during pre/post solve.
- `IKNodeCache` and nested `Entry`: add/reuse names, duplicate names, missing
  nodes, expired weak nodes, clear/rebuild, entry position/rotation updates,
  world-origin translation, and deterministic enumeration.
- `IKSolverComponent` and `BendCalculationParams`: cover node attach/detach,
  update masks, tree dirtying, initialize failure, frame-of-reference and
  parent-frame transforms, solver-node registration, debug drawing oriented and
  unoriented, bend target weights 0/1/outside range, zero direction, and
  maximum-distance geometry.
- `IKSolver`: rebuild all derived solver types, empty/invalid solver trees,
  dirty and expired caches, solve while paused versus enabled, solve from
  original versus current transforms, continuous rotation, world-origin
  update, pre/post event order, animated skeleton reset, repeated solve, and
  disposal/unsubscription. Assert no allocation in steady state if that is a
  performance contract.
- `IKIdentitySolver`: valid target/bone, missing target/bone, rotation offset
  update/default-zero initialization, target transform copy, disabled/expired
  target, world-origin update, and debug geometry.
- `IKChainSolver`: fewer than two bones, missing middle bone, valid FABRIK
  chain, target reachability/unreachability, chain length update, target move,
  reverse/zero-length segments, and debug segment order.
- `IKLimbSolver`, `IKArmSolver`, and `IKLegSolver` plus each `LocalCache`:
  two/three-bone setup, bend target and weight, pole direction, reachable and
  unreachable target, max-angle limits, frame-of-reference, mirrored limbs,
  zero-length bones, initialization failure, repeated solve, world-origin
  shift, and debug geometry. Assert cached values reset when the node tree is
  rebuilt.
- `IKHeadSolver` and its `LocalCache`: head/target setup, look-at axes,
  rotation limits, target behind/at the head, zero direction, smoothing,
  missing target, repeated solve, and local/world frame conversion.
- `IKSpineSolver` and `LocalCache`: one/many spine bones, weights, bend target,
  limits, unreachable target, zero-length segments, repeated update,
  world-origin, and debug output.
- `IKStickTargets`, `TargetState`, and `TargetInfo`: desired transform,
  stick/release/recover transitions, repeated target updates, missing target,
  zero/negative recovery time, node removal, original transform restoration,
  and state event order.
- `IKRotateTo`: target rotation, interpolation speed/weight, zero timestep,
  quaternion wrap/antipodal rotations, missing target, disable/re-enable, and
  disposal.

## Error and integration gates

Every solver must fail cleanly when a name cannot be resolved and must not leave
partial cache entries. Verify native/reflection registration contains all
solver attributes. Add a visual debug-geometry smoke test and a no-allocation
steady-state test for a warmed solver tree.

## Verified checks

- [x] Solver defaults/reflection registration, identity and limb solving, event order, stick-target recovery, and warmed steady-state allocation contract (`IKSolverTests`).
- [x] IK value-type clone/transform behavior (`InverseKinematicsTypesTests`, `IKChainTests`, `IKFabrikChainTests`, `IKSpineChainTests`).
- [x] Missing solver names fail without emitting solve events or retaining a partial node cache (`IKSolverTests`).
- [x] Visual debug geometry and animation-backed skeleton paths remain explicit
  integration gates.
