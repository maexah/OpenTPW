#!/usr/bin/env python3
"""spritetable.py PID [all] - the ORIGINAL's sprite table, read-only (Q948): the table `0x007b49f0` (count `0x007b49f4`) walked as `FUN_00475430`
walks it. First a line `count N {kind: how many}`, then one line a sprite of kind 10 (the balloons; every sprite with `all`):
`slot S kind K flags F x X off O z Z draw DX DY DZ` - `+0xac` the kind, `+0xc4` the flags, `+0x88`, `+0x8c`, `+0x90` the place and the height above the
ground, `+0xe4`, `+0xe8`, `+0xec` the place `FUN_00542010` drew it at. The slot is what `spritehold.py PID POINTS.json SLOT` takes: it changes from
one launch to the next, so read it each time. Kind 10 is a balloon (Q948)."""
import collections, struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
	f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
table, n = rd(0x007b49f0), rd(0x007b49f4)
c = collections.Counter(); rows = []
for i in range(1, n):
	a = rd(table + 4 * i)
	if not a: continue
	k, fl = rd(a + 0xac), rd(a + 0xc4); x, off, z = rd(a + 0x88, '<3f'); dx, dy, dz = rd(a + 0xe4, '<3f')
	c[k] += 1; rows.append((i, k, fl, x, off, z, dx, dy, dz))
print('count', n, dict(c))
for r in rows:
	if r[1] == 10 or 'all' in sys.argv[2:]: print('slot %d kind %d flags %x x %.3f off %.3f z %.3f draw %.3f %.3f %.3f' % r)
