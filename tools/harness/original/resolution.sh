#!/usr/bin/env bash
# resolution.sh 512|640|800|1024|1280|1600|2048|off|show : the reference original's resolution, by Data/Resolution.sam
# (the disc's _Resolution.sam renamed is what turns the override on; its own header says so). `off` removes the file, so
# the Options screen's setting rules again: 640 x 480 for the reference player. Run it with the original stopped; it
# reads the file at its start. Check the start took with `tpwmem.py read 00faa5c0:h` (the screen's height).
# What changes with it (Q377, TOOLING.md "The original under Proton"): the font set ([0x0078b534]: 640 -> 1, 800 -> 2,
# 1024 -> 3) and so every font-measured size, the frame's sharpness, AND THE 3D PARK VIEW'S FRAMING (Q260).
set -euo pipefail
G="${TPW_ORIGINAL:-$HOME/Games/TPW-original}/Data"; R="$G/Resolution.sam"
case "${1:-show}" in
	show) [ -f "$R" ] && grep -a "^Res.RESOLUTION" "$R" | tr -d '\r' || echo "off (the Options screen's setting)"; exit 0 ;;
	off) rm -f "$R"; echo "off (the Options screen's setting)"; exit 0 ;;
	512) n=1 ;; 640) n=2 ;; 800) n=3 ;; 1024) n=4 ;; 1280) n=5 ;; 1600) n=6 ;; 2048) n=7 ;;
	*) echo "resolution.sh 512|640|800|1024|1280|1600|2048|off|show" >&2; exit 1 ;;
esac
"$(dirname "$0")/original.sh" pid >/dev/null 2>&1 && { echo "the original is running: stop it first" >&2; exit 1; }
sed "s/^Res.RESOLUTION\t[0-9]*\r\$/Res.RESOLUTION\t$n\r/" "$G/_Resolution.sam" > "$R"
grep -aq "^Res.RESOLUTION	$n" "$R" || { echo "the file was not written as expected" >&2; exit 1; }
echo "Res.RESOLUTION $n"
