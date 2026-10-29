#!/usr/bin/env python3
"""basevertices.py PID [MODEL...] [--verbose] - the ORIGINAL's memory, read-only: how every placed model instance's
vertices stand on the park's ground, against the conform and drape `FUN_00469a80` runs at the making (Q812;
docs/exe/park-engine.md, "Where a placed object's model stands").

For each instance of the slot list (0x007a4614; [inst+8]+4 its header; MODEL... keeps the headers whose name, +0x18,
starts so) it walks the instance's node tree beside its master's ([header+0x9c], the type's rest vertices) and
carries each mesh vertex (+0x58 count, +0x60 in groups of four: x[4] y[4] z[4]) through the instance's node chain
(each node's +0x10 matrix, row vectors, parent +4) into the world. Then, against the park's heightfield
([[0x007a0854]+0x6c]: origin +8/+0xc, cell +0x10/+0x14, width +0x18, heights +0x28 a row of width+1, cell words +0x2c):

  conform  the base vertices the type load listed (the type model's [[inst+8]+8]: count u16 +0xc, indices u16 at
           [+0x14], consumed in the walk's order over the meshes flagged 0x1000, as 0x00469a80 does): how many stand
           at a cell corner's height (x and z within 0.01 of a line, y within 0.001 of the corner) and how many do
           not; and how many vertices of those meshes moved from the master's in all.
  drape    a mesh flagged 0x20000: each vertex's world rise from its rest place (the master's vertex through the same
           chain) less the ground under the rest place (the cell's triangle by its word's 0x4, as 0x0046a34c): a
           draped mesh gives one value for every vertex (minus the base height); the spread is printed.
  interp   a mesh with a list at +0x98 ({ count, pointer }: ProceduralBase_Build's second count, a mesh flagged 0x40): each
           listed vertex's world rise from rest against the ground under its place (snapped to a cell line within 0.1)
           less the root's height, the interpolating pass of 0x0046a2a7; the largest difference is printed.
  other    every other mesh: its vertices at a cell corner, and how many of them stand at the corner's height.

Where this walk differs from FUN_00469a80's (Q812 review): it skips a node flagged 0x80000000 (meshes()) and a mesh whose
vertex count differs from its master's, and moves the list cursor only on meshes it visits; FUN_00469a80 tests no 0x80000000 and
skips only nodes flagged 0x200. A mesh this walk skips that the original walks leaves the cursor behind, so 'used' can fall short of
'listed' (917 of 925 in Alexah's park: the two StdPylon bases, slots 202 and 210; the cause is not settled).

One line an instance: slot, name, the flags it was made with (+0xe4, FUN_00463060's second argument), the root's translation, then each kind's counts. --verbose prints a line a mesh.

  basevertices.py %PID%                    every placed instance
  basevertices.py %PID% sungod ttwist      only those models (names as the model's file, e.g. 'queue', 'sungod')
"""
import struct, sys

pid = int(sys.argv[1]); args = [a for a in sys.argv[2:] if not a.startswith('--')]; VERBOSE = '--verbose' in sys.argv
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def f32(a): return struct.unpack('<f', rd(a, 4))[0]
def cstr(a): return rd(a, 40).split(b'\0')[0].decode('ascii', 'replace')

hf = u32(u32(0x007a0854) + 0x6c)
OX, OZ, CX, CZ = (f32(hf + o) for o in (8, 0xc, 0x10, 0x14)); W, H = u32(hf + 0x18), u32(hf + 0x1c)
HEIGHTS, CELLS = u32(hf + 0x28), u32(hf + 0x2c)
heights = struct.unpack(f'<{(W + 1) * (H + 1)}f', rd(HEIGHTS, 4 * (W + 1) * (H + 1)))
def corner(ix, iz): return heights[(W + 1) * iz + ix]

