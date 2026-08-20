#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

tests_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
redbolt_root="$(cd "$tests_root/../RedBolt" && pwd)"
project="$redbolt_root/src/RedBolt/DearImGui/RedBolt.DearImGui.csproj"
browser_items="$(mktemp)"
linux_items="$(mktemp)"
trap 'rm -f -- "$browser_items" "$linux_items"' EXIT

dotnet msbuild "$project" \
    -p:Configuration=Debug \
    -p:RedBoltPlatform=browser-wasm \
    -getItem:ProjectReference,Compile > "$browser_items"

dotnet msbuild "$project" \
    -p:Configuration=Debug \
    -p:RedBoltPlatform=linux-x64 \
    -getItem:ProjectReference,Compile > "$linux_items"

if grep -Fq '"Identity": "../Utility/RedBolt.Utility.csproj"' "$browser_items" ||
    grep -Fq '"Identity": "SystemUI/' "$browser_items"; then
    printf 'Browser DearImGui unexpectedly includes editor SystemUI or Utility.\n' >&2
    exit 1
fi

grep -Fq '"Identity": "../Utility/RedBolt.Utility.csproj"' "$linux_items"
grep -Fq '"Identity": "SystemUI/StandardSerializableHooks.cs"' "$linux_items"

printf 'DearImGui platform graph is correct.\n'
