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
dotnet test RedBolt.Tests.csproj --configuration Debug
```

Collect production line and branch coverage in a headless run with:

```bash
./scripts/collect_coverage.sh
```

Set `REDBOLT_COVERAGE_OFFSCREEN=1` to include the software-rendered native
graphics and visual tests through the Xvfb/llvmpipe harness. The report is
written under `TestResults/coverage/` in Cobertura format.
