#!/usr/bin/env bash
# Copyright (c) 2026 RedBolt and contributors.

set -euo pipefail

if (($# == 0)); then
    printf 'Usage: %s command [args...]\n' "${0##*/}" >&2
    exit 2
fi

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

export LIBGL_ALWAYS_SOFTWARE=1
export MESA_LOADER_DRIVER_OVERRIDE=llvmpipe
export SDL_AUDIODRIVER="${SDL_AUDIODRIVER:-dummy}"

if [[ -n "${DISPLAY:-}" ]] || has_wayland_display; then
    exec -- "$@"
fi

xvfb_run="$(command -v xvfb-run || true)"
if [[ -z "$xvfb_run" ]]; then
    printf 'No usable desktop display was detected and xvfb-run was not found. Install Xvfb and ensure xvfb-run is on PATH.\n' >&2
    exit 1
fi

export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-x11}"

exec -- "$xvfb_run" -a -s "-screen 0 256x192x24 -nolisten tcp" -- "$@"
