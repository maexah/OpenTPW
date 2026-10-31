#!/usr/bin/env python3
"""spritedrawn.py PID [SECS] [STEP] - the ORIGINAL's sprites' DRAWN place, read-only (Q839). Every STEP seconds (default 0.2) for SECS
(default 30) walks the sprite table (0x007b49f0, count 0x007b49f4: the list FUN_00475430 walks) and, for each sprite, reads its place
(+0x88 x, +0x8c offset, +0x90 z) and the place FUN_00542010 stored for the draw (+0xe4 x, +0xe8 y, +0xec z: `fst`/`fstp` at 0x005420ca
and 0x0054225a..0x005422af). A sprite without +0xc4 bit 0x1000000 is drawn at y = FUN_004527f0( x, z ) + offset, so
st0 = y - offset is the sampler the draw asked; a sprite with the bit stands at +0x8c itself and is skipped. Prints one line per
distinct (slot, x, z) in the words original/brk.sh's sampler run uses, `spr kind K flags F x X z Z off O st0 S` (S = y - offset),
then the draw's own x and z against the place (`drawn dx dz`), so lib/spriteheights.py reads either. A `POLL` for loadfile.sh."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 30.0; step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.2
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
	f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
seen = set(); t0 = time.time(); polls = 0; worst = 0.0
while time.time() - t0 < secs:
	polls += 1
	table, n = rd(0x007b49f0), rd(0x007b49f4)
	for i in range(1, n):
		s = rd(table + 4 * i)
		if not s: continue
		flags, kind = rd(s + 0xc4), rd(s + 0xac)
		if flags & 0x1000000: continue
		x, off, z = rd(s + 0x88, '<3f'); dx, y, dz = rd(s + 0xe4, '<3f')
		if dx != x or dz != z: continue    # caught between the place's write and the draw's: skip it
		key = (i, round(x, 2), round(z, 2))
		if key in seen: continue
		seen.add(key)
		print(f'spr kind {kind} flags {flags:x} x {x:.5f} z {z:.5f} off {off:.5f} st0 {y - off:.6f} slot {i} poll {polls}', flush=True)
	time.sleep(step)
print(f'polls {polls}; {len(seen)} distinct (slot, place)', flush=True)
