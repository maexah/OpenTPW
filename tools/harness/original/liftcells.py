#!/usr/bin/env python3
"""liftcells.py PID X,Y [X,Y ...] - the running ORIGINAL's two grids at the cells named, read-only, once: what the track
tool's square lift reads (Q775; docs/exe/park.md, "A track square's lift and walls").

For each cell: the MAP record's type (the 0x44-byte cells at [0x008023a0], +8: 4 a footprint, 9 and 10 an entrance and an
exit, 1 path, 3 queue), the TRACK record's type ([0x007cf83c] + (y*128 + x + 1 + 0x6cde) * 0x28, +8), and the object
FUN_0053bf30 answers for the cell (the record's +4, or for a track type 0xc or 0x11 its +0x10 parent's), with that object's
model record ([0x007a4610 + handle*4]): whether it holds a .hmp (+0xcc) and the height its root stands at (the model's
root node, [[[record + 8] + 4] + 0x78], its +0x44). The OpenTPW side is the console's `cell X Y` and `strip`."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<i'):
	f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
maps = rd(0x008023a0, '<I'); track = rd(0x007cf83c, '<I')
def trec(cell): return track + (cell + 0x6cde) * 0x28
for a in sys.argv[2:]:
	x, y = (int(v) for v in a.split(','))
	cell = y * 128 + x + 1
	mtype = rd(maps + (cell - 1) * 0x44 + 8)
	r = trec(cell); ttype = rd(r + 8); handle = rd(r + 4); via = ''
	if ttype in (0xc, 0x11):
		parent = rd(r + 0x10, '<H')
		handle = rd(trec(parent) + 4) if parent else 0
		via = f' via ({(parent - 1) & 0x7f},{(parent - 1) >> 7})' if parent else ' via none'
	obj = ''
	if handle > 0:
		m = rd(0x007a4610 + handle * 4, '<I')
		if m:
			hmp = rd(m + 0xcc, '<I')
			root = rd(rd(rd(m + 8, '<I') + 4, '<I') + 0x78, '<I')
			obj = f' hmp {"yes" if hmp else "no"} root y {rd(root + 0x44, "<f"):.2f}' if root else f' hmp {"yes" if hmp else "no"}'
	print(f'({x},{y}) map type {mtype:#x} track type {ttype:#x} object {handle}{via}{obj}')
