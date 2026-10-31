#!/usr/bin/env bash
# markerup.sh <outdir> <cellX> <cellY> - the original with a Belly Bounce carried and the pointer on a cell, left running (Q838).
# loadfile.sh loads FILE (default q325's stock park); Q633's route puts a Belly Bounce in the hand (setcam.py to 470,270 zoom 125 rot 180,
# Ctrl+click on the stock one at (51,23)); then the pointer is centred (at an edge it scrolls the camera), the camera is set over the
# cell (CELLX*10, CELLY*10, zoom 125, rot 180, follow off), cellfind.py finds its pixel and the pointer goes there. Prints the
# hover cell [0x008186b8],[0x008186b4], the tool [0x0081ae2c] (4) and the cursor [0x007cb2d8] (7 or 8), and takes <outdir>/aimed.png.
# Then markercorners.py PID reads the squares (memhold.py PID SECS 0x007859a0:I=1 first); `loadfile.sh stop` ends it. Only
# <outdir>/save-before.md5 is written here: after `stop`, md5 save/ the same way and compare it yourself. Needs OPENTPW_HARNESS_WORK,
# OPENTPW_XVFB (loadfile.sh's) and a python with Xlib (docs/TOOLING.md, "A harness run needs a python with Xlib and PIL").
set -uo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$O/../lib/paths.sh"
D=$1; CX=$2; CY=$3; mkdir -p "$D"; export GDISPLAY=:77
FILE=${FILE:-$WORK/q325/stock.TPWS}; PY=${PY:-python3}
at() { (cd "$O" && $PY gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.5; }
val() { $PY "$O/tpwmem.py" read "$1" 2>/dev/null | awk '{print $2}' | tail -1; }
find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum > "$D/save-before.md5"
"$O/loadfile.sh" load "$FILE" "$D" load > "$D/load.out" 2>&1; sleep 2
P=$("$O/original.sh" pid); echo "pid $P"
$PY -u "$O/setcam.py" "$P" 470,270 125 180 off | tail -1; sleep 2
xy=$(PICK=1 $PY "$O/cellfind.py" "$P" 51,23 2>>"$D/cellfind.log") && at $xy
DISPLAY=:77 xdotool keydown Control_L; sleep 0.3; click; DISPLAY=:77 xdotool keyup Control_L; sleep 0.5
at 512 384
$PY -u "$O/setcam.py" "$P" $((CX * 10)),$((CY * 10)) 125 180 off | tail -1; sleep 2
xy=$(PICK=1 $PY "$O/cellfind.py" "$P" "$CX,$CY" 2>>"$D/cellfind.log") && echo "pixel $xy" && at $xy; sleep 1
echo "hover $(val 0x008186b8:i),$(val 0x008186b4:i) tool $(val 0x0081ae2c) item $(val 0x008186e0) cursor $(val 0x007cb2d8:H)"
import -window root -display :77 -crop 1024x768+0+0 "$D/aimed.png"
