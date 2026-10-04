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

# The combined report covers more than a single run, so it has a baseline of its own.
REDBOLT_COVERAGE_BASELINE="${REDBOLT_COVERAGE_BASELINE:-$script_directory/../coverage.combined.baseline}" \
    "$script_directory/coverage_advisory.sh" "$output_path" Combined
