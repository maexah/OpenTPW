#!/usr/bin/env bash
# queuerefund.sh <park file> <outdir> - Q1052 in the ORIGINAL: the file `lib/queuerefund.py F` saves (Mumbo just bought at (41,22), its queue node (42,21) unjoined, so no customer)
# loaded by loadfile.sh, the ride's window read (Age, Users last month, Scrap value), then the node edited, a 2-cell run laid north to (42,19) and taken up by one Backspace,
# the balance traced every 0.1 s by queuecells.py. Frames and trace.txt in <outdir>; `finished` is written last. Needs OPENTPW_HARNESS_WORK and OPENTPW_XVFB (docs/TOOLING.md, "The queue tool's clicks and Backspace"),
# a Full original running or not; end with `loadfile.sh stop`. The pixels are `camera.py PID --aim X,Y`'s on setcam.py 425,200 110 0 off (42,21) 511,294, (42,19) 511,409; (42,18) lies under a building.
FILE=$1; export D=$2; mkdir -p $D
O="$(cd "$(dirname "$0")" && pwd)"
PY=${OPENTPW_EMU_PYTHON_VENV:-$HOME/.cache/tpw-harnesses/review-codex/venv/bin/python}
pid() { $O/original.sh pid; }
shot() { import -window root -display :77 -crop 1024x768+0+0 "$D/$1.png"; echo "shot $1"; }
at() { (cd $O && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.2; }
key() { DISPLAY=:77 xdotool keydown $1; sleep 0.15; DISPLAY=:77 xdotool keyup $1; sleep 1.2; }
$O/loadfile.sh load $FILE $D c > $D/load.out 2>&1
P=$(pid)
(cd $O && $PY -u setcam.py $P 425,200 110 0 off > $D/setcam.out 2>&1); sleep 2.5
at 540 150; click; sleep 1; shot 09-window        # the ride's window: Age, Users, Scrap value
at 855 430; click; shot 10-closed
CELLS="42,21 42,20 42,19 42,18"
(cd $O && $PY -u queuecells.py $P 24 $CELLS > $D/trace.txt 2>&1 &)
sleep 1.5
shot 11-before
at 511 294; click; shot 12-edit
at 511 409; click; shot 13-run
key BackSpace; shot 14-backspace
sleep 1; shot 15-after
sleep 6
echo done > $D/finished
