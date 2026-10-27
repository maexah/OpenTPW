#!/usr/bin/env python3
"""trackcells.py PID SECS X,Y [X,Y ...] - the running ORIGINAL's track-grid records at the cells named, and the bank,
read-only: a line on the first reading and on every change (Q687; docs/exe/park.md, "A track piece's model, price and
open end").

A cell's record is [0x007cf83c] + (y*128 + x + 1 + 0x6cde) * 0x28: its type +8 (0xb kart track, 0xd kart station,
0x10 water track, 0x12 water station, 0x19 the boundary), neighbour mask +0xc, direction +0xd, flags +0xe (low four:
the hill shape), owner +0x10, the overlap counter +0x20, and the piece Track_RetileCell chose: family +0x14 (3 the
karts' kit, 4 the water's, 5 the boundary's), row +0x18, rotation +0x1c, and the model record's handle +4. For a kit
piece the row's model is named from the kit's table (0x007633e8 karts, 0x007634d8 water; 12 bytes a row, the name
at +4, a row with none takes the row before's), and the model record ([0x007a4610 + handle*4]) is marked `pulse` when
its flag word +4 carries 0x4000000, the open end's pulse (FUN_00469580). The bank is FUN_005195d0's thing +0xc."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
cells = [tuple(int(v) for v in a.split(',')) for a in sys.argv[3:]]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<i'):
	f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def cstr(a):
	f.seek(a); return f.read(32).split(b'\0')[0].decode('latin-1')
KITS = {3: 0x007633e8, 4: 0x007634d8}
def model_name(family, row):
	base = KITS.get(family)
	if base is None or row < 0 or row > 24:
		return '-'
	r = row
	while r >= 0:
		p = rd(base + r * 12 + 4, '<I')
		if p:
			return cstr(p)
		r -= 1
	return '-'
def bank():
	w = rd(0x0080239c, '<I')
	t = rd(0x007cfb90 + rd(w + 0x1da726, '<H') * 20, '<I')
	return rd(t + 0xc)
def snap():
	w = rd(0x007cf83c, '<I')
	out = [f"bank {bank()} tool mode {rd(0x0081ae2c):#x} anchor ({rd(0x0081ede4)},{rd(0x0081ede8)})"]
	for x, y in cells:
		r = w + (y * 128 + x + 1 + 0x6cde) * 0x28
		typ, mask, dirn, flags, owner = rd(r + 8), rd(r + 0xc, '<B'), rd(r + 0xd, '<B'), rd(r + 0xe, '<H'), rd(r + 0x10, '<H')
		fam, row, rot, handle, over = rd(r + 0x14), rd(r + 0x18), rd(r + 0x1c), rd(r + 4), rd(r + 0x20, '<h')
		pulse = ''
		if handle > 0:
			m = rd(0x007a4610 + handle * 4, '<I')
			pulse = ' pulse' if m and rd(m + 4, '<I') & 0x4000000 else ''
		out.append(f"  ({x},{y}) type {typ:#x} mask {mask:#04x} dir {dirn:#04x} flags {flags:#x} owner {owner} over {over} "
			f"family {fam} row {row} rot {rot} model {model_name(fam, row)} handle {handle}{pulse}")
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
