#!/usr/bin/env python3
"""facenormals.py PID [MODEL...] [--verbose] - the ORIGINAL's memory, read-only: every placed instance's normals tables
against the face normals FUN_00473a40 works out again from the instance's own vertices (Q819; docs/exe/park-engine.md,
"Where a placed object's model stands", the face normals).

For each instance of the slot list (0x007a4614; [inst+8]+4 its header; MODEL... keeps the headers whose name, +0x18,
starts so) it walks the instance's node tree beside its master's ([header+0x9c]) and, for each mesh with a table (+0x64):

  own      whether the instance's table is its own (a pointer other than the master's: the clone's flag 0x8 copies it)
  lit      the first run, the vertex-order length's entries (+0x5e) the lighter reads (0x00574660): how many differ
           from the master's by more than 1e-4
  faces    each face record (+0x70, +0x5c of them; word, a, b, c through the order +0x94 into the vertices +0x60, four to
           a 0x30 group): the entry its word's low 15 bits name against normalise( (c - b) x (a - b) ) of the instance's
           vertices, (0, -1, 0) for a degenerate face: how many agree to 1e-4, how many do not, and how many name an entry
           in the lit run; the same against the master's table (how many the recompute changed)
  unreadable  a mesh whose table or its master's will not read (counted, passed over)
  coloured a mesh flagged 0x1000000: its +0x64 is a colour stream for the draw, not normals (the pulse, Model_PulseOn
           0x00469580, writes its colours there; the hover's highlight swap FUN_00469180 points it at a tint block and
           keeps the pointer in [0x007afd94]); its lit run is counted apart. Each line names the instance HIGHLIGHTED
           ([0x007a1ce0]) or PULSING (the list [0x007afe14], through +0xd0), with its flags +4 (0x4000000 pulsing)
  stale    the node's flag 0x10000 (its normals marked to be worked out again: FUN_00469940 marks the root after a conform)

One line an instance, then the totals. --verbose prints a line a mesh.
"""
import math, struct, sys

pid = int(sys.argv[1]); args = [a for a in sys.argv[2:] if not a.startswith('--')]; VERBOSE = '--verbose' in sys.argv
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def cstr(a): return rd(a, 40).split(b'\0')[0].decode('ascii', 'replace')

