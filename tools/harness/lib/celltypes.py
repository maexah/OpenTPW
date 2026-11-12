#!/usr/bin/env python3
"""celltypes.py A.TPWS [B.TPWS] [X0 Y0 X1 Y1] - the map type and owner of the cells in a park file (Q888).

One file: the box's cells as `type/parent`, a row a line (parkfile.cells(): index y*128 + x, mType and mParentID).
Two files: the cells whose type or owner differ, `(x,y) A -> B`, and how many (a purchase's own typing: save the original's
park with original/savepark.sh before and after, or take the start file and the saved one). The box is X0..X1-1 and Y0..Y1-1
(default 0 0 96 85, the park). Needs tools/harness/lib on sys.path for parkfile; run it with `python3 -I`.
celltypes.py -s A.TPWS: the whole map's cells counted by mType, in the park and outside it (map flag 0x40), the NOMODIFY
ones (0x20), and the in-park bare and path cells by track type (the placement verdict's mode 4 takes in-park bare ground
and path, not NOMODIFY, with no track: Q37)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parkfile as pf

def box(args):
	n = [int(a) for a in args]
	return n if len(n) == 4 else [0, 0, 96, 85]

def summary(path):
	import struct, collections
	p = pf.Park(path); track = pf.cells(p); inside, outside, nomodify, ground = (collections.Counter() for _ in range(4))
	for i in range(16384):
		a = p.cell[i]
		kind, flags = (struct.unpack_from('<i', p.b, a + 25)[0], struct.unpack_from('<H', p.b, a + 2)[0]) if p.b[a] & 1 else (0, 0)
		(outside if flags & 0x40 else inside)[kind] += 1
		if flags & 0x20: nomodify[kind] += 1
		if not flags & 0x60 and kind in (0, 1): ground[(kind, track[i][2])] += 1
	print("in the park ", sorted(inside.items())); print("outside     ", sorted(outside.items()))
	print("NOMODIFY    ", sorted(nomodify.items())); print("in-park bare/path, not NOMODIFY, by (mType, track type)", sorted(ground.items()))
	return 0

def main(argv):
	if argv[:1] == ["-s"] and len(argv) == 2:
		return summary(argv[1])
	files = [a for a in argv if os.path.exists(a)]
	x0, y0, x1, y1 = box([a for a in argv if not os.path.exists(a)])
	if not files:
		print(__doc__); return 2
	cells = [pf.cells(pf.Park(f)) for f in files]
	if len(cells) == 1:
		for y in range(y0, y1):
			print(y, " ".join("%d/%d" % cells[0][y * 128 + x][:2] for x in range(x0, x1)))
		return 0
	n = 0
	for y in range(y0, y1):
		for x in range(x0, x1):
			a, b = cells[0][y * 128 + x][:2], cells[1][y * 128 + x][:2]
			if a != b:
				n += 1; print("(%d,%d) %d/%d -> %d/%d" % (x, y, a[0], a[1], b[0], b[1]))
	print("%d cells differ in x %d..%d, y %d..%d" % (n, x0, x1 - 1, y0, y1 - 1))
	return 0

if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
