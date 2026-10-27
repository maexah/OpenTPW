#!/usr/bin/env python3
"""scalecurve.py <wad> <model stem> <clip suffix> <axes file> - each reading of `original/modelnodes.py --axes --poll`
(or of this game's `meshes THING axes` lines, saved to a file) of a node a clip's scale track keys, against that track:
the frame on its keyed curve nearest the reading and how far off it is (Q598). The clip is <stem><suffix>.md2 in the wad,
its nodes named from <stem>.md2. A reading of (1, 1, 1) before a node's first other reading is the instance before its
clip binds, and is left out. Prints the readings and nodes counted and the worst distance; each node's nearest frames.

    scalecurve.py <data>/levels/jungle/shops/fries.wad fries C q598/orig/fries-axes.txt     the Fries Shop's construction
"""
import os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
import wadcat, md2surf
wad, stem, suffix, axes_file = sys.argv[1], sys.argv[2].lower(), sys.argv[3].lower(), sys.argv[4]
M = dict((n.split('\\')[-1].lower(), b) for n, b in wadcat.members(open(wad, 'rb').read()))
model = md2surf.Model(M[stem + '.md2']); d = M[stem + suffix + '.md2']
b = struct.unpack_from('<I', d, 0x98)[0]; n = struct.unpack_from('<H', d, b + 0x12)[0]; t = struct.unpack_from('<I', d, b + 0x2c)[0]
K = {}
for i in range(n):   # the scale channel: flags 0x80, the key count at +0x12, 16-byte keys (frame, 0, x, y, z) at +0x20
	o = t + 64 * i
	if struct.unpack_from('<I', d, o + 4)[0] & 0x80:
		tg = struct.unpack_from('<H', d, o + 0x14)[0]; k = struct.unpack_from('<H', d, o + 0x12)[0]; at = struct.unpack_from('<I', d, o + 0x20)[0]
		K[model.name(tg)] = [(struct.unpack_from('<H', d, at + 16 * j)[0], struct.unpack_from('<3f', d, at + 16 * j + 4)) for j in range(k)]
def lerp(keys, f):
	for (f0, v0), (f1, v1) in zip(keys, keys[1:]):
		if f0 <= f <= f1: u = (f - f0) / (f1 - f0); return [a + (c - a) * u for a, c in zip(v0, v1)]
	return keys[-1][1]
worst = 0.0; seen = {}; first = set()
for line in open(axes_file):
	m = re.match(r"\s*(?:\[dbg\]\s*node \d+ )?(\S.*?)(?:: flags 0x[0-9a-f]+)? axes \(([-\d.]+), ([-\d.]+), ([-\d.]+)\)", line)
	if not m or m.group(1) not in K: continue
	name = m.group(1); v = [float(m.group(i)) for i in (2, 3, 4)]
	if name not in first:
		if all(abs(x - 1) < 1e-4 for x in v): continue
		first.add(name)
	keys = K[name]; grid = [i / 20 for i in range(keys[-1][0] * 20 + 1)]
	f = min(grid, key=lambda g: max(abs(a - c) for a, c in zip(lerp(keys, g), v)))
	dist = max(abs(a - c) for a, c in zip(lerp(keys, f), v)); worst = max(worst, dist)
	seen.setdefault(name, []).append(f)
print(f"{sum(map(len, seen.values()))} readings of {len(seen)} of the clip's {len(K)} scaled nodes, worst distance to the keyed curve {worst:.4f}")
for name, fs in seen.items(): print(f"  {name}: {len(fs)} readings, nearest frames {min(fs):.1f} .. {max(fs):.1f}")
