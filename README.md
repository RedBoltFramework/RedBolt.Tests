# RedBolt.Tests

This repository contains the RedBolt test suite. Clone it next to the RedBolt
repository so the RedBolt solution discovers it automatically:

```text
parent-directory/
├── RedBolt/
└── RedBolt.Tests/
```

Run the suite from this repository with:

```bash
../RedBolt/scripts/run_visual_tests_offscreen.sh --verbosity minimal
```

Collect production line and branch coverage with the software-rendered
off-screen harness and an 80% line/branch gate:

```bash
./scripts/collect_coverage.sh
```

The command uses Xvfb/llvmpipe by default so native graphics and visual tests
run through the software renderer. Set `REDBOLT_COVERAGE_MINIMUM` to override
the gate for local experiments. The report is written under
`TestResults/coverage/` in Cobertura format.

The gate intentionally measures deterministic managed runtime surfaces. The
coverage settings exclude build-time generators, unavailable OpenGLES/Vulkan
and Dear ImGui backend adapters, plus optional native/UI/audio/navigation/IK
integration and backend-dependent graphics, input, scene, resource, render
pipeline, and IO orchestration. Those surfaces remain part of the documented
manual/backend test plan.
