# Sample application smoke-test plan

Source: `../../RedBolt/src/RedBolt/Sample`. Sample classes are composition examples rather
than reusable library units. Test them by startup/update/shutdown smoke tests,
not by changing sample code to expose private implementation details.

## Verified deterministic coverage

- [x] `HelloWorld`, `Navigation`, and `Actions` sample projects compile
  successfully against the current managed/native output, including their
  project references and generated bindings (focused sample build gate).

The runtime smoke gate remains unchecked: the available environment has no SDL
video/audio device, and the headless HelloWorld run currently fails inside the
Dear ImGui platform initializer. Full startup/update/resize/shutdown and
non-black screenshot checks therefore remain CI or developer-machine work.

## Classes and scenarios

Each program entry class must be checked for valid exit, exception reporting,
and no leaked native resources. Each application class must run its documented
feature for at least one frame, resize once, and shut down cleanly:

- `ActionsProgram`, `ActionsApplication`, nested `ActionPulseEvent` and
  `DemoElement`: actions, event pulse, cleanup after remove/dispose.
- `AdvancedUIProgram`, `AdvancedUIWindow`, `AdvancedUI`, nested `PageIndex`,
  `AdvancedUIApplication`, and `SampleExitRequestedEvent`: page switching,
  data binding, close/exit event, and modal/UI teardown.
- `AggregatedInputProgram`/`AggregatedInputApplication`,
  `InputLoggerProgram`/`InputLoggerApplication` with `LoggedEvent` and
  `ViewportData`, `PointerAdapterProgram`/`PointerAdapterApplication`, and
  `CameraOperatorProgram`/`CameraOperatorSampleApplication`: keyboard,
  pointer, joystick, viewport, and subscription cleanup.
- `AnimatingSceneProgram`/`AnimatingScene`, `MaterialAnimationProgram`/
  `MaterialAnimationApplication`, `LightAnimationProgram`/
  `LightAnimationApplication`, `SkeletalAnimationProgram`/
  `SkeletalAnimationApplication`, `Mover3D`, and `Rotator`: animation start,
  pause/resume, missing resources, and node removal.
- `BakedLightingProgram`/`BakedLighting`, `StaticSceneProgram`/`StaticScene`,
  `DynamicGeometryProgram`/`DynamicGeometry`, `BillboardsProgram`/
  `BillboardsApplication`, `DecalsProgram`/`DecalsApplication`,
  `TextureFormatsProgram`/`TextureFormatsSample`, and
  `RenderToTextureProgram`/`RenderToTexture`: resource load failure, geometry
  mutation, render target resize, and non-black screenshot.
- `CameraShakeProgram`/`CameraShakeApplication`, `HugeObjectCountProgram`/
  `HugeObjectCountApplication`, `MultipleViewportsProgram`/`MultipleViewports`,
  `WindowSettingsProgram`/`WindowSettingsDemo`, and `HelloWorldProgram`/
  `HelloWorld`: camera/window settings, high object count, multi-viewport,
  resize, and deterministic exit.
- `CrowdNavigationProgram`/`CrowdNavigationSample`, `NavigationProgram`/
  `NavigationSample`, `PhysicsProgram`/`PhysicsSample`, and
  `InverseKinematicsProgram`/`InverseKinematicsApplication`: native subsystem
  startup, movement/physics/IK updates, missing backend, and teardown.
- `HelloGUIProgram`/`HelloGUI`, `HelloRmlUIProgram`, `SimpleWindow`,
  `HelloRmlUI`, `SoundEffectsProgram`/`SoundEffects`,
  `SoundSynthesisProgram`/`SoundSynthesis`, `ParticlesProgram`/
  `ParticlesApplication`, `Text3DProgram`/`Text3DApplication`, and
  `VirtualFileSystemProgram`/`VirtualFileSystemApplication`: UI/audio/particle/
  text/resource behavior, no-device fallback, and cleanup.
- `WebProgram` and `BrowserDom`: browser callback/task completion, DOM
  interop failures, resize, and shutdown without synchronous worker-to-browser
  calls.

Run the most relevant sample for every changed subsystem with a short timeout,
capture a screenshot where rendering is involved, and inspect that it is not
black or otherwise visibly broken. Add headless variants for CI where possible.
