#!/usr/bin/env bash
# spikecut.sh <outdir> [X,Z [ZOOM [ROT]]] - which diagonal a ground cell's 0x2 flag draws, by a feature that crosses it (Q914).
# Starts the original off-screen on a park file (loadfile.sh), holds the camera (setcam.py, follow off) and the clock (memhold.py
# 0x007859a0), then takes frames: A1 A2 (pristine), B1 B2 (the two off-diagonal corners of CELL raised by RISE units, held: groundheight.py),
# C1 C2 (the same, CELL's 0x2 set: groundheight.py's partner groundcell.py), D1 (0x2 cleared again), E1 (the corners put back), then stops
# the original. `spikecut.py <outdir>` counts and measures them.
#   FILE=<park file> (default q325's stock park)  CELL=4,16 (a flat cell, flag 0x0004, in the frame at 50,150 zoom 70 rot 0)
#   RISE=15  CORNERS="5,16 4,17" (the vertices raised: for CELL x,z the pair (x+1,z) and (x,z+1) lies on the cut 0x2 clear draws).
# Needs the same environment as flipdiag.sh.
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$O/../lib/paths.sh"
D=$1; CAM=${2:-50,150}; ZOOM=${3:-70}; ROT=${4:-0}; mkdir -p "$D"
FILE=${FILE:-$WORK/q325/stock.TPWS}; CELL=${CELL:-4,16}; RISE=${RISE:-15}; CORNERS=${CORNERS:-"5,16 4,17"}
PY=${PY:-python3}
HOLD=; HH=
cleanup() { [ -n "$HOLD" ] && kill "$HOLD" 2>/dev/null; [ -n "$HH" ] && kill "$HH" 2>/dev/null; "$O/loadfile.sh" stop >/dev/null 2>&1; rm -f "$OSAVE/users/1ref/jungle/LOADFILE.TPWS"; echo "spikecut.sh failed: stopped the original and removed LOADFILE.TPWS" >&2; }
trap cleanup ERR INT TERM
shot() { import -window root -display :77 -crop 1024x768+0+0 "$D/$1.png"; }
find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum > "$D/save-before.md5"
"$O/loadfile.sh" load "$FILE" "$D" load > "$D/load.out" 2>&1
sleep 8
P=$("$O/original.sh" pid); echo "pid $P" | tee "$D/run.log"
$PY -u "$O/setcam.py" "$P" "$CAM" "$ZOOM" "$ROT" off 2>&1 | tee -a "$D/run.log"
$PY -u "$O/memhold.py" "$P" 90 0x007859a0:I=1 > "$D/hold.log" 2>&1 &
HOLD=$!
sleep 3
$PY -u "$O/setcam.py" "$P" "$CAM" "$ZOOM" "$ROT" off 2>&1 | tee -a "$D/run.log"
READ=3,15:6,17 $PY -u "$O/groundcell.py" "$P" 0.1 > "$D/before.log" 2>&1 || true
sleep 1; shot A1; sleep 2; shot A2
args=(); for c in $CORNERS; do args+=("$c=+$RISE"); done
$PY -u "$O/groundheight.py" "$P" 60 "${args[@]}" > "$D/height.log" 2>&1 &
HH=$!
sleep 4; shot B1; sleep 2; shot B2
$PY -u "$O/groundcell.py" "$P" 6 "$CELL=+0x2" > "$D/poke.log" 2>&1 &
PK=$!
sleep 3; shot C1; sleep 2; shot C2
wait $PK || true
$PY -u "$O/groundcell.py" "$P" 5 "$CELL=-0x2" > "$D/unpoke.log" 2>&1 &
PK=$!
sleep 3; shot D1
wait $PK || true
kill "$HH" 2>/dev/null || true; HH=; sleep 3; shot E1
READ=3,15:6,17 $PY -u "$O/groundcell.py" "$P" 0.1 > "$D/after.log" 2>&1 || true
trap - ERR INT TERM
kill "$HOLD" 2>/dev/null || true; sleep 1
"$O/loadfile.sh" stop 2>&1 | tee "$D/stop.out"
find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum > "$D/save-after.md5"
cmp -s "$D/save-before.md5" "$D/save-after.md5" && echo "save/ the same" || echo "save/ DIFFERS"
