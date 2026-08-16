#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
merger_project="$script_directory/coverage-merge/coverage-merge.csproj"
output_path=""

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    printf 'Usage: combine_coverage.sh --output <coverage.cobertura.xml> <report.xml|report-dir> [...]\n'
    exit 0
fi

for ((index = 1; index <= $#; ++index)); do
    argument="${!index}"
    case "$argument" in
        --output|-o)
            next_index=$((index + 1))
            if ((next_index > $#)); then
                printf '%s requires a path.\n' "$argument" >&2
                exit 2
            fi
            output_path="${!next_index}"
            ;;
    esac
done

if [[ -z "$output_path" ]]; then
    printf 'Usage: combine_coverage.sh --output <coverage.cobertura.xml> <report.xml|report-dir> [...]\n' >&2
    exit 2
fi

if [[ ! -f "$merger_project" ]]; then
    printf 'Coverage merger project was not found: %s\n' "$merger_project" >&2
    exit 1
fi

dotnet run --project "$merger_project" --configuration Release -- "$@"

if [[ ! -f "$output_path" ]]; then
    printf 'Coverage merger did not create %s\n' "$output_path" >&2
    exit 1
fi

coverage_header="$(rg -o '<coverage[^>]*' "$output_path" | head -1)"
line_rate="$(printf '%s\n' "$coverage_header" | sed -n 's/.*line-rate="\([^"]*\)".*/\1/p')"
branch_rate="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branch-rate="\([^"]*\)".*/\1/p')"
lines_covered="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-covered="\([^"]*\)".*/\1/p')"
lines_valid="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-valid="\([^"]*\)".*/\1/p')"
branches_covered="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branches-covered="\([^"]*\)".*/\1/p')"
branches_valid="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branches-valid="\([^"]*\)".*/\1/p')"

minimum_coverage="${REDBOLT_COVERAGE_MINIMUM:-95}"

awk -v line_rate="$line_rate" -v branch_rate="$branch_rate" \
    -v lines_covered="$lines_covered" -v lines_valid="$lines_valid" \
    -v branches_covered="$branches_covered" -v branches_valid="$branches_valid" \
    -v minimum_coverage="$minimum_coverage" \
    'BEGIN {
        printf "Combined line coverage: %.2f%% (%s/%s lines)\n", 100 * line_rate, lines_covered, lines_valid
        printf "Combined branch coverage: %.2f%% (%s/%s branches)\n", 100 * branch_rate, branches_covered, branches_valid
        if (100 * line_rate < minimum_coverage || 100 * branch_rate < minimum_coverage) {
            printf "Combined coverage gate failed: both line and branch coverage must be at least %.2f%%\n", minimum_coverage > "/dev/stderr"
            exit 1
        }
    }'