def vertices(node):
	n = u16(node + 0x58); block = u32(node + 0x60)
	if not n or not block: return []
	raw = rd(block, 0x30 * ((n + 3) // 4)); out = []
	for i in range(n):
		g, c = 0x30 * (i >> 2), 4 * (i & 3)
		out.append((struct.unpack_from('<f', raw, g + c)[0], struct.unpack_from('<f', raw, g + 0x10 + c)[0], struct.unpack_from('<f', raw, g + 0x20 + c)[0]))
	return out

def recomputed(a, b, c):
	e1 = (a[0] - b[0], a[1] - b[1], a[2] - b[2]); e2 = (c[0] - b[0], c[1] - b[1], c[2] - b[2])
	x = e1[2] * e2[1] - e1[1] * e2[2]; y = e1[0] * e2[2] - e1[2] * e2[0]; z = e1[1] * e2[0] - e1[0] * e2[1]
	n = math.sqrt(x * x + y * y + z * z)
	return (0.0, -1.0, 0.0) if n == 0.0 or n != n else (x / n, y / n, z / n)

def close(p, q, tol=1e-4): return all(abs(s - t) <= tol for s, t in zip(p, q))

def meshes(inst, master):
	while inst and master:
		yield inst, master
		yield from meshes(u32(inst + 0xc), u32(master + 0xc))
		inst, master = u32(inst + 8), u32(master + 8)

KEYS = ('meshes', 'unreadable', 'own', 'shared', 'lit_differ', 'coloured', 'coloured_lit_differ', 'faces', 'agree', 'disagree', 'face_in_lit', 'changed', 'stale', 'stale_disagree')
total = dict.fromkeys(KEYS, 0)
def mesh_line(node, mnode, line):
	"""One mesh pair's counts added to line (a mesh with no table or faces is passed over)."""
	table, mtable = u32(node + 0x64), u32(mnode + 0x64)
	nf, olen = u16(node + 0x5c), u16(node + 0x5e)
	if not table or not nf or u16(node + 0x58) != u16(mnode + 0x58): return
	line['meshes'] += 1; line['own' if table != mtable else 'shared'] += 1
	flags = u32(node); stale = bool(flags & 0x10000); line['stale'] += stale
	if flags & 0x1000000: line['coloured'] += 1
	mine = vertices(node); order = struct.unpack(f'<{olen}H', rd(u32(node + 0x94), 2 * olen))
	faces = [struct.unpack_from('<4H', rd(u32(node + 0x70), 8 * nf), 8 * k) for k in range(nf)]
	top = max([olen] + [(w & 0x7fff) + 1 for w, _a, _b, _c in faces])
	try:
		have = struct.unpack(f'<{3 * top}f', rd(table, 12 * top)); was = struct.unpack(f'<{3 * top}f', rd(mtable, 12 * top))
	except OSError:
		line['unreadable'] += 1; return
	entry = lambda t, k: t[3 * k:3 * k + 3]
	lit = [k for k in range(olen) if not close(entry(have, k), entry(was, k))]
	line['lit_differ' if not flags & 0x1000000 else 'coloured_lit_differ'] += len(lit)
	agree = disagree = 0
	for w, a, b, c in faces:
		k = w & 0x7fff
		if k < olen: line['face_in_lit'] += 1
		try:
			n = recomputed(mine[order[a]], mine[order[b]], mine[order[c]])
		except IndexError:
			continue
		if close(entry(have, k), n): agree += 1
		else: disagree += 1
		if not close(entry(have, k), entry(was, k)): line['changed'] += 1
	line['faces'] += nf; line['agree'] += agree; line['disagree'] += disagree
	if stale: line['stale_disagree'] += disagree
	if VERBOSE:
		print(f'  {cstr(u32(node + 0x54)) if u32(node + 0x54) else "?"}: flags 0x{flags:x} table {"own" if table != mtable else "SHARED"} order {olen} faces {nf}: agree {agree}, not {disagree}; lit run differs {len(lit)}'
			+ (f' (first: entry {lit[0]} 0x{table:x} has {entry(have, lit[0])} the master 0x{mtable:x} {entry(was, lit[0])})' if lit else ''))

HIGHLIGHT, PULSING = u32(0x007a1ce0), set()
p = u32(0x007afe14)
while p and len(PULSING) < 4000: PULSING.add(p); p = u32(p + 0xd0)
print(f'highlighted model [0x007a1ce0] 0x{HIGHLIGHT:x}; pulsing [0x007afe14] {len(PULSING)}')
slots = min(u32(0x007a4084) + u32(0x007a3ebc) + 2, 4000)
for s in range(slots):
	try:
		inst = u32(0x007a4614 + 4 * s)
		if not inst: continue
		header = u32(u32(inst + 8) + 4); name = cstr(header + 0x18)
	except (OSError, struct.error):
		continue
	if args and not any(name.lower().startswith(a.lower()) for a in args): continue
	master = u32(header + 0x9c)
	if not master or master == header: continue
	line = dict.fromkeys(KEYS, 0)
	try:
		pairs = list(meshes(u32(header + 0x78), u32(master + 0x78)))
	except (OSError, struct.error):
		pairs = []; line['unreadable'] += 1
	for node, mnode in pairs:
		try:
			mesh_line(node, mnode, line)
		except (OSError, struct.error):
			line['unreadable'] += 1
	for k in KEYS: total[k] += line[k]
	marks = ('' if inst != HIGHLIGHT else ' HIGHLIGHTED') + ('' if inst not in PULSING else ' PULSING')
	print(f'slot {s} {name} made 0x{u32(inst + 0xe4):x} flags 0x{u32(inst + 4):x}{marks}: meshes {line["meshes"]} (own table {line["own"]}, shared {line["shared"]}), lit run differs {line["lit_differ"]};'
		f' faces {line["faces"]}: agree {line["agree"]}, not {line["disagree"]}, naming a lit entry {line["face_in_lit"]}, changed from the master {line["changed"]};'
		f' stale meshes {line["stale"]} (not agreeing {line["stale_disagree"]}); colour-swapped meshes {line["coloured"]} (lit run unlike the master {line["coloured_lit_differ"]}); unreadable {line["unreadable"]}')
print('all: ' + ', '.join(f'{k} {total[k]}' for k in KEYS))
