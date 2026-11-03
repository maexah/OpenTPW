#!/usr/bin/env bash
# loadtime_clicks.sh <outdir> [CROP] : loadtime.py on every click of a loadtime_ours.py run (Q924): its film.mkv and press.txt (`k press release`,
# one row a Load or a Restart), each click read to half a second before the next one's press (the last to nine seconds after its release).
# CROP is the film's own pixels, w:h:x:y of the game's picture (default 640:360:0:0, the desk's window as loadtime_ours.py films it; the off-screen
# display's is 512:288:0:0). Prints each click's last lines: the first frame of the loaded park after the release, and the frames of another picture before it.
D=${1:?outdir}; CROP=${2:-640:360:0:0}; L=$(dirname "$(readlink -f "$0")")/loadtime.py
PY=${OPENTPW_HARNESS_PYTHON:-python3}
mapfile -t rows < "$D/press.txt"
for i in "${!rows[@]}"; do
	read -r k p r <<< "${rows[$i]}"
	if [ $((i+1)) -lt ${#rows[@]} ]; then read -r _ p2 _ <<< "${rows[$((i+1))]}"; end=$(python3 -c "print($p2-0.5)"); else end=$(python3 -c "print($r+9)"); fi
	echo "== $k"; PARKREF=4 $PY -W ignore "$L" "$D/film.mkv" "$p" "$r" "$CROP" "$end" 2>&1 | tail -8
done
