#!/usr/bin/env bash
# flipdiag.sh <outdir> [X,Z [ZOOM [ROT]]] - the original's picture of a ground cell's diagonal flag flipped (Q837).
# Starts the original off-screen on a park file (loadfile.sh), holds the camera (setcam.py, follow off) and the clock
# (memhold.py 0x007859a0, a frame keeps drawing), then takes frames A1 A2 (nothing changed: the control), pokes each
# cell of CELLS from FROM to TO in the live heightfield (groundcell.py, held) and takes B1 B2, puts the bits back and
# takes C1 C2, and stops the original. `flipdiag.py <outdir>` then counts the pixels that differ between the frames.
#   FILE=<park file> (default q325's stock park; its cells (3,13),(4,14),(5,15),(6,16) read 0x0804 and are the only bent ones in view
#   at 50,150 zoom 70 rot 0, the defaults)  CELLS="3,13 4,14 5,15 6,16"  FROM=0x4  TO=0x2
# Needs OPENTPW_HARNESS_WORK, OPENTPW_XVFB (loadfile.sh's) and a python with Xlib (the review-codex venv: lib/paths.py).
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$O/../lib/paths.sh"
D=$1; CAM=${2:-50,150}; ZOOM=${3:-70}; ROT=${4:-0}; mkdir -p "$D"
FILE=${FILE:-$WORK/q325/stock.TPWS}; CELLS=${CELLS:-"3,13 4,14 5,15 6,16"}; FROM=${FROM:-0x4}; TO=${TO:-0x2}
PY=${PY:-python3}
shot() { import -window root -display :77 -crop 1024x768+0+0 "$D/$1.png"; }
poke() { local a=(); for c in $CELLS; do a+=("$c=-$1" "$c=+$2"); done; echo "${a[@]}"; }
find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum > "$D/save-before.md5"
"$O/loadfile.sh" load "$FILE" "$D" load > "$D/load.out" 2>&1
sleep 8
P=$("$O/original.sh" pid); echo "pid $P" | tee "$D/run.log"
$PY -u "$O/setcam.py" "$P" "$CAM" "$ZOOM" "$ROT" off 2>&1 | tee -a "$D/run.log"
$PY -u "$O/memhold.py" "$P" 60 0x007859a0:I=1 > "$D/hold.log" 2>&1 &
HOLD=$!
sleep 3
$PY -u "$O/setcam.py" "$P" "$CAM" "$ZOOM" "$ROT" off 2>&1 | tee -a "$D/run.log"
READ=2,12:7,17 $PY -u "$O/groundcell.py" "$P" 0.1 > "$D/before.log" 2>&1 || true
sleep 1; shot A1; sleep 2; shot A2
$PY -u "$O/groundcell.py" "$P" 7 $(poke "$FROM" "$TO") > "$D/poke.log" 2>&1 &
PK=$!
sleep 3; shot B1; sleep 2; shot B2
wait $PK || true
$PY -u "$O/groundcell.py" "$P" 5 $(poke "$TO" "$FROM") > "$D/unpoke.log" 2>&1 &
PK=$!
sleep 3; shot C1; sleep 1; shot C2
wait $PK || true
READ=2,12:7,17 $PY -u "$O/groundcell.py" "$P" 0.1 > "$D/after.log" 2>&1 || true
$PY -u "$O/camera.py" "$P" 2>&1 | sed -n 1,12p | tee -a "$D/run.log"
kill "$HOLD" 2>/dev/null || true; sleep 1
"$O/loadfile.sh" stop 2>&1 | tee "$D/stop.out"
find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum > "$D/save-after.md5"
cmp -s "$D/save-before.md5" "$D/save-after.md5" && echo "save/ the same" || echo "save/ DIFFERS"
