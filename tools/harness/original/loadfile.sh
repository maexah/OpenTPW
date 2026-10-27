#!/usr/bin/env bash
# loadfile.sh - load a park file in the ORIGINAL, off-screen, from its Load Park list. It starts the original only when
# it is not running, so a second file costs the clicks alone (35 s against 123 s, measured 2026-10-10).
#   loadfile.sh load <file.TPWS> <outdir> <tag>   frames <outdir>/<tag>-1-load-list.png, -2-loaded.png (6 s on and an align sweep), -3-later.png (9 s after it)
#        POLL='python3 /path/cars.py %PID% 40'    a reader started half a second BEFORE the click; its output is
#                                                 <outdir>/<tag>-load.log (it runs on after this script returns)
#   loadfile.sh stop                              stops the original, takes the file out, and says whether save/ is as it was
#        (against logs/loadfile-before.*, written only by a load that STARTED the original: after a load into one already running, stop compares with an older run's). Needs xdotool and ImageMagick's import.
# The file goes into the reference player's jungle folder as LOADFILE.TPWS, the newest there, so it is the list's first
# row; the original also ENTERS the park on it when it starts. Never leave it there: always end with `stop`.
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; J=$OSAVE/users/1ref/jungle; S="$OLOGS/loadfile"
shot() { import -window root -display :77 "$1"; }
key() { DISPLAY=:77 xdotool keydown $1; sleep 0.15; DISPLAY=:77 xdotool keyup $1; sleep 1.2; }
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.2; }
align() { (cd "$O" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align); }
sum() { find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum; }
files() { find "$OSAVE" -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
case "${1:-}" in
load)
	F="$2"; D="$3"; TAG="$4"; mkdir -p "$D"
	if "$O/original.sh" pid >/dev/null 2>&1; then
		cp "$F" "$J/LOADFILE.TPWS"; touch "$J/LOADFILE.TPWS"; echo "the original is running: loading into it"
	else
		sum > "$S-before.md5"; files > "$S-before.txt"
		cp "$F" "$J/LOADFILE.TPWS"
		"$O/original.sh" start --offscreen; sleep 45
		align; at 660 58; click; click; sleep 28
	fi
	align; key Escape; at 510 50; click; sleep 1; shot "$D/$TAG-1-load-list.png"
	at 400 123
	if [ -n "${POLL:-}" ]; then ( bash -c "${POLL//%PID%/$("$O/original.sh" pid)}" > "$D/$TAG-load.log" 2>&1 & ); fi
	sleep 0.5; click
	sleep 6; align; at 900 600; sleep 0.5; shot "$D/$TAG-2-loaded.png"
	sleep 9; shot "$D/$TAG-3-later.png"
	echo loaded
	;;
stop)
	"$O/original.sh" stop --offscreen; sleep 3; rm -f "$J/LOADFILE.TPWS"
	sum > "$S-after.md5"; files > "$S-after.txt"
	diff "$S-before.txt" "$S-after.txt" >/dev/null && echo "save/ files the same" || { echo "save/ FILES DIFFER:"; diff "$S-before.txt" "$S-after.txt" || true; }
	cmp -s "$S-before.md5" "$S-after.md5" && echo "save/ sum the same" || echo "save/ SUM CHANGED"
	;;
*) sed -n '2,10p' "$0"; exit 1 ;;
esac
