#!/usr/bin/env python3
"""cellhead.py PID [THING ...] [@X,Y ...] - the running ORIGINAL's cell lists, read-only, once: for each THING the cell it
is linked into (the thing's bytes +5 and +7, the high bytes of mX and mY, which FUN_0050b6a0 compares to relink) and
its address ([0x007cfb90 + id * 0x14], what [0x007cc308] and [0x007b05ac] hold); for each @X,Y the chain on that cell,
head first (the cell's word +0x24 of the 0x44-byte cells at [0x008023a0], then each thing's +0xa, mMapChild).
One line each, then mGameTick. The OpenTPW side is the console's `cell X Y` (occupant) and `staff` (cell) (Q446)."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def thing(i): return rd(0x007cfb90 + i * 0x14)
out = []
for a in sys.argv[2:]:
    if a.startswith('@'):
        x, y = map(int, a[1:].split(','))
        who = rd(rd(0x008023a0) + (y * 128 + x) * 0x44 + 0x24, 'H'); chain = []
        while who and len(chain) < 64:
            chain.append(who); t = thing(who); who = rd(t + 0xa, 'H') if t else 0
        out.append(f'cell ({x},{y}) chain {chain}')
    else:
        i = int(a); t = thing(i)
        out.append(f'thing {i} at 0x{t:x} cell ({rd(t + 5, "B")},{rd(t + 7, "B")}) type {rd(t + 2, "B")}' if t else f'thing {i} none')
out.append(f'mGameTick {rd(rd(0x0080239c) + 0x1da70c)}')
print(' | '.join(out))
