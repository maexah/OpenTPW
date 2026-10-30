#!/usr/bin/env bash
# livecells.sh <file.TPWS> <outfile> - the ORIGINAL's live heightfield: a park file is loaded (loadfile.sh), left running, and
# every cell's flag word read once (groundcell.py READ over the whole map), then the original is stopped (Q820). Compare the
# result with a file's cells by `HFS_DUMP=<out> dotnet hfsamplers.dll ...` (docs/TOOLING.md); outfile lines read
# `cell (x,y) base flags 0xNNNN texture T; drawn flags ...`. Set PYTHON to a Python that has Xlib's neighbours if yours lacks them.
set -uo pipefail
O="$(cd "$(dirname "$0")" && pwd)"; . "$O/../lib/paths.sh"
PY="${PYTHON:-python3}"; D="$(dirname "$2")"; mkdir -p "$D"
"$O/loadfile.sh" load "$1" "$D" livecells
sleep 20
P=$("$O/original.sh" pid) || { "$O/loadfile.sh" stop; exit 1; }
echo "== pid $P"
READ=0,0:95,84 "$PY" -u "$O/groundcell.py" "$P" 1 > "$2" 2>&1
wc -l "$2"
"$O/loadfile.sh" stop
