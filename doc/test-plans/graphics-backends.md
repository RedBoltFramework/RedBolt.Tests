# Graphics backend test plan

Source: `../RedBolt/src/RedBolt/Graphics/OpenGL`, `OpenGLES`, and `Vulkan`. Existing
platform composition coverage is in `PlatformTests`; the backend
tests below require fakes plus opt-in native smoke tests.

## Verified coverage

- [x] Linux runtime composition exposes the OpenGL and Vulkan backend types and
  rejects unavailable D3D11 (`PlatformCompositionTests`).
- [ ] OpenGL context creation/resize/present/release and backend lifetime
  reinitialization are covered by an opt-in test, but remain unverified on this
  headless host (`GraphicsBackendLifecycleTests`).
- [ ] GLES and Vulkan native lifecycle/error matrices still need injectable
  fake collaborators and native smoke coverage.

- `IOpenGLGraphicsApi`, `OpenGLGraphicsBackend`, and nested `OpenGLLifetime`:
  verify creation from a valid SDL window, context ownership, default device
  settings, vsync, resize, swap/present, native-context exposure, device-lost
  and reset, missing OpenGL symbols, incompatible window, and exact release
  ordering. Test borrowed versus owned SDL/native handles and dispose after a
  partially created context.
- `IOpenGLESGraphicsApi`, `OpenGLESGraphicsBackend`, and nested
  `OpenGLESLifetime`: repeat the lifecycle matrix for GLES, including web/
  offscreen context constraints, extension/feature discovery, resize and
  present, unsupported backend, thread/context ownership, and browser callback
  failure.
- `IVulkanGraphicsApi`, `VulkanGraphicsBackend`, and nested `VulkanLifetime`:
  verify instance/device/swap-chain creation, optional feature and descriptor
  settings, surface extent query, vsync contract, resize success/failure,
  device lost/reset, missing Vulkan loader, unsupported feature, swap-chain
  release before common resources, and double disposal.

Use fake `IGraphicsBackend` collaborators to assert every branch without a GPU.
Then run one native smoke test per available backend that creates a window,
renders a clear frame, resizes, presents, and shuts down. Capture and review a
non-black frame. Add diagnostics for missing display, driver, loader, or SDL
context rather than treating them as silent passes.
