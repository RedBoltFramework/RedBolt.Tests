#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

# Prints the line and branch coverage of a Cobertura report and advises whether they dropped below the
# recorded baseline. The check is advisory: it only fails when the report itself cannot be read.
#   REDBOLT_COVERAGE_BASELINE=<file>        baseline to compare with (default: <tests root>/coverage.baseline)
#   REDBOLT_COVERAGE_UPDATE_BASELINE=1      record the measured values as the new baseline
#   REDBOLT_COVERAGE_ADVISORY=off           only print the coverage, for intermediate runs

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tests_root="$(cd -- "$script_directory/.." && pwd)"
baseline_file="${REDBOLT_COVERAGE_BASELINE:-$tests_root/coverage.baseline}"

if [[ $# -lt 1 || $# -gt 2 ]]; then
    printf 'Usage: coverage_advisory.sh <coverage.cobertura.xml> [label]\n' >&2
    exit 2
fi

coverage_file="$1"
label="${2:+$2 }"

if [[ ! -f "$coverage_file" ]]; then
    printf 'Coverage report was not found: %s\n' "$coverage_file" >&2
    exit 1
fi

coverage_header="$(rg -o '<coverage[^>]*' "$coverage_file" | head -1 || true)"
lines_covered="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-covered="\([^"]*\)".*/\1/p')"
lines_valid="$(printf '%s\n' "$coverage_header" | sed -n 's/.*lines-valid="\([^"]*\)".*/\1/p')"
branches_covered="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branches-covered="\([^"]*\)".*/\1/p')"
branches_valid="$(printf '%s\n' "$coverage_header" | sed -n 's/.*branches-valid="\([^"]*\)".*/\1/p')"

if [[ -z "$lines_covered" || -z "$lines_valid" || -z "$branches_covered" || -z "$branches_valid" ]]; then
    printf 'Coverage report has no readable coverage header: %s\n' "$coverage_file" >&2
    exit 1
fi

# The rate attributes are rounded differently by Coverlet and by the merger, so the percentages are
# derived from the counts, rounded down to two decimals.
coverage_percent() {
    awk -v covered="$1" -v valid="$2" \
        'BEGIN { printf "%.2f", (valid > 0 ? int(10000 * covered / valid) / 100 : 0) }'
}

line_percent="$(coverage_percent "$lines_covered" "$lines_valid")"
branch_percent="$(coverage_percent "$branches_covered" "$branches_valid")"

if [[ -n "$label" ]]; then
    printf '%s line coverage: %s%% (%s/%s lines)\n' "${label% }" "$line_percent" "$lines_covered" "$lines_valid"
    printf '%s branch coverage: %s%% (%s/%s branches)\n' "${label% }" "$branch_percent" "$branches_covered" "$branches_valid"
else
    printf 'Line coverage: %s%% (%s/%s lines)\n' "$line_percent" "$lines_covered" "$lines_valid"
    printf 'Branch coverage: %s%% (%s/%s branches)\n' "$branch_percent" "$branches_covered" "$branches_valid"
fi

if [[ "${REDBOLT_COVERAGE_ADVISORY:-}" == "off" ]]; then
    exit 0
fi

if [[ "${REDBOLT_COVERAGE_UPDATE_BASELINE:-}" == "1" ]]; then
    printf 'line=%s\nbranch=%s\n' "$line_percent" "$branch_percent" > "$baseline_file"
    printf 'Coverage advisory: recorded baseline in %s (line %s%%, branch %s%%)\n' \
        "$baseline_file" "$line_percent" "$branch_percent"
    exit 0
fi

if [[ ! -f "$baseline_file" ]]; then
    printf 'Coverage advisory: no baseline file at %s; nothing to compare with\n' "$baseline_file"
    exit 0
fi

baseline_line="$(sed -n 's/^line=\([0-9.]*\)$/\1/p' "$baseline_file" | head -1)"
baseline_branch="$(sed -n 's/^branch=\([0-9.]*\)$/\1/p' "$baseline_file" | head -1)"

if [[ -z "$baseline_line" || -z "$baseline_branch" ]]; then
    printf 'Coverage advisory: baseline file %s has no line= and branch= values; nothing to compare with\n' \
        "$baseline_file"
    exit 0
fi

dropped=0
raised=0

# Compares one measured percentage with its baseline and reports on stderr when it dropped.
compare_coverage() {
    local name="$1" measured="$2" baseline="$3"
    if awk -v measured="$measured" -v baseline="$baseline" 'BEGIN { exit !(measured + 0 < baseline + 0) }'; then
        printf 'Coverage advisory: %s coverage dropped from %s%% to %s%%\n' "$name" "$baseline" "$measured" >&2
        dropped=1
    elif awk -v measured="$measured" -v baseline="$baseline" 'BEGIN { exit !(measured + 0 > baseline + 0) }'; then
        raised=1
    fi
}

compare_coverage line "$line_percent" "$baseline_line"
compare_coverage branch "$branch_percent" "$baseline_branch"

if ((dropped == 0)); then
    printf 'Coverage advisory: coverage did not drop (baseline line %s%%, branch %s%%)\n' \
        "$baseline_line" "$baseline_branch"
    if ((raised == 1)); then
        printf 'Coverage advisory: coverage is above the baseline; rerun with REDBOLT_COVERAGE_UPDATE_BASELINE=1 to raise it\n'
    fi
fi
