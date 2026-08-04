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
./scripts/run_tests.sh --verbosity minimal
```

Collect production line and branch coverage with the software-rendered
off-screen harness and a 95% line/branch gate:

```bash
./scripts/collect_coverage.sh
```

The command uses an available desktop graphics session, or falls back to
Xvfb/llvmpipe when no desktop display is available. Set
`REDBOLT_COVERAGE_MINIMUM` to override the gate for local experiments. The
report is written under `TestResults/coverage/` in Cobertura format.

Run the supported Linux backend matrix and merge the reports into one gated
Cobertura report with:

```bash
./scripts/collect_coverage_matrix.sh
```

The matrix runs the full test suite once with OpenGL and once with Vulkan;
graphics fixtures use the backend selected for each run. Select a subset with
`REDBOLT_COVERAGE_APIS=opengl` or `--apis opengl,vulkan`. The merged report is
written to
`TestResults/coverage-matrix/combined/coverage.cobertura.xml`; override that
path with `--output` or `REDBOLT_COVERAGE_COMBINED_REPORT`.

Vulkan fixtures reuse one Diligent device/context for the lifetime of the test
process because Diligent does not support creating a second Vulkan device in
the same process.

Visual captures use the shared OpenGL reference unless a test explicitly marks
backend-native rasterization or format behavior. Those captures use a strict
`<name>.vulkan.png` reference. Regenerate only the marked backend references
with `REDBOLT_UPDATE_BACKEND_VISUAL_REFERENCES=1`; the ordinary
`REDBOLT_UPDATE_VISUAL_REFERENCES=1` switch still updates all selected visual
references.

To merge existing reports without rerunning tests:

```bash
./scripts/combine_coverage.sh --output TestResults/coverage-combined.xml \
  TestResults/coverage/opengl TestResults/coverage/vulkan
```

Cobertura does not provide stable IDs for individual branch edges. The merger
therefore unions line hits and caps the sum of covered branch edges at each
line's denominator.

The gate intentionally measures deterministic managed runtime surfaces. The
coverage settings exclude build-time generators, OpenGLES and backend-specific
native adapters, plus optional native/UI/audio/navigation/IK integration and
backend-dependent graphics, input, scene, resource, render pipeline, and IO
orchestration. Those surfaces remain part of the documented manual/backend
test plan.
