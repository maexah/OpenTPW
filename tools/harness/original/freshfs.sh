#!/usr/bin/env bash
# freshfs.sh - a FRESH Full Simulation park made by the ORIGINAL, off-screen: what its world setup makes with no park file.
#   freshfs.sh make <outdir>   adds player 2fsfresh to the reference install (Alexah's Full Simulation gms.dat and no park
#                              file), starts the original, picks that player (it goes straight into Lost Kingdom and
#                              makes the park), and copies the restart.INTS it writes to <outdir>/proton-restart.INTS
#                              with a frame, <outdir>/fresh-park.png. The original is left RUNNING in that park, about
#                              tick 150 on: read its memory (thingwords.py and the rest) and then `stop`.
#   freshfs.sh stop            stops the original, removes the player folder it added, says whether save/ is as it was
# The original must not be running at `make`. The player's slot is the list's second row (660, 158 of 1024 x 768).
#   POLL='python3 -u advisor.py %PID% 300 0xb9 poll'   a reader of the original's memory started just before the player's
#                              first click (so it sees the park from tick nought): its output is <outdir>/fresh.log (Q327)
#   GMS=<gms.dat>              the player's profile in place of Alexah's (a copy edited by lib/gmsfile.py put; Q706)
#   GRAB=N                     N frames of the display, one every GRAB_EVERY seconds (default 5) from that click, as
#                              <outdir>/g-NNN.png, in the background (Q327)
# Read the file with lib/parkfile.py (-l the lab). Q349.
set -euo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"; S="$OSAVE"; U=$S/users/2fsfresh; L="$OLOGS/freshfs"
SRC="${OPENTPW_FULLSIM_PLAYER:?set OPENTPW_FULLSIM_PLAYER (lib/paths.py)}"
at() { (cd "$O" && GDISPLAY=:77 python3 gmove.py $1 $2 >/dev/null 2>&1); sleep 0.8; }
click() { DISPLAY=:77 xdotool mousedown 1; sleep 0.2; DISPLAY=:77 xdotool mouseup 1; sleep 1.2; }
sum() { find $S -type f -exec md5sum {} \; | sort | md5sum; }
files() { find $S -type f -printf '%TY-%Tm-%Td %TH:%TM:%TS %s %P\n' | sort -k4; }
case "${1:-}" in
make)
	D="$2"; mkdir -p "$D"
	"$O/original.sh" pid >/dev/null 2>&1 && { echo "the original is running: stop it first"; exit 1; }
	[ -e "$U" ] && { echo "$U is there already: run stop first"; exit 1; }
	sum > "$L-before.md5"; files > "$L-before.txt"
	mkdir -p $U/jungle $U/fantasy $U/hallow $U/space $U/outbox
	cp "${GMS:-$SRC/gms.dat}" $U/gms.dat; cp "$SRC/addrbook.dat" $U/; chmod u+w $U/*.dat
	"$O/original.sh" start --offscreen; sleep 50
	(cd "$O" && GDISPLAY=:77 GW=1022 GH=766 python3 gmove.py align >/dev/null 2>&1)
	at 660 158
	if [ -n "${POLL:-}" ]; then ( cd "$O" && bash -c "${POLL//%PID%/$("$O/original.sh" pid)}" > "$D/fresh.log" 2>&1 & ); fi
	if [ -n "${GRAB:-}" ]; then ( for n in $(seq -w 1 "$GRAB"); do import -window root -display :77 "$D/g-$n.png"; sleep "${GRAB_EVERY:-5}"; done & ); fi
	click; click; sleep 46
	import -window root -display :77 "$D/fresh-park.png"
	cp $U/jungle/restart.INTS "$D/proton-restart.INTS"; echo "made: $D/proton-restart.INTS"
	;;
stop)
	"$O/original.sh" stop --offscreen; sleep 3; rm -rf "$U"
	sum > "$L-after.md5"; files > "$L-after.txt"
	diff "$L-before.txt" "$L-after.txt" >/dev/null && echo "save/ files the same" || { echo "save/ FILES DIFFER:"; diff "$L-before.txt" "$L-after.txt" || true; }
	cmp -s "$L-before.md5" "$L-after.md5" && echo "save/ sum the same" || echo "save/ SUM CHANGED"
	;;
*) sed -n '2,11p' "$0"; exit 1 ;;
esac