def ground(x, z):
	"""The height under a point by the cell's own diagonal (0x0046a34c: word bit 0x4 clear, fz <= fx takes h00 h10 h11)."""
	px, pz = max(0.0, (x - OX) / CX), max(0.0, (z - OZ) / CZ)
	ix, iz = min(int(px), W - 1), min(int(pz), H - 1); fx, fz = px - ix, pz - iz
	h00, h10, h01, h11 = corner(ix, iz), corner(ix + 1, iz), corner(ix, iz + 1), corner(ix + 1, iz + 1)
	if not u32(CELLS + 4 * (iz * W + ix)) & 4:
		return h00 + fx * (h10 - h00) + fz * (h11 - h10) if fz <= fx else h00 + fz * (h01 - h00) + fx * (h11 - h01)
	if 1 - fz <= fx:
		return h01 + fx * (h11 - h01) + (1 - fz) * (h10 - h11)   # the h01 h11 h10 triangle
	return h00 + fx * (h10 - h00) + fz * (h01 - h00)              # the h00 h10 h01 triangle

def matrix(node): return struct.unpack('<16f', rd(node + 0x10, 64))
def apply(m, p):
	x, y, z = p
	return (x * m[0] + y * m[4] + z * m[8] + m[12], x * m[1] + y * m[5] + z * m[9] + m[13], x * m[2] + y * m[6] + z * m[10] + m[14])
def chain(node):
	out = []
	while node: out.append(matrix(node)); node = u32(node + 4)
	return out
def world(ms, p):
	for m in ms: p = apply(m, p)
	return p
