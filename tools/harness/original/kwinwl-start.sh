#!/usr/bin/env bash
# kwinwl-start.sh - starts, in the background, a private, headless, GPU-backed X display for the original: kwin_wayland
# --virtual with Xwayland on :78, 1024 x 768, its log in logs/kwinwl.log. It has no stop (kill that kwin_wayland), and no
# other script here starts or uses it: original.sh --offscreen is Xvfb on :77, and lib/parkrun.py's OFFSCREEN takes :78 too.
# Own D-Bus, own config dirs, own Wayland socket. Touches neither the desktop's screen nor its input.
. "$(dirname "$0")/../lib/paths.sh"
K="$WORK/original/kwinwl"
env -u WAYLAND_DISPLAY -u DISPLAY XDG_CONFIG_HOME="$K/config" XDG_DATA_HOME="$K/data" XDG_CACHE_HOME="$K/cache" XDG_STATE_HOME="$K/state" \
	dbus-run-session -- kwin_wayland --virtual --xwayland --socket wayland-tpw78 --xwayland-display :78 \
	--width 1024 --height 768 --no-lockscreen --no-global-shortcuts --no-kactivities >"$OLOGS/kwinwl.log" 2>&1 &
echo started
