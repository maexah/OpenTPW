#!/usr/bin/env python3
"""stampkinds.py PARK.TPWS CELLS.txt - the original's live ground map's footprint stamps (`0x200`) by what the park file holds on each cell (Q891).

CELLS.txt is a `groundcell.py` read (`READ=0,0:95,84`, original/livecells.sh, or a run's cells-N.txt). For each cell kind
of the file it counts the cells and those carrying `0x200`: by map type (`parkfile.cells()`, mType) and by track-layer type (a
kart anchor 0xb, a station 0xd, the boundary's hoardings 0x19). A kind the stamp does not reach counts 0 (the New Save: types 0, 1, 2, 7,
0x15 coaster nodes, 0x1e, and the tracks 0x7, 0xb, 0x19). Run it with `python3 -I`."""
import os, re, sys, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parkfile as pf

def main(argv):
	if len(argv) != 2:
		print(__doc__); return 2
	cells = pf.cells(pf.Park(argv[0])); by_type = collections.defaultdict(lambda: [0, 0]); by_track = collections.defaultdict(lambda: [0, 0])
	for line in open(argv[1]):
		m = re.match(r"\s*cell \((\d+),(\d+)\) base flags 0x([0-9a-f]+)", line)
		if not m:
			continue
		x, y, word = int(m[1]), int(m[2]), int(m[3], 16)
		map_type, _, track_type, _ = cells[y * 128 + x]
		by_type[map_type][0] += 1; by_type[map_type][1] += bool(word & 0x200)
		if track_type:
			by_track[track_type][0] += 1; by_track[track_type][1] += bool(word & 0x200)
	print("by map type (cells, with 0x200):", {hex(k): tuple(v) for k, v in sorted(by_type.items())})
	print("by track type (cells, with 0x200):", {hex(k): tuple(v) for k, v in sorted(by_track.items())})
	return 0

if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
