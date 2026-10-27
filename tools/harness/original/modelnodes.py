#!/usr/bin/env python3
"""modelnodes.py PID [HEADER] [NAME...] - a loaded model's mesh nodes in the running ORIGINAL, read-only: for each mesh
of the tree from the header's root (+0x78), its name (+0x54), flag word (+0x00), normal count (+0x5e), the lengths of
its normals (+0x64, 12 bytes each) as a histogram, and the mean of the colours its last lighting wrote (the cache
[mesh+0x9c]: a stamp, then one 0xAARRGGBB a normal, Light_ModelSetup 0x0057aa10 -> Light_ModelVertexColours
0x00574660). HEADER is the model's header address, or the word holding it as @ADDR; default @0x007a0854, the park's
Terrain base.MD2; or =MODEL, every placed instance whose model is named so (the slot list 0x007a4614, [inst+8]+4, as
footprints.py walks it; Q658). NAME... keeps only meshes whose name starts with one of them. Bit 0x10 of the flag word
is a hidden node (docs/exe/park.md, "Node visibility").
The park's land outside the park sets its terrain meshes' normals to 0.225 long (FUN_00450b30, docs/exe/park-engine.md,
"Land outside the park is drawn at half light"), so the histogram reads which vertices stand on such cells.

  modelnodes.py %PID%                    every terrain mesh
  modelnodes.py %PID% @0x007a0854 river surface newcliff
  modelnodes.py %PID% surface Object12 --scale 4.444 --secs 12    WRITTEN: the named meshes' normals times 4.444 for 12 s,
                                     read again 1 s in, then each put back byte for byte (exit 2 if one was not)
  modelnodes.py %PID% =fries --axes --poll 6     each mesh's own axis lengths (the node's +0x10 local row-vector matrix,
                                     rows 0-2), a line per mesh per pass for 6 s, each pass stamped with its time in seconds and the wall clock, the
                                     instances looked up again each pass (so a poll may start before the placing click):
                                     a scale track's keys as posed (Q598; this game's `meshes THING axes`)
"""
import collections, math, struct, sys

import time
pid = int(sys.argv[1]); args = sys.argv[2:]
SCALE = SECS = None
if '--scale' in args: i = args.index('--scale'); SCALE = float(args[i + 1]); del args[i:i + 2]
if '--secs' in args: i = args.index('--secs'); SECS = float(args[i + 1]); del args[i:i + 2]
AXES = '--axes' in args
if AXES: args.remove('--axes')
POLL = None
if '--poll' in args: i = args.index('--poll'); POLL = float(args[i + 1]); del args[i:i + 2]
hdr_arg = args.pop(0) if args and (args[0][:1] in '@=' or args[0].startswith('0x')) else '@0x007a0854'
f = open(f'/proc/{pid}/mem', 'r+b' if SCALE else 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def cstr(a):
	b = rd(a, 40); return b.split(b'\0')[0].decode('ascii', 'replace')
def placed(model):
	found = []
	for s in range(min(u32(0x007a4084) + u32(0x007a3ebc) + 2, 4000)):
		try:
			inst = u32(0x007a4614 + 4 * s)
			if inst and cstr(u32(u32(inst + 8) + 4) + 0x18).lower().startswith(model.lower()): found.append((s, u32(u32(inst + 8) + 4)))
		except (OSError, struct.error): continue
	return found
if hdr_arg.startswith('='):
	headers = placed(hdr_arg[1:])
	print(f'{len(headers)} placed instances of {hdr_arg[1:]}: ' + ', '.join(f'slot {s} header 0x{h:08x}' for s, h in headers))
else:
	headers = [(None, u32(int(hdr_arg[1:], 16)) if hdr_arg.startswith('@') else int(hdr_arg, 16))]
tot = collections.Counter(); saved = []
def axes(node):
	rows = struct.unpack('<12f', rd(node + 0x10, 48))
	return tuple(math.sqrt(sum(v * v for v in rows[4 * r:4 * r + 3])) for r in range(3))
def walk(node, depth):
	while node:
		flags = u32(node); name = cstr(u32(node + 0x54)) if u32(node + 0x54) else ''
		if AXES:
			if not flags & 0x200 and (not args or any(name.lower().startswith(a.lower()) for a in args)):
				print(f'{"  " * depth}{name or "?"}: flags 0x{flags:x} axes (%.4f, %.4f, %.4f)' % axes(node))
			walk(u32(node + 0xc), depth + 1); node = u32(node + 8); continue
		if not flags & 0x200 and (not args or any(name.lower().startswith(a.lower()) for a in args)):
			n = u16(node + 0x5e); np_ = u32(node + 0x64); cache = u32(node + 0x9c)
			lens = collections.Counter()
			if n and np_:
				raw = rd(np_, 12 * n)
				if SCALE and not READ_ONLY:
					saved.append((np_, raw))
					f.seek(np_); f.write(struct.pack(f'<{3 * n}f', *(v * SCALE for v in struct.unpack(f'<{3 * n}f', raw))))
				for i in range(n):
					x, y, z = struct.unpack_from('<3f', raw, 12 * i); L = math.sqrt(x*x + y*y + z*z); lens['nan' if L != L else round(L, 3)] += 1
			col = ''
			if n and cache:
				cs = struct.unpack(f'<{n}I', rd(cache + 4, 4 * n))
				col = 'colour mean (%.1f, %.1f, %.1f)' % tuple(sum((c >> s) & 0xff for c in cs) / n for s in (16, 8, 0))
			top = ', '.join(f'{k}x{v}' for k, v in lens.most_common(4)) + (f', +{sum(lens.values()) - sum(v for _, v in lens.most_common(4))} in {len(lens) - 4} more' if len(lens) > 4 else '')
			tot.update(lens)
			print(f'{"  " * depth}{name or "?"}: flags 0x{flags:x} normals {n} lengths [{top}] {col}')
		walk(u32(node + 0xc), depth + 1)
		node = u32(node + 8)
READ_ONLY = False
if AXES:
	t0 = time.time()
	while True:
		if hdr_arg.startswith('=') and POLL is not None: headers = placed(hdr_arg[1:])   # a thing placed while it polls
		print(f'-- t {time.time() - t0:.3f} wall {time.time():.3f} ' + ', '.join(f'slot {s} header 0x{h:08x}' for s, h in headers))
		for _, hdr in headers: walk(u32(hdr + 0x78), 0)
		if POLL is None or time.time() - t0 >= POLL: break
		time.sleep(0.03)
	sys.exit(0)
for _, hdr in headers:
	root = u32(hdr + 0x78)
	print(f'header 0x{hdr:08x} root 0x{root:08x}')
	walk(root, 0)
if SCALE:
	print(f'WROTE normals x{SCALE} on {len(saved)} meshes; holding {SECS} s'); time.sleep(1.0)
	READ_ONLY = True; tot.clear(); print('-- read again 1 s in'); walk(root, 0)
	time.sleep(max(0.0, (SECS or 0) - 1.0))
	bad = 0
	for a, raw in saved:
		f.seek(a); f.write(raw)
		if rd(a, len(raw)) != raw: bad += 1
	print(f'put back {len(saved)} meshes, {bad} not equal'); sys.exit(2 if bad else 0)
print('all meshes: ' + ', '.join(f'{k}x{v}' for k, v in tot.most_common(6)))
