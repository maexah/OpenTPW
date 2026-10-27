#!/usr/bin/env bash
# lobbypanel.sh OUTDIR (Q608): the original on the REAL screen (rule 128) at the lobby's island panel for a Full Simulation player
# (a copy of Alexah's 1FullSimCheated gms.dat, keys 2, as player 2fsfresh, the list's second row): the welcome clicked past,
# the slot clicked, frames every 2 s for 16 s (a-NN: the flight, then the panel from about a-04); then the same point
# clicked again, which lands on the lobby's view and enters the park, frames every 1.5 s for 15 s (b-NN). Then stopped,
# the player folder removed, save/ checked. Frames OUTDIR/a-NN.png and b-NN.png (1024 x 768) and -full (1920 x 1440).
# FILM=SECS (Q630) films the game's 4:3 picture from about 2 s before the entering click (2.06 s measured) for SECS, 30 fps, scaled to 1024 x 768,
# to OUTDIR/entry.mkv (the click's wall clock in OUTDIR/click.t, the film's start in entry.t0), in place of the b-NN stills.
# START_WAIT=SECS (Q670) waits that long after the start before the welcome click, default 40 (the intro once outlasted 40).
# POLL='cmd %PID%' runs beside it from about 4 s before the click (2 s before the film starts) to the end, its output in OUTDIR/poll.txt (emitters.py for
# the key puff, effect 98).
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; D=$1; mkdir -p "$D"
S="$OSAVE"; U=$S/users/2fsfresh; SRC="${OPENTPW_FULLSIM_PLAYER:?set OPENTPW_FULLSIM_PLAYER (lib/paths.py)}"
sum() { find $S -type f -exec md5sum {} \; | sort | md5sum; }
files() { find $S -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
shot() { import -window root -display :0 "$D/full-tmp.png"
	python3 -c "
from PIL import Image; im=Image.open('$D/full-tmp.png').convert('RGB'); w,h=im.size; gw=h*4//3; x0=(w-gw)//2
im.crop((x0,0,x0+gw,h)).save('$D/$1-full.png'); im.crop((x0,0,x0+gw,h)).resize((1024,768)).save('$D/$1.png')"; rm -f "$D/full-tmp.png"; }
gm() { (cd $O && GDISPLAY=:0 python3 gmove.py "$@" >/dev/null 2>&1); }
click() { DISPLAY=:0 xdotool mousedown 1; sleep 0.2; DISPLAY=:0 xdotool mouseup 1; sleep 0.5; }
"$O/original.sh" pid >/dev/null 2>&1 && { echo "the original is running: stop it first"; exit 1; }
[ -e "$U" ] && { echo "$U is there already"; exit 1; }
sum > $D/save-before.md5; files > $D/files-before.txt
mkdir -p $U/jungle $U/fantasy $U/hallow $U/space $U/outbox
cp "$SRC/gms.dat" $U/gms.dat; cp "$SRC/addrbook.dat" $U/; chmod u+w $U/*.dat
DISPLAY=:0 $O/original.sh start; sleep "${START_WAIT:-40}"
gm align; gm 2540 1420; click; sleep 12
gm align; gm 1558 296; click; echo "slot click 1 $(date +%T)"
gm 1280 1000
for n in 01 02 03 04 05 06 07 08; do sleep 2; shot a-$n; done
PID=$("$O/original.sh" pid 2>/dev/null || true)
if [ -n "${POLL:-}" ]; then ( eval "${POLL//%PID%/$PID}" ) > "$D/poll.txt" 2>&1 & PP=$!; sleep 2; fi
if [ -n "${FILM:-}" ]; then
	date +%s.%N > "$D/entry.t0"
	ffmpeg -loglevel error -y -f x11grab -draw_mouse 1 -framerate 30 -video_size 1920x1440 -i :0.0+320,0 -t "$FILM" \
		-vf scale=1024:768 -c:v libx264 -preset ultrafast -qp 0 -pix_fmt yuv444p "$D/entry.mkv" & FP=$!; sleep 1
fi
gm 1558 296; date +%s.%N > "$D/click.t"; click; echo "slot click 2 $(date +%T)"
gm 1280 1000
if [ -n "${FILM:-}" ]; then wait $FP; else for n in 01 02 03 04 05 06 07 08 09 10; do sleep 1.5; shot b-$n; done; fi
[ -n "${PP:-}" ] && { kill $PP 2>/dev/null || true; wait $PP 2>/dev/null || true; }
$O/original.sh stop; sleep 3; rm -rf "$U"
sum > $D/save-after.md5; files > $D/files-after.txt
diff $D/files-before.txt $D/files-after.txt && echo "save/ files the same" || echo "save/ FILES DIFFER"
cmp -s $D/save-before.md5 $D/save-after.md5 && echo "save/ sum the same" || echo "save/ SUM CHANGED"
