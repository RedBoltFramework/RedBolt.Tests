#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tests_root="$(cd -- "$script_directory/.." && pwd)"
# shellcheck source=redbolt_build_root.sh
source "$script_directory/redbolt_build_root.sh" "$tests_root"
results_directory="${REDBOLT_COVERAGE_RESULTS_DIR:-$redbolt_build_root/TestResults/coverage}"

mkdir -p "$results_directory"

# Coverlet only instruments the modules next to the test assembly, while the library assemblies live in
# the shared core/ bucket and are found through additionalProbingPaths. Instrumenting rewrites assemblies,
# so the shared outputs must not be touched: run from a private copy of the tests output directory with
# the RedBolt library assemblies and their pdbs beside it. The copy is a sibling of tests/ so the relative
# lookups from AppContext.BaseDirectory (the repository root and the data directories) still resolve.
# The managed bindings from extern/ are copied without pdbs: Coverlet needs them beside the library
# assemblies to resolve references while instrumenting, but they are not measured themselves.
configuration=Debug
build_platform_directory="$redbolt_build_root/bin/$configuration/linux-x64"
tests_output_directory="$build_platform_directory/tests"
coverage_output_directory="$build_platform_directory/tests-coverage"

build_arguments=(
    --configuration "$configuration" --no-restore
    ${redbolt_build_root_arguments[@]+"${redbolt_build_root_arguments[@]}"}
    -p:BuildProjectReferences=false
)

# A caller's --no-build means the tests output is already current; it is not passed on a second time.
build_tests=true
test_arguments=()
for argument in "$@"; do
    if [[ "$argument" == --no-build ]]; then
        build_tests=false
    else
        test_arguments+=("$argument")
    fi
done

if [[ "$build_tests" == true ]]; then
    dotnet build "$tests_root/RedBolt.Tests.csproj" "${build_arguments[@]}"
fi

# The copy is recreated on every run and shared by all runs of one build root: run one at a time.
rm -rf "$coverage_output_directory"
mkdir -p "$coverage_output_directory"
cp -a "$tests_output_directory/." "$coverage_output_directory/"
cp -f "$build_platform_directory"/extern/RedBolt*.dll "$coverage_output_directory/"
cp -f "$build_platform_directory"/core/RedBolt*.dll "$coverage_output_directory/"
cp -f "$tests_output_directory"/pdb/*.pdb "$build_platform_directory"/core/pdb/RedBolt*.pdb "$coverage_output_directory/"

offscreen_coverage_arguments=(
    --results-directory "$results_directory"
    --settings "$tests_root/coverage.runsettings"
    --collect:"XPlat Code Coverage"
    --no-build
    -p:OutputPath="$coverage_output_directory/"
)

"$script_directory/with_software_renderer.sh" \
    dotnet test "$tests_root/RedBolt.Tests.csproj" \
    --configuration "$configuration" --no-restore \
    ${redbolt_build_root_arguments[@]+"${redbolt_build_root_arguments[@]}"} \
    "${offscreen_coverage_arguments[@]}" ${test_arguments[@]+"${test_arguments[@]}"}

coverage_file="$(find "$results_directory" -type f -name coverage.cobertura.xml -printf '%T@ %p\n' \
    | sort -nr | sed -n '1{s/^[^ ]* //;p}')"

if [[ -z "$coverage_file" ]]; then
    printf 'Coverage report was not produced under %s\n' "$results_directory" >&2
    exit 1
fi

"$script_directory/coverage_advisory.sh" "$coverage_file"
printf 'Cobertura report: %s\n' "$coverage_file"
