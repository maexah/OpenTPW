#!/usr/bin/env python3
"""staffplace.py PID - the ORIGINAL's sprites of kind 6 (the staff), slot and place (+0x88/+0x8c/+0x90)."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
table, n = rd(0x007b49f0), rd(0x007b49f4)
for i in range(1, n):
    s = rd(table + 4 * i)
    if s and rd(s + 0xac) == 6: print('slot %d place (%.3f, %.3f, %.3f)' % ((i,) + rd(s + 0x88, '<3f')))
