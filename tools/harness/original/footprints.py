#!/usr/bin/env python3
"""footprints.py PID [SECS] | footprints.py PID items - every model instance of the ORIGINAL's slot list (0x007a4614) with the footprint table
ProceduralBase_SkirtFlags built for its model (record [inst+8]: byte 0's 0x40 set, +0x18 the table of {u16 flags,
u16 frame index} a cell, the item's .hmp [[inst]+0xcc] +8/+10 its width and depth), the instance's rectangle
(+0xc0/+0xc4, packed x << 16 | y), its angle ([inst+8]+0x10) and the heightfield words of the rectangle's cells
(frame index << 16 | word; [[0x007a0854] + 0x6c] + 0x2c, +0x18 across). docs/exe/park-engine.md, "A built thing's floor".
Waits up to SECS (default 0) for the list to hold an instance with a table. `items` prints instead every loaded item's
template (the list at 0x007ae6a8, count [0x007a439c]): its model's name and table, whether placed or not. Q649."""
import sys, struct, time
pid = int(sys.argv[1])
m = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): m.seek(a); return m.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def i32(a): return struct.unpack('<i', rd(a, 4))[0]
def cstr(a, n=24):
	try: return rd(a, n).split(b'\0')[0].decode('latin-1')
	except OSError: return '?'
def scan():
	out = []
	slots = u32(0x007a4084) + u32(0x007a3ebc) + 2
	for s in range(min(slots, 4000)):
		inst = u32(0x007a4614 + 4 * s)
		if not inst: continue
		try:
			rec = u32(inst + 8); hdr = u32(rec + 4); name = cstr(hdr + 0x18)
			b0 = rd(rec, 1)[0]; table = u32(rec + 0x18); angle = i32(rec + 0x10)
			tpl = u32(inst); hmp = u32(tpl + 0xcc) if tpl else 0
			w, d = (u16(hmp + 8), u16(hmp + 10)) if hmp else (0, 0)
			r0, r1 = u32(inst + 0xc0), u32(inst + 0xc4)
			cells = []
			if table and b0 & 0x40 and 0 < w * d <= 400:
				for k in range(w * d):
					f, t = struct.unpack('<HH', rd(table + 4 * k, 4)); cells.append((f, t))
			out.append((s, name, b0, angle, w, d, r0, r1, table, cells))
		except OSError:
			continue
	return out
if len(sys.argv) > 2 and sys.argv[2] == 'items':
	n = u32(0x007a439c); shown = 0
	for i in range(1, min(n, 2000) + 1):
		try:
			tpl = u32(0x007ae6a8 + 4 * i)
			if not tpl: continue
			rec = u32(tpl + 8); hdr = u32(rec + 4); name = cstr(hdr + 0x18); b0 = rd(rec, 1)[0]; table = u32(rec + 0x18)
			hmp = u32(tpl + 0xcc); w, d = (u16(hmp + 8), u16(hmp + 10)) if hmp else (0, 0)
			if not (table and b0 & 0x40 and 0 < w * d <= 400): continue
			cells = [struct.unpack('<HH', rd(table + 4 * k, 4)) for k in range(w * d)]
			print(f'{i} {name} {w}x{d} ' + ' / '.join(' '.join(f'{f:04x}:{t if t != 10000 else "-"}' for f, t in cells[z * w:(z + 1) * w]) for z in range(d)))
			shown += 1
		except OSError:
			continue
	print(f'{shown} item models with a table, of {n} loaded')
	sys.exit(0)
secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
end = time.time() + secs
while True:
	rows = scan()
	if any(r[9] for r in rows) or time.time() >= end: break
	time.sleep(0.5)
for s, name, b0, angle, w, d, r0, r1, table, cells in rows:
	if not cells: continue
	print(f'slot {s} {name} byte0 {b0:#x} angle {angle} hmp {w}x{d} rect ({r0 >> 16},{r0 & 0xffff})..({r1 >> 16},{r1 & 0xffff}) table {table:#x}')
	for z in range(d):
		print('   ' + ' '.join(f'{f:04x}:{t if t != 10000 else "-"}' for f, t in cells[z * w:(z + 1) * w]))
	hf = u32(u32(0x007a0854) + 0x6c); across = u32(hf + 0x18); words = u32(hf + 0x2c)
	for y in range(r0 & 0xffff, (r1 & 0xffff) + 1):
		print(f'   row {y}: ' + ' '.join(f'({x},{y}) {u32(words + 4 * (y * across + x)):08x}' for x in range(r0 >> 16, (r1 >> 16) + 1)))
print(f'{sum(1 for r in rows if r[9])} instances with a table, {sum(1 for r in rows if r[2] & 0x40)} with byte 0 0x40, {len(rows)} in the list')
