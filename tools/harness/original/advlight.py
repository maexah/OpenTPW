#!/usr/bin/env python3
"""advlight.py PID [SLOT] [--secs S] [--json OUT] - how the ORIGINAL lit its advisor's meshes, read-only (Q621).

Slot SLOT's model (0x0079039c + 0xb0 x SLOT, default 0, the global fly; 1 is the theme's own), its node tree as
advnodes.py walks it ([[[[model +8] +0x1c] +8] +0x78]). For each mesh with normals: its name (+0x54), flag word,
normal count (+0x5e), the normals (+0x64, 12 bytes each) and the colours its last lighting wrote (the cache [+0x9c]: a
stamp, then one 0xAARRGGBB a normal, Light_ModelVertexColours 0x00574660), with the stamp against the frame's
[0x008bd50c]. Prints the ambient the lighter starts from (0x00879dd8..e4) and the advisor record's light (record
+0x48: type +0x50, colour +4..+0x10, world direction +0x2c), then a line a mesh: colour min, max and mean, how many
normals have red equal to green, and blue less red where blue is under 255.

Then the law, per mesh: the light vector v (= -k d, in the mesh's own normal space; the node's +0x78 words are not the
lighter's matrix) fitted by least squares to its unsaturated normals, red = 255 (ambient + v.n), then every normal
predicted clamp(round((ambient + max(0, v.n)) x 255)) and the channels off by more than 1 counted. |v| is the light's
strength (2.00 in a park: Q621). --json writes every mesh's normals and colours.
docs/exe/ui.md, "How he is lit".
"""
import json, struct, sys, time

args = sys.argv[1:]
JSON = SECS = None
if '--json' in args: i = args.index('--json'); JSON = args[i + 1]; del args[i:i + 2]
if '--secs' in args: i = args.index('--secs'); SECS = float(args[i + 1]); del args[i:i + 2]
pid = int(args[0]); slot = int(args[1]) if len(args) > 1 else 0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def rdn(a, fmt):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
def cstr(a):
	f.seek(a); return f.read(40).split(b'\0')[0].decode('ascii', 'replace')

def once():
	rec = 0x00790398 + 0xb0 * slot
	model = rd(rec + 4)
	if not model: return None
	amb = rdn(0x00879dd8, '4f')
	light = rec + 0x48
	# The light object is reached through its virtual base: [light] +8 is the offset to its fields
	fields = light + rd(rd(light) + 8)
	ltype = rd(fields + 0x50); lcol = rdn(fields + 4, '4f'); ldir = rdn(fields + 0x2c, '3f')
	frame = rd(0x008bd50c)
	print(f'slot {slot} model {model:#x} frame {frame}  ambient (a {amb[0]:.3f}) r {amb[1]:.3f} g {amb[2]:.3f} b {amb[3]:.3f}')
	print(f'record light: type {ltype:#x} colour (a {lcol[0]:.3f}) {lcol[1]:.3f} {lcol[2]:.3f} {lcol[3]:.3f} '
		f'world direction ({ldir[0]:.3f}, {ldir[1]:.3f}, {ldir[2]:.3f})')
	data = rd(model + 8); rend = rd(data + 0x1c); root = rd(rd(rend + 8) + 0x78)
	meshes = []
	def walk(node, depth):
		n = 0
		while node and n < 64:
			flags = rd(node)
			name = cstr(rd(node + 0x54)) if rd(node + 0x54) else '?'
			cnt = rd(node + 0x5e, 'H'); np_ = rd(node + 0x64); cache = rd(node + 0x9c)
			if not flags & 0x200 and cnt and np_ and cache:
				normals = [rdn(np_ + 12 * k, '3f') for k in range(cnt)]
				stamp = rd(cache)
				cols = rdn(cache + 4, f'{cnt}I')
				meshes.append(dict(name=name, depth=depth, flags=flags, normals=normals, stamp=stamp,
					colours=[((c >> 16) & 255, (c >> 8) & 255, c & 255) for c in cols]))
			if rd(node + 0xc):
				walk(rd(node + 0xc), depth + 1)
			node = rd(node + 8); n += 1
	walk(root, 0)
	import numpy as np
	A = np.array(amb[1:]); off = tot = 0
	for me in meshes:
		cs = me['colours']; n = len(cs); C = np.array(cs, float); N = np.array(me['normals'])
		mn = tuple(min(c[i] for c in cs) for i in range(3)); mx = tuple(max(c[i] for c in cs) for i in range(3))
		mean = tuple(round(sum(c[i] for c in cs) / n, 1) for i in range(3))
		rg = sum(1 for c in cs if c[0] == c[1])
		br = sorted({c[2] - c[0] for c in cs if c[2] < 255})
		law = ''
		mid = (C[:, 0] > A[0] * 255 + 1) & (C[:, 0] < 254)
		if me['stamp'] == frame and mid.sum() >= 3:
			v = np.linalg.lstsq(N[mid], C[mid, 0] / 255 - A[0], rcond=None)[0]
			P = np.clip(np.round((A[None, :] + np.maximum(0, N @ v)[:, None]) * 255), 0, 255)
			bad = int((np.abs(P - C) > 1).sum()); off += bad; tot += C.size
			law = f' law |v| {np.linalg.norm(v):.3f} off {bad}/{C.size}'
		print(f'{"  " * me["depth"]}{me["name"]}: flags {me["flags"]:#x} normals {n} stamp {me["stamp"]}'
			f'{" (now)" if me["stamp"] == frame else ""} min {mn} max {mx} mean {mean} r=g {rg}/{n} b-r {br[:6]}{law}')
	print(f'law over the fitted meshes: {off} of {tot} channels off by more than 1')
	if JSON:
		json.dump(dict(slot=slot, ambient=amb, light=dict(type=ltype, colour=lcol, direction=ldir), frame=frame,
			meshes=meshes), open(JSON, 'w'))
	return True

if SECS:
	t0 = time.time()
	while time.time() - t0 < SECS:
		try:
			if once(): break
		except (OSError, struct.error):
			pass
		time.sleep(0.2)
else:
	once()
