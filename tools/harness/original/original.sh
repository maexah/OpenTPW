#!/usr/bin/env bash
# original.sh - run the ORIGINAL game (reference install ~/Games/TPW-original, testme.exe) as an RE instrument.
#   original.sh start [--offscreen]   silent (null ALSA), Proton log + fps log in logs/, 30 fps cap (play.sh)
#                                     --offscreen: private Xvfb :77 (llvmpipe; see TOOLING.md for what that changes)
#   original.sh stop [--offscreen]    wineserver -k for the reference prefix only (+ the private KWin/Xvfb)
#   original.sh pid                   the game's Linux pid (for tpwmem.py)
# Extra Wine channels: ORIG_WINEDEBUG=+file,+reg original.sh start   (added to +timestamp,+fps)
# The game starts in a time namespace that reads a fresh boot (timens.py, under the steam AppArmor profile, which Ubuntu's
# userns restriction allows), so its park clock is true at any uptime (TOOLING.md warning 1). ORIG_TIMENS=0 starts it plainly.
# ORIG_FPS=<n> caps it at n frames a second instead of play.sh's 30 (a copy of play.sh in logs/, Q642: 60 tells a per-frame
# draw from a per-tick one, which 30 against 32 ticks cannot)
# TPW_ORIGINAL=<folder> names another install (loadfile.sh does not follow it); ORIG_ASOUND=<file> another ALSA config (default asound-null.conf here)
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
. "$(dirname "$0")/../lib/paths.sh"
G="${TPW_ORIGINAL:-$HOME/Games/TPW-original}"
PROTON_DIR="$(sed -n 's|^exec python3 "\(.*\)/proton" .*|\1|p' "$G/play.sh")"
case "${1:-}" in
start)
	if [ "${2:-}" = "--offscreen" ]; then
		pgrep -f "Xvfb :77" >/dev/null || { "$XVFB" :77 -screen 0 1024x768x24 -nolisten tcp -noreset >"$OLOGS/xvfb.log" 2>&1 & sleep 2; }
		export DISPLAY=:77
		# A private KWin on :77 only (own D-Bus, own config dirs): without a window manager the game is not scaled
		# to the screen and shows a cropped corner. Never run kwin_x11 without DISPLAY=:77 in front of it.
		if ! xprop -root _NET_SUPPORTING_WM_CHECK 2>/dev/null | grep -q "window id"; then
			K="$WORK/original/kwinhome"; mkdir -p "$K/config" "$K/data" "$K/cache" "$K/state"
			env DISPLAY=:77 XDG_CONFIG_HOME="$K/config" XDG_DATA_HOME="$K/data" XDG_CACHE_HOME="$K/cache" \
				XDG_STATE_HOME="$K/state" KWIN_COMPOSE=N dbus-run-session -- kwin_x11 >"$OLOGS/kwin.log" 2>&1 &
			sleep 4
		fi
	fi
	export DBUS_SESSION_BUS_ADDRESS="disabled:"   # no "restart your computer" pop-up on the desktop for RE runs
	rm -f "$OLOGS/steam-0.log" "$OLOGS/steam-umu-tpw.log"
	export WINEDLLOVERRIDES="winepulse.drv=d" ALSA_CONFIG_PATH="/usr/share/alsa/alsa.conf:${ORIG_ASOUND:-$H/asound-null.conf}"
	export PROTON_LOG=1 PROTON_LOG_DIR="$OLOGS" WINEDEBUG="+timestamp,+fps${ORIG_WINEDEBUG:+,$ORIG_WINEDEBUG}"
	PLAY="$G/play.sh"
	if [ -n "${ORIG_FPS:-}" ]; then
		PLAY="$OLOGS/play-fps.sh"; sed "s/fps_limit=30/fps_limit=$ORIG_FPS/" "$G/play.sh" > "$PLAY"; chmod +x "$PLAY"
		grep -q "fps_limit=$ORIG_FPS," "$PLAY" || { echo "original.sh: play.sh has no fps_limit=30 to change" >&2; exit 1; }
		echo "capped at $ORIG_FPS frames a second ($PLAY)"
	fi
	if [ "${ORIG_TIMENS:-1}" != 0 ] && aa-exec -p steam -- python3 "$H/timens.py" 60 true 2>/dev/null; then
		nohup aa-exec -p steam -- python3 "$H/timens.py" 60 "$PLAY" >"$OLOGS/play.out" 2>&1 &
		echo "started on DISPLAY=$DISPLAY in a fresh-boot clock (timens.py); logs in $OLOGS"
	else
		[ "${ORIG_TIMENS:-1}" != 0 ] && echo "original.sh: the time namespace could not be made; the park clock follows the uptime (TOOLING.md warning 1)" >&2
		nohup "$PLAY" >"$OLOGS/play.out" 2>&1 &
		echo "started on DISPLAY=$DISPLAY; logs in $OLOGS"
	fi
	;;
stop)
	WINEPREFIX="$G/.prefix/pfx" "$PROTON_DIR/files/bin/wineserver" -k 2>/dev/null || true
	if [ "${2:-}" = "--offscreen" ]; then   # also the private KWin and Xvfb on :77 (never the desktop's KWin)
		for p in $(pgrep -x kwin_x11); do tr '\0' '\n' < "/proc/$p/environ" 2>/dev/null | grep -qx 'DISPLAY=:77' && kill "$p"; done
		for p in $(pgrep -x Xvfb); do grep -q ':77' "/proc/$p/cmdline" && kill "$p"; done
	fi
	echo "stopped"
	;;
pid)
	for p in $(pgrep -f '\.exe'); do
		env_prefix="$(tr '\0' '\n' < "/proc/$p/environ" 2>/dev/null | sed -n 's/^STEAM_COMPAT_DATA_PATH=//p')"
		cmd="$(tr '\0' ' ' < "/proc/$p/cmdline" 2>/dev/null)"
		case "$cmd" in [A-Z]:\\TP-nodisc.exe*) [ "$env_prefix" = "$G/.prefix" ] && { echo "$p"; exit 0; } ;; esac
	done
	echo "not running" >&2; exit 1
	;;
*) sed -n '2,8p' "$0"; exit 1 ;;
esac
