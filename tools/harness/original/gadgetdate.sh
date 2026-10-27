#!/usr/bin/env bash
# gadgetdate.sh OUTDIR TAG TICK... : the original, off-screen, enters Lost Kingdom from the lobby (the reference player's
# newest jungle file; the stock easymode.TPWI enters on tick 755) and photographs the frame as mGameTick first reaches
# each TICK given (ascending), the tick read just before and just after each grab (printed "shot TAG-TICK tick A..B"),
# with the gadget cropped to TAG-TICK-gadget.png (its date box is (80,530)-(200,560) of the 1024 x 768 frame).
# A tick.py log runs beside it (TAG-tick.log). Nothing is saved: the original is stopped by wineserver -k, and save/'s
# sum is taken either side. Q149 (q149/orig/run.sh is its first form).
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; D=$1; T=$2; shift 2; mkdir -p "$D"
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.0; }
align() { (cd "$O" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align >/dev/null 2>&1); }
tk() { python3 -c "
import struct
f=open('/proc/$PID/mem','rb',buffering=0)
def rd(a): f.seek(a); return struct.unpack('<I',f.read(4))[0]
print(rd(rd(0x0080239c)+0x1da70c))"; }
if "$O/original.sh" pid >/dev/null 2>&1; then echo "the original is running: stop it first"; exit 1; fi
sum() { find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum; }
sum > "$D/$T-save-before.md5"
"$O/original.sh" start --offscreen; sleep 45; align
PID=$("$O/original.sh" pid)
( python3 -u $O/tick.py $PID 900 > "$D/$T-tick.log" 2>&1 & )
echo "== entering from the lobby $(date +%s.%N)"; at 660 58; click; click
# The pointer mid-frame during the load (an edge scrolls the camera): aligned after the park is up, it took over ten
# seconds and the first ticks went by unphotographed (q149/orig/b).
sleep 4; align; at 900 600
until [ "$(tk)" -gt 0 ]; do sleep 0.2; done
for want in "$@"; do
	until [ "$(tk)" -ge "$want" ]; do sleep 0.1; done
	a=$(tk); import -window root -display :77 "$D/$T-$want.png"; b=$(tk); echo "shot $T-$want tick $a..$b"
	python3 -c "from PIL import Image; Image.open('$D/$T-$want.png').crop((0,384,300,768)).save('$D/$T-$want-gadget.png')"
done
"$O/original.sh" stop --offscreen; sleep 3
sum > "$D/$T-save-after.md5"
cmp -s "$D/$T-save-before.md5" "$D/$T-save-after.md5" && echo "save/ sum the same" || echo "save/ SUM CHANGED"
