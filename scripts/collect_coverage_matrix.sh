#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tests_root="$(cd -- "$script_directory/.." && pwd)"
default_apis="${REDBOLT_COVERAGE_APIS:-opengl,vulkan}"
matrix_results_directory="${REDBOLT_COVERAGE_MATRIX_RESULTS_DIR:-$tests_root/TestResults/coverage-matrix}"
combined_report="${REDBOLT_COVERAGE_COMBINED_REPORT:-$matrix_results_directory/combined/coverage.cobertura.xml}"
minimum_coverage="${REDBOLT_COVERAGE_MINIMUM:-95}"

declare -a requested_apis=()
declare -a test_arguments=()

append_apis() {
    local api_list="$1"
    api_list="${api_list//,/ }"
    read -r -a api_values <<< "$api_list"
    requested_apis+=("${api_values[@]}")
}

append_apis "$default_apis"

while (($# > 0)); do
    case "$1" in
        --apis)
            (($# >= 2)) || { printf '%s requires a comma-separated list.\n' "$1" >&2; exit 2; }
            requested_apis=()
            append_apis "$2"
            shift 2
            ;;
        --api)
            (($# >= 2)) || { printf '%s requires a backend name.\n' "$1" >&2; exit 2; }
            requested_apis+=("$2")
            shift 2
            ;;
        --results-directory)
            (($# >= 2)) || { printf '%s requires a directory.\n' "$1" >&2; exit 2; }
            matrix_results_directory="$2"
            shift 2
            ;;
        --output)
            (($# >= 2)) || { printf '%s requires a report path.\n' "$1" >&2; exit 2; }
            combined_report="$2"
            shift 2
            ;;
        --minimum)
            (($# >= 2)) || { printf '%s requires a percentage.\n' "$1" >&2; exit 2; }
            minimum_coverage="$2"
            shift 2
            ;;
        --help|-h)
            cat <<'USAGE'
Usage: collect_coverage_matrix.sh [options] [dotnet test arguments]

Options:
  --apis <list>               Comma/space-separated APIs (default: opengl,vulkan)
  --api <api>                 Add one API to the matrix
  --results-directory <dir>   Store per-API runs below this directory
  --output <file>             Write the merged Cobertura report here
  --minimum <percent>         Combined line/branch gate (default: 95)
  --                          End matrix options and pass the rest to dotnet test

The Linux matrix supports OpenGL and Vulkan. Both backends run the full test
suite; the graphics tests use the backend selected for that matrix entry.
USAGE
            exit 0
            ;;
        --)
            shift
            test_arguments+=("$@")
            break
            ;;
        *)
            test_arguments+=("$1")
            shift
            ;;
    esac
done

if ((${#requested_apis[@]} == 0)); then
    printf 'At least one graphics API is required.\n' >&2
    exit 2
fi

declare -a reports=()

for requested_api in "${requested_apis[@]}"; do
    api="${requested_api,,}"
    case "$api" in
        gl|opengl)
            api="opengl"
            ;;
        vk|vulkan)
            api="vulkan"
            ;;
        *)
            printf "Unsupported graphics API '%s'. Supported APIs: opengl, vulkan.\n" "$requested_api" >&2
            exit 2
            ;;
    esac

    run_directory="$matrix_results_directory/$api/$(date -u +%Y%m%dT%H%M%SZ)-$$"
    mkdir -p "$run_directory"

    declare -a api_test_arguments=("${test_arguments[@]}")

    printf 'Collecting %s coverage into %s\n' "$api" "$run_directory"
    if [[ "$api" == "vulkan" ]]; then
        REDBOLT_TEST_GRAPHICS_API="$api" \
        REDBOLT_COVERAGE_RESULTS_DIR="$run_directory" \
        REDBOLT_COVERAGE_MINIMUM=0 \
            "$script_directory/collect_coverage.sh" "${api_test_arguments[@]}"
    else
        REDBOLT_TEST_GRAPHICS_API="$api" \
        REDBOLT_COVERAGE_RESULTS_DIR="$run_directory" \
        REDBOLT_COVERAGE_MINIMUM=0 \
            "$script_directory/collect_coverage.sh" "${api_test_arguments[@]}"
    fi

    coverage_file="$(find "$run_directory" -type f -name coverage.cobertura.xml -printf '%T@ %p\n' \
        | sort -nr | sed -n '1{s/^[^ ]* //;p}')"
    if [[ -z "$coverage_file" ]]; then
        printf 'No Cobertura report was produced for %s.\n' "$api" >&2
        exit 1
    fi

    reports+=("$coverage_file")
    printf 'Recorded %s report: %s\n' "$api" "$coverage_file"
done

declare -a merge_arguments=(--output "$combined_report")
merge_arguments+=("${reports[@]}")
REDBOLT_COVERAGE_MINIMUM="$minimum_coverage" \
    "$script_directory/combine_coverage.sh" "${merge_arguments[@]}"

printf 'Combined coverage report: %s\n' "$combined_report"
