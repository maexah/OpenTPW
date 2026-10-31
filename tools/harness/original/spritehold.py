#!/usr/bin/env python3
"""spritehold.py PID POINTS.json [SLOT] - a sprite of the running ORIGINAL stood at chosen ground points, and the height the draw
gives it there (Q839). WRITES the original's memory (as thingpoke.py does): stop the original afterwards, never save a park under it.

POINTS.json is a list of [x, z] world points. SLOT (default: the first sprite of kind 0, a walking guest, without +0xc4 bit 0x1000000).
For each point it writes the sprite's place (+0x88 x, +0x90 z; +0x8c is left) over and over until FUN_00542010 has stored the same x and
z as the draw's place (+0xe4, +0xec), then reads y (+0xe8) and prints, in spriteheights.py's words, `spr kind K flags F x X z Z off O
st0 S` with S = y - offset: the sampler FUN_004527f0 gave the draw at that point (0x005420ee; docs/exe/park-engine.md). The person's
own tick writes the place from its cell every game tick, so a point is caught only on a frame between that and the next write; a point
not caught in 2 s is printed as `miss`. The sprite is left where the person's tick puts it back.

  spritehold.py PID POINTS.json [SLOT] --stay X,Z,SECS   after the points (POINTS.json may be `[]`), write the place X, Z over and over
        for SECS and print how often the draw's place (+0xe4, +0xec) was that place and the last y read; a frame grabbed meanwhile
        (import -window root -display :77 -crop 1024x768+0+0 OUT.png) shows the person standing there."""
import json, struct, sys, time
args = [a for a in sys.argv[1:] if not a.startswith('--stay')]; stay = None
if '--stay' in sys.argv: stay = [float(v) for v in sys.argv[sys.argv.index('--stay') + 1].split(',')]; args = [a for a in args if a != sys.argv[sys.argv.index('--stay') + 1]]
pid = int(args[0]); pts = json.load(open(args[1])); slot = int(args[2]) if len(args) > 2 else None
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='<I'):
	f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
def wr(a, fmt, *v): f.seek(a); f.write(struct.pack(fmt, *v))
table, n = rd(0x007b49f0), rd(0x007b49f4); s = None
for i in range(1, n):
	a = rd(table + 4 * i)
	if a and (slot == i or (slot is None and rd(a + 0xac) == 0 and not rd(a + 0xc4) & 0x1000000)): s = a; slot = i; break
assert s, 'no sprite to hold'
kind, flags = rd(s + 0xac), rd(s + 0xc4)
print(f'holding slot {slot} kind {kind} flags {flags:x}', flush=True)
for x, z in pts:
	fx, fz = struct.unpack('<f', struct.pack('<f', x))[0], struct.unpack('<f', struct.pack('<f', z))[0]
	t0 = time.time(); got = None
	while time.time() - t0 < 2.0:
		wr(s + 0x88, '<f', fx); wr(s + 0x90, '<f', fz)
		dx, y, dz = rd(s + 0xe4, '<3f')
		if dx == fx and dz == fz:
			# the draw stored this place; the offset and y are from the same pass if the place is still ours
			off = rd(s + 0x8c, '<f'); x2, z2 = rd(s + 0x88, '<f'), rd(s + 0x90, '<f')
			if x2 == fx and z2 == fz: got = (y, off); break
	if got: print(f'spr kind {kind} flags {flags:x} x {fx:.5f} z {fz:.5f} off {got[1]:.5f} st0 {got[0] - got[1]:.6f} slot {slot}', flush=True)
	else: print(f'miss x {x} z {z}', flush=True)
if stay:
	fx, fz = struct.unpack('<f', struct.pack('<f', stay[0]))[0], struct.unpack('<f', struct.pack('<f', stay[1]))[0]
	t0 = time.time(); seen = n = 0; y = None
	while time.time() - t0 < stay[2]:
		wr(s + 0x88, '<f', fx); wr(s + 0x90, '<f', fz); n += 1
		dx, y2, dz = rd(s + 0xe4, '<3f')
		if dx == fx and dz == fz: seen += 1; y = y2 - rd(s + 0x8c, '<f')
	print(f'stay x {fx:.5f} z {fz:.5f}: {n} writes, the draw stood there at {seen} reads, y - off {y}', flush=True)
