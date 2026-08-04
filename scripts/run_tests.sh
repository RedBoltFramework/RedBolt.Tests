#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tests_root="$(cd -- "$script_directory/.." && pwd)"
reference_root="${REDBOLT_VISUAL_REFERENCE_ROOT:-$tests_root/src/RedBolt}"

export REDBOLT_RUN_VISUAL_TESTS="${REDBOLT_RUN_VISUAL_TESTS:-1}"
export REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE="${REDBOLT_RUN_GRAPHICS_BACKEND_LIFECYCLE:-1}"
export REDBOLT_RUN_DILIGENT_GL_SMOKE="${REDBOLT_RUN_DILIGENT_GL_SMOKE:-1}"
export REDBOLT_VISUAL_REFERENCE_ROOT="$reference_root"

test_command=(
    dotnet test "$tests_root/RedBolt.Tests.csproj"
    --configuration Debug --no-restore "$@"
)

has_wayland_display() {
    local wayland_socket="${WAYLAND_DISPLAY:-}"

    if [[ -z "$wayland_socket" ]]; then
        return 1
    fi

    if [[ "$wayland_socket" != /* ]]; then
        if [[ -z "${XDG_RUNTIME_DIR:-}" ]]; then
            return 1
        fi
        wayland_socket="$XDG_RUNTIME_DIR/$wayland_socket"
    fi

    [[ -S "$wayland_socket" ]]
}

if [[ -n "${DISPLAY:-}" ]] || has_wayland_display; then
    exec "${test_command[@]}"
fi

xvfb_run="$(command -v xvfb-run || true)"
if [[ -z "$xvfb_run" ]]; then
    printf 'No usable desktop display was detected and xvfb-run was not found. Install Xvfb and ensure xvfb-run is on PATH.\n' >&2
    exit 1
fi

export LIBGL_ALWAYS_SOFTWARE=1
export MESA_LOADER_DRIVER_OVERRIDE=llvmpipe
export SDL_VIDEODRIVER=x11
export SDL_AUDIODRIVER=dummy

exec "$xvfb_run" -a -s "-screen 0 256x192x24 -nolisten tcp" "${test_command[@]}"
