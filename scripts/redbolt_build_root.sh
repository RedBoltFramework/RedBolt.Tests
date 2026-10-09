# Copyright (c) 2026 RedBolt and contributors.
# Sourced by the test scripts. Resolves the active build root with the same default as the MSBuild props:
# an explicit REDBOLT_BUILD_ROOT, otherwise the build/ beside this repository. RedBolt.Workspace.props is
# not read; when it sets RedBoltBuildRoot, set REDBOLT_BUILD_ROOT to the same directory.
# Usage: source redbolt_build_root.sh <tests repository root>; sets redbolt_build_root.

redbolt_build_root="${REDBOLT_BUILD_ROOT:-$(cd -- "$1/.." && pwd)/build}"

# Extra dotnet arguments: only an explicit REDBOLT_BUILD_ROOT is forwarded as RedBoltBuildRoot; otherwise
# the MSBuild props decide, so the scripts and the build agree.
redbolt_build_root_arguments=()
if [[ -n "${REDBOLT_BUILD_ROOT:-}" ]]; then
    redbolt_build_root_arguments=("-p:RedBoltBuildRoot=$redbolt_build_root")
fi
