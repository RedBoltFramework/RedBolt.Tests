# Platform subsystem test plan

Source: `../RedBolt/src/RedBolt/Platform`. Existing composition tests are in
`PlatformTests`.

- `Application` (shared partial), `Application.Linux`, and `Application.Web`:
  test command-line/default parameter setup, context/runtime service
  composition, window creation, graphics backend selection, headless mode,
  startup/update/shutdown order, exit request, exception during startup and
  update, repeat run, and disposal after partial initialization. Verify the
  shared partial implementation behaves identically on both platforms where
  the platform contract is shared.
- `GraphicsBackendResources`, `LinuxPlatform`, `LinuxPlatformRuntime`,
  `WebPlatform`, `WebPlatformRuntime`, and private `EmptyPlatformRuntime`:
  verify resource ownership and runtime service registration, supported backend
  lists, unavailable backend rejection, native library loading, web callback
  and task boundaries, browser-thread restrictions, and shutdown idempotence.

Use fake runtime services and backend factories for pure composition tests.
Keep one Linux OpenGL/Vulkan smoke test and one Web build/boot test as separate
platform gates. Cover missing environment variables, missing native libraries,
invalid backend names, context creation failure, and shutdown after a failed
context creation.

## Verified checks

- [x] Linux runtime composition, supported OpenGL/Vulkan backend lookup, file-logging capability, and unavailable-backend rejection (`PlatformCompositionTests`).
- [ ] Application startup/update/shutdown failure paths and Web boot/thread restrictions remain platform gates.
