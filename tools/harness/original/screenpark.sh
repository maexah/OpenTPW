#!/usr/bin/env bash
# screenpark.sh FILE OUTDIR TAG : the original on the REAL screen (rule 128) entering a park file: the file put in the reference
# player's jungle folder as the newest (the player's click enters the park on it), the welcome clicked past, the
# player clicked twice 3 s apart (the second lands after the island flight; at 1024 x 768 a 1.2 s gap fell in the flight, Q377), frames OUTDIR/TAG-1..3 (and -N-full at 1920x1440) from 35 s on, the pointer mid-screen (an edge scrolls the camera); then stopped, the file taken out, save/ checked.
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; F=$1; D=$2; T=$3; mkdir -p "$D"
J=$OSAVE/users/1ref/jungle
sum() { find "$OSAVE" -type f -exec md5sum {} \; | sort | md5sum; }
files() { find "$OSAVE" -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
shot() { import -window root -display :0 "$D/full-$T.png"
	python3 -c "
from PIL import Image; im=Image.open('$D/full-$T.png').convert('RGB'); w,h=im.size; gw=h*4//3; x0=(w-gw)//2
im.crop((x0,0,x0+gw,h)).save('$D/$T-$1-full.png'); im.crop((x0,0,x0+gw,h)).resize((1024,768)).save('$D/$T-$1.png')"; rm -f "$D/full-$T.png"; }
gm() { (cd $O && GDISPLAY=:0 python3 gmove.py "$@" >/dev/null 2>&1); }
click() { DISPLAY=:0 xdotool mousedown 1; sleep 0.2; DISPLAY=:0 xdotool mouseup 1; sleep 1.2; }
sum > $D/$T-save-before.md5; files > $D/$T-files-before.txt
cp "$F" "$J/LOADFILE.TPWS"
DISPLAY=:0 $O/original.sh start; sleep 40
gm align; gm 2540 1420; click; sleep 12
gm align; gm 1558 108; click; sleep 3; click; sleep 35
gm 2007 1125; sleep 1; shot 1; sleep 3; shot 2; sleep 5; shot 3
$O/original.sh stop; sleep 3; rm -f "$J/LOADFILE.TPWS"
sum > $D/$T-save-after.md5; files > $D/$T-files-after.txt
diff $D/$T-files-before.txt $D/$T-files-after.txt && echo "save/ files the same" || echo "save/ FILES DIFFER"
cmp -s $D/$T-save-before.md5 $D/$T-save-after.md5 && echo "save/ sum the same" || echo "save/ SUM CHANGED"
