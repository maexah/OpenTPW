#!/usr/bin/env bash
# pickscan.sh PARK.TPWS OUTDIR TAG "X,Z ROT;X,Z ROT;..." - the original's pick point P over a grid of pointer pixels at park
# cameras (Q824): loads the park off-screen (loadfile.sh), and for each camera i writes the camera with setcam.py (zoom 110,
# follow off), reads the eye [0x00790aa0] [0x00790aa4] [0x00790aa8] in full, takes a frame, and runs hoverscan.py ... pick
# exact over GRID (default 220,1000,60,120,680,60: 14 x 10 points, off the gadget and the money). Writes OUTDIR/TAG-run.out
# (`== camera i X,Z ROT eye X H Z`), TAG-scan<i>.log and TAG-cam<i>.png: the inputs of lib/pickrays.py. STOP=0 leaves the
# original running. Needs the display :77's tools (xdotool, ImageMagick's import) as loadfile.sh does.
set -uo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; PARK=$1; D=$2; T=$3; CAMS=$4; GRID=${GRID:-220,1000,60,120,680,60}
export DISPLAY=:77; mkdir -p "$D"
"$O/loadfile.sh" load "$PARK" "$D" "$T"
sleep 2
P=$("$O/original.sh" pid); echo "== pid $P"
rd() { python3 "$O/tpwmem.py" --pid $P read "$@" 2>/dev/null | awk '{print $NF}' | tr '\n' ' '; }
i=0
IFS=';'; for C in $CAMS; do
	i=$((i+1)); IFS=' '; set -- $C
	python3 "$O/setcam.py" $P $1 110 $2 off | tail -2; sleep 2
	echo "== camera $i $C eye $(rd 0x00790aa0:f 0x00790aa4:f 0x00790aa8:f)"
	import -window root -display :77 "$D/$T-cam$i.png"
	python3 "$O/hoverscan.py" $P 99 any pick exact grid $GRID > "$D/$T-scan$i.log" 2>&1
	echo "== scan $i: $(grep -c ' met 0' "$D/$T-scan$i.log") ground points of $(grep -c '^(' "$D/$T-scan$i.log")"
done
[ "${STOP:-1}" = 1 ] && "$O/loadfile.sh" stop
echo "== done"
