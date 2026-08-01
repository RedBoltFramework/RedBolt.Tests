#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tests_root="$(cd -- "$script_directory/.." && pwd)"
redbolt_root="${REDBOLT_ROOT:-$tests_root/../RedBolt}"
redbolt_configuration="${REDBOLT_COVERAGE_CONFIGURATION:-Debug}"
results_directory="${REDBOLT_COVERAGE_RESULTS_DIR:-$tests_root/TestResults/coverage}"
redbolt_user_home="$(getent passwd "$(id -u)" | cut -d: -f6)"
redbolt_data_root="${XDG_DATA_HOME:-$redbolt_user_home/.local/share}"
cmake_root="${REDBOLT_CMAKE_ROOT:-$redbolt_data_root/uv/tools/cmake/bin}"
ninja_root="${REDBOLT_NINJA_ROOT:-$redbolt_data_root/uv/tools/ninja/bin}"

if [[ -d "$cmake_root" ]]; then
    PATH="$cmake_root:$PATH"
fi
if [[ -d "$ninja_root" ]]; then
    PATH="$ninja_root:$PATH"
fi
export PATH

mkdir -p "$results_directory"

coverage_arguments=(
    --configuration "$redbolt_configuration"
    --no-restore
    --results-directory "$results_directory"
    --settings "$tests_root/coverage.runsettings"
    --collect:"XPlat Code Coverage"
    -p:RedBoltKeepParentRuntimeAssemblies=true
    -p:BuildProjectReferences=false
)

offscreen_coverage_arguments=(
    --results-directory "$results_directory"
    --settings "$tests_root/coverage.runsettings"
    --collect:"XPlat Code Coverage"
    -p:RedBoltKeepParentRuntimeAssemblies=true
    -p:BuildProjectReferences=false
)

if [[ "${REDBOLT_COVERAGE_OFFSCREEN:-0}" == "1" ]]; then
    "$redbolt_root/scripts/run_visual_tests_offscreen.sh" \
        "${offscreen_coverage_arguments[@]}" "$@"
else
    env \
        -u DISPLAY \
        -u WAYLAND_DISPLAY \
        -u SDL_VIDEODRIVER \
        -u SDL_AUDIODRIVER \
        -u REDBOLT_RUN_VISUAL_TESTS \
        -u REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE \
        -u REDBOLT_RUN_DILIGENT_GL_SMOKE \
        dotnet test "$tests_root/RedBolt.Tests.csproj" \
        "${coverage_arguments[@]}" "$@"
fi

coverage_file="$(find "$results_directory" -type f -name coverage.cobertura.xml -printf '%T@ %p\n' \
    | sort -nr | sed -n '1{s/^[^ ]* //;p}')"

if [[ -z "$coverage_file" ]]; then
    printf 'Coverage report was not produced under %s\n' "$results_directory" >&2
    exit 1
fi

coverage_header="$(rg -o '<coverage[^>]*' "$coverage_file" | head -1)"
line_rate="$(printf '%s\n' "$coverage_header" | sed -n 's/.*line-rate="\([^"]*\)".*/\1/p')"
branch_rate="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branch-rate="\([^"]*\)".*/\1/p')"
lines_covered="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-covered="\([^"]*\)".*/\1/p')"
lines_valid="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-valid="\([^"]*\)".*/\1/p')"

awk -v line_rate="$line_rate" -v branch_rate="$branch_rate" \
    -v lines_covered="$lines_covered" -v lines_valid="$lines_valid" \
    'BEGIN {
        printf "Line coverage: %.2f%% (%s/%s lines)\n", 100 * line_rate, lines_covered, lines_valid
        printf "Branch coverage: %.2f%%\n", 100 * branch_rate
    }'
printf 'Cobertura report: %s\n' "$coverage_file"
