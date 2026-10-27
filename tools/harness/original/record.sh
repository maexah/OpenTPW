#!/usr/bin/env bash
# record.sh - film the ORIGINAL's off-screen display (:77, 1024x768) so a thing on screen for a second is not missed
# between timed stills.
# Needs ffmpeg. GDISPLAY=<display> films another (the size stays 1024x768). FPS defaults to 30, COLS to 6. start also writes shots/NAME.fps and logs to logs/record.log; frames first deletes shots/NAME-f*.png.
#   record.sh start NAME [FPS]     lossless video to shots/NAME.mkv; the wall clock at its start in shots/NAME.t0
#   record.sh stop                 ends the recording cleanly
#   record.sh frames NAME FROM TO  every frame between FROM and TO seconds of the film to shots/NAME-f<n>.png, and
#                                  a line per frame: its file and its wall-clock time (t0 + n / fps, good to 0.2 s)
#   record.sh sheet NAME FROM TO [COLS]   the same frames as one contact sheet, shots/NAME-sheet.png
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; . "$H/../lib/paths.sh"; S="$WORK/original/shots"; D="${GDISPLAY:-:77}"; mkdir -p "$S"
case "${1:-}" in
start)
	N="$2"; F="${3:-30}"
	[ -f "$S/.record.pid" ] && kill -0 "$(cat "$S/.record.pid")" 2>/dev/null && { echo "already recording" >&2; exit 1; }
	date +%s.%N > "$S/$N.t0"; echo "$F" > "$S/$N.fps"
	nohup ffmpeg -loglevel error -y -f x11grab -draw_mouse 1 -framerate "$F" -video_size 1024x768 -i "$D" \
		-c:v libx264 -preset ultrafast -qp 0 -pix_fmt yuv444p "$S/$N.mkv" >"$OLOGS/record.log" 2>&1 &
	echo $! > "$S/.record.pid"; echo "recording $D to $S/$N.mkv at $F fps"
	;;
stop)
	P="$(cat "$S/.record.pid")"; kill -INT "$P" 2>/dev/null || true
	for _ in $(seq 50); do kill -0 "$P" 2>/dev/null || break; sleep 0.1; done
	rm -f "$S/.record.pid"; echo "stopped"
	;;
frames)
	N="$2"; A="$3"; B="$4"; F="$(cat "$S/$N.fps")"; T0="$(cat "$S/$N.t0")"
	rm -f "$S/$N-f"*.png
	ffmpeg -loglevel error -y -i "$S/$N.mkv" -vf "select='between(t,$A,$B)'" -vsync 0 -frame_pts 1 "$S/$N-f%05d.png"
	for f in "$S/$N-f"*.png; do n="$(basename "$f" .png)"; n="${n##*-f}"; python3 -c "print('$f', 'film %.2f s' % (int('$n')/$F), 'wall %.2f' % ($T0 + int('$n')/$F))"; done
	;;
sheet)
	N="$2"; A="$3"; B="$4"; C="${5:-6}"
	ffmpeg -loglevel error -y -i "$S/$N.mkv" -vf "select='between(t,$A,$B)',scale=512:-1,tile=${C}x$(python3 -c "import math;print(max(1,math.ceil((($B-$A)*$(cat "$S/$N.fps")+1)/$C)))")" -frames:v 1 "$S/$N-sheet.png"
	echo "$S/$N-sheet.png"
	;;
*) sed -n '2,9p' "$0"; exit 1 ;;
esac
