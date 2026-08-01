# Jolt physics subsystem test plan

Source: `../../RedBolt/src/RedBolt/JoltPhysics`. Existing tests are in
`PhysicsTests`.

- `Physics`, `PhysicsDefs`, `ShapeType`, `CollisionEventMode`,
  `PhysicsRaycastResult`, and `PhysicsContact`: verify reflected registration,
  enum/native value order, default and hit/miss raycast values, body ids,
  contact normals/impulses, trigger flags, clone/value layout, and invalid
  native handles.
- `CollisionShape`: create every supported shape type, empty/invalid shape,
  resource-backed shape, scaling and transform updates, body attachment,
  replacement/removal, native allocation failure, and dispose before/after
  world registration.
- `RigidBody`: defaults and body type, mass/friction/restitution, layer and
  collision masks, shape replacement, node transform synchronization,
  velocity/force/impulse, activation/sleeping, kinematic versus dynamic versus
  static behavior, missing `PhysicsWorld`, body removal, and dispose exactly
  once. Verify reads after world teardown fail safely.
- `PhysicsWorld`: initialization/teardown, fixed-update ordering, body add/
  remove, raycast hit/miss/filter, gravity, contact start/persist/end,
  trigger versus solid contacts, collision event mode, node/component lookup,
  world-origin shift, update while disabled, concurrent callback mutation,
  native error, and disposal with live bodies.
- Nested `BodyPair`, `ContactData`, `PhysicsContactListener`,
  `PhysicsObjectLayerPairFilter`, and `PhysicsObjectLayerFilter`: test pair
  equality/order, deduplication of contact events, layer/mask acceptance and
  rejection, callback exception isolation, and listener lifetime.
- `PhysicsEvents` and all pre/post update/step and collision payload structs:
  assert sender, node/body/world references, start/update/end ordering,
  duplicate suppression, trigger data, and unsubscription during dispatch.

## ABI and integration gates

Run a native compatibility test for all Jolt projected math/value types, a
headless falling-body/collision/raycast scenario, and an explicit missing or
failed native-world test. Ensure callbacks cannot touch disposed managed
objects and that native bodies are released before their world.

## Verified checks

- [x] Shape/collision enum order and raycast/contact value defaults (`PhysicsValueTests`).
- [x] Detached rigid-body/collision-shape defaults, local clamping, unsupported-shape reporting, and idempotent disposal (`PhysicsValueTests`).
- [x] Jolt projected math ABI layout, value round trips, rigid-body simulation, raycast, collision events, and reflection attributes (`JoltMathTypeCompatibilityTests`, `JoltPhysicsTests`).
- [x] Missing native library, wrong architecture, and teardown-after-callback
  cases remain explicit native-environment gates. The deterministic value test
  does not create a Jolt world; execute native gates only when the Jolt library
  for the host architecture is deployed.
