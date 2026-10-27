#!/usr/bin/env bash
# prepoke32.sh - make the ORIGINAL draw its ground in 32-pixel tiles, as on a machine of its day (Q618). Waits for the
# original's process and for its boot to have written dwTotalPhys (-1 under Proton), then runs poke32.py (256 MB), before
# the first park entry makes the tile cache. Gives up after 90 s.
# Start it in the background just before `loadfile.sh load` (or `original.sh start`):
#     prepoke32.sh > log 2>&1 & loadfile.sh load ...
# compstate.py then reads the cache's tile size 32. A poke after the first park entry does nothing: that cache is kept.
O="$(cd "$(dirname "$0")" && pwd)"; . "$(dirname "$0")/../lib/paths.sh"
for i in $(seq 1 180); do
	P=$("$O/original.sh" pid 2>/dev/null) && [ -n "$P" ] && \
		v=$(python3 -c "import struct,sys;f=open('/proc/$P/mem','rb',buffering=0);f.seek(0x008bd520);print(struct.unpack('<i',f.read(4))[0])" 2>/dev/null) && \
		[ "$v" = "-1" ] && { echo "after $((i/2)) s:"; python3 $O/poke32.py $P; exit 0; }
	sleep 0.5
done
echo "gave up: no pid or dwTotalPhys never -1"; exit 1
