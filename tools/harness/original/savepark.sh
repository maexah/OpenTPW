#!/usr/bin/env bash
# savepark.sh <NAME> <dest.TPWS> - the RUNNING original (off-screen, :77) saves its park from its own menu: Escape, Save
# (510,128), NAME typed over the box's "New Save", the tick (850,383); then the file is MOVED out of the reference
# player's jungle folder to <dest.TPWS>, so the install is left as it was (gms.dat's time changes, its bytes do not).
# Points are on the 1024 x 768 frame (gmove.py). The menu pauses the game; the save leaves it running. Frames:
# <dest>-typed.png and <dest>-after.png. Found by trial for Q523 (q523/orig, 2026-10-19). A NAME already there is refused.
# PLAYER=2fsfresh saves from freshfs.sh's player instead of 1ref (Q709; the save rewrites that player's gms.dat first).
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; J=$OSAVE/users/${PLAYER:-1ref}/jungle; N="$1"; DEST="$2"
[ -e "$J/$N.TPWS" ] && { echo "$J/$N.TPWS is there already: refused"; exit 1; }
"$O/original.sh" pid >/dev/null || { echo "the original is not running"; exit 1; }
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.2; }
DISPLAY=:77 xdotool keydown Escape; sleep 0.15; DISPLAY=:77 xdotool keyup Escape; sleep 1.5
at 510 128; click; sleep 1; DISPLAY=:77 xdotool type --delay 120 "$N"; sleep 0.8
import -window root -display :77 "${DEST%.TPWS}-typed.png"
at 850 383; click; sleep 3; import -window root -display :77 "${DEST%.TPWS}-after.png"
for i in $(seq 1 20); do [ -s "$J/$N.TPWS" ] && break; sleep 0.5; done
[ -s "$J/$N.TPWS" ] || { echo "no $N.TPWS written"; exit 1; }
sleep 1; mv "$J/$N.TPWS" "$DEST"; echo "saved: $DEST ($(stat -c %s "$DEST") bytes)"
