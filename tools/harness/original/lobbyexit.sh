#!/usr/bin/env bash
# lobbyexit.sh <outdir> <tag> (Q749): the original's lobby eye after Exit To Lobby, polled and filmed.
# started off-screen (:77), the park entered from the lobby (660 58 twice), 30 s in, rendereye.py + record.sh started, Escape,
# Exit To Lobby (510 522), 25 s of lobby; autosave.TPWS taken out; stopped.
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; D=$1; T=${2:-a}; mkdir -p "$D"; J=~/Games/TPW-original/save/users/1ref/jungle
R=$O
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.0; }
shot() { import -window root -display :77 $D/$T-$1.png; }
align() { (cd "$O" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align >/dev/null 2>&1); }
if "$O/original.sh" pid >/dev/null 2>&1; then echo "the original is running: stop it first"; exit 1; fi
[ -e "$J/autosave.TPWS" ] && { echo "an autosave is already there: not this run's"; exit 1; }
find ~/Games/TPW-original/save -type f -exec md5sum {} \; | sort | md5sum > $D/$T-save-before.md5
"$O/original.sh" start --offscreen; sleep 45; align
PID=$("$O/original.sh" pid)
python3 -u $R/rendereye.py $PID 120 2 > $D/$T-eye.txt 2>&1 &
shot lobby-before
echo "== entering from the lobby $(date +%s.%N)"; at 660 58; click; click; sleep 30
align; at 900 600; sleep 0.5; shot park
"$O/record.sh" start lobbyexit$T 30
sleep 1
DISPLAY=:77 xdotool keydown Escape; sleep 0.15; DISPLAY=:77 xdotool keyup Escape; sleep 1.5; shot menu
echo "== Exit To Lobby click $(date +%s.%N)"; at 510 522; date +%s.%N > $D/$T-click.t; click; sleep 25
"$O/record.sh" stop
shot lobby-after
cp "$J/autosave.TPWS" "$D/$T-autosave.TPWS" || true
"$O/original.sh" stop --offscreen; sleep 3
rm -f "$J/autosave.TPWS"
find ~/Games/TPW-original/save -type f -exec md5sum {} \; | sort | md5sum > $D/$T-save-after.md5
echo "== done"; cat $D/$T-save-before.md5 $D/$T-save-after.md5
