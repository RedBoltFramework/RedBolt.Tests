# Copyright (c) 2026 RedBolt and contributors.
# Sourced by the test scripts. Resolves the active build root the same way the MSBuild props do:
# an explicit REDBOLT_BUILD_ROOT, the monorepo root's build/ (RedBolt.slnx beside this repository),
# or this repository's own build/.
# Usage: source redbolt_build_root.sh <tests repository root>; sets redbolt_build_root.

redbolt_build_root="${REDBOLT_BUILD_ROOT:-}"
if [[ -z "$redbolt_build_root" ]]; then
    if [[ -f "$1/../RedBolt.slnx" ]]; then
        redbolt_build_root="$(cd -- "$1/.." && pwd)/build"
    else
        redbolt_build_root="$1/build"
    fi
fi

# Extra dotnet arguments: only an explicit REDBOLT_BUILD_ROOT is forwarded as RedBoltBuildRoot; otherwise
# the MSBuild props decide, so the scripts and the build always agree.
redbolt_build_root_arguments=()
if [[ -n "${REDBOLT_BUILD_ROOT:-}" ]]; then
    redbolt_build_root_arguments=("-p:RedBoltBuildRoot=$redbolt_build_root")
fi
