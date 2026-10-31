#!/usr/bin/env python3
"""markercorners.py PID [SECS] [--glide GX,GZ] - the build markers' drawn corners in the running ORIGINAL (Q838), read-only
(with --glide, the two glide words are held while it samples).

FUN_0053ddd0 writes each corner of a marker square into its mesh's arrays: x, y and z in three runs of four, a group of four
corners 0x30 bytes (mesh+0x64), y = wave table[(phase + z + x) * scale & mask] + FUN_004527f0( x, z ) + the record's lift
(0x0053ddea..0x0053de40; docs/exe/park-engine.md, "Which height each asker takes"). The three meshes are the pointers at
[0x00871f64], [0x00871ee4] and [0x00871ed8] (FUN_0053c3f0 hands each to FUN_0053d9e0); the corner count is mesh+0x9c, which
the draw zeroes and refills each frame, so this samples for SECS and keeps the last full list (the most corners seen) that read the same count either side.

Prints the phase [0x00874fc0], the wave table's scale [0x007b095c] and mask [0x007b0960], the glide words [0x00820af8] (z) and
[0x00820afc] (x), then a line a corner:  mesh i  x z  y  wave  y-wave   (y-wave is FUN_004527f0 + the lift). The wave is
computed from the phase read in the same pass, so it is exact only where the phase holds (the clock paused: memhold.py
0x007859a0:I=1) and is printed as 'wave ?' otherwise.

  markercorners.py %PID% 3                   a carried thing's corners, glide as it eases
  markercorners.py %PID% 3 --glide 4,3       the glide words held at the pointer's cell corner + (4, 3) world units
                                             (the words themselves hold the corner: see --glide below)

--glide GX,GZ writes [0x00820afc] = [0x008186b8] * [0x00700de0] + GX and [0x00820af8] = [0x008186b4] * [0x00700de0] + GZ
(the pointer cell's corner, FUN_0053c3f0 0x0053c3f6..0x0053c42b, plus the offset) before every read; the frame's own update
then moves the glide halfway back toward the corner, so the corners read are the ones the draw laid. The words are put back at
the end."""
import struct, sys, time

pid = int(sys.argv[1])
args = sys.argv[2:]
secs = float(args[0]) if args and not args[0].startswith('--') else 2.0
glide = None
if '--glide' in args:
	gx, gz = args[args.index('--glide') + 1].split(','); glide = (float(gx), float(gz))

f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, n):
	f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def i32(a): return struct.unpack('<i', rd(a, 4))[0]
def f32(a): return struct.unpack('<f', rd(a, 4))[0]
def wr32f(a, v):
	f.seek(a); f.write(struct.pack('<f', v))

MESHES = (0x00871f64, 0x00871ee4, 0x00871ed8)
PHASE, SCALE, MASK, TABLE = 0x00874fc0, 0x007b095c, 0x007b0960, 0x007b0968
GLIDE_X, GLIDE_Z, CELL_X, CELL_Z, TEN = 0x00820afc, 0x00820af8, 0x008186b8, 0x008186b4, 0x00700de0

was = (f32(GLIDE_X), f32(GLIDE_Z))
best = {}
t0 = time.time(); reads = 0
while time.time() - t0 < secs:
	if glide:
		wr32f(GLIDE_X, i32(CELL_X) * f32(TEN) + glide[0]); wr32f(GLIDE_Z, i32(CELL_Z) * f32(TEN) + glide[1])
	for m in MESHES:
		mesh = u32(m)
		if not mesh: continue
		n = u32(mesh + 0x9c)
		if not 0 < n <= 0x1000 or n % 4: continue
		base = u32(mesh + 0x64)
		data = rd(base, (n // 4) * 0x30)
		if u32(mesh + 0x9c) != n: continue
		if n >= len(best.get(m, b'')) // 0x30 * 4: best[m] = data
	reads += 1
phase = f32(PHASE); scale = f32(SCALE); mask = u32(MASK)
if glide:
	wr32f(GLIDE_X, was[0]); wr32f(GLIDE_Z, was[1])
print(f'phase {phase:.5f} scale {scale:.4f} mask {mask:#x} glide x {f32(GLIDE_X):.3f} z {f32(GLIDE_Z):.3f} cell ({i32(CELL_X)},{i32(CELL_Z)}) reads {reads}')
for i, m in enumerate(MESHES):
	data = best.get(m, b'')
	for g in range(len(data) // 0x30):
		r = data[g * 0x30:(g + 1) * 0x30]
		xs, ys, zs = struct.unpack_from('<4f', r, 0), struct.unpack_from('<4f', r, 0x10), struct.unpack_from('<4f', r, 0x20)
		for k in range(4):
			idx = int(round((phase + zs[k] + xs[k]) * scale)) & mask
			wave = struct.unpack('<f', rd(TABLE + idx * 4, 4))[0]
			print(f'mesh {i} x {xs[k]:.4f} z {zs[k]:.4f} y {ys[k]:.4f} wave {wave:.4f} y-wave {ys[k] - wave:.4f}')
