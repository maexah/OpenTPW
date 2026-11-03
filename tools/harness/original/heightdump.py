#!/usr/bin/env python3
"""heightdump.py PID OUT.json - the ORIGINAL's live ground corner heights, read-only (Q948): the heightfield [[0x007a0854] + 0x6c]
(heights at +0x28, columns +0x18 + 1, rows +0x1c + 1) as {"cols": C, "rows": R, "h": [R * C floats]} (vertex X, Z at index Z * C + X). Run it
after the load has finished (fsload.sh / loadfile.sh's POLL, 50 s in): a file's ground is the level's until the load's own changes, and the
two parks' dumps beside each other (`heightdump.py --diff A.json B.json`) are the cells and corners a played park changed."""
import json, struct, sys
if sys.argv[1] == '--diff':
	a, b = (json.load(open(p)) for p in sys.argv[2:4]); assert a['cols'] == b['cols'] and a['rows'] == b['rows']
	out = [(i % a['cols'], i // a['cols'], x, y) for i, (x, y) in enumerate(zip(a['h'], b['h'])) if abs(x - y) > 1e-4]
	print(f"{len(out)} of {len(a['h'])} corners differ")
	for x, z, p, q in out: print(f"  vertex ({x},{z}) {p:.4f} -> {q:.4f}")
	sys.exit(0)
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
hf = rd(rd(0x007a0854) + 0x6c); hp = rd(hf + 0x28); cols, rows = rd(hf + 0x18) + 1, rd(hf + 0x1c) + 1
f.seek(hp); h = list(struct.unpack(f'<{cols * rows}f', f.read(4 * cols * rows)))
json.dump(dict(cols=cols, rows=rows, h=h), open(sys.argv[2], 'w')); print(f'heightfield {hf:#x} {cols} x {rows}, {len(h)} heights')