def vertices(node):
	n = u16(node + 0x58); block = u32(node + 0x60)
	if not n or not block: return []
	raw = rd(block, 0x30 * ((n + 3) // 4)); out = []
	for i in range(n):
		g, c = 0x30 * (i >> 2), 4 * (i & 3)
		out.append(struct.unpack_from('<f', raw, g + c)[0:1] + struct.unpack_from('<f', raw, g + 0x10 + c) + struct.unpack_from('<f', raw, g + 0x20 + c))
	return out
def near_line(v, o, c):
	p = (v - o) / c; fr = p - int(p) if p >= 0 else 0.0
	return (abs(fr) < 0.01 or abs(fr - 1) < 0.01), int(round(p))

def meshes(inst, master, ms_parent=None):
	"""The mesh pairs of the instance's tree and its master's, walked together (same shape: the clone copies the tree)."""
	while inst and master:
		flags = u32(inst)
		if not flags & 0x80000000 and u16(inst + 0x58):
			yield inst, master, flags
		yield from meshes(u32(inst + 0xc), u32(master + 0xc))
		inst, master = u32(inst + 8), u32(master + 8)

slots = min(u32(0x007a4084) + u32(0x007a3ebc) + 2, 4000)
total = {'conform_on': 0, 'conform_off': 0, 'drape_meshes': 0, 'drape_spread_max': 0.0}
for s in range(slots):
	try:
		inst = u32(0x007a4614 + 4 * s)
		if not inst: continue
		header = u32(u32(inst + 8) + 4); name = cstr(header + 0x18)
	except (OSError, struct.error):
		continue
	if args and not any(name.lower().startswith(a.lower()) for a in args): continue
	master = u32(header + 0x9c); root = u32(header + 0x78)
	if not master or master == header:
		print(f'slot {s} {name}: no master apart from itself (+0x9c 0x{master:08x})'); continue
	t = matrix(root)[12:15]
	typemodel = u32(u32(inst + 8) + 8); count = u16(typemodel + 0xc); at = u32(typemodel + 0x14)
	listing = struct.unpack(f'<{count}H', rd(at, 2 * count)) if count and at else (); listed = [0]
	line = {'conform': [0, 0, 0], 'drape': [], 'other': [0, 0], 'interp': [0, 0.0]}
	for node, mnode, flags in meshes(root, u32(master + 0x78)):
		ms = chain(node); mine, rest = vertices(node), vertices(mnode)
		if len(mine) != len(rest): continue
		mname = cstr(u32(node + 0x54)) if u32(node + 0x54) else '?'
		if flags & 0x20000:
			es = []
			for a, b in zip(mine, rest):
				wa, wb = world(ms, a), world(ms, b)
				es.append((wa[1] - wb[1]) - ground(wb[0], wb[2]))
			spread = max(es) - min(es); line['drape'].append((mname, spread, -sum(es) / len(es)))
			total['drape_meshes'] += 1; total['drape_spread_max'] = max(total['drape_spread_max'], spread)
			if VERBOSE: print(f'  {mname}: drape {len(es)} vertices, rise less ground from {min(es):.4f} to {max(es):.4f} (spread {spread:.4f})')
			continue
		plist = u32(node + 0x98)
		if plist and u32(plist) and u32(plist) < 4096:
			idx = struct.unpack(f'<{u32(plist)}H', rd(u32(plist + 4), 2 * u32(plist)))
			worst = 0.0
			for i in idx:
				if i >= len(mine): continue
				wa, wb = world(ms, mine[i]), world(ms, rest[i])
				sx, sz = wa[0], wa[2]
				worst = max(worst, abs((wa[1] - wb[1]) - (ground(sx, sz) - t[1])))
			line['interp'][0] += len(idx); line['interp'][1] = max(line['interp'][1], worst)
			total['interp'] = total.get('interp', 0) + len(idx); total['interp_worst'] = max(total.get('interp_worst', 0.0), worst)
			if VERBOSE: print(f'  {mname}: interp {len(idx)} listed, largest (rise - (ground - root height)) {worst:.4f}')
		on = off = moved = 0
		for i, (a, b) in enumerate(zip(mine, rest)):
			if abs(a[1] - b[1]) > 1e-4 or abs(a[0] - b[0]) > 1e-4 or abs(a[2] - b[2]) > 1e-4: moved += 1
			if flags & 0x1000:
				if listed[0] >= len(listing) or listing[listed[0]] != i: continue
				listed[0] += 1
			w = world(ms, a)
			lx, ix = near_line(w[0], OX, CX); lz, iz = near_line(w[2], OZ, CZ)
			if lx and lz and 0 <= ix <= W and 0 <= iz <= H and abs(w[1] - corner(ix, iz)) < 0.001: on += 1
			elif flags & 0x1000 or (lx and lz): off += 1
		if flags & 0x1000:
			line['conform'][0] += on; line['conform'][1] += off; line['conform'][2] += moved
			total['conform_on'] += on; total['conform_off'] += off
		else:
			line['other'][0] += on + off; line['other'][1] += on
		if VERBOSE: print(f'  {mname}: flags 0x{flags:x} {len(mine)} vertices, {moved} moved; at a corner {on} on its height, {off} not')
	c, d, o = line['conform'], line['drape'], line['other']
	ip = line['interp']
	ds = (f'; interp {ip[0]} listed, largest difference {ip[1]:.4f}' if ip[0] else '')
	ds += (f'; drape {len(d)} meshes, spread {max(x[1] for x in d):.4f}, base {d[0][2]:.3f}' if d else '')
	print(f'slot {s} {name} made 0x{u32(inst + 0xe4):x} root ({t[0]:.2f}, {t[1]:.3f}, {t[2]:.2f}): listed {len(listing)}, used {listed[0]}: {c[0]} on a corner, {c[1]} not; {c[2]} moved;'
		f' other corners {o[0]} ({o[1]} on){ds}')
print(f'all: listed base vertices {total["conform_on"]} on a corner, {total["conform_off"]} not; drape meshes {total["drape_meshes"]},'
	f' largest spread {total["drape_spread_max"]:.4f}; interp listed {total.get("interp", 0)}, largest difference {total.get("interp_worst", 0.0):.4f}')
