#!/usr/bin/env python3
"""mapgrid.py PID [grid|records|all] - the ORIGINAL's park map overlay as the open map holds it (the static at 0x00f86db8;
docs/exe/hud.md, "The map's overlay"). Read only while the map is open (Space in a park): the grid and the list are made
by the opener and freed by the close.

grid    : the 128 x 128 code grid at +0x60 (8 bytes a cell: the code dword, the thing id word; FUN_005f2050 fills it),
          over the heightfield's cells (+0x24 across, +0x28 down): a count per code, then per thing id its code, cell
          count and bounding box, then the land codes 9 to 12's bounding boxes.
records : the sprite records of the list at +0x20080 (FUN_005f3940; 0x30 bytes each): the sheet (v r s l e g, which
          FUN_005f3940 pairs one to one with the kinds 0 to 5), kind +0x14, the filter mode +0x1c (1 layer bit, 2 staff picker,
          3 metric) and value +0x20, scale +0x24, shown +0x2c, the cell position +4/+8, the screen point +0xc/+0x10,
          the thing (+0x18: its id +0, type byte +2, and for staff the patrol cells +0x20a/+0x20c as (x,y)).
all     : both (the default), after the map's own words (layers +0x18, staff +0x1c, metric +0x20, zoom +0x5c,
          hovered record +0x20060).
hover   : the hover as the viewport's 0x10001 left it (0x005f198e..0x005f1cfc; Q665): the hovered record +0x20060 (its
          place in the list, kind, thing), the thing id word +0x20064 of the cell under the pointer, and the text the
          label 0x991 is given for them, worked out here from the thing (a member's name +0x1a0; a guest's UITEXT 456
          "Visitor #" with +0x1d8 or 457 "New Visitor"; otherwise the word's thing, 0x007cfb90 + id x 20, named by
          FUN_004e2780's join of +0x3c8 and +0x40a)."""
import struct, sys
pid = int(sys.argv[1]); what = sys.argv[2] if len(sys.argv) > 2 else 'all'
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def i32(a): return struct.unpack('<i', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def flt(a): return struct.unpack('<f', rd(a, 4))[0]
M = 0x00f86db8
W, H = i32(M + 0x24), i32(M + 0x28)
def wstr(a, n=64):
	b = rd(a, n * 2); out = ''
	for i in range(0, len(b), 2):
		c = b[i] | b[i + 1] << 8
		if c == 0: break
		out += chr(c)
	return out
def joined(th):   # FUN_004e2780
	a, b = wstr(th + 0x3c8), wstr(th + 0x40a)
	if not a: return b
	if a.endswith('-'): return a[:-1] + b
	return a if not b else f'{a} {b}'
def cellxy(packed): return ((packed - 1) & 0x7f, (packed - 1) >> 7)
print(f'map: layers 0x{u32(M+0x18):02x} staff {i32(M+0x1c)} metric {i32(M+0x20)} zoom {flt(M+0x5c):.2f} '
      f'cells {W}x{H} hovered 0x{u32(M+0x20060):08x}')
if what == 'hover':
	hov, word = u32(M + 0x20060), u16(M + 0x20064)
	lst = u32(M + 0x20080); node = u32(lst + 4) if lst else 0; idx = 0; n = 0
	while node and n < 5000:
		n += 1; rec, node = u32(node), u32(node + 4)
		if rec == hov: idx = n
	th = u32(hov + 0x18) if hov else 0
	tt = rd(th, 4)[2] if th else 0
	print(f'hover: record {idx} (0x{hov:08x}) kind {i32(hov + 0x14) if hov else "-"} thing {u16(th) if th else 0} type {tt} '
	      f'word {word}')
	if th and tt in (4, 5, 6, 7, 8): text = wstr(th + 0x1a0)
	elif th and tt == 1: v = i32(th + 0x1d8); text = f'Visitor #{v}' if v else 'New Visitor'
	elif word: text = joined(u32(0x007cfb90 + word * 20))
	else: text = ''
	print(f'hover text: "{text}"')
	sys.exit(0)
if what in ('grid', 'all'):
	raw = rd(M + 0x60, 128 * 128 * 8)
	counts, things, land = {}, {}, {}
	for y in range(min(H, 128)):
		for x in range(min(W, 128)):
			code, tid = struct.unpack_from('<IH', raw, (y * 128 + x) * 8)
			counts[code] = counts.get(code, 0) + 1
			box = None
			if 1 <= code <= 8: box = things.setdefault(tid, [code, 0, x, y, x, y])
			elif code: box = land.setdefault(code, [code, 0, x, y, x, y])
			if box:
				box[1] += 1; box[2] = min(box[2], x); box[3] = min(box[3], y); box[4] = max(box[4], x); box[5] = max(box[5], y)
	print('grid codes: ' + ' '.join(f'{c}:{n}' for c, n in sorted(counts.items())))
	for tid, (code, n, x0, y0, x1, y1) in sorted(things.items()):
		print(f'  thing {tid}: code {code}, {n} cells, ({x0},{y0})-({x1},{y1})')
	for code, (c, n, x0, y0, x1, y1) in sorted(land.items()):
		print(f'  land code {code}: {n} cells, ({x0},{y0})-({x1},{y1})')
if what in ('records', 'all'):
	lst = u32(M + 0x20080)
	node = u32(lst + 4) if lst else 0
	n = 0
	while node and n < 5000:
		rec, node = u32(node), u32(node + 4)
		n += 1
		kind = i32(rec + 0x14)
		sheet = 'vrsleg'[kind] if 0 <= kind < 6 else '?'   # FUN_005f3940 pairs each kind with one sheet
		th = u32(rec + 0x18)
		tdesc = ''
		if th:
			tt = rd(th, 8)[2]
			tdesc = f' thing {u16(th)} type {tt}'
			if tt in (4, 5, 6, 7):
				tdesc += f' patrol {cellxy(u16(th + 0x20a))}-{cellxy(u16(th + 0x20c))}'
		print(f'  rec {n}: sheet {sheet} kind {i32(rec+0x14)} mode {i32(rec+0x1c)} value {i32(rec+0x20)} '
		      f'scale {flt(rec+0x24):.2f} shown {i32(rec+0x2c)} at ({flt(rec+4):.2f},{flt(rec+8):.2f}) '
		      f'screen ({flt(rec+0xc):.1f},{flt(rec+0x10):.1f}){tdesc}')
	print(f'records: {n}')
