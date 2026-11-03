#!/usr/bin/env bash
# loadtime.sh - a Load Park or a Restart Park timed in the ORIGINAL, off-screen (:77), by a film of the display with wall-clock frame stamps, the
# mouse press and release stamped by the driver, and mGameTick logged with wall stamps (tickwall.py). Q921. Analyse with lib/loadtime.py.
#   loadtime.sh load <file.TPWS> <outdir> [n]   starts the original through loadfile.sh (the file is the Load list's first row, entered once), then n timed
#                                               Loads of it by Escape, the menu's Load row (510,50) and the row (400,123): <outdir>/film.mkv, ticks.log,
#                                               press.txt (`k press release` in unix seconds), k-list.png, k-loaded.png
#   loadtime.sh restart <outdir> [n]            n timed Restart Parks of the original already running: Escape, the Restart Park row (510,228), the
#                                               question's tick (708,283); `rk press release` lines
#   Always end with `loadfile.sh stop` (it removes the file and says whether save/ is as it was). The click is held 0.1 s.
# OPENTPW_XVFB must name the Xvfb binary where it is not on PATH (original.sh starts the private display from it).
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; export OPENTPW_HARNESS_WORK="${OPENTPW_HARNESS_WORK:-$HOME/.cache/opentpw-harness}" SDL_AUDIODRIVER="${SDL_AUDIODRIVER:-dummy}"
key() { DISPLAY=:77 xdotool keydown $1; sleep 0.15; DISPLAY=:77 xdotool keyup $1; sleep 1.2; }
at() { (cd "$H" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
align() { (cd "$H" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align); }
shot() { import -window root -display :77 "$1"; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.2; }
timed_click() { # <outdir> <label>: press, 0.1 s, release; the two unix times to press.txt
	TD=$(date +%s.%N); DISPLAY=:77 xdotool mousedown 1; sleep 0.1; DISPLAY=:77 xdotool mouseup 1; TU=$(date +%s.%N)
	echo "$2 $TD $TU" >> "$1/press.txt"; echo "$2: press $TD release $TU"
}
film_and_ticks() { # <outdir>: both start now; stop with `film_stop`
	PID=$("$H/original.sh" pid); python3 -u "$H/tickwall.py" "$PID" 900 > "$1/ticks.log" 2>&1 & TP=$!
	ffmpeg -loglevel error -y -use_wallclock_as_timestamps 1 -f x11grab -framerate 30 -video_size 1024x768 -i :77 \
		-vf scale=512:-2 -fps_mode passthrough -copyts -c:v libx264 -preset ultrafast -crf 16 -pix_fmt yuv420p "$1/film.mkv" 2>"$1/ffmpeg.log" & FP=$!
	sleep 2
}
film_stop() { kill -INT $FP; wait $FP || true; kill $TP 2>/dev/null || true; echo "film and ticks done"; }
case "${1:-}" in
load)
	F="$2"; D="$3"; N="${4:-3}"; mkdir -p "$D"
	"$H/loadfile.sh" load "$F" "$D" start | tee "$D/start.out"
	film_and_ticks "$D"
	for k in $(seq 1 "$N"); do
		align; key Escape; at 510 50; click; shot "$D/$k-list.png"
		at 400 123; sleep 0.5; timed_click "$D" "$k"
		sleep 12; shot "$D/$k-loaded.png"
	done
	film_stop ;;
restart)
	D="$2"; N="${3:-2}"; mkdir -p "$D"
	film_and_ticks "$D"
	for k in $(seq 1 "$N"); do
		align; key Escape; at 510 228; click; shot "$D/r$k-question.png"
		at 708 283; sleep 0.5; timed_click "$D" "r$k"
		sleep 25; shot "$D/r$k-loaded.png"
	done
	film_stop ;;
*) sed -n '2,12p' "$0"; exit 1 ;;
esac
