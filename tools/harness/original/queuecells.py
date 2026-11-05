#!/usr/bin/env python3
"""queuecells.py PID SECS X,Y [X,Y ...] - the running ORIGINAL's queue tool and the map cells named, read-only: a line on
the first reading and on every change (Q1038; docs/exe/park-engine.md, "A queue cell's counter").

The tool: mode [0x0081ae2c] (3 the queue tool), the anchor [0x0081ede4], [0x0081ede8], the click counter [0x008186dc], the
pending list (count [0x00820a8c], the cells' x, y in pairs from 0x0081b740: FUN_0052fbd0 empties it, FUN_0052fbf0 pushes) and
the bank's balance (the thing FUN_005195d0 names, +0xc, as trackcells.py reads it). A cell's map record is
[0x007cf83c] + 0x294 + (y*128 + x + 1) * 0x44: type +8, neighbour mask +0xc, direction +0xd, flags +0xe, owner +0x10 and the
counter +0x20 (a signed word), the fields ours' console `cell X Y` prints as type, neighbours and overlap."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
cells = [tuple(int(v) for v in a.split(',')) for a in sys.argv[3:]]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<i'):
	f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def snap():
	w = rd(0x007cf83c, '<I')
	n = rd(0x00820a8c)
	pend = [(rd(0x0081b740 + 8 * i), rd(0x0081b744 + 8 * i)) for i in range(min(max(n, 0), 12))]
	t = rd(0x007cfb90 + rd(rd(0x0080239c, '<I') + 0x1da726, '<H') * 20, '<I')
	out = [f"mode {rd(0x0081ae2c)} anchor ({rd(0x0081ede4)},{rd(0x0081ede8)}) clicks {rd(0x008186dc)} pending {n} {pend} balance {rd(t + 0xc)}"]
	for x, y in cells:
		m = w + 0x294 + (y * 128 + x + 1) * 0x44
		out.append(f"  ({x},{y}) type {rd(m + 8)} neighbours {rd(m + 0xc, '<B'):#04x} dir {rd(m + 0xd, '<B'):#04x} flags {rd(m + 0xe, '<H'):#x} owner {rd(m + 0x10, '<H')} overlap {rd(m + 0x20, '<h')}")
	return out
last = None; t0 = time.time()
while time.time() - t0 < secs:
	try:
		s = snap()
	except (OSError, struct.error):
		print(f"{time.time() - t0:8.2f} s process gone"); break
	if s != last:
		print(f"{time.time() - t0:8.2f} s " + s[0]); print('\n'.join(s[1:]), flush=True); last = s
	time.sleep(0.1)
