#!/usr/bin/env python3
"""eyeprobe.py PID [SECS] - Q822: the original's first-person eye height against the samplers. Reads the live park
heightfield ([[0x007a0854]+0x6c]: +0x8/+0xc origin, +0x10/+0x14 spacing, +0x18/+0x1c cells, +0x28 heights, +0x2c cell
words), takes the two 0x800 ground cells with the largest bend whose heights differ, holds the stand 0x007908f0 / 0x007908f8 at (0.6, 0.3)
inside the first for SECS, then inside the second for SECS, polling the eye 0x00790aa4 (written at 0x0042c737 from
FUN_00452560 at 0x0042c729), and prints each distinct value seen with the four candidates + 5."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 3.0
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def wr(a, fmt, v):
	f.seek(a); f.write(struct.pack('<' + fmt, v))
hf = rd(rd(0x007a0854) + 0x6c)
ox, oz, sx, sz = rd(hf + 8, 'f'), rd(hf + 0xc, 'f'), rd(hf + 0x10, 'f'), rd(hf + 0x14, 'f')
W, H, hp, cp = rd(hf + 0x18), rd(hf + 0x1c), rd(hf + 0x28), rd(hf + 0x2c)
print(f'heightfield {hf:#x}: {W} x {H} at ({ox},{oz}) by ({sx},{sz}); heights {hp:#x} cells {cp:#x}; camera flags {rd(0x00790ab0):#x}', flush=True)
f.seek(hp); heights = struct.unpack(f'<{(W + 1) * (H + 1)}f', f.read((W + 1) * (H + 1) * 4))
f.seek(cp); words = struct.unpack(f'<{W * H}I', f.read(W * H * 4))
h = lambda x, z: heights[z * (W + 1) + x]
cands = []
for z in range(H):
	for x in range(W):
		w = words[z * W + x] & 0xffff
		if w & 0x800 and not w & 1:
			bend = abs(h(x + 1, z + 1) - h(x + 1, z) - h(x, z + 1) + h(x, z))
			cands.append((bend, x, z, w))
cands.sort(reverse=True)
print(f'{len(cands)} ground cells flagged 0x800; largest bends: ' + ', '.join(f'({x},{z}) 0x{w:04x} {b:.3f}' for b, x, z, w in cands[:6]), flush=True)
def models(x, z, w, u=0.6, v=0.3):
	h00, h10, h01, h11 = h(x, z), h(x + 1, z), h(x, z + 1), h(x + 1, z + 1)
	def tri(other):
		if not other:
			return h00 + (h10 - h00) * u + (h11 - h10) * v if v <= u else h00 + (h11 - h01) * u + (h01 - h00) * v
		return h11 + (h11 - h01) * (u - 1) + (h11 - h10) * (v - 1) if u + v >= 1 else h00 + (h10 - h00) * u + (h01 - h00) * v
	near = h00 + (h10 - h00) * u + (h01 - h00) * v
	bil = (h00 + (h10 - h00) * u) * (1 - v) + (h01 + (h11 - h01) * u) * v
	return {'triangle by 0x4': tri(bool(w & 4)), 'other diagonal': tri(not (w & 4)), 'bilinear': bil, 'near-corner plane': near}
a = cands[0]
b = next(c for c in cands[1:] if abs(models(c[1], c[2], c[3])['triangle by 0x4'] - models(a[1], a[2], a[3])['triangle by 0x4']) > 0.5)
was = (rd(0x007908f0, 'f'), rd(0x007908f8, 'f'))
for bend, x, z, w in (a, b):
	px, pz = ox + (x + 0.6) * sx, oz + (z + 0.3) * sz
	m = models(x, z, w)
	print(f'== cell ({x},{z}) word 0x{w:04x} bend {bend:.3f}: stand ({px:.3f}, {pz:.3f}); corners {h(x,z):.3f} {h(x+1,z):.3f} {h(x,z+1):.3f} {h(x+1,z+1):.3f}', flush=True)
	for k, val in m.items(): print(f'   {k:18s} + 5 = {val + 5:.4f}', flush=True)
	seen = []; t0 = time.time(); n = 0
	while time.time() - t0 < secs:
		wr(0x007908f0, 'f', px); wr(0x007908f8, 'f', pz)
		e = rd(0x00790aa4, 'f'); n += 1
		if not seen or seen[-1][1] != e: seen.append((time.time() - t0, e))
		time.sleep(0.002)
	print(f'   {n} polls; eye values in order: ' + ', '.join(f'{e:.4f} @{t:.3f}s' for t, e in seen), flush=True)
	best = min(m, key=lambda k: abs(m[k] + 5 - seen[-1][1]))
	print(f'   last eye {seen[-1][1]:.4f}: nearest {best} (off {abs(m[best] + 5 - seen[-1][1]):.5f}); camera flags {rd(0x00790ab0):#x}', flush=True)
wr(0x007908f0, 'f', was[0]); wr(0x007908f8, 'f', was[1])
print(f'stand put back to ({was[0]:.3f}, {was[1]:.3f})', flush=True)
