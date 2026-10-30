#!/usr/bin/env python3
"""spriteflags.py PID [SECS] - Q825: the ORIGINAL's sprite table and three words nothing was known to write. Every 0.5 s for SECS
(default 10) reads FUN_00519410/FUN_00519420's globals 0x00785584 and 0x00785588 (a person's particle's offset, FUN_004fa470),
[0x00790abc] and [0x007908f4] (the camcorder's further term, 0x0042c9b8), and the sprite table 0x007b49f0 (count 0x007b49f4):
a histogram of each sprite's +0xc4 drawing flags by kind (+0xac), and the place (+0x88/+0x8c/+0x90) of each sprite carrying
0x1000000, which FUN_00542010 (0x005420ae) stands at +0x8c itself. Prints every distinct reading once and a summary."""
import struct, sys, time
from collections import Counter
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 10.0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
	f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
seen = set(); polls = 0; hist = Counter(); heads = {}; t0 = time.time()
while time.time() - t0 < secs:
	polls += 1
	g = (rd(0x00785584), rd(0x00785588), rd(0x00790abc), rd(0x007908f4))
	if g not in seen:
		seen.add(g); print('globals 0x785584 0x785588 [0x790abc] [0x7908f4] = ' + ' '.join('%#x' % x for x in g), flush=True)
	table, n = rd(0x007b49f0), rd(0x007b49f4)
	for i in range(1, n):
		s = rd(table + 4 * i)
		if not s: continue
		flags, kind, state = rd(s + 0xc4), rd(s + 0xac), rd(s + 0x18)
		hist[(kind, flags)] += 1
		if flags & 0x1000000: heads[i] = (kind, flags) + tuple(rd(s + 0x88, '<3f'))
	time.sleep(0.5)
print(f'polls {polls}; {len(seen)} distinct global readings')
print('flags by (kind, word), summed over the polls:')
for (k, w), c in sorted(hist.items()): print(f'  kind {k} flags {w:#x}: {c}')
print(f'sprites with 0x1000000: {len(heads)}')
for i, v in sorted(heads.items())[:20]: print(f'  slot {i}: kind {v[0]} flags {v[1]:#x} place {v[2]:.3f} {v[3]:.3f} {v[4]:.3f}')
