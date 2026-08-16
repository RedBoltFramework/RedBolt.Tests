#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
shard_count="${REDBOLT_TEST_SHARD_COUNT:-4}"
configuration="${REDBOLT_TEST_CONFIGURATION:-Debug}"

if [[ ! "$shard_count" =~ ^[1-9][0-9]*$ ]]; then
    printf 'REDBOLT_TEST_SHARD_COUNT must be a positive integer.\n' >&2
    exit 2
fi

results_dir="$repo_dir/TestResults/parallel-$(date -u +%Y%m%dT%H%M%SZ)-$$"
mkdir -p -- "$results_dir"

pids=()
for ((shard_index = 0; shard_index < shard_count; ++shard_index)); do
    (
        export REDBOLT_TEST_SHARD_COUNT="$shard_count"
        export REDBOLT_TEST_SHARD_INDEX="$shard_index"
        export XDG_DATA_HOME="${XDG_DATA_HOME:-/home/rk/.local/share}"
        "$script_dir/with_software_renderer.sh" \
            dotnet test "$repo_dir/RedBolt.Tests.csproj" \
            --configuration "$configuration" \
            --no-build \
            "$@"
    ) >"$results_dir/shard-$shard_index.log" 2>&1 &
    pids+=("$!")
done

exit_code=0
for ((shard_index = 0; shard_index < shard_count; ++shard_index)); do
    if ! wait "${pids[$shard_index]}"; then
        exit_code=1
    fi
    printf '\n===== Test shard %d/%d =====\n' "$((shard_index + 1))" "$shard_count"
    sed -e "s/^/[shard $shard_index] /" "$results_dir/shard-$shard_index.log"
done

printf '\nShard logs: %s\n' "$results_dir"
exit "$exit_code"
