#!/usr/bin/env bash
# fsload.sh <file> <outdir> - start the original on a fresh Full Simulation player (freshfs.sh make), load <file>
# from that player's own Load list (Escape, Load, row 1), advisor.py from just before the click, a frame every ~2 s for
# 80 s, then freshfs.sh stop. MSGS=0x1,0x2,... names the messages (default the rows Q327 ran). A Full Simulation file
# must be loaded by a Full Simulation player (the game type is the player's): loadfile.sh's 1ref is Instant Action. Q327.
# POLL='python3 -u object.py %PID% 160 43 & python3 -u loadframes.py %PID% 160 412,240 70 0 $D/o 1.5 & wait' replaces the two
# readers (its output is <outdir>/load.log), as loadfile.sh's; GRABS=N frames g-NN, one each ~2 s, which is how long the
# original runs before the stop (default 35, ~80 s). Q580's rest.
set -euo pipefail
F="$1"; D="$2"; mkdir -p "$D"; O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; J=$OSAVE/users/2fsfresh/jungle
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.6; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.0; }
key() { DISPLAY=:77 xdotool keydown $1; sleep 0.15; DISPLAY=:77 xdotool keyup $1; sleep 1.0; }
shot() { import -window root -display :77 -crop 1024x768+0+0 "$1.png"; }
align() { (cd "$O" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align >/dev/null 2>&1); }
"$O/freshfs.sh" make "$D/fresh" > "$D/make.txt" 2>&1
cp "$F" "$J/$(basename "$F")"; touch "$J/$(basename "$F")"
align; key Escape; at 510 50; click; shot "$D/1-load-list"; at 400 123
P=$("$O/original.sh" pid)
if [ -n "${POLL:-}" ]; then ( cd "$O" && bash -c "${POLL//%PID%/$P}" > "$D/load.log" 2>&1 & )
else
( cd "$O" && timeout 100 python3 -u advisor.py $P 95 ${MSGS:-0x1,0x2,0x3e,0x50,0xb9,0x103,0x104,0x105,0x106,0x107,0x11a} poll > "$D/load.log" 2>&1 & )
( cd "$O" && timeout 100 python3 -u tick.py $P 95 > "$D/tick.log" 2>&1 & )
fi
sleep 0.5; click
for n in $(seq -w 1 ${GRABS:-35}); do shot "$D/g-$n"; sleep 2; done
"$O/freshfs.sh" stop > "$D/stop.txt" 2>&1; cat "$D/stop.txt"
