#!/usr/bin/env bash
# lobbystorm.sh (Q1037): the original's lobby bolt on the island the picked player stands at. lobbypanel.sh's start (Full Simulation player 2fsfresh,
# welcome click, slot click), then lobbystrike.py holds the nearest island's lightning mask at 63, boltpoll.py reads the bolt records, ffmpeg films
# the desktop's 4:3 picture (1920 x 1440 at x 320, scaled to 1024 x 768) at 30 fps. Usage: run.sh OUTDIR [FILMSECS]
set -euo pipefail
export OPENTPW_HARNESS_WORK=$HOME/.cache/tpw-harnesses OPENTPW_FULLSIM_PLAYER=$HOME/repos/game/saves-fullsim/save/users/1FullSimCheated
R=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd); O=$R/original; . $R/lib/paths.sh; D=$1; FS=${2:-26}; mkdir -p "$D"
S="$OSAVE"; U=$S/users/2fsfresh; SRC="$OPENTPW_FULLSIM_PLAYER"
sum() { find $S -type f -exec md5sum {} \; | sort | md5sum; }
files() { find $S -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
gm() { (cd $O && GDISPLAY=:0 python3 gmove.py "$@" >/dev/null 2>&1); }
click() { DISPLAY=:0 xdotool mousedown 1; sleep 0.2; DISPLAY=:0 xdotool mouseup 1; sleep 0.5; }
"$O/original.sh" pid >/dev/null 2>&1 && { echo "the original is running: stop it first"; exit 1; }
[ -e "$U" ] && { echo "$U is there already"; exit 1; }
sum > $D/save-before.md5; files > $D/files-before.txt
mkdir -p $U/jungle $U/fantasy $U/hallow $U/space $U/outbox
cp "$SRC/gms.dat" $U/gms.dat; cp "$SRC/addrbook.dat" $U/; chmod u+w $U/*.dat
DISPLAY=:0 $O/original.sh start; sleep "${START_WAIT:-40}"
gm align; gm 2540 1420; click; sleep 12
gm align; gm 1558 296; click; echo "slot click $(date +%T)"
gm 1280 1000
sleep 8
PID=$("$O/original.sh" pid)
python3 -u $O/lobbystrike.py $PID $((FS+6)) 63 > $D/strike.log 2>&1 & SP=$!
python3 -u $O/boltpoll.py $PID $((FS+8)) > $D/bolt.log 2>&1 & BP=$!
sleep 3
date +%s.%N > $D/film.t0
ffmpeg -loglevel error -y -f x11grab -draw_mouse 0 -framerate 30 -video_size 1920x1440 -i :0.0+320,0 -t $FS -vf scale=1024:768 \
	-c:v libx264 -preset ultrafast -qp 0 -pix_fmt yuv444p "$D/film.mkv"
import -window root -display :0 "$D/end-full.png" || true
wait $SP || true; wait $BP || true
$O/original.sh stop; sleep 3; rm -rf "$U"
sum > $D/save-after.md5; files > $D/files-after.txt
diff $D/files-before.txt $D/files-after.txt && echo "save/ files the same" || echo "save/ FILES DIFFER"
cmp -s $D/save-before.md5 $D/save-after.md5 && echo "save/ sum the same" || echo "save/ SUM CHANGED"
