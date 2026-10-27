#!/usr/bin/env bash
# screenchains.sh (Q559): the original on the REAL screen entering its reference player's newest jungle file (easymode.TPWI, the stock
# park; screenpark.sh's clicks, no file put in), chains.py and mixer.py from the moment its pid exists, so the gates'
# held chain 177 is seen come; the camera put on the gates (setcam 450,160) and three frames while it lives (each
# stamped against chains.py's clock); then stopped and save/ checked. TAG=a. BRK=ADDR (BRK_PRINT, BRK_HITS) runs brk.sh
# in the lobby (rounds 4, 5). FILE=<park file> is entered instead of the stock park (round 5); CAM=X,Z the camera's point,
# ROT= its rotation in degrees (0). EXTRA="<reader> [args];..." runs more readers beside chains.py, each given PID SECS
# before its args, into $T-<reader>.log; LATE= the same, started in the park beside mixer.py with the seconds left
# (Q561: EXTRA="musicvoices.py 6" LATE="mixfreq.py"). SHOTS="t1 t2 ..." takes the frames at those seconds after the pid
# instead of three 4 s apart (Q561: the bus at its stops, SHOTS="189 192 199").
set -uo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; D=${OUT:?OUT=<folder> is needed}; T=${TAG:-a}; SECS=${SECS:-150}; mkdir -p "$D"
sum() { find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum; }
files() { find "$OSAVE" -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
shot() { import -window root -display :0 "$D/full-$T.png"
	python3 -c "
from PIL import Image; im=Image.open('$D/full-$T.png').convert('RGB'); w,h=im.size; gw=h*4//3; x0=(w-gw)//2
im.crop((x0,0,x0+gw,h)).resize((1024,768)).save('$D/$T-$1.png')"; rm -f "$D/full-$T.png"; echo "== shot $1 +$(python3 -c "import time; print(int((time.time()-$CH0)*1000))") ms"; }
gm() { (cd $O && GDISPLAY=:0 python3 gmove.py "$@" >/dev/null 2>&1); }
click() { DISPLAY=:0 xdotool mousedown 1; sleep 0.2; DISPLAY=:0 xdotool mouseup 1; sleep 1.2; }
sum > $D/$T-save-before.md5; files > $D/$T-files-before.txt
J=$OSAVE/users/1ref/jungle
if [ -n "${FILE:-}" ]; then cp "$FILE" "$J/LOADFILE.TPWS"; fi   # the newest file, which the player's click enters
DISPLAY=:0 $O/original.sh start
for i in $(seq 60); do P=$("$O/original.sh" pid 2>/dev/null) && break; sleep 0.5; done
echo "== pid $P $(date +%T.%N | cut -c1-12)"
CH0=$(date +%s.%N)
( cd "$O" && timeout $((SECS+10)) python3 -u chains.py $P $SECS > "$D/$T-chains.log" 2>&1 & )
readers() { local secs=$1 r; IFS=';' read -ra RS <<< "$2"; for r in "${RS[@]}"; do set -- $r; [ $# -gt 0 ] || continue; local X=$1; shift
	( cd "$O" && timeout $((secs+10)) python3 -u $X $P $secs "$@" > "$D/$T-${X%.py}.log" 2>&1 & ); done; }
readers $SECS "${EXTRA:-}"
sleep ${LOBBY_WAIT:-40}   # LOBBY_WAIT= more seconds before the first click when the lobby comes up slowly (Q560 run a: 40 was too few)
gm align; gm 2540 1420; click; sleep 12
# Set breakpoints in the lobby, as run g did: run d's 0x006bdb1d could not hit (no fresh chain branched) .
if [ -n "${BRK:-}" ]; then ( cd "$O" && BRK_PRINT="$BRK_PRINT" ./brk.sh $BRK ${BRK_HITS:-4} $((SECS - 70)) 2 > "$D/$T-brk.log" 2>&1 & ); sleep 3; fi
# The player's row, then the island (game point 500,470) clicked again until the park's clock moves.
gm align; gm 1558 108; click; sleep 10
for k in 1 2 3 4 5 6; do
	gm align; gm 1257 881; click; sleep 12
	grep -q "effect ${WAITFOR:-177} (0x0404)" "$D/$T-chains.log" && { echo "== in the park after click $k +$(python3 -c "import time; print(int((time.time()-$CH0)*1000))") ms"; break; }
done
sleep 15
( cd "$O" && timeout 70 python3 -u mixer.py $P 60 > "$D/$T-mixer.log" 2>&1 & )
readers $((SECS - $(python3 -c "import time; print(int(time.time()-$CH0))"))) "${LATE:-}"
gm 2007 1125; (cd "$O" && python3 setcam.py $P ${CAM:-450,160} 90 ${ROT:-0} off) 2>&1 | tail -3
python3 $O/tpwmem.py --pid $P clock 2>&1 | tail -2
if [ -n "${SHOTS:-}" ]; then k=0; for at in $SHOTS; do k=$((k+1))   # the shots at these seconds after the pid (Q561)
	while [ $(python3 -c "import time; print(int(time.time()-$CH0))") -lt $at ]; do sleep 0.5; done; shot $k; done
else sleep 3; shot 1; sleep 4; shot 2; sleep 4; shot 3; fi
while [ $(python3 -c "import time; print(int(time.time()-$CH0))") -lt $((SECS + 2)) ]; do sleep 2; done
if [ -n "${KEEP:-}" ]; then echo "== kept running: stop it with original.sh stop, then compare save/ by hand"; exit 0; fi
$O/original.sh stop; sleep 3; rm -f "$J/LOADFILE.TPWS"
sum > $D/$T-save-after.md5; files > $D/$T-files-after.txt
diff $D/$T-files-before.txt $D/$T-files-after.txt && echo "save/ files the same" || echo "save/ FILES DIFFER"
cmp -s $D/$T-save-before.md5 $D/$T-save-after.md5 && echo "save/ sum the same" || echo "save/ SUM CHANGED"
echo "== done"
