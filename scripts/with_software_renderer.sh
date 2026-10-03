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

# Render on a private Xvfb display by default so results do not depend on the
# host display's DPI, visual or sRGB capability. Set
# REDBOLT_TESTS_USE_HOST_DISPLAY=1 to run on the host desktop display instead.
if [[ "${REDBOLT_TESTS_USE_HOST_DISPLAY:-}" == "1" ]] &&
    { [[ -n "${DISPLAY:-}" ]] || has_wayland_display; }; then
    exec -- "$@"
fi

xvfb="$(command -v Xvfb || true)"
if [[ -z "$xvfb" ]]; then
    printf 'No usable desktop display was detected and Xvfb was not found. Install Xvfb and ensure Xvfb is on PATH.\n' >&2
    exit 1
fi

export SDL_VIDEODRIVER=x11

# Let Xvfb pick a free display atomically (-displayfd) so concurrent wrappers
# never share or race for a display number.
work_directory="$(mktemp -d)"
xvfb_pid=""

cleanup() {
    if [[ -n "$xvfb_pid" ]]; then
        kill "$xvfb_pid" 2>/dev/null || true
        wait "$xvfb_pid" 2>/dev/null || true
    fi
    rm -rf -- "$work_directory"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 129' HUP
trap 'exit 143' TERM

display_file="$work_directory/display"
xvfb_log="$work_directory/xvfb.log"
display_number=""

# Start Xvfb and wait for it to report its display number. Extra arguments are
# passed through to Xvfb.
start_xvfb() {
    local attempt

    display_number=""
    : >"$display_file"
    "$xvfb" -displayfd 3 -screen 0 256x192x24 -nolisten tcp "$@" 3>"$display_file" >/dev/null 2>>"$xvfb_log" &
    xvfb_pid=$!

    for ((attempt = 0; attempt < 200; attempt++)); do
        if [[ -s "$display_file" ]] && IFS= read -r display_number <"$display_file" && [[ -n "$display_number" ]]; then
            return 0
        fi
        if ! kill -0 "$xvfb_pid" 2>/dev/null; then
            break
        fi
        sleep 0.05
    done

    kill "$xvfb_pid" 2>/dev/null || true
    wait "$xvfb_pid" 2>/dev/null || true
    xvfb_pid=""
    return 1
}

# Fall back to abstract sockets only when /tmp/.X11-unix is not writable.
if ! start_xvfb && ! start_xvfb -nolisten unix; then
    printf 'Xvfb did not report a display number.\n' >&2
    cat -- "$xvfb_log" >&2
    exit 1
fi

export DISPLAY=":$display_number"
unset WAYLAND_DISPLAY

status=0
"$@" || status=$?
exit "$status"
